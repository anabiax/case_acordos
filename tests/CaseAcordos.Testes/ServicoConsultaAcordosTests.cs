using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CaseAcordos.Testes
{
    public class ServicoConsultaAcordosTests
    {
        private readonly IRepositorioConsulta _repositorio = Substitute.For<IRepositorioConsulta>();

        private ServicoConsultaAcordos CriarServico(bool mascarar = false)
            => new(
                _repositorio,
                Options.Create(new OpcoesMascaramento { MascararRespostas = mascarar }),
                NullLogger<ServicoConsultaAcordos>.Instance);

        private static AutorRegistro AutorPadrao() => new()
        {
            Id = 1,
            Nome = "Leanne Graham",
            Email = "leanne@april.biz",
            Telefone = "(11) 99999-0000",
            Rua = "Rua X",
            Complemento = "Apto 10",
            Cidade = "Cidade",
            Cep = "00000-000",
            EmpresaNome = "Empresa Y",
            EmpresaSegmento = "Segmento Z"
        };

        private static DocumentoRegistro Doc(int id, int acordoId) => new()
        {
            Id = id,
            AcordoId = acordoId,
            Titulo = $"Documento {id}",
            Url = $"https://exemplo/doc/{id}",
            ThumbnailUrl = $"https://exemplo/thumb/{id}"
        };

        // ---------- Consulta consolidada ----------

        [Fact]
        public async Task Consultar_AutorExistente_RetornaAutorResumoAcordosEDocumentos()
        {
            _repositorio.ObterAutorAsync(1, Arg.Any<CancellationToken>()).Returns(AutorPadrao());
            _repositorio.ListarAcordosAsync(1, Arg.Any<CancellationToken>()).Returns(new List<AcordoRegistro>
        {
            new() { Id = 10, Titulo = "Acordo A", QuantidadeDocumentos = 3 },
            new() { Id = 11, Titulo = "Acordo B", QuantidadeDocumentos = 1 }
        });
            _repositorio.ListarDocumentosPaginadosPorAutorAsync(1, 0, 2, Arg.Any<CancellationToken>())
                .Returns(new List<DocumentoRegistro> { Doc(100, 10), Doc(101, 10), Doc(200, 11) });

            var resposta = await CriarServico().ConsultarPorAutorAsync(1, 1, 2, CancellationToken.None);

            Assert.Equal(1, resposta.Autor.Id);
            Assert.Equal("Leanne Graham", resposta.Autor.Nome);
            Assert.Equal("Rua X, Apto 10 - Cidade - CEP 00000-000", resposta.Autor.Endereco);
            Assert.Equal("Empresa Y", resposta.Autor.Empresa.Nome);
            Assert.Equal("Segmento Z", resposta.Autor.Empresa.Segmento);

            Assert.Equal(2, resposta.Resumo.QuantidadeAcordos);
            Assert.Equal(4, resposta.Resumo.QuantidadeDocumentos);

            var acordoA = resposta.Acordos[0];
            Assert.Equal(10, acordoA.Id);
            Assert.Equal(3, acordoA.QuantidadeDocumentos);
            Assert.Equal(new[] { 100, 101 }, acordoA.Documentos.Select(d => d.Id));
            Assert.Equal("https://exemplo/thumb/100", acordoA.Documentos[0].ThumbnailUrl);

            var acordoB = resposta.Acordos[1];
            Assert.Equal(new[] { 200 }, acordoB.Documentos.Select(d => d.Id));
        }

        [Fact]
        public async Task Consultar_InformaMetadadosDePaginacaoPorAcordo()
        {
            _repositorio.ObterAutorAsync(1, Arg.Any<CancellationToken>()).Returns(AutorPadrao());
            _repositorio.ListarAcordosAsync(1, Arg.Any<CancellationToken>()).Returns(new List<AcordoRegistro>
        {
            new() { Id = 10, Titulo = "Grande", QuantidadeDocumentos = 120 }
        });
            _repositorio.ListarDocumentosPaginadosPorAutorAsync(1, 40, 20, Arg.Any<CancellationToken>())
                .Returns(new List<DocumentoRegistro> { Doc(41, 10) });

            var resposta = await CriarServico().ConsultarPorAutorAsync(1, 3, 20, CancellationToken.None);

            var paginacao = resposta.Acordos[0].PaginacaoDocumentos;
            Assert.Equal(3, paginacao.Pagina);
            Assert.Equal(20, paginacao.TamanhoPagina);
            Assert.Equal(120, paginacao.TotalItens);
            Assert.Equal(6, paginacao.TotalPaginas);
            Assert.True(paginacao.PossuiProximaPagina);
        }

        [Fact]
        public async Task Consultar_PaginaCalculaDeslocamentoCorreto()
        {
            _repositorio.ObterAutorAsync(1, Arg.Any<CancellationToken>()).Returns(AutorPadrao());
            _repositorio.ListarAcordosAsync(1, Arg.Any<CancellationToken>()).Returns(new List<AcordoRegistro>
        {
            new() { Id = 10, Titulo = "A", QuantidadeDocumentos = 500 }
        });
            _repositorio.ListarDocumentosPaginadosPorAutorAsync(default, default, default, default)
                .ReturnsForAnyArgs(new List<DocumentoRegistro>());

            await CriarServico().ConsultarPorAutorAsync(1, 5, 50, CancellationToken.None);

            await _repositorio.Received(1)
                .ListarDocumentosPaginadosPorAutorAsync(1, 200, 50, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Consultar_AutorSemAcordos_RetornaListasVaziasSemConsultarDocumentos()
        {
            _repositorio.ObterAutorAsync(1, Arg.Any<CancellationToken>()).Returns(AutorPadrao());
            _repositorio.ListarAcordosAsync(1, Arg.Any<CancellationToken>()).Returns(new List<AcordoRegistro>());

            var resposta = await CriarServico().ConsultarPorAutorAsync(1, 1, 20, CancellationToken.None);

            Assert.Empty(resposta.Acordos);
            Assert.Equal(0, resposta.Resumo.QuantidadeAcordos);
            Assert.Equal(0, resposta.Resumo.QuantidadeDocumentos);
            await _repositorio.DidNotReceiveWithAnyArgs()
                .ListarDocumentosPaginadosPorAutorAsync(default, default, default, default);
        }

        [Fact]
        public async Task Consultar_AcordoSemDocumentos_RetornaListaVaziaETotalPaginasZero()
        {
            _repositorio.ObterAutorAsync(1, Arg.Any<CancellationToken>()).Returns(AutorPadrao());
            _repositorio.ListarAcordosAsync(1, Arg.Any<CancellationToken>()).Returns(new List<AcordoRegistro>
        {
            new() { Id = 10, Titulo = "Sem docs", QuantidadeDocumentos = 0 }
        });
            _repositorio.ListarDocumentosPaginadosPorAutorAsync(default, default, default, default)
                .ReturnsForAnyArgs(new List<DocumentoRegistro>());

            var resposta = await CriarServico().ConsultarPorAutorAsync(1, 1, 20, CancellationToken.None);

            var acordo = Assert.Single(resposta.Acordos);
            Assert.Empty(acordo.Documentos);
            Assert.Equal(0, acordo.PaginacaoDocumentos.TotalPaginas);
            Assert.False(acordo.PaginacaoDocumentos.PossuiProximaPagina);
        }

        [Fact]
        public async Task Consultar_AutorInexistente_LancaRecursoNaoEncontrado()
        {
            _repositorio.ObterAutorAsync(99, Arg.Any<CancellationToken>()).Returns((AutorRegistro?)null);

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(
                () => CriarServico().ConsultarPorAutorAsync(99, 1, 20, CancellationToken.None));

            await _repositorio.DidNotReceiveWithAnyArgs().ListarAcordosAsync(default, default);
        }

        [Theory]
        [InlineData(0, 1, 20)]
        [InlineData(-5, 1, 20)]
        [InlineData(1, 0, 20)]
        [InlineData(1, LimitesPaginacao.PaginaMaxima + 1, 20)]
        [InlineData(1, 1, 0)]
        [InlineData(1, 1, LimitesPaginacao.TamanhoMaximo + 1)]
        public async Task Consultar_EntradaInvalida_LancaValidacaoSemTocarNoBanco(int autorId, int pagina, int tamanho)
        {
            await Assert.ThrowsAsync<ValidacaoException>(
                () => CriarServico().ConsultarPorAutorAsync(autorId, pagina, tamanho, CancellationToken.None));

            await _repositorio.DidNotReceiveWithAnyArgs().ObterAutorAsync(default, default);
        }

        [Fact]
        public async Task Consultar_ComMascaramentoHabilitado_MascaraDadosPessoaisDoAutor()
        {
            _repositorio.ObterAutorAsync(1, Arg.Any<CancellationToken>()).Returns(AutorPadrao());
            _repositorio.ListarAcordosAsync(1, Arg.Any<CancellationToken>()).Returns(new List<AcordoRegistro>());

            var resposta = await CriarServico(mascarar: true).ConsultarPorAutorAsync(1, 1, 20, CancellationToken.None);

            Assert.Equal("L*** G***", resposta.Autor.Nome);
            Assert.Equal("l***@april.biz", resposta.Autor.Email);
            Assert.Equal("(**) *****-0000", resposta.Autor.Telefone);
            Assert.DoesNotContain("Rua X", resposta.Autor.Endereco);
            Assert.Equal("Empresa Y", resposta.Autor.Empresa.Nome); // dado da empresa não é pessoal
        }

        // ---------- Documentos paginados de um acordo ----------

        [Fact]
        public async Task ConsultarDocumentos_AcordoDoAutor_RetornaPaginaEMetadados()
        {
            _repositorio.ObterAcordoDoAutorAsync(1, 10, Arg.Any<CancellationToken>())
                .Returns(new AcordoRegistro { Id = 10, Titulo = "Acordo A", QuantidadeDocumentos = 45 });
            _repositorio.ListarDocumentosDoAcordoAsync(10, 20, 20, Arg.Any<CancellationToken>())
                .Returns(new List<DocumentoRegistro> { Doc(21, 10), Doc(22, 10) });

            var resposta = await CriarServico().ConsultarDocumentosDoAcordoAsync(1, 10, 2, 20, CancellationToken.None);

            Assert.Equal(10, resposta.AcordoId);
            Assert.Equal("Acordo A", resposta.Titulo);
            Assert.Equal(new[] { 21, 22 }, resposta.Itens.Select(d => d.Id));
            Assert.Equal(2, resposta.Paginacao.Pagina);
            Assert.Equal(45, resposta.Paginacao.TotalItens);
            Assert.Equal(3, resposta.Paginacao.TotalPaginas);
            Assert.True(resposta.Paginacao.PossuiProximaPagina);
        }

        [Fact]
        public async Task ConsultarDocumentos_UltimaPagina_NaoPossuiProxima()
        {
            _repositorio.ObterAcordoDoAutorAsync(1, 10, Arg.Any<CancellationToken>())
                .Returns(new AcordoRegistro { Id = 10, Titulo = "Acordo A", QuantidadeDocumentos = 45 });
            _repositorio.ListarDocumentosDoAcordoAsync(10, 40, 20, Arg.Any<CancellationToken>())
                .Returns(new List<DocumentoRegistro> { Doc(41, 10) });

            var resposta = await CriarServico().ConsultarDocumentosDoAcordoAsync(1, 10, 3, 20, CancellationToken.None);

            Assert.False(resposta.Paginacao.PossuiProximaPagina);
        }

        [Fact]
        public async Task ConsultarDocumentos_AcordoDeOutroAutorOuInexistente_LancaRecursoNaoEncontrado()
        {
            _repositorio.ObterAcordoDoAutorAsync(1, 10, Arg.Any<CancellationToken>()).Returns((AcordoRegistro?)null);

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(
                () => CriarServico().ConsultarDocumentosDoAcordoAsync(1, 10, 1, 20, CancellationToken.None));
        }

        [Theory]
        [InlineData(0, 10, 1, 20)]
        [InlineData(1, 0, 1, 20)]
        [InlineData(1, 10, 0, 20)]
        [InlineData(1, 10, 1, 101)]
        public async Task ConsultarDocumentos_EntradaInvalida_LancaValidacao(int autorId, int acordoId, int pagina, int tamanho)
        {
            await Assert.ThrowsAsync<ValidacaoException>(
                () => CriarServico().ConsultarDocumentosDoAcordoAsync(autorId, acordoId, pagina, tamanho, CancellationToken.None));
        }

        // ---------- Endereço ----------

        [Fact]
        public void MontarEndereco_SemComplemento_OmiteAParteAusente()
        {
            var autor = new AutorRegistro { Rua = "Rua X", Cidade = "Cidade", Cep = "00000-000" };

            Assert.Equal("Rua X - Cidade - CEP 00000-000", ServicoConsultaAcordos.MontarEndereco(autor));
        }

        [Fact]
        public void MontarEndereco_SemCep_OmiteCep()
        {
            var autor = new AutorRegistro { Rua = "Rua X", Complemento = "Apto 10", Cidade = "Cidade" };

            Assert.Equal("Rua X, Apto 10 - Cidade", ServicoConsultaAcordos.MontarEndereco(autor));
        }
    }

}

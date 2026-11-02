using CaseAcordos.Api.DTO;
using CaseAcordos.Api.Tratamento;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CaseAcordos.Testes
{
    /// <summary>
    /// Testes de integração da camada HTTP: códigos de status, autenticação, validação de entrada,
    /// formato dos erros (ProblemDetails) e correlação. O serviço é substituído; cada teste usa ids próprios.
    /// </summary>
    public class AcordosApiTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
    {
        private static ConsultaAcordosDto RespostaExemplo(int autorId) => new(
            new AutorDto(autorId, "Autor", "autor@email.com", "(11) 99999-0000",
                "Rua X, Apto 10 - Cidade - CEP 00000-000", new EmpresaDto("Empresa Y", "Segmento Z")),
            new ResumoDto(1, 1),
            [
                new AcordoDto(1, "Acordo", 1,
                [new DocumentoDto(1, "Documento", "https://exemplo/1", "https://exemplo/t1")],
                PaginacaoDto.Criar(1, 20, 1))
            ]);

        private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        // ---------- 200 ----------

        [Fact]
        public async Task Get_ComApiKeyValida_Retorna200ComPayloadConsolidado()
        {
            fabrica.Servico.ConsultarPorAutorAsync(1001, 1, 20, Arg.Any<CancellationToken>())
                .Returns(RespostaExemplo(1001));

            var resposta = await fabrica.CriarClienteAutenticado().GetAsync("/api/v1/usuarios/1001/acordos");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var json = await LerJsonAsync(resposta);
            Assert.Equal(1001, json.GetProperty("autor").GetProperty("id").GetInt32());
            Assert.Equal("Empresa Y", json.GetProperty("autor").GetProperty("empresa").GetProperty("nome").GetString());
            Assert.Equal(1, json.GetProperty("resumo").GetProperty("quantidadeAcordos").GetInt32());
            Assert.Equal("Documento", json.GetProperty("acordos")[0].GetProperty("documentos")[0].GetProperty("titulo").GetString());
            Assert.Equal(1, json.GetProperty("acordos")[0].GetProperty("paginacaoDocumentos").GetProperty("totalPaginas").GetInt32());
        }

        [Fact]
        public async Task Get_RepassaParametrosDePaginacaoAoServico()
        {
            fabrica.Servico.ConsultarPorAutorAsync(1002, 3, 50, Arg.Any<CancellationToken>())
                .Returns(RespostaExemplo(1002));

            var resposta = await fabrica.CriarClienteAutenticado()
                .GetAsync("/api/v1/usuarios/1002/acordos?paginaDocumentos=3&tamanhoPaginaDocumentos=50");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            await fabrica.Servico.Received(1).ConsultarPorAutorAsync(1002, 3, 50, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetDocumentosDoAcordo_Retorna200ComPagina()
        {
            fabrica.Servico.ConsultarDocumentosDoAcordoAsync(1003, 7, 2, 10, Arg.Any<CancellationToken>())
                .Returns(new PaginaDocumentosDto(7, "Acordo", [new DocumentoDto(11, "Doc", "u", "t")], PaginacaoDto.Criar(2, 10, 11)));

            var resposta = await fabrica.CriarClienteAutenticado()
                .GetAsync("/api/v1/usuarios/1003/acordos/7/documentos?pagina=2&tamanhoPagina=10");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var json = await LerJsonAsync(resposta);
            Assert.Equal(7, json.GetProperty("acordoId").GetInt32());
            Assert.Equal(2, json.GetProperty("paginacao").GetProperty("totalPaginas").GetInt32());
        }

        // ---------- 401 ----------

        [Fact]
        public async Task Get_SemApiKey_Retorna401ComProblemDetails()
        {
            var resposta = await fabrica.CreateClient().GetAsync("/api/v1/usuarios/1/acordos");

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
            Assert.True(resposta.Headers.Contains("WWW-Authenticate"));
            var json = await LerJsonAsync(resposta);
            Assert.Equal(401, json.GetProperty("status").GetInt32());
        }

        [Fact]
        public async Task Get_ComApiKeyIncorreta_Retorna401()
        {
            var cliente = fabrica.CreateClient();
            cliente.DefaultRequestHeaders.Add("X-Api-Key", "chave-errada-chave-errada");

            var resposta = await cliente.GetAsync("/api/v1/usuarios/1/acordos");

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        // ---------- 400 ----------

        [Theory]
        [InlineData("/api/v1/usuarios/abc/acordos")]
        [InlineData("/api/v1/usuarios/0/acordos")]
        [InlineData("/api/v1/usuarios/-3/acordos")]
        [InlineData("/api/v1/usuarios/1/acordos?paginaDocumentos=0")]
        [InlineData("/api/v1/usuarios/1/acordos?tamanhoPaginaDocumentos=101")]
        [InlineData("/api/v1/usuarios/1/acordos?tamanhoPaginaDocumentos=abc")]
        [InlineData("/api/v1/usuarios/1/acordos/0/documentos")]
        [InlineData("/api/v1/usuarios/1/acordos/5/documentos?pagina=-1")]
        public async Task Get_ComEntradaInvalida_Retorna400SemChamarOServico(string caminho)
        {
            var resposta = await fabrica.CriarClienteAutenticado().GetAsync(caminho);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            var json = await LerJsonAsync(resposta);
            Assert.Equal(400, json.GetProperty("status").GetInt32());
            Assert.True(json.TryGetProperty("errors", out _));
        }

        // ---------- 404 / 503 / 500 (tratamento centralizado) ----------

        [Fact]
        public async Task Get_AutorInexistente_Retorna404()
        {
            fabrica.Servico.ConsultarPorAutorAsync(1004, 1, 20, Arg.Any<CancellationToken>())
                .ThrowsAsync(new RecursoNaoEncontradoException("Autor 1004 não encontrado."));

            var resposta = await fabrica.CriarClienteAutenticado().GetAsync("/api/v1/usuarios/1004/acordos");

            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
            var json = await LerJsonAsync(resposta);
            Assert.Equal("Autor 1004 não encontrado.", json.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task Get_BancoIndisponivel_Retorna503SemVazarDetalhesInternos()
        {
            fabrica.Servico.ConsultarPorAutorAsync(1005, 1, 20, Arg.Any<CancellationToken>())
                .ThrowsAsync(new FalhaConsultaException("Host=segredo;Password=123", new Exception("detalhe interno")));

            var resposta = await fabrica.CriarClienteAutenticado().GetAsync("/api/v1/usuarios/1005/acordos");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
            var corpo = await resposta.Content.ReadAsStringAsync();
            Assert.DoesNotContain("segredo", corpo);
            Assert.DoesNotContain("detalhe interno", corpo);
        }

        [Fact]
        public async Task Get_ErroInesperado_Retorna500GenericoComCorrelationId()
        {
            fabrica.Servico.ConsultarPorAutorAsync(1006, 1, 20, Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("detalhe-interno-sensivel"));

            var resposta = await fabrica.CriarClienteAutenticado().GetAsync("/api/v1/usuarios/1006/acordos");

            Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
            var corpo = await resposta.Content.ReadAsStringAsync();
            Assert.DoesNotContain("detalhe-interno-sensivel", corpo);

            var json = JsonDocument.Parse(corpo).RootElement;
            Assert.Equal(500, json.GetProperty("status").GetInt32());
            Assert.Equal(
                resposta.Headers.GetValues("X-Correlation-Id").Single(),
                json.GetProperty("correlationId").GetString());
        }

        [Fact]
        public async Task Get_RotaInexistente_Retorna404()
        {
            var resposta = await fabrica.CriarClienteAutenticado().GetAsync("/api/v1/nao-existe");

            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        }

        // ---------- Correlation ID ----------

        [Fact]
        public async Task CorrelationId_Valido_EhReaproveitadoNaResposta()
        {
            fabrica.Servico.ConsultarPorAutorAsync(1007, 1, 20, Arg.Any<CancellationToken>())
                .Returns(RespostaExemplo(1007));
            var cliente = fabrica.CriarClienteAutenticado();
            cliente.DefaultRequestHeaders.Add("X-Correlation-Id", "meu-id-123");

            var resposta = await cliente.GetAsync("/api/v1/usuarios/1007/acordos");

            Assert.Equal("meu-id-123", resposta.Headers.GetValues("X-Correlation-Id").Single());
        }

        [Fact]
        public async Task CorrelationId_Invalido_EhSubstituidoPorUmNovo()
        {
            var cliente = fabrica.CreateClient();
            cliente.DefaultRequestHeaders.Add("X-Correlation-Id", "invalido com espacos!");

            var resposta = await cliente.GetAsync("/health/live");

            var recebido = resposta.Headers.GetValues("X-Correlation-Id").Single();
            Assert.NotEqual("invalido com espacos!", recebido);
            Assert.Matches("^[A-Za-z0-9._-]{1,64}$", recebido);
        }

        // ---------- Rotas públicas ----------

        [Fact]
        public async Task HealthLive_EPublicoERetorna200()
        {
            var resposta = await fabrica.CreateClient().GetAsync("/health/live");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var json = await LerJsonAsync(resposta);
            Assert.Equal("Healthy", json.GetProperty("status").GetString());
        }

        [Fact]
        public async Task Swagger_DocumentoOpenApi_EPublicoEDescreveOsEndpoints()
        {
            var resposta = await fabrica.CreateClient().GetAsync("/swagger/v1/swagger.json");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await resposta.Content.ReadAsStringAsync();
            Assert.Contains("/api/v1/usuarios/{id}/acordos", corpo);
            Assert.Contains("/api/v1/usuarios/{id}/acordos/{acordoId}/documentos", corpo);
            Assert.Contains("X-Api-Key", corpo);
        }
    }

}

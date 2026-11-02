using CaseAcordos.Api.Configuracao;
using CaseAcordos.Api.DTO;
using CaseAcordos.Api.Interfaces;
using CaseAcordos.Api.Mascaramento;
using CaseAcordos.Api.Modelo;
using CaseAcordos.Api.Tratamento;
using Microsoft.Extensions.Options;

namespace CaseAcordos.Api.Servicos
{
    /// <summary>
    /// Regra da consulta: valida entradas, orquestra o repositório, monta o endereço,
    /// calcula o resumo/paginação e aplica o mascaramento (quando habilitado).
    /// Não conhece HTTP nem SQL.
    /// </summary>
    public sealed class ServicoConsultaAcordos(
        IRepositorioConsulta repositorio,
        IOptions<OpcoesMascaramento> opcoesMascaramento,
        ILogger<ServicoConsultaAcordos> logger) : IServicoConsultaAcordos
    {
        public async Task<ConsultaAcordosDto> ConsultarPorAutorAsync(
            int autorId, int paginaDocumentos, int tamanhoPaginaDocumentos, CancellationToken cancellationToken)
        {
            ValidarId(autorId, "autor");
            ValidarPaginacao(paginaDocumentos, tamanhoPaginaDocumentos);

            var autor = await repositorio.ObterAutorAsync(autorId, cancellationToken)
                ?? throw new RecursoNaoEncontradoException($"Autor {autorId} não encontrado.");

            var acordos = await repositorio.ListarAcordosAsync(autorId, cancellationToken);

            IReadOnlyList<DocumentoRegistro> documentos = acordos.Count == 0
                ? Array.Empty<DocumentoRegistro>()
                : await repositorio.ListarDocumentosPaginadosPorAutorAsync(
                    autorId,
                    CalcularDeslocamento(paginaDocumentos, tamanhoPaginaDocumentos),
                    tamanhoPaginaDocumentos,
                    cancellationToken);

            var documentosPorAcordo = documentos.ToLookup(d => d.AcordoId);

            var acordosDto = acordos
                .Select(a => new AcordoDto(
                    a.Id,
                    a.Titulo,
                    a.QuantidadeDocumentos,
                    documentosPorAcordo[a.Id].Select(ParaDto).ToList(),
                    PaginacaoDto.Criar(paginaDocumentos, tamanhoPaginaDocumentos, a.QuantidadeDocumentos)))
                .ToList();

            var resumo = new ResumoDto(acordos.Count, acordos.Sum(a => a.QuantidadeDocumentos));

            logger.LogInformation(
                "Consulta consolidada do autor {AutorId}: {QuantidadeAcordos} acordos, {QuantidadeDocumentos} documentos (página {Pagina}, tamanho {TamanhoPagina})",
                autorId, resumo.QuantidadeAcordos, resumo.QuantidadeDocumentos, paginaDocumentos, tamanhoPaginaDocumentos);

            return new ConsultaAcordosDto(MontarAutor(autor), resumo, acordosDto);
        }

        public async Task<PaginaDocumentosDto> ConsultarDocumentosDoAcordoAsync(
            int autorId, int acordoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken)
        {
            ValidarId(autorId, "autor");
            ValidarId(acordoId, "acordo");
            ValidarPaginacao(pagina, tamanhoPagina);

            var acordo = await repositorio.ObterAcordoDoAutorAsync(autorId, acordoId, cancellationToken)
                ?? throw new RecursoNaoEncontradoException($"Acordo {acordoId} não encontrado para o autor {autorId}.");

            IReadOnlyList<DocumentoRegistro> documentos = acordo.QuantidadeDocumentos == 0
                ? Array.Empty<DocumentoRegistro>()
                : await repositorio.ListarDocumentosDoAcordoAsync(
                    acordoId, CalcularDeslocamento(pagina, tamanhoPagina), tamanhoPagina, cancellationToken);

            return new PaginaDocumentosDto(
                acordo.Id,
                acordo.Titulo,
                documentos.Select(ParaDto).ToList(),
                PaginacaoDto.Criar(pagina, tamanhoPagina, acordo.QuantidadeDocumentos));
        }

        /// <summary>"Rua X, Apto 10 - Cidade - CEP 00000-000"; partes ausentes são omitidas.</summary>
        public static string MontarEndereco(AutorRegistro autor)
        {
            var logradouro = string.Join(", ",
                new[] { autor.Rua, autor.Complemento }
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p!.Trim()));

            var cep = string.IsNullOrWhiteSpace(autor.Cep) ? null : $"CEP {autor.Cep.Trim()}";

            return string.Join(" - ",
                new[] { logradouro, autor.Cidade?.Trim(), cep }
                    .Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private AutorDto MontarAutor(AutorRegistro autor)
        {
            var dto = new AutorDto(
                autor.Id,
                autor.Nome,
                autor.Email,
                autor.Telefone,
                MontarEndereco(autor),
                new EmpresaDto(autor.EmpresaNome, autor.EmpresaSegmento));

            if (!opcoesMascaramento.Value.MascararRespostas)
                return dto;

            return dto with
            {
                Nome = MascaradorDadosPessoais.MascararNome(dto.Nome)!,
                Email = MascaradorDadosPessoais.MascararEmail(dto.Email)!,
                Telefone = MascaradorDadosPessoais.MascararTelefone(dto.Telefone),
                Endereco = MascaradorDadosPessoais.MascararEndereco(dto.Endereco)!
            };
        }

        private static DocumentoDto ParaDto(DocumentoRegistro d) => new(d.Id, d.Titulo, d.Url, d.ThumbnailUrl);

        private static long CalcularDeslocamento(int pagina, int tamanhoPagina)
            => (pagina - 1L) * tamanhoPagina;

        private static void ValidarId(int id, string entidade)
        {
            if (id < 1)
                throw new ValidacaoException($"O id do {entidade} deve ser um inteiro positivo.");
        }

        private static void ValidarPaginacao(int pagina, int tamanhoPagina)
        {
            if (pagina < LimitesPaginacao.PaginaMinima || pagina > LimitesPaginacao.PaginaMaxima)
                throw new ValidacaoException(
                    $"A página deve estar entre {LimitesPaginacao.PaginaMinima} e {LimitesPaginacao.PaginaMaxima}.");

            if (tamanhoPagina < LimitesPaginacao.TamanhoMinimo || tamanhoPagina > LimitesPaginacao.TamanhoMaximo)
                throw new ValidacaoException(
                    $"O tamanho da página deve estar entre {LimitesPaginacao.TamanhoMinimo} e {LimitesPaginacao.TamanhoMaximo}.");
        }
    }

}
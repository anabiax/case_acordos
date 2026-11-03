using CaseAcordos.Api.DTO;

namespace CaseAcordos.Api
{
    public interface IServicoConsultaAcordos
    {
        /// <summary>Consulta consolidada: autor, resumo, acordos e uma página de documentos de cada acordo.</summary>
        /// <exception cref="Tratamento.ValidacaoException">Id ou parâmetros de paginação inválidos.</exception>
        /// <exception cref="Tratamento.RecursoNaoEncontradoException">Autor inexistente.</exception>
        Task<ConsultaAcordosDto> ConsultarPorAutorAsync(int autorId, int paginaDocumentos, int tamanhoPaginaDocumentos, CancellationToken cancellationToken);


        /// <summary>Navega pelas páginas de documentos de um único acordo do autor.</summary>
        /// <exception cref="Tratamento.ValidacaoException">Ids ou parâmetros de paginação inválidos.</exception>
        /// <exception cref="Tratamento.RecursoNaoEncontradoException">Acordo inexistente para o autor.</exception>
        Task<PaginaDocumentosDto> ConsultarDocumentosDoAcordoAsync(int autorId, int acordoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken);
    }
}
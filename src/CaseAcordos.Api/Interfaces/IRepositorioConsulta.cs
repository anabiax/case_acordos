using CaseAcordos.Api.Modelo;

namespace CaseAcordos.Api.Interfaces
{
    /// <summary>
    /// Acesso somente-leitura ao banco relacional (Autor → Acordos → Documentos).
    /// </summary>
    public interface IRepositorioConsulta
    {
        Task<AutorRegistro?> ObterAutorAsync(int autorId, CancellationToken cancellationToken);


        /// <summary>Acordos do autor, já com a contagem total de documentos de cada um.</summary>
        Task<IReadOnlyList<AcordoRegistro>> ListarAcordosAsync(int autorId, CancellationToken cancellationToken);


        /// <summary>
        /// Uma página de documentos POR acordo, para todos os acordos do autor, em uma única ida ao banco.
        /// <paramref name="deslocamento"/> é o número de documentos a pular dentro de cada acordo.
        /// </summary>
        Task<IReadOnlyList<DocumentoRegistro>> ListarDocumentosPaginadosPorAutorAsync(
            int autorId, long deslocamento, int limite, CancellationToken cancellationToken);


        /// <summary>Acordo (com contagem de documentos) somente se pertencer ao autor informado.</summary>
        Task<AcordoRegistro?> ObterAcordoDoAutorAsync(int autorId, int acordoId, CancellationToken cancellationToken);


        Task<IReadOnlyList<DocumentoRegistro>> ListarDocumentosDoAcordoAsync(int acordoId, long deslocamento, int limite, CancellationToken cancellationToken);
    }
}
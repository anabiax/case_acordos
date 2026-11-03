namespace CaseAcordos.Api.DTO
{
    /// <param name="Id">Identificador do documento.</param>
    /// <param name="Titulo">Nome do documento.</param>
    /// <param name="Url">Link para acesso ao documento.</param>
    /// <param name="ThumbnailUrl">Link para a miniatura de pré-visualização.</param>
    public sealed record DocumentoDto(int Id, string Titulo, string Url, string ThumbnailUrl);
}
namespace CaseAcordos.Api.DTO
{
    /// <param name="Id">Identificador do acordo.</param>
    /// <param name="Titulo">Nome do acordo.</param>
    /// <param name="QuantidadeDocumentos">Total de documentos do acordo (não apenas os da página retornada).</param>
    /// <param name="Documentos">Documentos da página solicitada.</param>
    /// <param name="PaginacaoDocumentos">Metadados da paginação dos documentos deste acordo.</param>
    public sealed record AcordoDto(
        int Id,
        string Titulo,
        int QuantidadeDocumentos,
        IReadOnlyList<DocumentoDto> Documentos,
        PaginacaoDto PaginacaoDocumentos);
}
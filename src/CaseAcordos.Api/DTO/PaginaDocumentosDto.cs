namespace CaseAcordos.Api.DTO
{
    /// <summary>Resposta do endpoint dedicado à paginação dos documentos de um acordo.</summary>
    /// <param name="AcordoId">Identificador do acordo.</param>
    /// <param name="Titulo">Nome do acordo.</param>
    /// <param name="Itens">Documentos da página solicitada.</param>
    /// <param name="Paginacao">Metadados de paginação.</param>
    public sealed record PaginaDocumentosDto(
        int AcordoId,
        string Titulo,
        IReadOnlyList<DocumentoDto> Itens,
        PaginacaoDto Paginacao);
}
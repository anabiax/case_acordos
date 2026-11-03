namespace CaseAcordos.Api.DTO
{
    /// <param name="QuantidadeAcordos">Total de acordos do autor.</param>
    /// <param name="QuantidadeDocumentos">Total de documentos em todos os acordos do autor.</param>
    public sealed record ResumoDto(int QuantidadeAcordos, int QuantidadeDocumentos);
}
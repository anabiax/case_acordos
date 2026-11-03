namespace CaseAcordos.Api.DTO
{
    /// <param name="Nome">Nome da empresa.</param>
    /// <param name="Segmento">Segmento de atuação da empresa.</param>
    public sealed record EmpresaDto(string? Nome, string? Segmento);
}
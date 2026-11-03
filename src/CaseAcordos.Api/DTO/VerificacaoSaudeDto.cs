namespace CaseAcordos.Api.DTO
{
    /// <param name="Nome">Nome da verificação, ex.: "postgres".</param>
    /// <param name="Status">Estado da verificação.</param>
    /// <param name="Descricao">Descrição resumida (sem detalhes internos).</param>
    public sealed record VerificacaoSaudeDto(string Nome, string Status, string? Descricao);
}

namespace CaseAcordos.Api.DTO
{
    /// <summary>Relatório de saúde da API.</summary>
    /// <param name="Status">Estado geral: Healthy, Degraded ou Unhealthy.</param>
    /// <param name="DuracaoMs">Tempo total das verificações, em milissegundos.</param>
    /// <param name="Verificacoes">Resultado de cada verificação executada (vazio em /health/live).</param>
    public sealed record RelatorioSaudeDto(
        string Status,
        long DuracaoMs,
        IReadOnlyList<VerificacaoSaudeDto> Verificacoes);
}
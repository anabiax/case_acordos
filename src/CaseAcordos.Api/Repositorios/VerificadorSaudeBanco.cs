using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CaseAcordos.Api.Repositorios
{
    /// <summary>Health check de readiness: confirma que o PostgreSQL responde a um SELECT 1.</summary>
    public sealed class VerificadorSaudeBanco(NpgsqlDataSource fonteDados) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conexao = await fonteDados.OpenConnectionAsync(cancellationToken);
                await using var comando = new NpgsqlCommand("SELECT 1", conexao);
                await comando.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy("Banco de dados acessível.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A exceção vai para o log/relatório interno do health check; a resposta HTTP não a expõe.
                return HealthCheckResult.Unhealthy("Banco de dados indisponível.", ex);
            }
        }
    }
}
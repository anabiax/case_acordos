using CaseAcordos.Api.DTO;
using CaseAcordos.Api.Interfaces;
using CaseAcordos.Api.Middleware;
using CaseAcordos.Api.Repositorios;
using CaseAcordos.Api.Servicos;
using CaseAcordos.Api.Tratamento;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CaseAcordos.Api.Configuracao
{
    public static class ServicosExtensions
    {
        /// <summary>Opções tipadas, validadas na subida (fail-fast se faltar API Key ou conexão com o banco).</summary>
        public static IServiceCollection AddOpcoesDaApi(this IServiceCollection servicos)
        {
            servicos.AddOptions<OpcoesAutenticacao>()
                .BindConfiguration(OpcoesAutenticacao.Secao)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            servicos.AddOptions<OpcoesBanco>()
                .Configure<IConfiguration>((opcoes, configuracao) =>
                    opcoes.StringConexao = configuracao.GetConnectionString(OpcoesBanco.NomeConexao) ?? string.Empty)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            servicos.AddOptions<OpcoesSwagger>().BindConfiguration(OpcoesSwagger.Secao);
            servicos.AddOptions<OpcoesMascaramento>().BindConfiguration(OpcoesMascaramento.Secao);

            return servicos;
        }

        /// <summary>Camadas: serviço (regra) → repositório (Dapper/Npgsql).</summary>
        public static IServiceCollection AddCamadasDaApi(this IServiceCollection servicos)
        {
            servicos.AddSingleton(sp =>
                NpgsqlDataSource.Create(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcoesBanco>>().Value.StringConexao));

            servicos.AddScoped<IRepositorioConsulta, RepositorioConsultaPostgres>();
            servicos.AddScoped<IServicoConsultaAcordos, ServicoConsultaAcordos>();

            return servicos;
        }

        /// <summary>Erros padronizados (ProblemDetails/RFC 7807) com correlationId, e validação de entrada no mesmo formato.</summary>
        public static IServiceCollection AddTratamentoDeErros(this IServiceCollection servicos)
        {
            servicos.AddProblemDetails(o => o.CustomizeProblemDetails = TratadorGlobalExcecoes.Enriquecer);
            servicos.AddExceptionHandler<TratadorGlobalExcecoes>();

            // Falhas de validação de entrada (ex.: id "abc", id 0, página fora do limite) → 400 padronizado.
            servicos.Configure<ApiBehaviorOptions>(o =>
                o.InvalidModelStateResponseFactory = contexto =>
                {
                    var problema = new ValidationProblemDetails(contexto.ModelState)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Requisição inválida",
                        Detail = "Um ou mais parâmetros são inválidos.",
                        Instance = contexto.HttpContext.Request.Path.Value
                    };
                    problema.Extensions["correlationId"] =
                        contexto.HttpContext.Items[CorrelationIdMiddleware.ChaveItem]?.ToString();

                    return new BadRequestObjectResult(problema) { ContentTypes = { "application/problem+json" } };
                });

            return servicos;
        }

        /// <summary>/health (readiness, inclui o banco) e /health/live (liveness, sem dependências).</summary>
        public static IServiceCollection AddVerificacoesDeSaude(this IServiceCollection servicos)
        {
            servicos.AddHealthChecks()
                .AddCheck<VerificadorSaudeBanco>("postgres", tags: ["ready"]);

            return servicos;
        }

        public static WebApplication MapVerificacoesDeSaude(this WebApplication app)
        {
            app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                ResponseWriter = EscreverRelatorioAsync
            });

            app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = EscreverRelatorioAsync
            });

            return app;
        }

        private static Task EscreverRelatorioAsync(HttpContext contexto, HealthReport relatorio)
            => contexto.Response.WriteAsJsonAsync(new RelatorioSaudeDto(
                relatorio.Status.ToString(),
                (long)relatorio.TotalDuration.TotalMilliseconds,
                relatorio.Entries
                    .Select(e => new VerificacaoSaudeDto(e.Key, e.Value.Status.ToString(), e.Value.Description))
                    .ToList()));
    }
}
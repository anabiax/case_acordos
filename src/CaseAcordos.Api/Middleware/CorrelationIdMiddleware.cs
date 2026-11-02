using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CaseAcordos.Api.Middleware
{
    /// <summary>
    /// Garante um identificador de correlação por requisição: reaproveita o cabeçalho
    /// <c>X-Correlation-Id</c> (se válido) ou gera um novo, devolve-o na resposta e o coloca
    /// no escopo de log (todos os logs JSON da requisição saem com "CorrelationId").
    /// Também registra uma linha de log por requisição (método, caminho, status, duração).
    /// </summary>
    public sealed partial class CorrelationIdMiddleware(RequestDelegate proximo, ILogger<CorrelationIdMiddleware> logger)
    {
        public const string Cabecalho = "X-Correlation-Id";
        public const string ChaveItem = "CorrelationId";

        // Aceita só caracteres seguros (evita injeção de quebras de linha/lixo nos logs).
        [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
        private static partial Regex FormatoValido();

        public async Task InvokeAsync(HttpContext contexto)
        {
            var id = ObterOuCriar(contexto);
            contexto.Items[ChaveItem] = id;

            // OnStarting: o ExceptionHandler limpa os cabeçalhos da resposta; assim o id sempre sai.
            contexto.Response.OnStarting(() =>
            {
                contexto.Response.Headers[Cabecalho] = id;
                return Task.CompletedTask;
            });

            var cronometro = Stopwatch.StartNew();
            using (logger.BeginScope(new Dictionary<string, object> { [ChaveItem] = id }))
            {
                try
                {
                    await proximo(contexto);
                }
                finally
                {
                    logger.LogInformation(
                        "HTTP {Metodo} {Caminho} respondeu {StatusCode} em {DuracaoMs} ms",
                        contexto.Request.Method,
                        contexto.Request.Path.Value,
                        contexto.Response.StatusCode,
                        cronometro.ElapsedMilliseconds);
                }
            }
        }

        private static string ObterOuCriar(HttpContext contexto)
        {
            var recebido = contexto.Request.Headers[Cabecalho].ToString();
            return FormatoValido().IsMatch(recebido) ? recebido : Guid.NewGuid().ToString("N");
        }
    }

}
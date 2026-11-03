using CaseAcordos.Api.Configuracao;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace CaseAcordos.Api.Middleware
{
    /// <summary>
    /// Autenticação simples por API Key fixa (cabeçalho <c>X-Api-Key</c>), lida de variável de ambiente.
    /// Sem a chave correta → 401. Rotas públicas: /health*, /swagger* e a raiz (redireciona ao Swagger).
    /// </summary>
    public sealed class AutenticacaoApiKeyMiddleware
    {
        private readonly RequestDelegate _proximo;
        private readonly IProblemDetailsService _problemDetails;
        private readonly ILogger<AutenticacaoApiKeyMiddleware> _logger;
        private readonly string _nomeCabecalho;
        private readonly byte[] _hashChaveEsperada;

        public AutenticacaoApiKeyMiddleware(
            RequestDelegate proximo,
            IOptions<OpcoesAutenticacao> opcoes,
            IProblemDetailsService problemDetails,
            ILogger<AutenticacaoApiKeyMiddleware> logger)
        {
            _proximo = proximo;
            _problemDetails = problemDetails;
            _logger = logger;
            _nomeCabecalho = opcoes.Value.NomeCabecalho;
            // Guardamos apenas o hash: a comparação é feita em tempo constante sobre hashes de mesmo tamanho.
            _hashChaveEsperada = Hash(opcoes.Value.ApiKey);
        }

        public async Task InvokeAsync(HttpContext contexto)
        {
            if (EhCaminhoPublico(contexto.Request.Path))
            {
                await _proximo(contexto);
                return;
            }

            var apresentada = contexto.Request.Headers[_nomeCabecalho].ToString();

            if (string.IsNullOrEmpty(apresentada) || !ChaveValida(apresentada))
            {
                // Nunca logamos o valor da chave apresentada.
                _logger.LogWarning(
                    "Requisição não autenticada: {Metodo} {Caminho} (cabeçalho {Cabecalho} {Situacao})",
                    contexto.Request.Method,
                    contexto.Request.Path.Value,
                    _nomeCabecalho,
                    string.IsNullOrEmpty(apresentada) ? "ausente" : "inválido");

                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                contexto.Response.Headers.WWWAuthenticate = "ApiKey";

                await _problemDetails.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = contexto,
                    ProblemDetails =
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Não autenticado",
                    Detail = $"Informe uma API Key válida no cabeçalho '{_nomeCabecalho}'."
                }
                });
                return;
            }

            await _proximo(contexto);
        }

        private bool ChaveValida(string apresentada)
            => CryptographicOperations.FixedTimeEquals(Hash(apresentada), _hashChaveEsperada);

        private static byte[] Hash(string valor) => SHA256.HashData(Encoding.UTF8.GetBytes(valor));

        private static bool EhCaminhoPublico(PathString caminho)
            => caminho == "/"
               || caminho.StartsWithSegments("/health")
               || caminho.StartsWithSegments("/swagger");
    }

}
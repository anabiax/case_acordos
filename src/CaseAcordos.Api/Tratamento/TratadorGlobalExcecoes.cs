using CaseAcordos.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics;

namespace CaseAcordos.Api.Tratamento
{
    /// <summary>
    /// Tratamento centralizado de erros: traduz exceções em respostas RFC 7807 (ProblemDetails)
    /// com o status HTTP adequado, sem vazar detalhes internos (stack trace, SQL, host do banco).
    /// </summary>
    public sealed class TratadorGlobalExcecoes(
        IProblemDetailsService problemDetails,
        ILogger<TratadorGlobalExcecoes> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext contexto, Exception excecao, CancellationToken cancellationToken)
        {
            // Cliente desistiu da requisição: não há quem receba a resposta.
            if (excecao is OperationCanceledException && contexto.RequestAborted.IsCancellationRequested)
            {
                contexto.Response.StatusCode = 499;
                return true;
            }

            var (status, titulo, detalhe) = excecao switch
            {
                ValidacaoException e => (StatusCodes.Status400BadRequest, "Requisição inválida", e.Message),
                RecursoNaoEncontradoException e => (StatusCodes.Status404NotFound, "Recurso não encontrado", e.Message),
                FalhaConsultaException => (
                    StatusCodes.Status503ServiceUnavailable,
                    "Serviço temporariamente indisponível",
                    "Não foi possível consultar a base de dados. Tente novamente em instantes."),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "Erro interno",
                    "Ocorreu um erro inesperado. Informe o identificador de correlação ao suporte.")
            };

            if (status >= 500)
            {
                logger.LogError(excecao, "Erro ao processar {Metodo} {Caminho}: {Mensagem}",
                    contexto.Request.Method, contexto.Request.Path.Value, excecao.Message);
            }
            else
            {
                // Erros do cliente (4xx) não são falha do sistema: sem stack trace.
                logger.LogWarning("Requisição rejeitada ({Status}) em {Metodo} {Caminho}: {Mensagem}",
                    status, contexto.Request.Method, contexto.Request.Path.Value, excecao.Message);
            }

            contexto.Response.StatusCode = status;

            return await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = contexto,
                Exception = excecao,
                ProblemDetails =
            {
                Status = status,
                Title = titulo,
                Detail = detalhe
            }
            });
        }

        /// <summary>Acrescenta ao ProblemDetails de qualquer resposta o caminho e o correlationId.</summary>
        public static void Enriquecer(ProblemDetailsContext contexto)
        {
            contexto.ProblemDetails.Instance ??= contexto.HttpContext.Request.Path.Value;
            contexto.ProblemDetails.Extensions["correlationId"] =
                contexto.HttpContext.Items[CorrelationIdMiddleware.ChaveItem]?.ToString();
        }
    }
}
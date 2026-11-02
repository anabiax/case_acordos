using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CaseAcordos.Contratos;
using CaseAcordos.Persistencia;
using Microsoft.Extensions.Logging;

namespace CaseAcordos.Consumidor;

/// <summary>
/// Camada de serviço do consumidor: interpreta o corpo da mensagem, valida, grava e CLASSIFICA o resultado.
/// Não conhece o SQS (por isso é testável e poderia ser chamada por um Lambda com trigger de fila).
/// </summary>
public sealed class ProcessadorMensagem
{
    private readonly IRepositorioAcordos _repositorio;
    private readonly ILogger<ProcessadorMensagem> _logger;

    public ProcessadorMensagem(IRepositorioAcordos repositorio, ILogger<ProcessadorMensagem> logger)
    {
        _repositorio = repositorio;
        _logger = logger;
    }

    public async Task<ResultadoProcessamento> ProcessarAsync(string corpo, CancellationToken ct)
    {
        MensagemAcordo? mensagem;
        try
        {
            mensagem = SerializadorMensagens.Desserializar<MensagemAcordo>(corpo);
        }
        catch (JsonException ex)
        {
            return ResultadoProcessamento.NaoRetentavel($"JSON inválido: {ex.Message}");
        }

        var erroValidacao = ValidadorMensagem.Validar(mensagem);
        if (erroValidacao is not null)
        {
            return ResultadoProcessamento.NaoRetentavel($"Mensagem inválida: {erroValidacao}", mensagem?.CorrelationId);
        }

        var correlationId = mensagem!.CorrelationId;
        using var escopo = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId ?? "sem-correlation-id" });

        try
        {
            await _repositorio.GravarAsync(mensagem, ct);

            _logger.LogInformation(
                "Acordo gravado. AutorId={AutorId} AcordoId={AcordoId} Documentos={Documentos}",
                mensagem.Autor.Id, mensagem.Acordo.Id, mensagem.Documentos.Count);

            return ResultadoProcessamento.Sucesso(correlationId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FalhaPersistenciaException ex)
        {
            _logger.LogError(ex, "Falha ao gravar. AcordoId={AcordoId} Retentavel={Retentavel}", mensagem.Acordo.Id, ex.Retentavel);
            return ex.Retentavel
                ? ResultadoProcessamento.Retentavel(ex.Message, correlationId)
                : ResultadoProcessamento.NaoRetentavel(ex.Message, correlationId);
        }
        catch (Exception ex)
        {
            // Erro desconhecido: por segurança é tratado como retentável. Se persistir, a fila move para a DLQ após 3 recebimentos.
            _logger.LogError(ex, "Erro inesperado ao gravar. AcordoId={AcordoId}", mensagem.Acordo.Id);
            return ResultadoProcessamento.Retentavel(ex.Message, correlationId);
        }
    }
}

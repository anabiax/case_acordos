using Amazon.SQS;
using Amazon.SQS.Model;
using CaseAcordos.Mensageria;

namespace CaseAcordos.Consumidor;

/// <summary>
/// Lê a fila de acordos (long polling) e delega ao <see cref="ProcessadorMensagem"/>.
/// Sucesso: apaga. Falha retentável: não apaga (o SQS reentrega após o visibility timeout e,
/// no 3º recebimento, move para a DLQ de consumo via redrive policy).
/// Falha não retentável: copia para a DLQ de consumo com o motivo e apaga da fila de origem.
/// </summary>
public sealed class ConsumidorSqs : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly ResolvedorUrlFila _filas;
    private readonly OpcoesFilas _opcoesFilas;
    private readonly OpcoesConsumidor _opcoes;
    private readonly ProcessadorMensagem _processador;
    private readonly ILogger<ConsumidorSqs> _logger;

    public ConsumidorSqs(
        IAmazonSQS sqs,
        ResolvedorUrlFila filas,
        OpcoesFilas opcoesFilas,
        OpcoesConsumidor opcoes,
        ProcessadorMensagem processador,
        ILogger<ConsumidorSqs> logger)
    {
        _sqs = sqs;
        _filas = filas;
        _opcoesFilas = opcoesFilas;
        _opcoes = opcoes;
        _processador = processador;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Consumidor iniciado. Fila={Fila} GrauParalelismo={GrauParalelismo}", _opcoesFilas.FilaAcordos, _opcoes.GrauParalelismo);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var urlFila = await _filas.ObterUrlAsync(_opcoesFilas.FilaAcordos, ct);

                var resposta = await _sqs.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = urlFila,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 20,
                        MessageAttributeNames = new List<string> { "All" }
                    },
                    ct);

                if (resposta.Messages is not { Count: > 0 })
                {
                    continue;
                }

                await Parallel.ForEachAsync(
                    resposta.Messages,
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _opcoes.GrauParalelismo), CancellationToken = ct },
                    (mensagem, tk) => new ValueTask(TratarAsync(urlFila, mensagem, tk)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consumir a fila. Nova tentativa em 5s");
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("Consumidor encerrado");
    }

    private async Task TratarAsync(string urlFila, Message mensagem, CancellationToken ct)
    {
        var correlationId = ObterAtributo(mensagem, "CorrelationId") ?? "sem-correlation-id";
        using var escopo = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["MessageId"] = mensagem.MessageId ?? string.Empty
        });

        ResultadoProcessamento resultado;
        try
        {
            resultado = await _processador.ProcessarAsync(mensagem.Body ?? string.Empty, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            resultado = ResultadoProcessamento.Retentavel(ex.Message, correlationId);
        }

        switch (resultado.Tipo)
        {
            case TipoResultado.Sucesso:
                await ApagarAsync(urlFila, mensagem, ct);
                break;

            case TipoResultado.FalhaNaoRetentavel:
                _logger.LogError("Mensagem não retentável enviada à DLQ de consumo. Motivo={Motivo}", resultado.Motivo);
                if (await EnviarParaDlqAsync(mensagem, correlationId, resultado.Motivo, ct))
                {
                    await ApagarAsync(urlFila, mensagem, ct);
                }
                break;

            default:
                _logger.LogWarning("Falha retentável; a mensagem será reentregue pela fila. Motivo={Motivo}", resultado.Motivo);
                break;
        }
    }

    private async Task<bool> EnviarParaDlqAsync(Message mensagem, string correlationId, string? motivo, CancellationToken ct)
    {
        try
        {
            var urlDlq = await _filas.ObterUrlAsync(_opcoesFilas.FilaDlqConsumo, ct);
            await _sqs.SendMessageAsync(
                new SendMessageRequest
                {
                    QueueUrl = urlDlq,
                    MessageBody = mensagem.Body ?? string.Empty,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        ["CorrelationId"] = Atributo(correlationId),
                        ["MotivoFalha"] = Atributo(Truncar(motivo ?? "desconhecido", 1000)),
                        ["MessageIdOriginal"] = Atributo(mensagem.MessageId ?? string.Empty)
                    }
                },
                ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Não apaga da fila de origem: ela reentrega e, após 3 recebimentos, o próprio SQS move para a DLQ.
            _logger.LogError(ex, "Não foi possível enviar a mensagem para a DLQ de consumo");
            return false;
        }
    }

    private async Task ApagarAsync(string urlFila, Message mensagem, CancellationToken ct)
    {
        await _sqs.DeleteMessageAsync(
            new DeleteMessageRequest { QueueUrl = urlFila, ReceiptHandle = mensagem.ReceiptHandle }, ct);
    }

    private static MessageAttributeValue Atributo(string valor) =>
        new() { DataType = "String", StringValue = valor };

    private static string? ObterAtributo(Message mensagem, string nome) =>
        mensagem.MessageAttributes is not null && mensagem.MessageAttributes.TryGetValue(nome, out var valor)
            ? valor.StringValue
            : null;

    private static string Truncar(string texto, int max) => texto.Length <= max ? texto : texto[..max];
}

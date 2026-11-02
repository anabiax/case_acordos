using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using CaseAcordos.Contratos;
using CaseAcordos.Ingestao.Interfaces;
using CaseAcordos.Mensageria;

namespace CaseAcordos.Ingestao.Publicacao;

/// <summary>Publica no SQS (LocalStack localmente). O CorrelationId vai no corpo e como atributo da mensagem.</summary>
public sealed class PublicadorSqs : IPublicadorMensagens
{
    private readonly IAmazonSQS _sqs;
    private readonly ResolvedorUrlFila _filas;
    private readonly OpcoesFilas _opcoes;

    public PublicadorSqs(IAmazonSQS sqs, ResolvedorUrlFila filas, OpcoesFilas opcoes)
    {
        _sqs = sqs;
        _filas = filas;
        _opcoes = opcoes;
    }

    public Task PublicarAcordoAsync(MensagemAcordo mensagem, CancellationToken ct) =>
        EnviarAsync(_opcoes.FilaAcordos, SerializadorMensagens.Serializar(mensagem), mensagem.CorrelationId, ct);

    public Task PublicarFalhaAsync(FalhaIngestao falha, CancellationToken ct) =>
        EnviarAsync(_opcoes.FilaDlqIngestao, SerializadorMensagens.Serializar(falha), falha.CorrelationId, ct);

    private async Task EnviarAsync(string nomeFila, string corpo, string correlationId, CancellationToken ct)
    {
        var url = await _filas.ObterUrlAsync(nomeFila, ct);

        await _sqs.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = url,
                MessageBody = corpo,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["CorrelationId"] = new MessageAttributeValue { DataType = "String", StringValue = correlationId }
                }
            },
            ct);
    }
}
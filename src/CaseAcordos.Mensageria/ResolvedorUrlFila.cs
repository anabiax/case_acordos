using System.Collections.Concurrent;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace CaseAcordos.Mensageria;

/// <summary>
/// Descobre a URL de uma fila pelo nome e guarda em cache.
/// No LocalStack monta a URL a partir do endpoint configurado (evita o host
/// "*.localhost.localstack.cloud" que não resolve dentro da rede do Docker).
/// Na AWS usa GetQueueUrl.
/// </summary>
public sealed class ResolvedorUrlFila
{
    private readonly IAmazonSQS _sqs;
    private readonly OpcoesFilas _opcoes;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public ResolvedorUrlFila(IAmazonSQS sqs, OpcoesFilas opcoes)
    {
        _sqs = sqs;
        _opcoes = opcoes;
    }

    public async Task<string> ObterUrlAsync(string nomeFila, CancellationToken ct)
    {
        if (_cache.TryGetValue(nomeFila, out var urlEmCache))
        {
            return urlEmCache;
        }

        string url;
        if (!string.IsNullOrWhiteSpace(_opcoes.UrlServicoSqs))
        {
            url = $"{_opcoes.UrlServicoSqs.TrimEnd('/')}/{_opcoes.ContaId}/{nomeFila}";
        }
        else
        {
            var resposta = await _sqs.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = nomeFila }, ct);
            url = resposta.QueueUrl;
        }

        _cache[nomeFila] = url;
        return url;
    }
}

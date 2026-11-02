using Microsoft.Extensions.Logging.Abstractions;

namespace CaseAcordos.Ingestao.Resiliencia;

/// <summary>
/// Retentativa seletiva com backoff exponencial e jitter.
/// Só repete quando a falha é elegível (decidido por <c>ehElegivel</c>) e enquanto houver retentativas.
/// Quando esgota ou a falha não é elegível, a exceção original sobe para quem chamou.
/// </summary>
public sealed class PoliticaRetentativa
{
    private readonly OpcoesRetentativa _opcoes;
    private readonly ILogger _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _aguardar;
    private readonly Func<double> _sorteio;

    /// <param name="aguardar">Substituível nos testes para não esperar de verdade.</param>
    /// <param name="sorteio">Devolve um número entre 0 e 1; substituível nos testes.</param>
    public PoliticaRetentativa(
        OpcoesRetentativa opcoes,
        ILogger<PoliticaRetentativa>? logger = null,
        Func<TimeSpan, CancellationToken, Task>? aguardar = null,
        Func<double>? sorteio = null)
    {
        _opcoes = opcoes;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _aguardar = aguardar ?? ((atraso, ct) => Task.Delay(atraso, ct));
        _sorteio = sorteio ?? (() => Random.Shared.NextDouble());
    }

    /// <summary>Atraso antes da retentativa número (tentativa + 1): base * 2^tentativa + jitter.</summary>
    public TimeSpan CalcularAtraso(int tentativa)
    {
        var exponencial = TimeSpan.FromTicks(_opcoes.AtrasoBase.Ticks * (1L << tentativa));
        var jitter = TimeSpan.FromTicks((long)(_opcoes.JitterMaximo.Ticks * _sorteio()));
        return exponencial + jitter;
    }

    public async Task<T> ExecutarAsync<T>(
        Func<CancellationToken, Task<T>> operacao,
        Func<Exception, bool> ehElegivel,
        string descricao,
        CancellationToken ct)
    {
        for (var tentativa = 0; ; tentativa++)
        {
            try
            {
                return await operacao(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && tentativa < _opcoes.Maximo && ehElegivel(ex))
            {
                var atraso = CalcularAtraso(tentativa);
                _logger.LogWarning(
                    ex,
                    "Falha transitória em {Operacao}; retentativa {Retentativa}/{Maximo} em {AtrasoMs} ms",
                    descricao, tentativa + 1, _opcoes.Maximo, (int)atraso.TotalMilliseconds);

                await _aguardar(atraso, ct).ConfigureAwait(false);
            }
        }
    }
}

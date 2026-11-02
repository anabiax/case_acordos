using CaseAcordos.Ingestao.Servicos;

namespace CaseAcordos.Ingestao;

/// <summary>
/// Dispara a carga. Localmente substitui "EventBridge + Lambda":
/// IntervaloHoras = 0 executa uma vez e encerra o processo; IntervaloHoras = 3 repete a cada 3h.
/// O código de saída do processo é 1 quando alguma unidade da carga falhou.
/// </summary>
public sealed class ExecucaoIngestaoWorker : BackgroundService
{
    private readonly IServiceScopeFactory _escopos;
    private readonly OpcoesIngestao _opcoes;
    private readonly IHostApplicationLifetime _ciclo;
    private readonly ILogger<ExecucaoIngestaoWorker> _logger;

    public ExecucaoIngestaoWorker(
        IServiceScopeFactory escopos,
        OpcoesIngestao opcoes,
        IHostApplicationLifetime ciclo,
        ILogger<ExecucaoIngestaoWorker> logger)
    {
        _escopos = escopos;
        _opcoes = opcoes;
        _ciclo = ciclo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await ExecutarUmaVezAsync(ct);

            if (_opcoes.IntervaloHoras > 0)
            {
                using var temporizador = new PeriodicTimer(TimeSpan.FromHours(_opcoes.IntervaloHoras));
                while (await temporizador.WaitForNextTickAsync(ct))
                {
                    await ExecutarUmaVezAsync(ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // encerramento solicitado
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Falha inesperada na carga");
            Environment.ExitCode = 1;
        }
        finally
        {
            if (_opcoes.IntervaloHoras <= 0)
            {
                _ciclo.StopApplication();
            }
        }
    }

    private async Task ExecutarUmaVezAsync(CancellationToken ct)
    {
        var correlationId = Guid.NewGuid().ToString("N");

        // Um escopo por carga: o HttpClient tipado é recriado a cada execução (importante no modo a cada 3h).
        using var escopo = _escopos.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<ServicoIngestao>();

        var resultado = await servico.ExecutarAsync(correlationId, ct);
        if (!resultado.Sucesso)
        {
            Environment.ExitCode = 1;
        }
    }
}
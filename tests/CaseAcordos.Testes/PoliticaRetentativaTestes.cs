using System.Net;
using CaseAcordos.Ingestao.Resiliencia;

namespace CaseAcordos.Testes;

public sealed class PoliticaRetentativaTestes
{
    private readonly List<TimeSpan> _atrasos = new();

    // sorteio fixo em 0,5 => jitter de 250 ms (metade de 500 ms); atrasos esperados: 1,25s / 2,25s / 4,25s
    private PoliticaRetentativa CriarPolitica() => new(
        new OpcoesRetentativa(),
        logger: null,
        aguardar: (atraso, ct) => { _atrasos.Add(atraso); return Task.CompletedTask; },
        sorteio: () => 0.5);

    [Fact]
    public async Task Sucesso_na_primeira_tentativa_nao_espera()
    {
        var tentativas = 0;

        var resultado = await CriarPolitica().ExecutarAsync(
            _ => { tentativas++; return Task.FromResult(42); },
            ClassificadorFalhaHttp.EhElegivel, "teste", CancellationToken.None);

        Assert.Equal(42, resultado);
        Assert.Equal(1, tentativas);
        Assert.Empty(_atrasos);
    }

    [Fact]
    public async Task Falha_transitoria_ate_dar_certo_usa_backoff_exponencial()
    {
        var tentativas = 0;

        var resultado = await CriarPolitica().ExecutarAsync(
            _ =>
            {
                tentativas++;
                if (tentativas <= 2) throw new TimeoutException("lento");
                return Task.FromResult("ok");
            },
            ClassificadorFalhaHttp.EhElegivel, "teste", CancellationToken.None);

        Assert.Equal("ok", resultado);
        Assert.Equal(3, tentativas);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(1250), TimeSpan.FromMilliseconds(2250) }, _atrasos.ToArray());
    }

    [Fact]
    public async Task Esgota_tres_retentativas_e_propaga_a_excecao_original()
    {
        var tentativas = 0;

        var erro = await Assert.ThrowsAsync<TimeoutException>(() => CriarPolitica().ExecutarAsync<int>(
            _ => { tentativas++; throw new TimeoutException("sempre lento"); },
            ClassificadorFalhaHttp.EhElegivel, "teste", CancellationToken.None));

        Assert.Equal("sempre lento", erro.Message);
        Assert.Equal(4, tentativas); // 1 tentativa + 3 retentativas
        Assert.Equal(
            new[] { TimeSpan.FromMilliseconds(1250), TimeSpan.FromMilliseconds(2250), TimeSpan.FromMilliseconds(4250) },
            _atrasos.ToArray());
    }

    [Fact]
    public async Task Falha_nao_elegivel_nao_e_repetida()
    {
        var tentativas = 0;

        await Assert.ThrowsAsync<ArgumentException>(() => CriarPolitica().ExecutarAsync<int>(
            _ => { tentativas++; throw new ArgumentException("erro de negócio"); },
            ClassificadorFalhaHttp.EhElegivel, "teste", CancellationToken.None));

        Assert.Equal(1, tentativas);
        Assert.Empty(_atrasos);
    }

    [Fact]
    public async Task Cancelamento_do_chamador_interrompe_sem_repetir()
    {
        using var cts = new CancellationTokenSource();
        var tentativas = 0;

        await Assert.ThrowsAsync<TimeoutException>(() => CriarPolitica().ExecutarAsync<int>(
            _ => { tentativas++; cts.Cancel(); throw new TimeoutException("lento"); },
            ClassificadorFalhaHttp.EhElegivel, "teste", cts.Token));

        Assert.Equal(1, tentativas);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(408, true)]
    [InlineData(429, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    public void Classifica_status_http_elegivel_a_retentativa(int status, bool esperado)
    {
        var erro = new HttpRequestException("falha", null, (HttpStatusCode)status);

        Assert.Equal(esperado, ClassificadorFalhaHttp.EhElegivel(erro));
    }

    [Fact]
    public void Falha_de_rede_sem_status_e_timeout_sao_elegiveis_e_outros_erros_nao()
    {
        Assert.True(ClassificadorFalhaHttp.EhElegivel(new HttpRequestException("sem conexão")));
        Assert.True(ClassificadorFalhaHttp.EhElegivel(new TimeoutException()));
        Assert.False(ClassificadorFalhaHttp.EhElegivel(new InvalidOperationException()));
    }
}

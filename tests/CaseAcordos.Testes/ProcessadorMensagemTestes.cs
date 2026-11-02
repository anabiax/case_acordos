using System;
using System.Threading;
using System.Threading.Tasks;
using CaseAcordos.Consumidor;
using CaseAcordos.Contratos;
using CaseAcordos.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseAcordos.Testes;

public sealed class ProcessadorMensagemTestes
{
    private readonly RepositorioFake _repositorio = new();

    private ProcessadorMensagem CriarProcessador() =>
        new(_repositorio, NullLogger<ProcessadorMensagem>.Instance);

    private static string Corpo(MensagemAcordo mensagem) => SerializadorMensagens.Serializar(mensagem);

    [Fact]
    public async Task Mensagem_valida_e_gravada_e_retorna_sucesso()
    {
        var resultado = await CriarProcessador().ProcessarAsync(Corpo(Dados.Mensagem(1, 10, 1, 2, 3)), CancellationToken.None);

        Assert.Equal(TipoResultado.Sucesso, resultado.Tipo);
        Assert.Equal("corr-1", resultado.CorrelationId);
        var gravada = Assert.Single(_repositorio.Gravadas);
        Assert.Equal(10, gravada.Acordo.Id);
        Assert.Equal(3, gravada.Documentos.Count);
    }

    [Fact]
    public async Task Mesma_mensagem_entregue_duas_vezes_e_aceita_nas_duas_pois_a_idempotencia_esta_no_upsert()
    {
        var corpo = Corpo(Dados.Mensagem());
        var processador = CriarProcessador();

        var primeira = await processador.ProcessarAsync(corpo, CancellationToken.None);
        var segunda = await processador.ProcessarAsync(corpo, CancellationToken.None);

        Assert.Equal(TipoResultado.Sucesso, primeira.Tipo);
        Assert.Equal(TipoResultado.Sucesso, segunda.Tipo);
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("{ \"autor\": ")]
    public async Task Json_invalido_e_nao_retentavel_e_nao_toca_no_banco(string corpo)
    {
        var resultado = await CriarProcessador().ProcessarAsync(corpo, CancellationToken.None);

        Assert.Equal(TipoResultado.FalhaNaoRetentavel, resultado.Tipo);
        Assert.Contains("JSON inválido", resultado.Motivo!);
        Assert.Empty(_repositorio.Gravadas);
    }

    [Fact]
    public async Task Json_literal_null_e_nao_retentavel()
    {
        var resultado = await CriarProcessador().ProcessarAsync("null", CancellationToken.None);

        Assert.Equal(TipoResultado.FalhaNaoRetentavel, resultado.Tipo);
        Assert.Empty(_repositorio.Gravadas);
    }

    [Fact]
    public async Task Autor_sem_nome_e_nao_retentavel()
    {
        var mensagem = Dados.Mensagem() with { Autor = Dados.Mensagem().Autor with { Nome = " " } };

        var resultado = await CriarProcessador().ProcessarAsync(Corpo(mensagem), CancellationToken.None);

        Assert.Equal(TipoResultado.FalhaNaoRetentavel, resultado.Tipo);
        Assert.Contains("autor.nome", resultado.Motivo!);
        Assert.Equal("corr-1", resultado.CorrelationId);
        Assert.Empty(_repositorio.Gravadas);
    }

    [Fact]
    public async Task Documentos_com_id_repetido_na_mesma_mensagem_sao_nao_retentaveis()
    {
        var resultado = await CriarProcessador().ProcessarAsync(Corpo(Dados.Mensagem(1, 10, 7, 7)), CancellationToken.None);

        Assert.Equal(TipoResultado.FalhaNaoRetentavel, resultado.Tipo);
        Assert.Contains("repetido", resultado.Motivo!);
    }

    [Fact]
    public async Task Corpo_sem_a_lista_de_documentos_e_nao_retentavel()
    {
        var resultado = await CriarProcessador().ProcessarAsync(
            "{\"correlationId\":\"c\",\"autor\":{\"id\":1,\"nome\":\"A\",\"email\":\"a@a\"},\"acordo\":{\"id\":2,\"titulo\":\"T\"}}",
            CancellationToken.None);

        Assert.Equal(TipoResultado.FalhaNaoRetentavel, resultado.Tipo);
        Assert.Contains("documentos", resultado.Motivo!);
    }

    [Fact]
    public async Task Falha_de_persistencia_retentavel_nao_apaga_a_mensagem()
    {
        _repositorio.Erro = new FalhaPersistenciaException("banco indisponível", retentavel: true);

        var resultado = await CriarProcessador().ProcessarAsync(Corpo(Dados.Mensagem()), CancellationToken.None);

        Assert.Equal(TipoResultado.FalhaRetentavel, resultado.Tipo);
        Assert.Equal("banco indisponível", resultado.Motivo);
    }

    [Fact]
    public async Task Falha_de_persistencia_nao_retentavel_vai_direto_para_a_dlq()
    {
        _repositorio.Erro = new FalhaPersistenciaException("texto maior que a coluna", retentavel: false);

        var resultado = await CriarProcessador().ProcessarAsync(Corpo(Dados.Mensagem()), CancellationToken.None);

        Assert.Equal(TipoResultado.FalhaNaoRetentavel, resultado.Tipo);
    }

    [Fact]
    public async Task Excecao_inesperada_e_tratada_como_retentavel()
    {
        _repositorio.Erro = new InvalidOperationException("algo estranho");

        var resultado = await CriarProcessador().ProcessarAsync(Corpo(Dados.Mensagem()), CancellationToken.None);

        Assert.Equal(TipoResultado.FalhaRetentavel, resultado.Tipo);
    }

    [Fact]
    public async Task Cancelamento_nao_e_classificado_como_falha()
    {
        _repositorio.Erro = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CriarProcessador().ProcessarAsync(Corpo(Dados.Mensagem()), CancellationToken.None));
    }
}

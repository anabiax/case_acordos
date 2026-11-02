using CaseAcordos.Ingestao;
using CaseAcordos.Ingestao.Servicos;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaseAcordos.Testes;

public sealed class ServicoIngestaoTestes
{
    private readonly ClienteFake _cliente = new();
    private readonly PublicadorFake _publicador = new();

    private ServicoIngestao CriarServico(int paralelismo = 1) =>
        new(_cliente, _publicador, new OpcoesIngestao { GrauParalelismo = paralelismo }, NullLogger<ServicoIngestao>.Instance);

    /// <summary>Usuário 1 com acordos 11 (2 docs) e 12 (1 doc); usuário 2 com acordo 21 (3 docs).</summary>
    private void MontarCenarioFeliz()
    {
        _cliente.Usuarios.AddRange(new[] { Dados.Usuario(1), Dados.Usuario(2) });
        _cliente.AcordosPorUsuario[1] = new() { Dados.Acordo(1, 11), Dados.Acordo(1, 12) };
        _cliente.AcordosPorUsuario[2] = new() { Dados.Acordo(2, 21) };
        _cliente.DocumentosPorAcordo[11] = new() { Dados.Documento(11, 1), Dados.Documento(11, 2) };
        _cliente.DocumentosPorAcordo[12] = new() { Dados.Documento(12, 3) };
        _cliente.DocumentosPorAcordo[21] = new() { Dados.Documento(21, 4), Dados.Documento(21, 5), Dados.Documento(21, 6) };
    }

    [Fact]
    public async Task Carga_completa_publica_uma_mensagem_por_acordo_com_o_mesmo_correlationId()
    {
        MontarCenarioFeliz();

        var resultado = await CriarServico().ExecutarAsync("corr-1", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(2, resultado.Usuarios);
        Assert.Equal(3, resultado.Acordos);
        Assert.Equal(6, resultado.Documentos);
        Assert.Equal(3, resultado.MensagensPublicadas);
        Assert.Equal(0, resultado.Falhas);

        var mensagens = _publicador.Mensagens;
        Assert.Equal(3, mensagens.Count);
        Assert.True(mensagens.All(m => m.CorrelationId == "corr-1"));
        Assert.Equal(new[] { 11, 12, 21 }, mensagens.Select(m => m.Acordo.Id).OrderBy(i => i).ToArray());
        Assert.Equal(2, mensagens.Single(m => m.Acordo.Id == 11).Documentos.Count);
        Assert.Equal(2, mensagens.Single(m => m.Acordo.Id == 21).Autor.Id); // autor embutido na mensagem
        Assert.Empty(_publicador.Falhas);
    }

    [Fact]
    public async Task Falha_nos_documentos_de_um_acordo_vai_para_a_dlq_e_a_carga_continua()
    {
        MontarCenarioFeliz();
        _cliente.AcordosSemDocumentos.Add(11);

        var resultado = await CriarServico().ExecutarAsync("corr-2", CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(1, resultado.Falhas);
        Assert.Equal(2, resultado.MensagensPublicadas); // 12 e 21 seguiram

        var falha = Assert.Single(_publicador.Falhas);
        Assert.Equal("corr-2", falha.CorrelationId);
        Assert.Equal("documentos", falha.Etapa);
        Assert.Equal(1, falha.UsuarioId);
        Assert.Equal(11, falha.AcordoId);
        Assert.True(falha.Retentavel); // 503 é transitório
    }

    [Fact]
    public async Task Falha_nos_acordos_de_um_usuario_nao_impede_os_demais_usuarios()
    {
        MontarCenarioFeliz();
        _cliente.UsuariosSemAcordos.Add(1);

        var resultado = await CriarServico().ExecutarAsync("corr-3", CancellationToken.None);

        Assert.Equal(1, resultado.Falhas);
        Assert.Equal(1, resultado.MensagensPublicadas); // só o acordo 21 do usuário 2

        var falha = Assert.Single(_publicador.Falhas);
        Assert.Equal("acordos", falha.Etapa);
        Assert.Equal(1, falha.UsuarioId);
        Assert.Null(falha.AcordoId);
    }

    [Fact]
    public async Task Falha_ao_listar_usuarios_registra_falha_e_encerra_a_carga()
    {
        _cliente.FalharListaDeUsuarios = true;

        var resultado = await CriarServico().ExecutarAsync("corr-4", CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(1, resultado.Falhas);
        Assert.Equal(0, resultado.MensagensPublicadas);
        Assert.Equal("usuarios", Assert.Single(_publicador.Falhas).Etapa);
    }

    [Fact]
    public async Task Falha_ao_publicar_na_fila_e_registrada_como_publicacao_e_a_carga_continua()
    {
        MontarCenarioFeliz();
        _publicador.FalharAoPublicarAcordo = true;

        var resultado = await CriarServico().ExecutarAsync("corr-5", CancellationToken.None);

        Assert.Equal(3, resultado.Falhas);
        Assert.Equal(0, resultado.MensagensPublicadas);
        Assert.True(_publicador.Falhas.All(f => f.Etapa == "publicacao"));
    }

    [Fact]
    public async Task Se_a_dlq_de_ingestao_tambem_falhar_a_carga_nao_quebra()
    {
        MontarCenarioFeliz();
        _cliente.AcordosSemDocumentos.Add(11);
        _publicador.FalharAoPublicarFalha = true;

        var resultado = await CriarServico().ExecutarAsync("corr-6", CancellationToken.None);

        Assert.Equal(1, resultado.Falhas);
        Assert.Equal(2, resultado.MensagensPublicadas);
    }

    [Fact]
    public async Task Paralelismo_maior_que_um_gera_o_mesmo_resultado()
    {
        MontarCenarioFeliz();

        var resultado = await CriarServico(paralelismo: 4).ExecutarAsync("corr-7", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(3, resultado.MensagensPublicadas);
        Assert.Equal(6, resultado.Documentos);
    }
}

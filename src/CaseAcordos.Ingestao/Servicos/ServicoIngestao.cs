using CaseAcordos.Contratos;
using CaseAcordos.Ingestao.DTO;
using CaseAcordos.Ingestao.Integracao;
using CaseAcordos.Ingestao.Interfaces;
using CaseAcordos.Ingestao.Resiliencia;

namespace CaseAcordos.Ingestao.Servicos;

/// <summary>
/// Orquestra a carga: /users -> /users/{id}/albums -> /albums/{id}/photos, traduz e publica
/// UMA mensagem por acordo (com o autor embutido).
/// Cada unidade que falha (usuários, acordos de um usuário, documentos de um acordo, publicação)
/// é registrada na DLQ de ingestão e a carga CONTINUA com o restante.
/// Todos os logs e mensagens levam o mesmo CorrelationId da carga.
/// </summary>
public sealed class ServicoIngestao
{
    private readonly IClienteIntegracaoExterna _cliente;
    private readonly IPublicadorMensagens _publicador;
    private readonly OpcoesIngestao _opcoes;
    private readonly ILogger<ServicoIngestao> _logger;

    public ServicoIngestao(
        IClienteIntegracaoExterna cliente,
        IPublicadorMensagens publicador,
        OpcoesIngestao opcoes,
        ILogger<ServicoIngestao> logger)
    {
        _cliente = cliente;
        _publicador = publicador;
        _opcoes = opcoes;
        _logger = logger;
    }

    public async Task<ResultadoIngestao> ExecutarAsync(string correlationId, CancellationToken ct)
    {
        using var escopo = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var contadores = new Contadores();

        _logger.LogInformation("Carga iniciada. Grau de paralelismo: {GrauParalelismo}", _opcoes.GrauParalelismo);

        IReadOnlyList<UsuarioExternoDTO>? usuarios = null;
        try
        {
            usuarios = await _cliente.ListarUsuariosAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RegistrarFalhaAsync(contadores, correlationId, "usuarios", null, null, ex, ct);
        }

        if (usuarios is not null)
        {
            var paralelismo = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, _opcoes.GrauParalelismo),
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(
                usuarios,
                paralelismo,
                (usuario, tk) => new ValueTask(ProcessarUsuarioAsync(usuario, correlationId, contadores, tk)));
        }

        relogio.Stop();
        var resultado = new ResultadoIngestao(
            correlationId,
            contadores.Usuarios,
            contadores.Acordos,
            contadores.Documentos,
            contadores.Mensagens,
            contadores.Falhas,
            relogio.Elapsed);

        _logger.LogInformation(
            "Carga finalizada. Usuarios={Usuarios} Acordos={Acordos} Documentos={Documentos} MensagensPublicadas={MensagensPublicadas} Falhas={Falhas} DuracaoMs={DuracaoMs}",
            resultado.Usuarios, resultado.Acordos, resultado.Documentos, resultado.MensagensPublicadas,
            resultado.Falhas, (long)resultado.Duracao.TotalMilliseconds);

        return resultado;
    }

    private async Task ProcessarUsuarioAsync(UsuarioExternoDTO usuario, string correlationId, Contadores contadores, CancellationToken ct)
    {
        IReadOnlyList<AcordoExternoDTO> acordos;
        try
        {
            acordos = await _cliente.ListarAcordosUsuarioAsync(usuario.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RegistrarFalhaAsync(contadores, correlationId, "acordos", usuario.Id, null, ex, ct);
            return;
        }

        Interlocked.Increment(ref contadores.Usuarios);

        foreach (var acordo in acordos)
        {
            await ProcessarAcordoAsync(usuario, acordo, correlationId, contadores, ct);
        }
    }

    private async Task ProcessarAcordoAsync(UsuarioExternoDTO usuario, AcordoExternoDTO acordo, string correlationId, Contadores contadores, CancellationToken ct)
    {
        IReadOnlyList<DocumentoExternoDTO> documentos;
        try
        {
            documentos = await _cliente.ListarDocumentosAcordoAsync(acordo.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RegistrarFalhaAsync(contadores, correlationId, "documentos", usuario.Id, acordo.Id, ex, ct);
            return;
        }

        var mensagem = Mapeador.ParaMensagem(correlationId, usuario, acordo, documentos);

        try
        {
            await _publicador.PublicarAcordoAsync(mensagem, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RegistrarFalhaAsync(contadores, correlationId, "publicacao", usuario.Id, acordo.Id, ex, ct);
            return;
        }

        Interlocked.Increment(ref contadores.Acordos);
        Interlocked.Increment(ref contadores.Mensagens);
        Interlocked.Add(ref contadores.Documentos, documentos.Count);

        _logger.LogInformation(
            "Acordo publicado. UsuarioId={UsuarioId} AcordoId={AcordoId} Documentos={Documentos}",
            usuario.Id, acordo.Id, documentos.Count);
    }

    private async Task RegistrarFalhaAsync(
        Contadores contadores, string correlationId, string etapa, int? usuarioId, int? acordoId, Exception erro, CancellationToken ct)
    {
        Interlocked.Increment(ref contadores.Falhas);

        _logger.LogError(
            erro,
            "Falha na ingestão. Etapa={Etapa} UsuarioId={UsuarioId} AcordoId={AcordoId}",
            etapa, usuarioId, acordoId);

        var falha = new FalhaIngestao(
            correlationId, etapa, usuarioId, acordoId, erro.Message,
            ClassificadorFalhaHttp.EhElegivel(erro), DateTimeOffset.UtcNow);

        try
        {
            await _publicador.PublicarFalhaAsync(falha, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Não foi possível publicar a falha na DLQ de ingestão. Etapa={Etapa}", etapa);
        }
    }

    private sealed class Contadores
    {
        public int Usuarios;
        public int Acordos;
        public int Documentos;
        public int Mensagens;
        public int Falhas;
    }
}

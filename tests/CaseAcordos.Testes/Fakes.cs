using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CaseAcordos.Contratos.Falhas;
using CaseAcordos.Contratos.Mensagens;
using CaseAcordos.Ingestao.DTO;
using CaseAcordos.Ingestao.Integracao;
using CaseAcordos.Ingestao.Interfaces;
using CaseAcordos.Persistencia.Interfaces;

namespace CaseAcordos.Testes;

internal static class Dados
{
    public static UsuarioExternoDTO Usuario(int id) => new(
        id, $"Usuario {id}", $"usuario{id}@teste.com", "(11) 99999-0000",
        new EnderecoExternoDTO("Rua X", "Apto 10", "Cidade", "00000-000"),
        new EmpresaExternaDTO($"Empresa {id}", "segmento do usuario"));

    public static AcordoExternoDTO Acordo(int usuarioId, int id) => new(usuarioId, id, $"Acordo {id}");

    public static DocumentoExternoDTO Documento(int acordoId, int id) =>
        new(acordoId, id, $"Documento {id}", $"https://exemplo.com/{id}", $"https://exemplo.com/t/{id}");

    public static MensagemAcordo Mensagem(int autorId = 1, int acordoId = 10, params int[] documentoIds) => new(
        "corr-1",
        Mapeador.ParaAutor(Usuario(autorId)),
        new AcordoMensagem(acordoId, "Acordo"),
        (documentoIds.Length == 0 ? new[] { 100 } : documentoIds)
            .Select(id => new DocumentoMensagem(id, $"Doc {id}", "https://e.com/u", "https://e.com/t"))
            .ToList());
}

internal sealed class ClienteFake : IClienteIntegracaoExterna
{
    public List<UsuarioExternoDTO> Usuarios { get; } = new();
    public Dictionary<int, List<AcordoExternoDTO>> AcordosPorUsuario { get; } = new();
    public Dictionary<int, List<DocumentoExternoDTO>> DocumentosPorAcordo { get; } = new();

    public bool FalharListaDeUsuarios { get; set; }
    public HashSet<int> UsuariosSemAcordos { get; } = new();   // chamada de acordos falha
    public HashSet<int> AcordosSemDocumentos     { get; } = new();      // chamada de documentos falha

    public static HttpRequestException Indisponivel() =>
        new("serviço indisponível", null, HttpStatusCode.ServiceUnavailable);

    public Task<IReadOnlyList<UsuarioExternoDTO>> ListarUsuariosAsync(CancellationToken ct)
    {
        if (FalharListaDeUsuarios) throw Indisponivel();
        return Task.FromResult<IReadOnlyList<UsuarioExternoDTO>>(Usuarios);
    }

    public Task<IReadOnlyList<AcordoExternoDTO>> ListarAcordosUsuarioAsync(int usuarioId, CancellationToken ct)
    {
        if (UsuariosSemAcordos.Contains(usuarioId)) throw Indisponivel();
        var lista = AcordosPorUsuario.TryGetValue(usuarioId, out var a) ? a : new List<AcordoExternoDTO>();
        return Task.FromResult<IReadOnlyList<AcordoExternoDTO>>(lista);
    }

    public Task<IReadOnlyList<DocumentoExternoDTO>> ListarDocumentosAcordoAsync(int acordoId, CancellationToken ct)
    {
        if (AcordosSemDocumentos.Contains(acordoId)) throw Indisponivel();
        var lista = DocumentosPorAcordo.TryGetValue(acordoId, out var f) ? f : new List<DocumentoExternoDTO>();
        return Task.FromResult<IReadOnlyList<DocumentoExternoDTO>>(lista);
    }
}

internal sealed class PublicadorFake : IPublicadorMensagens
{
    private readonly object _trava = new();
    private readonly List<MensagemAcordo> _mensagens = new();
    private readonly List<FalhaIngestao> _falhas = new();

    public bool FalharAoPublicarAcordo { get; set; }
    public bool FalharAoPublicarFalha { get; set; }

    public List<MensagemAcordo> Mensagens { get { lock (_trava) return _mensagens.ToList(); } }
    public List<FalhaIngestao> Falhas { get { lock (_trava) return _falhas.ToList(); } }

    public Task PublicarAcordoAsync(MensagemAcordo mensagem, CancellationToken ct)
    {
        if (FalharAoPublicarAcordo) throw new InvalidOperationException("fila fora do ar");
        lock (_trava) _mensagens.Add(mensagem);
        return Task.CompletedTask;
    }

    public Task PublicarFalhaAsync(FalhaIngestao falha, CancellationToken ct)
    {
        if (FalharAoPublicarFalha) throw new InvalidOperationException("DLQ fora do ar");
        lock (_trava) _falhas.Add(falha);
        return Task.CompletedTask;
    }
}

internal sealed class RepositorioFake : IRepositorioAcordos
{
    public List<MensagemAcordo> Gravadas { get; } = new();
    public Exception? Erro { get; set; }

    public Task GravarAsync(MensagemAcordo mensagem, CancellationToken ct)
    {
        if (Erro is not null) throw Erro;
        Gravadas.Add(mensagem);
        return Task.CompletedTask;
    }
}

/// <summary>Responde às chamadas HTTP na ordem em que foram enfileiradas.</summary>
internal sealed class ManipuladorHttpFake : HttpMessageHandler
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _respostas = new();

    public int Chamadas { get; private set; }
    public List<string> Urls { get; } = new();

    public ManipuladorHttpFake Enfileirar(Func<CancellationToken, Task<HttpResponseMessage>> resposta)
    {
        _respostas.Enqueue(resposta);
        return this;
    }

    public ManipuladorHttpFake Status(HttpStatusCode status) =>
        Enfileirar(_ => Task.FromResult(new HttpResponseMessage(status)));

    public ManipuladorHttpFake Json(string json) =>
        Enfileirar(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage requisicao, CancellationToken ct)
    {
        Chamadas++;
        Urls.Add(requisicao.RequestUri!.ToString());
        return await _respostas.Dequeue()(ct);
    }
}
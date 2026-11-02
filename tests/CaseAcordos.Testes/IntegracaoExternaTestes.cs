using System;
using System.Net;
using CaseAcordos.Ingestao.Integracao;
using CaseAcordos.Ingestao.Resiliencia;
using Xunit;

namespace CaseAcordos.Testes;

public sealed class IntegracaoExternaTestes
{
    private const string UsuarioJson = """
        [{
          "id": 5, "name": "Chelsey Dietrich", "username": "Kamren", "email": "Lucio_Hettinger@annie.ca",
          "address": { "street": "Skiles Walks", "suite": "Suite 351", "city": "Roscoeview", "zipcode": "33263",
                       "geo": { "lat": "-31.8129", "lng": "62.5342" } },
          "phone": "(254)954-1289", "website": "demarco.info",
          "company": { "name": "Keebler LLC", "catchPhrase": "User-centric fault-tolerant solution", "bs": "revolutionize end-to-end systems" }
        }]
        """;

    private static ClienteIntegracaoExterna CriarCliente(ManipuladorHttpFake manipulador, TimeSpan? timeout = null)
    {
        var opcoes = new OpcoesRetentativa { TimeoutPorTentativa = timeout ?? TimeSpan.FromSeconds(5) };
        var politica = new PoliticaRetentativa(opcoes, null, (_, _) => Task.CompletedTask, () => 0);
        var http = new HttpClient(manipulador) { BaseAddress = new Uri("https://jsonplaceholder.typicode.com/") };
        return new ClienteIntegracaoExterna(http, politica, opcoes);
    }

    [Fact]
    public async Task Desserializa_usuarios_ignorando_campos_nao_mapeados()
    {
        var manipulador = new ManipuladorHttpFake().Json(UsuarioJson);

        var usuarios = await CriarCliente(manipulador).ListarUsuariosAsync(CancellationToken.None);

        var usuario = Assert.Single(usuarios);
        Assert.Equal(5, usuario.Id);
        Assert.Equal("Chelsey Dietrich", usuario.Name);
        Assert.Equal("Suite 351", usuario.Address!.Suite);
        Assert.Equal("revolutionize end-to-end systems", usuario.Company!.Bs);
        Assert.Equal("https://jsonplaceholder.typicode.com/users", manipulador.Urls[0]);
    }

    [Fact]
    public async Task Chama_os_endpoints_do_case_de_acordos_e_documentos()
    {
        var manipulador = new ManipuladorHttpFake().Json("[]").Json("[]");
        var cliente = CriarCliente(manipulador);

        await cliente.ListarAcordosUsuarioAsync(5, CancellationToken.None);
        await cliente.ListarDocumentosAcordoAsync(41, CancellationToken.None);

        Assert.Equal("https://jsonplaceholder.typicode.com/users/5/albums", manipulador.Urls[0]);
        Assert.Equal("https://jsonplaceholder.typicode.com/albums/41/photos", manipulador.Urls[1]);
    }

    [Fact]
    public async Task Repete_em_503_ate_dar_certo()
    {
        var manipulador = new ManipuladorHttpFake()
            .Status(HttpStatusCode.ServiceUnavailable)
            .Status(HttpStatusCode.ServiceUnavailable)
            .Json("[]");

        var albuns = await CriarCliente(manipulador).ListarAcordosUsuarioAsync(1, CancellationToken.None);

        Assert.Empty(albuns);
        Assert.Equal(3, manipulador.Chamadas);
    }

    [Fact]
    public async Task Repete_em_429()
    {
        var manipulador = new ManipuladorHttpFake().Status((HttpStatusCode)429).Json("[]");

        await CriarCliente(manipulador).ListarUsuariosAsync(CancellationToken.None);

        Assert.Equal(2, manipulador.Chamadas);
    }

    [Fact]
    public async Task Nao_repete_em_404()
    {
        var manipulador = new ManipuladorHttpFake().Status(HttpStatusCode.NotFound);

        var erro = await Assert.ThrowsAsync<HttpRequestException>(
            () => CriarCliente(manipulador).ListarDocumentosAcordoAsync(999, CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, erro.StatusCode);
        Assert.Equal(1, manipulador.Chamadas);
    }

    [Fact]
    public async Task Esgotadas_as_retentativas_propaga_a_falha()
    {
        var manipulador = new ManipuladorHttpFake()
            .Status(HttpStatusCode.InternalServerError).Status(HttpStatusCode.InternalServerError)
            .Status(HttpStatusCode.InternalServerError).Status(HttpStatusCode.InternalServerError);

        var erro = await Assert.ThrowsAsync<HttpRequestException>(
            () => CriarCliente(manipulador).ListarUsuariosAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, erro.StatusCode);
        Assert.Equal(4, manipulador.Chamadas); // 1 + 3 retentativas
    }

    [Fact]
    public async Task Timeout_da_tentativa_vira_TimeoutException_e_e_repetido()
    {
        var manipulador = new ManipuladorHttpFake()
            .Enfileirar(async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct); // só termina quando o limite da tentativa cancelar
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
            .Json("[]");

        var usuarios = await CriarCliente(manipulador, TimeSpan.FromMilliseconds(50))
            .ListarUsuariosAsync(CancellationToken.None);

        Assert.Empty(usuarios);
        Assert.Equal(2, manipulador.Chamadas);
    }
}

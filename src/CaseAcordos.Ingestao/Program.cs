using Amazon.SQS;
using CaseAcordos.Ingestao;
using CaseAcordos.Ingestao.Integracao;
using CaseAcordos.Ingestao.Interfaces;
using CaseAcordos.Ingestao.Publicacao;
using CaseAcordos.Ingestao.Resiliencia;
using CaseAcordos.Ingestao.Servicos;
using CaseAcordos.Mensageria;

var builder = Host.CreateApplicationBuilder(args);

// Logs estruturados em JSON (uma linha por evento), com os escopos (CorrelationId) incluídos.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

var config = builder.Configuration;
var opcoesFilas = config.GetSection(OpcoesFilas.Secao).Get<OpcoesFilas>() ?? new OpcoesFilas();
var opcoesRetentativa = config.GetSection(OpcoesRetentativa.Secao).Get<OpcoesRetentativa>() ?? new OpcoesRetentativa();
var opcoesIngestao = config.GetSection(OpcoesIngestao.Secao).Get<OpcoesIngestao>() ?? new OpcoesIngestao();
var opcoesApi = config.GetSection(OpcoesJsonPlaceholder.Secao).Get<OpcoesJsonPlaceholder>() ?? new OpcoesJsonPlaceholder();

builder.Services.AddSingleton(opcoesFilas);
builder.Services.AddSingleton(opcoesRetentativa);
builder.Services.AddSingleton(opcoesIngestao);

builder.Services.AddSingleton<IAmazonSQS>(_ => FabricaSqs.Criar(opcoesFilas));
builder.Services.AddSingleton<ResolvedorUrlFila>();
builder.Services.AddSingleton<IPublicadorMensagens, PublicadorSqs>();

builder.Services.AddSingleton(sp => new PoliticaRetentativa(
    opcoesRetentativa,
    sp.GetRequiredService<ILogger<PoliticaRetentativa>>()));

builder.Services.AddHttpClient<IClienteIntegracaoExterna, ClienteIntegracaoExterna>(http =>
{
    http.BaseAddress = new Uri(opcoesApi.UrlBase.EndsWith('/') ? opcoesApi.UrlBase : opcoesApi.UrlBase + "/");
    http.Timeout = Timeout.InfiniteTimeSpan; // o limite de cada tentativa é da PoliticaRetentativa
});

builder.Services.AddScoped<ServicoIngestao>();
builder.Services.AddHostedService<ExecucaoIngestaoWorker>();

await builder.Build().RunAsync();
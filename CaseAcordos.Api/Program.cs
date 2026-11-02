using CaseAcordos.Api.Configuracao;
using CaseAcordos.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Logs estruturados (JSON) no stdout; o escopo traz o CorrelationId de cada requisição.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

// Segredos e conexão vêm SOMENTE de variáveis de ambiente / configuração externa:
//   Autenticacao__ApiKey   ConnectionStrings__Postgres
builder.Services
    .AddOpcoesDaApi()
    .AddCamadasDaApi()
    .AddTratamentoDeErros()
    .AddVerificacoesDeSaude()
    .AddSwaggerDocumentacao();

builder.Services.AddControllers();

var app = builder.Build();

// Ordem importa: correlação (mais externo) → tratamento de erros → autenticação → endpoints.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<AutenticacaoApiKeyMiddleware>();

if (app.Configuration.GetValue(OpcoesSwagger.Secao + ":Habilitado", true))
{
    app.UseSwaggerDocumentacao();
}

app.MapControllers();
app.MapVerificacoesDeSaude();

app.Run();

// Necessário para os testes de integração (WebApplicationFactory<Program>).
public partial class Program { }

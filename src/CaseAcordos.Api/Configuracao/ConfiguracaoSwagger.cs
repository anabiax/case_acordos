using Microsoft.OpenApi.Models;

namespace CaseAcordos.Api.Configuracao
{
    public static class ConfiguracaoSwagger
    {
        private const string EsquemaApiKey = "ApiKey";

        public static IServiceCollection AddSwaggerDocumentacao(this IServiceCollection servicos)
        {
            servicos.AddEndpointsApiExplorer();
            servicos.AddSwaggerGen(opcoes =>
            {
                opcoes.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "CaseAcordos API de Consulta",
                    Version = "v1",
                    Description =
                        "Consulta consolidada de autores, acordos e documentos a partir da base própria " +
                        "(PostgreSQL), alimentada pela ingestão assíncrona. Hierarquia: Autor → Acordos → Documentos."
                });

                var esquema = new OpenApiSecurityScheme
                {
                    Name = OpcoesAutenticacao.NomeCabecalhoPadrao,
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Description = "API Key fixa, informada no cabeçalho X-Api-Key.",
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = EsquemaApiKey }
                };

                opcoes.AddSecurityDefinition(EsquemaApiKey, esquema);
                opcoes.AddSecurityRequirement(new OpenApiSecurityRequirement { [esquema] = Array.Empty<string>() });
                opcoes.DocumentFilter<DocumentoSaudeFilter>();

                var arquivoXml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
                if (File.Exists(arquivoXml))
                    opcoes.IncludeXmlComments(arquivoXml);
            });

            return servicos;
        }

        public static WebApplication UseSwaggerDocumentacao(this WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(opcoes =>
            {
                opcoes.SwaggerEndpoint("/swagger/v1/swagger.json", "CaseAcordos API v1");
                opcoes.DocumentTitle = "CaseAcordos API";
            });

            // Raiz amigável: abre a documentação.
            app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

            return app;
        }
    }
}
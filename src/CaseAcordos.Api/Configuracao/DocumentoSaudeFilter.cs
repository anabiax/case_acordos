using CaseAcordos.Api.DTO;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CaseAcordos.Api.Configuracao
{
    /// <summary>Descreve no OpenAPI os endpoints de health check (mapeados fora de controllers).</summary>
    public sealed class DocumentoSaudeFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument documento, DocumentFilterContext contexto)
        {
            var schema = contexto.SchemaGenerator.GenerateSchema(typeof(RelatorioSaudeDto), contexto.SchemaRepository);

            documento.Paths["/health"] = ItemGet(
                resumo: "Readiness: a API está pronta para receber tráfego?",
                descricao: "Verifica as dependências (PostgreSQL). Não exige API Key.",
                schema,
                incluir503: true);

            documento.Paths["/health/live"] = ItemGet(
                resumo: "Liveness: o processo da API está de pé?",
                descricao: "Não consulta dependências. Não exige API Key.",
                schema,
                incluir503: false);
        }

        private static OpenApiPathItem ItemGet(string resumo, string descricao, OpenApiSchema schema, bool incluir503)
        {
            var respostas = new OpenApiResponses
            {
                ["200"] = Resposta("Saudável (Healthy) ou degradado (Degraded).", schema)
            };

            if (incluir503)
                respostas["503"] = Resposta("Alguma dependência está indisponível (Unhealthy).", schema);

            var operacao = new OpenApiOperation
            {
                Tags = [new OpenApiTag { Name = "Saúde" }],
                Summary = resumo,
                Description = descricao,
                Security = [],          // sobrescreve a exigência global de API Key
                Responses = respostas
            };

            return new OpenApiPathItem { Operations = { [OperationType.Get] = operacao } };
        }

        private static OpenApiResponse Resposta(string descricao, OpenApiSchema schema) => new()
        {
            Description = descricao,
            Content = { ["application/json"] = new OpenApiMediaType { Schema = schema } }
        };
    }
}
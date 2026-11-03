using CaseAcordos.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace CaseAcordos.Testes
{
    /// <summary>
    /// Sobe a API real em memória (pipeline completo: correlação, tratamento de erros, API Key, MVC, Swagger),
    /// trocando apenas o serviço de consulta por um substituto — nenhum banco é necessário.
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const string ApiKey = "chave-de-teste-com-mais-de-16-caracteres";

        public IServicoConsultaAcordos Servico { get; } = Substitute.For<IServicoConsultaAcordos>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuracao) =>
                configuracao.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Autenticacao:ApiKey"] = ApiKey,
                    ["ConnectionStrings:Acordos"] = "Host=localhost;Database=teste;Username=teste;Password=teste"
                }));

            builder.ConfigureServices(servicos =>
            {
                servicos.RemoveAll<IServicoConsultaAcordos>();
                servicos.AddSingleton<IServicoConsultaAcordos>(Servico);
            });
        }

        public HttpClient CriarClienteAutenticado()
        {
            var cliente = CreateClient();
            cliente.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
            return cliente;
        }
    }
}
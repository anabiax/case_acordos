using System;
using Amazon.SQS;
using CaseAcordos.Consumidor.Configuracao;
using CaseAcordos.Consumidor.Infraestrutura;
using CaseAcordos.Consumidor.Servicos;
using CaseAcordos.Mensageria.Configuracao;
using CaseAcordos.Mensageria.Infraestrutura;
using CaseAcordos.Persistencia.Interfaces;
using CaseAcordos.Persistencia.Repositorios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

var config = builder.Configuration;
var opcoesFilas = config.GetSection(OpcoesFilas.Secao).Get<OpcoesFilas>() ?? new OpcoesFilas();
var opcoesConsumidor = config.GetSection(OpcoesConsumidor.Secao).Get<OpcoesConsumidor>() ?? new OpcoesConsumidor();
var stringConexao = config.GetConnectionString("Acordos")
    ?? throw new InvalidOperationException("ConnectionStrings:Acordos não configurada.");

builder.Services.AddSingleton(opcoesFilas);
builder.Services.AddSingleton(opcoesConsumidor);

builder.Services.AddSingleton<IAmazonSQS>(_ => FabricaSqs.Criar(opcoesFilas));
builder.Services.AddSingleton<ResolvedorUrlFila>();

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(stringConexao));
builder.Services.AddSingleton<IRepositorioAcordos, RepositorioAcordosPostgres>();
builder.Services.AddSingleton<ProcessadorMensagem>();

builder.Services.AddHostedService<ConsumidorSqs>();

await builder.Build().RunAsync();

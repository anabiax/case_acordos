using System;

namespace CaseAcordos.Ingestao.Servicos;

public sealed record ResultadoIngestao(
    string CorrelationId,
    int Usuarios,
    int Acordos,
    int Documentos,
    int MensagensPublicadas,
    int Falhas,
    TimeSpan Duracao)
{
    public bool Sucesso => Falhas == 0;
}

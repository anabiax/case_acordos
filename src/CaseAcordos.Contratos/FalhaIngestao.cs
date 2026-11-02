using System;

namespace CaseAcordos.Contratos;

/// <summary>
/// Mensagem publicada na DLQ de ingestão quando uma unidade da carga (usuários, acordos de um usuário,
/// documentos de um acordo ou a publicação na fila) falha depois de esgotadas as retentativas.
/// Localmente simula o destino On-Failure do Lambda de ingestão.
/// </summary>
/// <param name="Etapa">usuarios | acordos | documentos | publicacao</param>
/// <param name="Retentavel">true quando a falha era do tipo transitório (timeout, 5xx, 408, 429).</param>
public sealed record FalhaIngestao(
    string CorrelationId,
    string Etapa,
    int? UsuarioId,
    int? AcordoId,
    string Erro,
    bool Retentavel,
    DateTimeOffset OcorridoEm);

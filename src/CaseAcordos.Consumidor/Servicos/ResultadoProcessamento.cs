namespace CaseAcordos.Consumidor.Servicos;

public enum TipoResultado
{
    /// <summary>Gravou: a mensagem pode ser apagada da fila.</summary>
    Sucesso,

    /// <summary>Falha transitória: NÃO apagar. A fila reentrega e, após 3 recebimentos, move para a DLQ de consumo.</summary>
    FalhaRetentavel,

    /// <summary>Mensagem inválida: reentregar não adianta. Vai direto para a DLQ de consumo e é apagada da fila.</summary>
    FalhaNaoRetentavel
}

public sealed record ResultadoProcessamento(TipoResultado Tipo, string? Motivo = null, string? CorrelationId = null)
{
    public static ResultadoProcessamento Sucesso(string? correlationId) =>
        new(TipoResultado.Sucesso, null, correlationId);

    public static ResultadoProcessamento Retentavel(string motivo, string? correlationId = null) =>
        new(TipoResultado.FalhaRetentavel, motivo, correlationId);

    public static ResultadoProcessamento NaoRetentavel(string motivo, string? correlationId = null) =>
        new(TipoResultado.FalhaNaoRetentavel, motivo, correlationId);
}

namespace CaseAcordos.Consumidor;

/// <summary>Seção "Consumidor".</summary>
public sealed class OpcoesConsumidor
{
    public const string Secao = "Consumidor";

    /// <summary>Quantas mensagens do lote (até 10) são gravadas ao mesmo tempo.</summary>
    public int GrauParalelismo { get; set; } = 4;
}

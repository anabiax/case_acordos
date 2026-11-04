namespace CaseAcordos.Mensageria.Configuracao;

/// <summary>
/// Configuração das filas (seção "Filas"). Com <see cref="UrlServicoSqs"/> preenchida usa LocalStack;
/// em branco usa o SQS real da AWS com a cadeia padrão de credenciais.
/// </summary>
public sealed class OpcoesFilas
{
    public const string Secao = "Filas";

    /// <summary>Endpoint do LocalStack (ex.: http://localhost:4566).</summary>
    public string? UrlServicoSqs { get; set; }

    public string Regiao { get; set; } = "us-east-1";

    /// <summary>Conta usada para montar a URL das filas no LocalStack.</summary>
    public string ContaId { get; set; } = "000000000000";

    /// <summary>Credenciais fictícias do LocalStack. Ignoradas quando <see cref="UrlServicoSqs"/> está vazia.</summary>
    public string? ChaveAcesso { get; set; }
    public string? ChaveSecreta { get; set; }

    public string FilaAcordos { get; set; } = "acordos";
    public string FilaDlqConsumo { get; set; } = "acordos-dlq-consumo";
    public string FilaDlqIngestao { get; set; } = "acordos-dlq-ingestao";
}

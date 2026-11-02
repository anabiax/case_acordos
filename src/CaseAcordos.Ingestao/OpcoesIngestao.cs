namespace CaseAcordos.Ingestao;

/// <summary>Seção "Ingestao".</summary>
public sealed class OpcoesIngestao
{
    public const string Secao = "Ingestao";

    /// <summary>Quantos usuários são processados ao mesmo tempo. Padrão 1 (sequencial) para não sobrecarregar a API pública.</summary>
    public int GrauParalelismo { get; set; } = 1;

    /// <summary>0 = executa a carga uma vez e encerra. Maior que 0 = repete a cada N horas (ex.: 3).</summary>
    public double IntervaloHoras { get; set; } = 0;
}

/// <summary>Seção "JsonPlaceholder".</summary>
public sealed class OpcoesJsonPlaceholder
{
    public const string Secao = "JsonPlaceholder";

    public string UrlBase { get; set; } = "https://jsonplaceholder.typicode.com/";
}

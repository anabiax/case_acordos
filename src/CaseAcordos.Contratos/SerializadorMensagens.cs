using System.Text.Json;

namespace CaseAcordos.Contratos;

/// <summary>Serialização única das mensagens da fila (camelCase, leitura sem diferenciar maiúsculas).</summary>
public static class SerializadorMensagens
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opcoes);

    /// <exception cref="JsonException">quando o conteúdo não é um JSON válido para o tipo.</exception>
    public static T? Desserializar<T>(string json) => JsonSerializer.Deserialize<T>(json, Opcoes);
}

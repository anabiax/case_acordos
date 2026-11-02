using System.Text.Json;
using CaseAcordos.Ingestao.DTO;
using CaseAcordos.Ingestao.Interfaces;
using CaseAcordos.Ingestao.Resiliencia;

namespace CaseAcordos.Ingestao.Integracao;

/// <summary>
/// Cliente HTTP da API pública, com retentativa seletiva (timeout, falha de rede, 5xx, 408 e 429).
/// O HttpClient deve ter BaseAddress terminando em "/" e Timeout infinito: o limite de cada
/// tentativa é controlado aqui (OpcoesRetentativa.TimeoutPorTentativa).
/// </summary>
public sealed class ClienteIntegracaoExterna : IClienteIntegracaoExterna
{
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly PoliticaRetentativa _politicaRetentativa;
    private readonly OpcoesRetentativa _opcoesRetentativa;

    public ClienteIntegracaoExterna(HttpClient httpClient, PoliticaRetentativa politicaRetentativa, OpcoesRetentativa opcoesRetentativa)
    {
        _httpClient = httpClient;
        _politicaRetentativa = politicaRetentativa;
        _opcoesRetentativa = opcoesRetentativa;
    }

    public Task<IReadOnlyList<UsuarioExternoDTO>> ListarUsuariosAsync(CancellationToken ct) =>
        ObterListaAsync<UsuarioExternoDTO>("users", ct);

    public Task<IReadOnlyList<AcordoExternoDTO>> ListarAcordosUsuarioAsync(int usuarioId, CancellationToken ct) =>
        ObterListaAsync<AcordoExternoDTO>($"users/{usuarioId}/albums", ct);

    public Task<IReadOnlyList<DocumentoExternoDTO>> ListarDocumentosAcordoAsync(int acordoId, CancellationToken ct) =>
        ObterListaAsync<DocumentoExternoDTO>($"albums/{acordoId}/photos", ct);

    private Task<IReadOnlyList<T>> ObterListaAsync<T>(string caminho, CancellationToken ct) =>
        _politicaRetentativa.ExecutarAsync(
            tk => ObterUmaVezAsync<T>(caminho, tk),
            ClassificadorFalhaHttp.EhElegivel,
            $"GET {caminho}",
            ct);

    private async Task<IReadOnlyList<T>> ObterUmaVezAsync<T>(string caminho, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_opcoesRetentativa.TimeoutPorTentativa);

        try
        {
            using var resposta = await _httpClient.GetAsync(caminho, limite.Token).ConfigureAwait(false);

            if (!resposta.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"GET {caminho} retornou {(int)resposta.StatusCode} ({resposta.StatusCode})",
                    inner: null,
                    statusCode: resposta.StatusCode);
            }

            var json = await resposta.Content.ReadAsStringAsync(limite.Token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<T>>(json, OpcoesJson) ?? new List<T>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Quem cancelou foi o limite da tentativa, não o chamador: vira timeout (elegível a retentativa).
            throw new TimeoutException($"GET {caminho} excedeu {_opcoesRetentativa.TimeoutPorTentativa.TotalSeconds:0.##}s");
        }
    }
}

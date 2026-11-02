using System;
using System.Net;
using System.Net.Http;

namespace CaseAcordos.Ingestao.Resiliencia;

/// <summary>
/// Decide se uma falha ao chamar a API pública merece nova tentativa.
/// Elegíveis: timeout, falha de rede/conexão, 5xx, 408 e 429.
/// Não elegíveis: demais 4xx (400, 404...) e erros de leitura do conteúdo.
/// </summary>
public static class ClassificadorFalhaHttp
{
    public static bool EhElegivel(Exception ex) => ex switch
    {
        TimeoutException => true,
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException h => EhStatusElegivel(h.StatusCode!.Value),
        _ => false
    };

    public static bool EhStatusElegivel(HttpStatusCode status) =>
        (int)status >= 500 ||
        status == HttpStatusCode.RequestTimeout ||
        (int)status == 429;
}

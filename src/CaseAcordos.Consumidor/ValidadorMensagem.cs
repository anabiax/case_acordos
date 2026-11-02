using System.Collections.Generic;
using CaseAcordos.Contratos;

namespace CaseAcordos.Consumidor;

/// <summary>Valida a mensagem antes de tocar no banco. Devolve o motivo do erro, ou null se estiver válida.</summary>
public static class ValidadorMensagem
{
    public static string? Validar(MensagemAcordo? mensagem)
    {
        if (mensagem is null) return "mensagem vazia";
        if (mensagem.Autor is null) return "autor ausente";
        if (mensagem.Acordo is null) return "acordo ausente";
        if (mensagem.Documentos is null) return "lista de documentos ausente";

        if (mensagem.Autor.Id <= 0) return "autor.id inválido";
        if (string.IsNullOrWhiteSpace(mensagem.Autor.Nome)) return "autor.nome obrigatório";
        if (string.IsNullOrWhiteSpace(mensagem.Autor.Email)) return "autor.email obrigatório";

        if (mensagem.Acordo.Id <= 0) return "acordo.id inválido";
        if (string.IsNullOrWhiteSpace(mensagem.Acordo.Titulo)) return "acordo.titulo obrigatório";

        var idsVistos = new HashSet<int>();
        foreach (var documento in mensagem.Documentos)
        {
            if (documento is null) return "documento nulo na lista";
            if (documento.Id <= 0) return "documento.id inválido";
            if (!idsVistos.Add(documento.Id)) return $"documento.id {documento.Id} repetido na mensagem";
            if (string.IsNullOrWhiteSpace(documento.Titulo)) return $"documento {documento.Id}: titulo obrigatório";
            if (string.IsNullOrWhiteSpace(documento.Url)) return $"documento {documento.Id}: url obrigatória";
            if (string.IsNullOrWhiteSpace(documento.ThumbnailUrl)) return $"documento {documento.Id}: thumbnailUrl obrigatória";
        }

        return null;
    }
}

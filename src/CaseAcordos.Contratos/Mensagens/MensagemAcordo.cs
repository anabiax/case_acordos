namespace CaseAcordos.Contratos.Mensagens;

/// <summary>
/// Contrato da mensagem publicada na fila: um acordo, com o autor embutido e seus documentos.
/// Já está no vocabulário do domínio (a tradução do formato do jsonplaceholder acontece só na ingestão).
/// Cada mensagem é autocontida: o consumidor grava autor, acordo e documentos em uma única transação.
/// </summary>
public sealed record MensagemAcordo(
    string CorrelationId,
    AutorMensagem Autor,
    AcordoMensagem Acordo,
    IReadOnlyList<DocumentoMensagem> Documentos);

public sealed record AutorMensagem(
    int Id,
    string Nome,
    string Email,
    string? Telefone,
    string? EnderecoRua,
    string? EnderecoComplemento,
    string? EnderecoCidade,
    string? EnderecoCep,
    string? EmpresaNome,
    string? EmpresaSegmento);

public sealed record AcordoMensagem(int Id, string Titulo);

public sealed record DocumentoMensagem(int Id, string Titulo, string Url, string ThumbnailUrl);

namespace CaseAcordos.Api.DTO
{
    /// <summary>Resposta consolidada: autor + resumo + acordos (com a página solicitada de documentos de cada um).</summary>
    /// <param name="Autor">Dados cadastrais do autor da ação judicial.</param>
    /// <param name="Resumo">Totais de acordos e documentos do autor (independem da paginação).</param>
    /// <param name="Acordos">Acordos do autor, cada um com uma página de seus documentos.</param>
    public sealed record ConsultaAcordosDto(
        AutorDto Autor,
        ResumoDto Resumo,
        IReadOnlyList<AcordoDto> Acordos);
}
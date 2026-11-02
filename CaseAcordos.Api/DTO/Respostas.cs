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

    /// <param name="Id">Identificador do autor.</param>
    /// <param name="Nome">Nome do autor.</param>
    /// <param name="Email">E-mail do autor.</param>
    /// <param name="Telefone">Telefone do autor.</param>
    /// <param name="Endereco">Endereço completo, ex.: "Rua X, Apto 10 - Cidade - CEP 00000-000".</param>
    /// <param name="Empresa">Empresa do autor.</param>
    public sealed record AutorDto(
        int Id,
        string Nome,
        string Email,
        string? Telefone,
        string Endereco,
        EmpresaDto Empresa);

    /// <param name="Nome">Nome da empresa.</param>
    /// <param name="Segmento">Segmento de atuação da empresa.</param>
    public sealed record EmpresaDto(string? Nome, string? Segmento);

    /// <param name="QuantidadeAcordos">Total de acordos do autor.</param>
    /// <param name="QuantidadeDocumentos">Total de documentos em todos os acordos do autor.</param>
    public sealed record ResumoDto(int QuantidadeAcordos, int QuantidadeDocumentos);

    /// <param name="Id">Identificador do acordo.</param>
    /// <param name="Titulo">Nome do acordo.</param>
    /// <param name="QuantidadeDocumentos">Total de documentos do acordo (não apenas os da página retornada).</param>
    /// <param name="Documentos">Documentos da página solicitada.</param>
    /// <param name="PaginacaoDocumentos">Metadados da paginação dos documentos deste acordo.</param>
    public sealed record AcordoDto(
        int Id,
        string Titulo,
        int QuantidadeDocumentos,
        IReadOnlyList<DocumentoDto> Documentos,
        PaginacaoDto PaginacaoDocumentos);

    /// <param name="Id">Identificador do documento.</param>
    /// <param name="Titulo">Nome do documento.</param>
    /// <param name="Url">Link para acesso ao documento.</param>
    /// <param name="ThumbnailUrl">Link para a miniatura de pré-visualização.</param>
    public sealed record DocumentoDto(int Id, string Titulo, string Url, string ThumbnailUrl);

    /// <summary>Resposta do endpoint dedicado à paginação dos documentos de um acordo.</summary>
    /// <param name="AcordoId">Identificador do acordo.</param>
    /// <param name="Titulo">Nome do acordo.</param>
    /// <param name="Itens">Documentos da página solicitada.</param>
    /// <param name="Paginacao">Metadados de paginação.</param>
    public sealed record PaginaDocumentosDto(
        int AcordoId,
        string Titulo,
        IReadOnlyList<DocumentoDto> Itens,
        PaginacaoDto Paginacao);

    /// <param name="Pagina">Página atual (base 1).</param>
    /// <param name="TamanhoPagina">Itens por página.</param>
    /// <param name="TotalItens">Total de itens existentes.</param>
    /// <param name="TotalPaginas">Total de páginas (0 quando não há itens).</param>
    /// <param name="PossuiProximaPagina">Indica se há uma página seguinte.</param>
    public sealed record PaginacaoDto(
        int Pagina,
        int TamanhoPagina,
        int TotalItens,
        int TotalPaginas,
        bool PossuiProximaPagina)
    {
        public static PaginacaoDto Criar(int pagina, int tamanhoPagina, int totalItens)
        {
            var totalPaginas = totalItens <= 0
                ? 0
                : (int)Math.Ceiling(totalItens / (double)tamanhoPagina);

            return new PaginacaoDto(pagina, tamanhoPagina, totalItens, totalPaginas, pagina < totalPaginas);
        }
    }
}
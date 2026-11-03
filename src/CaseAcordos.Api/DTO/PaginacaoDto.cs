namespace CaseAcordos.Api.DTO
{
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
            var totalPaginas = totalItens <= 0 ? 0 : (int)Math.Ceiling(totalItens / (double)tamanhoPagina);

            return new PaginacaoDto(pagina, tamanhoPagina, totalItens, totalPaginas, pagina < totalPaginas);
        }
    }
}
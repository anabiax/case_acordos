namespace CaseAcordos.Api.Configuracao
{
    /// <summary>
    /// Limites da paginação de documentos. Constantes para poderem ser usadas em atributos
    /// de validação ([Range]) no controller e na regra de negócio, mantendo uma única fonte de verdade.
    /// </summary>
    public static class LimitesPaginacao
    {
        public const int PaginaMinima = 1;

        /// <summary>Teto defensivo: evita offsets absurdos (e overflow no cálculo do OFFSET).</summary>
        public const int PaginaMaxima = 100_000;

        public const int TamanhoPadrao = 20;
        public const int TamanhoMinimo = 1;
        public const int TamanhoMaximo = 100;
    }
}
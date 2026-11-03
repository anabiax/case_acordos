namespace CaseAcordos.Api.Modelo
{
    // Modelos de leitura (read models) devolvidos pelo repositório de consulta.
    // Classes com setters "init" e construtor padrão: o Dapper materializa por nome de coluna/alias,
    // sem depender da ordem nem de tipos exatos de um construtor posicional.
    public sealed class AcordoRegistro
    {
        public int Id { get; init; }
        public string Titulo { get; init; } = string.Empty;
        public int QuantidadeDocumentos { get; init; }
    }
}

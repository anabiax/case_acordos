namespace CaseAcordos.Api.Modelo
{
    // Modelos de leitura (read models) devolvidos pelo repositório de consulta.
    // Classes com setters "init" e construtor padrão: o Dapper materializa por nome de coluna/alias,
    // sem depender da ordem nem de tipos exatos de um construtor posicional.

    public sealed class AutorRegistro
    {
        public int Id { get; init; }
        public string Nome { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Telefone { get; init; }
        public string? Rua { get; init; }
        public string? Complemento { get; init; }
        public string? Cidade { get; init; }
        public string? Cep { get; init; }
        public string? EmpresaNome { get; init; }
        public string? EmpresaSegmento { get; init; }
    }

    public sealed class AcordoRegistro
    {
        public int Id { get; init; }
        public string Titulo { get; init; } = string.Empty;
        public int QuantidadeDocumentos { get; init; }
    }

    public sealed class DocumentoRegistro
    {
        public int Id { get; init; }
        public int AcordoId { get; init; }
        public string Titulo { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string ThumbnailUrl { get; init; } = string.Empty;
    }
}

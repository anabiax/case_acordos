namespace CaseAcordos.Api.DTO
{
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
}
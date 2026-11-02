namespace CaseAcordos.Ingestao.DTO;

public sealed record UsuarioExternoDTO(
    int Id,
    string? Name,
    string? Email,
    string? Phone,
    EnderecoExternoDTO? Address,
    EmpresaExternaDTO? Company
);

public sealed record EnderecoExternoDTO(string? Street, string? Suite, string? City, string? Zipcode);
public sealed record EmpresaExternaDTO(string? Name, string? Bs);
using CaseAcordos.Ingestao.DTO;

namespace CaseAcordos.Ingestao.Interfaces;

public interface IClienteIntegracaoExterna
{
    Task<IReadOnlyList<UsuarioExternoDTO>> ListarUsuariosAsync(CancellationToken ct);

    Task<IReadOnlyList<AcordoExternoDTO>> ListarAcordosUsuarioAsync(int usuarioId, CancellationToken ct);

    Task<IReadOnlyList<DocumentoExternoDTO>> ListarDocumentosAcordoAsync(int acordoId, CancellationToken ct);
}
using CaseAcordos.Contratos;

namespace CaseAcordos.Persistencia;

public interface IRepositorioAcordos
{
    /// <summary>
    /// Grava autor, acordo e documentos da mensagem em UMA transação, com upsert idempotente.
    /// Executar a mesma mensagem várias vezes não duplica registros.
    /// </summary>
    /// <exception cref="FalhaPersistenciaException">falha classificada como retentável ou não.</exception>
    Task GravarAsync(MensagemAcordo mensagem, CancellationToken ct);
}

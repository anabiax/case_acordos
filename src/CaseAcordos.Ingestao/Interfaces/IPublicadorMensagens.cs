using System.Threading;
using System.Threading.Tasks;
using CaseAcordos.Contratos.Falhas;
using CaseAcordos.Contratos.Mensagens;

namespace CaseAcordos.Ingestao.Interfaces;

public interface IPublicadorMensagens
{
    /// <summary>Publica na fila de acordos (1 mensagem por acordo).</summary>
    Task PublicarAcordoAsync(MensagemAcordo mensagem, CancellationToken ct);

    /// <summary>Publica na DLQ de ingestão uma unidade da carga que falhou.</summary>
    Task PublicarFalhaAsync(FalhaIngestao falha, CancellationToken ct);
}

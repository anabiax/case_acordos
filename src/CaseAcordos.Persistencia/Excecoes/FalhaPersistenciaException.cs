using System;

namespace CaseAcordos.Persistencia.Excecoes;

/// <summary>
/// Falha ao gravar no banco, já classificada. Retentável = transitória (conexão, timeout, deadlock);
/// não retentável = dado inválido para o banco (violação de integridade, texto maior que a coluna etc.).
/// </summary>
public sealed class FalhaPersistenciaException : Exception
{
    public bool Retentavel { get; }

    public FalhaPersistenciaException(string mensagem, bool retentavel, Exception? causa = null)
        : base(mensagem, causa)
    {
        Retentavel = retentavel;
    }
}

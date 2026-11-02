using System;

namespace CaseAcordos.Ingestao.Resiliencia;

/// <summary>Seção "Retentativa". Padrão: 3 retentativas com atrasos de ~1s, 2s e 4s (mais jitter).</summary>
public sealed class OpcoesRetentativa
{
    public const string Secao = "Retentativa";

    /// <summary>Quantidade de retentativas depois da primeira tentativa (3 = até 4 chamadas no total).</summary>
    public int Maximo { get; set; } = 3;

    /// <summary>Atraso antes da 1ª retentativa. Dobra a cada nova retentativa.</summary>
    public TimeSpan AtrasoBase { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Ruído aleatório (0 até este valor) somado ao atraso, para não sincronizar clientes.</summary>
    public TimeSpan JitterMaximo { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Tempo máximo de cada tentativa individual.</summary>
    public TimeSpan TimeoutPorTentativa { get; set; } = TimeSpan.FromSeconds(10);
}

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PcSpec.Core.Bench;

/// <summary>
/// Uma carga de trabalho do benchmark. <see cref="Run"/> recebe uma quantidade de
/// trabalho (iteracoes ou bytes) e devolve a quantidade de operacoes executadas,
/// para que a taxa seja calculada independentemente do tamanho do bloco.
/// </summary>
internal abstract class Workload
{
    public abstract string Name { get; }
    public abstract string Unit { get; }
    public abstract double Reference { get; }
    public abstract long InitialChunk { get; }

    public abstract Workload Create();

    public abstract double Run(long amount);

    public static IReadOnlyList<Workload> All { get; } =
    [
        new IntegerWorkload(),
        new FloatingPointWorkload(),
        new MemoryWorkload(),
        new Sha256Workload(),
    ];

    /// <summary>Mantem o resultado vivo para o JIT não eliminar o laco inteiro.</summary>
    protected static long Sink;
}

internal sealed class IntegerWorkload : Workload
{
    public override string Name => "Inteiro";
    public override string Unit => "Mops/s";

    // 12 operacoes por iteracao (4 cadeias xorshift independentes, 3 ops cada).
    public override double Reference => 6000;

    public override long InitialChunk => 1 << 20;

    private long _a = unchecked((long)0x2545F4914F6CDD1D);
    private long _b = unchecked((long)0x9E3779B97F4A7C15);
    private long _c = unchecked((long)0xBF58476D1CE4E5B9);
    private long _d = unchecked((long)0x94D049BB133111EB);

    public override Workload Create() => new IntegerWorkload();

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    public override double Run(long amount)
    {
        var a = _a;
        var b = _b;
        var c = _c;
        var d = _d;

        for (long i = 0; i < amount; i++)
        {
            a ^= a << 13;
            a ^= (long)((ulong)a >> 7);
            a ^= a << 17;

            b ^= b << 13;
            b ^= (long)((ulong)b >> 7);
            b ^= b << 17;

            c ^= c << 13;
            c ^= (long)((ulong)c >> 7);
            c ^= c << 17;

            d ^= d << 13;
            d ^= (long)((ulong)d >> 7);
            d ^= d << 17;
        }

        _a = a;
        _b = b;
        _c = c;
        _d = d;
        Sink = a ^ b ^ c ^ d;

        return amount * 12d;
    }
}

internal sealed class FloatingPointWorkload : Workload
{
    public override string Name => "Ponto flutuante";
    public override string Unit => "Mops/s";

    // 4 FMA por iteracao, em cadeias independentes (mede throughput, não latência).
    public override double Reference => 8000;

    public override long InitialChunk => 1 << 20;

    private double _a0 = 1.0;
    private double _a1 = 1.1;
    private double _a2 = 1.2;
    private double _a3 = 1.3;
    private readonly double _mul = 1.0;
    private readonly double _add = 1e-9;

    public override Workload Create() => new FloatingPointWorkload();

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    public override double Run(long amount)
    {
        var a0 = _a0;
        var a1 = _a1;
        var a2 = _a2;
        var a3 = _a3;
        var mul = _mul;
        var add = _add;

        for (long i = 0; i < amount; i++)
        {
            a0 = a0 * mul + add;
            a1 = a1 * mul + add;
            a2 = a2 * mul + add;
            a3 = a3 * mul + add;
        }

        _a0 = a0;
        _a1 = a1;
        _a2 = a2;
        _a3 = a3;
        Sink = (long)(a0 + a1 + a2 + a3);

        return amount * 4d;
    }
}

internal sealed class MemoryWorkload : Workload
{
    private const int BufferBytes = 48 * 1024 * 1024;

    public override string Name => "Memória";
    public override string Unit => "GB/s";

    // 20 GB/s ~ banda prática de DDR4-3200 dual channel.
    public override double Reference => 20;

    // Uma passada completa pelo buffer de 48 MB.
    public override long InitialChunk => 1;

    private readonly byte[] _buffer;

    public MemoryWorkload()
    {
        _buffer = GC.AllocateArray<byte>(BufferBytes, pinned: true);

        // Preenche para forcar o commit das paginas antes da medição.
        for (var i = 0; i < _buffer.Length; i += 4096)
        {
            _buffer[i] = 1;
        }
    }

    public override Workload Create() => new MemoryWorkload();

    /// <summary>Uma "unidade" e uma passada completa; retorna os bytes lidos.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    public override double Run(long passes)
    {
        var view = MemoryMarshal.Cast<byte, long>(_buffer);
        var length = view.Length;
        long sum = 0;

        for (long pass = 0; pass < passes; pass++)
        {
            // Passo de 8 longs = 64 bytes = uma linha de cache por acesso.
            for (var i = 0; i < length; i += 8)
            {
                sum += view[i];
            }
        }

        Sink = sum;
        return passes * (double)BufferBytes;
    }
}

internal sealed class Sha256Workload : Workload
{
    private const int BufferBytes = 4 * 1024 * 1024;

    public override string Name => "SHA-256";
    public override string Unit => "GB/s";

    // ~2 GB/s por núcleo em CPUs com SHA-NI.
    public override double Reference => 2;

    public override long InitialChunk => 1;

    private readonly byte[] _buffer;

    public Sha256Workload()
    {
        _buffer = new byte[BufferBytes];
        Random.Shared.NextBytes(_buffer);
    }

    public override Workload Create() => new Sha256Workload();

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    public override double Run(long blocks)
    {
        Span<byte> hash = stackalloc byte[32];

        for (long block = 0; block < blocks; block++)
        {
            SHA256.HashData(_buffer, hash);
        }

        Sink = hash[0];
        return blocks * (double)BufferBytes;
    }
}

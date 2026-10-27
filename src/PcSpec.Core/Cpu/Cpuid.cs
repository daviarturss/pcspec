using System.Runtime.Intrinsics.X86;

namespace PcSpec.Core.Cpu;

public readonly record struct CpuidLeaf(uint Eax, uint Ebx, uint Ecx, uint Edx)
{
    public bool Bit(int register, int bit) => register switch
    {
        0 => ((Eax >> bit) & 1) != 0,
        1 => ((Ebx >> bit) & 1) != 0,
        2 => ((Ecx >> bit) & 1) != 0,
        3 => ((Edx >> bit) & 1) != 0,
        _ => false,
    };
}

/// <summary>
/// Acesso a instrução CPUID. Usa <see cref="X86Base"/> do runtime (sem helper nativo
/// nem driver), que e exatamente o mesmo mecanismo que o CPU-Z usa para a aba CPU.
/// </summary>
public static class Cpuid
{
    public static bool IsSupported => X86Base.IsSupported;

    public static CpuidLeaf Call(uint leaf, uint subLeaf = 0)
    {
        var result = X86Base.CpuId(unchecked((int)leaf), unchecked((int)subLeaf));
        return new CpuidLeaf((uint)result.Eax, (uint)result.Ebx, (uint)result.Ecx, (uint)result.Edx);
    }

    public static uint MaxBasicLeaf => Call(0).Eax;

    public static uint MaxExtendedLeaf => Call(0x8000_0000).Eax;

    public static bool HasLeaf(uint leaf) =>
        leaf < 0x8000_0000 ? leaf <= MaxBasicLeaf : leaf <= MaxExtendedLeaf;

    /// <summary>Fabricante em ASCII: "AuthenticAMD", "GenuineIntel" etc.</summary>
    public static string Vendor
    {
        get
        {
            var leaf = Call(0);
            Span<byte> bytes = stackalloc byte[12];
            BitConverter.TryWriteBytes(bytes[..4], leaf.Ebx);
            BitConverter.TryWriteBytes(bytes[4..8], leaf.Edx);
            BitConverter.TryWriteBytes(bytes[8..], leaf.Ecx);
            return System.Text.Encoding.ASCII.GetString(bytes).Trim('\0', ' ');
        }
    }

    /// <summary>Brand string (folhas 0x80000002..0x80000004), 48 caracteres.</summary>
    public static string BrandString
    {
        get
        {
            if (MaxExtendedLeaf < 0x8000_0004)
            {
                return string.Empty;
            }

            Span<byte> bytes = stackalloc byte[48];
            for (uint i = 0; i < 3; i++)
            {
                var leaf = Call(0x8000_0002 + i);
                BitConverter.TryWriteBytes(bytes[(int)(i * 16)..], leaf.Eax);
                BitConverter.TryWriteBytes(bytes[(int)(i * 16 + 4)..], leaf.Ebx);
                BitConverter.TryWriteBytes(bytes[(int)(i * 16 + 8)..], leaf.Ecx);
                BitConverter.TryWriteBytes(bytes[(int)(i * 16 + 12)..], leaf.Edx);
            }

            return System.Text.Encoding.ASCII.GetString(bytes).Trim('\0', ' ');
        }
    }
}

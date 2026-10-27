using System.Text;

namespace PcSpec.Core.Cpu;

/// <summary>
/// Monta a lista de instruções suportadas a partir das folhas CPUID, no mesmo
/// estilo da linha "Instructions" do CPU-Z.
/// </summary>
public static class InstructionFlags
{
    private static readonly (int Register, int Bit, string Name)[] Leaf1Edx =
    [
        (3, 0, "FPU"), (3, 4, "TSC"), (3, 5, "MSR"), (3, 6, "PAE"), (3, 8, "CX8"),
        (3, 9, "APIC"), (3, 11, "SEP"), (3, 12, "MTRR"), (3, 15, "CMOV"), (3, 16, "PAT"),
        (3, 19, "CLFSH"), (3, 23, "MMX"), (3, 24, "FXSR"), (3, 25, "SSE"), (3, 26, "SSE2"),
        (3, 28, "HTT"), (3, 29, "TM"),
    ];

    private static readonly (int Register, int Bit, string Name)[] Leaf1Ecx =
    [
        (2, 0, "SSE3"), (2, 1, "PCLMULQDQ"), (2, 3, "MONITOR"), (2, 5, "VMX"), (2, 6, "SMX"),
        (2, 7, "EIST"), (2, 8, "TM2"), (2, 9, "SSSE3"), (2, 12, "FMA"), (2, 13, "CX16"),
        (2, 19, "SSE4.1"), (2, 20, "SSE4.2"), (2, 21, "x2APIC"), (2, 22, "MOVBE"),
        (2, 23, "POPCNT"), (2, 25, "AES"), (2, 26, "XSAVE"), (2, 27, "OSXSAVE"),
        (2, 28, "AVX"), (2, 29, "F16C"), (2, 30, "RDRAND"), (2, 31, "HYPERVISOR"),
    ];

    private static readonly (int Register, int Bit, string Name)[] Leaf7Ebx =
    [
        (1, 0, "FSGSBASE"), (1, 3, "BMI1"), (1, 4, "HLE"), (1, 5, "AVX2"), (1, 7, "SMEP"),
        (1, 8, "BMI2"), (1, 9, "ERMS"), (1, 10, "INVPCID"), (1, 11, "RTM"), (1, 14, "MPX"),
        (1, 16, "AVX512F"), (1, 17, "AVX512DQ"), (1, 18, "RDSEED"), (1, 19, "ADX"),
        (1, 20, "SMAP"), (1, 21, "AVX512IFMA"), (1, 23, "CLFLUSHOPT"), (1, 24, "CLWB"),
        (1, 26, "AVX512PF"), (1, 27, "AVX512ER"), (1, 28, "AVX512CD"), (1, 29, "SHA"),
        (1, 30, "AVX512BW"), (1, 31, "AVX512VL"),
    ];

    private static readonly (int Register, int Bit, string Name)[] Leaf7Ecx =
    [
        (2, 0, "PREFETCHWT1"), (2, 1, "AVX512VBMI"), (2, 2, "UMIP"), (2, 3, "PKU"),
        (2, 5, "WAITPKG"), (2, 6, "AVX512VBMI2"), (2, 8, "GFNI"), (2, 9, "VAES"),
        (2, 10, "VPCLMULQDQ"), (2, 11, "AVX512VNNI"), (2, 12, "AVX512BITALG"),
        (2, 14, "AVX512VPOPCNTDQ"), (2, 22, "RDPID"), (2, 27, "MOVDIRI"),
        (2, 28, "MOVDIR64B"), (2, 30, "SGX_LC"), (2, 31, "PKS"),
    ];

    private static readonly (int Register, int Bit, string Name)[] Leaf7Edx =
    [
        (3, 4, "FSRM"), (3, 8, "AVX512VP2INTERSECT"), (3, 10, "MD_CLEAR"),
        (3, 14, "SERIALIZE"), (3, 15, "HYBRID"), (3, 16, "TSXLDTRK"), (3, 20, "CET_IBT"),
        (3, 22, "AMX_BF16"), (3, 23, "AVX512_FP16"), (3, 24, "AMX_TILE"), (3, 25, "AMX_INT8"),
    ];

    private static readonly (int Register, int Bit, string Name)[] ExtEdx =
    [
        (3, 11, "SYSCALL"), (3, 20, "NX"), (3, 22, "MMXEXT"), (3, 26, "PDPE1GB"),
        (3, 27, "RDTSCP"), (3, 29, "LM"), (3, 30, "3DNOWEXT"), (3, 31, "3DNOW"),
    ];

    private static readonly (int Register, int Bit, string Name)[] ExtEcx =
    [
        (2, 0, "LAHF"), (2, 2, "SVM"), (2, 5, "ABM"), (2, 6, "SSE4A"), (2, 7, "MISALIGNSSE"),
        (2, 8, "3DNOWPREFETCH"), (2, 10, "IBS"), (2, 11, "XOP"), (2, 16, "FMA4"),
        (2, 21, "TBM"), (2, 22, "TOPOEXT"), (2, 27, "PERFTSC"), (2, 28, "L2I"),
    ];

    /// <summary>Lista ordenada e sem duplicatas, ex.: "MMX, SSE, SSE2, AVX, AVX2, ...".</summary>
    public static string Describe()
    {
        if (!Cpuid.IsSupported)
        {
            return "indisponível (CPU não x86)";
        }

        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(IEnumerable<(int Register, int Bit, string Name)> table, CpuidLeaf leaf)
        {
            foreach (var (register, bit, name) in table)
            {
                if (leaf.Bit(register, bit) && seen.Add(name))
                {
                    found.Add(name);
                }
            }
        }

        var leaf1 = Cpuid.Call(1);
        Add(Leaf1Edx, leaf1);
        Add(Leaf1Ecx, leaf1);

        if (Cpuid.HasLeaf(7))
        {
            Add(Leaf7Ebx, Cpuid.Call(7, 0));
            Add(Leaf7Ecx, Cpuid.Call(7, 0));
            Add(Leaf7Edx, Cpuid.Call(7, 0));
        }

        if (Cpuid.HasLeaf(0x8000_0001))
        {
            var ext = Cpuid.Call(0x8000_0001);
            Add(ExtEdx, ext);
            Add(ExtEcx, ext);
        }

        // Ordena por "geração" para a leitura ficar parecida com a do CPU-Z.
        // Nomes fora desta lista vao para o fim, em ordem alfabetica.
        var order = new[]
        {
            "FPU", "TSC", "MSR", "PAE", "CX8", "APIC", "SEP", "MTRR", "CMOV", "PAT", "CLFSH",
            "HTT", "TM", "EIST", "TM2", "MONITOR", "CX16", "x2APIC",
            "MMX", "MMXEXT", "3DNOW", "3DNOWEXT", "3DNOWPREFETCH", "FXSR", "SSE", "SSE2",
            "SSE3", "SSSE3", "SSE4.1", "SSE4.2", "SSE4A", "MISALIGNSSE", "XOP", "FMA4",
            "AVX", "F16C", "AVX2", "FMA", "AVX512F", "AVX512DQ", "AVX512CD", "AVX512BW",
            "AVX512VL", "AVX512IFMA", "AVX512VBMI", "AVX512VBMI2", "AVX512VNNI",
            "AVX512BITALG", "AVX512VPOPCNTDQ", "AVX512VP2INTERSECT", "AVX512_FP16",
            "AES", "VAES", "PCLMULQDQ", "VPCLMULQDQ", "SHA", "GFNI",
            "BMI1", "BMI2", "ABM", "POPCNT", "LZCNT", "MOVBE", "ADX", "RDRAND", "RDSEED",
            "RDPID", "AMX_TILE", "AMX_INT8", "AMX_BF16",
            "NX", "LM", "SYSCALL", "RDTSCP", "PDPE1GB", "LAHF",
            "VMX", "SMX", "SVM", "HYPERVISOR", "SGX_LC", "IBS", "PERFTSC", "L2I", "TOPOEXT",
            "XSAVE", "OSXSAVE", "FSGSBASE", "SMEP", "SMAP", "ERMS", "INVPCID", "CLFLUSHOPT",
            "CLWB", "UMIP", "PKU", "PKS", "WAITPKG", "MOVDIRI", "MOVDIR64B", "PREFETCHWT1",
            "FSRM", "MD_CLEAR", "SERIALIZE", "TSXLDTRK", "CET_IBT", "HLE", "RTM", "MPX",
            "HYBRID",
        };

        var rank = order.Select((name, index) => (name, index))
                        .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        found.Sort((a, b) =>
        {
            var hasA = rank.TryGetValue(a, out var ra);
            var hasB = rank.TryGetValue(b, out var rb);
            if (hasA && hasB)
            {
                return ra.CompareTo(rb);
            }

            if (hasA)
            {
                return -1;
            }

            return hasB ? 1 : string.CompareOrdinal(a, b);
        });

        var builder = new StringBuilder();
        for (var i = 0; i < found.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(found[i]);
        }

        return builder.ToString();
    }
}

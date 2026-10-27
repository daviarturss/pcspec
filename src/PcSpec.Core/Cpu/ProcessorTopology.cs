using System.Runtime.InteropServices;
using PcSpec.Core.Interop;

namespace PcSpec.Core.Cpu;

/// <summary>
/// Topologia e caches lidos de <c>GetLogicalProcessorInformationEx</c>.
/// Serve de fonte primaria para contagem de núcleos e de fallback para os caches
/// quando as folhas CPUID 4 / 0x8000001D não estão disponíveis.
/// </summary>
public static class ProcessorTopology
{
    public sealed record CoreInfo(int EfficiencyClass);

    public sealed record CacheInfo(byte Level, byte Associativity, ushort LineSize, uint Size, uint Type);

    public static IReadOnlyList<CoreInfo> Cores { get; } = ReadCores();

    public static IReadOnlyList<CacheInfo> Caches { get; } = ReadCaches();

    public static int PhysicalCores => Math.Max(1, Cores.Count);

    public static int LogicalProcessors
    {
        get
        {
            try
            {
                var count = (int)NativeMethods.GetActiveProcessorCount(NativeMethods.AllProcessorGroups);
                return count > 0 ? count : Environment.ProcessorCount;
            }
            catch
            {
                return Environment.ProcessorCount;
            }
        }
    }

    public static int Packages => Math.Max(1, ReadRelationshipCount(NativeMethods.RelationProcessorPackage));

    /// <summary>Núcleos de performance (EfficiencyClass 0) e de eficiencia (classe maior).</summary>
    public static (int Performance, int Efficiency) CoreClasses
    {
        get
        {
            if (Cores.Count == 0)
            {
                return (PhysicalCores, 0);
            }

            var maxClass = Cores.Max(c => c.EfficiencyClass);
            if (maxClass == 0)
            {
                return (Cores.Count, 0);
            }

            var perf = Cores.Count(c => c.EfficiencyClass == 0);
            var eff = Cores.Count(c => c.EfficiencyClass == maxClass);
            return (perf, eff);
        }
    }

    private static List<(int Relationship, byte[] Data)> Query(int relationship)
    {
        var items = new List<(int, byte[])>();

        try
        {
            uint length = 0;
            NativeMethods.GetLogicalProcessorInformationEx(relationship, IntPtr.Zero, ref length);
            if (length == 0)
            {
                return items;
            }

            var buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (!NativeMethods.GetLogicalProcessorInformationEx(relationship, buffer, ref length))
                {
                    return items;
                }

                var offset = 0;
                while (offset + 8 <= length)
                {
                    var relation = Marshal.ReadInt32(buffer, offset);
                    var size = Marshal.ReadInt32(buffer, offset + 4);
                    if (size < 8 || offset + size > length)
                    {
                        break;
                    }

                    var data = new byte[size];
                    Marshal.Copy(buffer + offset, data, 0, size);
                    items.Add((relation, data));
                    offset += size;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            // Sem topologia detalhada: os chamadores caem para Environment.ProcessorCount.
        }

        return items;
    }

    private static List<CoreInfo> ReadCores()
    {
        var cores = new List<CoreInfo>();
        foreach (var (relation, data) in Query(NativeMethods.RelationProcessorCore))
        {
            // SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX: Relationship(4) + Size(4) + union.
            if (relation != NativeMethods.RelationProcessorCore || data.Length < 10)
            {
                continue;
            }

            // PROCESSOR_RELATIONSHIP (na union, offset 8): Flags(0), EfficiencyClass(1).
            cores.Add(new CoreInfo(data[9]));
        }

        return cores;
    }

    private static int ReadRelationshipCount(int relationship)
    {
        var count = 0;
        foreach (var (relation, _) in Query(relationship))
        {
            if (relation == relationship)
            {
                count++;
            }
        }

        return count;
    }

    private static List<CacheInfo> ReadCaches()
    {
        var caches = new List<CacheInfo>();
        var seen = new HashSet<(byte, uint, uint)>();

        foreach (var (relation, data) in Query(NativeMethods.RelationCache))
        {
            if (relation != NativeMethods.RelationCache || data.Length < 20)
            {
                continue;
            }

            // CACHE_RELATIONSHIP (na union, offset 8): Level(0), Associativity(1),
            // LineSize(2, WORD), CacheSize(4, DWORD), Type(8, DWORD).
            const int union = 8;
            var level = data[union];
            var associativity = data[union + 1];
            var lineSize = BitConverter.ToUInt16(data, union + 2);
            var size = BitConverter.ToUInt32(data, union + 4);
            var type = BitConverter.ToUInt32(data, union + 8);

            // A API devolve uma entrada por núcleo; agrupa por nível/tipo/tamanho.
            if (size > 0 && seen.Add((level, type, size)))
            {
                caches.Add(new CacheInfo(level, associativity, lineSize, size, type));
            }
        }

        return caches
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Type)
            .ThenByDescending(c => c.Size)
            .ToList();
    }
}

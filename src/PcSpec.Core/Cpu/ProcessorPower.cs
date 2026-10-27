using System.Runtime.InteropServices;
using PcSpec.Core.Interop;

namespace PcSpec.Core.Cpu;

/// <summary>
/// Frequência corrente por processador lógico, via <c>CallNtPowerInformation</c>
/// (nível ProcessorInformation). E o que o Gerenciador de Tarefas usa, então
/// reflete o boost real sem precisar de driver/MSR.
/// </summary>
public static class ProcessorPower
{
    public sealed record Reading(uint MaxMhz, uint CurrentMhz, uint MhzLimit);

    public static IReadOnlyList<Reading> Read()
    {
        var readings = new List<Reading>();
        var count = ProcessorTopology.LogicalProcessors;
        if (count <= 0 || count > 1024)
        {
            return readings;
        }

        const int structSize = 6 * sizeof(uint);
        var size = count * structSize;
        var buffer = Marshal.AllocHGlobal(size);

        try
        {
            var status = NativeMethods.CallNtPowerInformation(
                NativeMethods.ProcessorInformation,
                IntPtr.Zero,
                0,
                buffer,
                (uint)size);

            if (status != 0)
            {
                return readings;
            }

            for (var i = 0; i < count; i++)
            {
                var offset = i * structSize;
                var max = (uint)Marshal.ReadInt32(buffer, offset + 4);
                var current = (uint)Marshal.ReadInt32(buffer, offset + 8);
                var limit = (uint)Marshal.ReadInt32(buffer, offset + 12);

                if (current > 0 && current < 20_000)
                {
                    readings.Add(new Reading(max, current, limit));
                }
            }
        }
        catch
        {
            // Sem informação de P-state: o provider usa a frequência nominal.
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return readings;
    }
}

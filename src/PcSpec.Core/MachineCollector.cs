using PcSpec.Core.Bench;
using PcSpec.Core.Board;
using PcSpec.Core.Cpu;
using PcSpec.Core.Gpu;
using PcSpec.Core.Memory;
using PcSpec.Core.Model;
using PcSpec.Core.Smbios;
using PcSpec.Core.Platform;
using PcSpec.Core.SystemInfo;

namespace PcSpec.Core;

/// <summary>
/// Coleta tudo em um <see cref="MachineSpec"/>. Cada seção e isolada: uma falha
/// vira uma linha em <see cref="MachineSpec.Diagnostics"/> em vez de derrubar a coleta.
/// </summary>
public static class MachineCollector
{
    public static MachineSpec Collect(BenchmarkOptions? benchmark = null)
    {
        var diagnostics = new List<string>();

        SmbiosTable? smbios = null;
        try
        {
            smbios = SmbiosTable.Read();
            if (smbios is null)
            {
                diagnostics.Add("GetSystemFirmwareTable('RSMB') não retornou dados; usando apenas WMI.");
            }
            else if (smbios.Structures.Count == 0)
            {
                diagnostics.Add("Tabela SMBIOS vazia.");
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Falha ao ler SMBIOS: {ex.GetType().Name}: {ex.Message}");
        }

        var system = Guard(() => SystemInfoProvider.Collect(smbios, diagnostics), new SystemSpec(), "sistema", diagnostics);
        var (board, bios) = Guard(
            () => BoardInfoProvider.Collect(smbios, diagnostics),
            (new BoardSpec(), new BiosSpec()),
            "placa-mãe",
            diagnostics);
        var cpu = Guard(() => CpuInfoProvider.Collect(smbios, diagnostics), new CpuSpec(), "cpu", diagnostics);
        var memory = Guard(() => MemoryInfoProvider.Collect(smbios, diagnostics), new MemorySpec(), "memória", diagnostics);
        var gpus = Guard(() => GpuInfoProvider.Collect(diagnostics), (IReadOnlyList<GpuSpec>)[], "gráficos", diagnostics);

        BenchmarkResult? result = null;
        if (benchmark is not null)
        {
            result = Guard(() => BenchmarkRunner.Run(benchmark, cpu.Name), null, "benchmark", diagnostics);
        }

        if (!Wmi.IsAvailable)
        {
            diagnostics.AddRange(Wmi.Diagnostics);
        }

        return new MachineSpec
        {
            CollectedAt = DateTimeOffset.Now,
            System = system,
            Bios = bios,
            Board = board,
            Cpu = cpu,
            Memory = memory,
            Gpus = gpus,
            Benchmark = result,
            Diagnostics = diagnostics.Distinct().ToList(),
        };
    }

    private static T Guard<T>(Func<T> action, T fallback, string section, ICollection<string> diagnostics)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Seção '{section}' falhou: {ex.GetType().Name}: {ex.Message}");
            return fallback;
        }
    }
}

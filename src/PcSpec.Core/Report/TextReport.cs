using System.Globalization;
using System.Text;
using PcSpec.Core.Model;

namespace PcSpec.Core.Report;

[Flags]
public enum ReportSections
{
    None = 0,
    System = 1,
    Bios = 2,
    Board = 4,
    Cpu = 8,
    Caches = 16,
    Memory = 32,
    Graphics = 64,
    Benchmark = 128,
    Diagnostics = 256,
    All = System | Bios | Board | Cpu | Caches | Memory | Graphics | Benchmark | Diagnostics,
}

/// <summary>Relatório em texto puro, no estilo de bloco de especificações.</summary>
public static class TextReport
{
    private const int LabelWidth = 26;

    public static string Build(MachineSpec spec, ReportSections sections = ReportSections.All)
    {
        var sb = new StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine(" PcSpec 0.1 — Relatório de hardware");
        sb.AppendLine($" Gerado em {spec.CollectedAt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}");
        sb.AppendLine("================================================================================");

        if (sections.HasFlag(ReportSections.System))
        {
            System(sb, spec);
        }

        if (sections.HasFlag(ReportSections.Bios))
        {
            Bios(sb, spec);
        }

        if (sections.HasFlag(ReportSections.Board))
        {
            Board(sb, spec);
        }

        if (sections.HasFlag(ReportSections.Cpu))
        {
            Cpu(sb, spec);
        }

        if (sections.HasFlag(ReportSections.Caches))
        {
            Caches(sb, spec);
        }

        if (sections.HasFlag(ReportSections.Memory))
        {
            Memory(sb, spec);
        }

        if (sections.HasFlag(ReportSections.Graphics))
        {
            Graphics(sb, spec);
        }

        if (sections.HasFlag(ReportSections.Benchmark))
        {
            Benchmark(sb, spec);
        }

        if (sections.HasFlag(ReportSections.Diagnostics) && spec.Diagnostics.Count > 0)
        {
            Header(sb, "DIAGNÓSTICO");
            foreach (var item in spec.Diagnostics)
            {
                sb.AppendLine($" * {item}");
            }

            sb.AppendLine();
        }

        sb.AppendLine(" Fim do relatório.");
        return sb.ToString();
    }

    private static void System(StringBuilder sb, MachineSpec spec)
    {
        var s = spec.System;
        Header(sb, "SISTEMA");
        Field(sb, "Fabricante", s.Manufacturer);
        Field(sb, "Modelo", s.Model);
        Field(sb, "Família", s.Family);
        Field(sb, "SKU", s.Sku);
        Field(sb, "Número de série", s.SerialNumber);
        Field(sb, "UUID", s.Uuid);
        Field(sb, "Tipo de chassi", s.ChassisType);
        Field(sb, "Nome da máquina", s.MachineName);
        Field(sb, "Sistema operacional", s.OsName);
        Field(sb, "Versão", s.OsVersion);
        Field(sb, "Arquitetura", s.OsArchitecture);
        Field(sb, "Instalação", s.OsInstallDate);
        Field(sb, "Memória total", Format.Bytes(s.TotalPhysicalMemoryBytes));
        Field(sb, "Modo de boot", s.BootMode);
        Field(sb, "Secure Boot", s.SecureBoot);
        sb.AppendLine();
    }

    private static void Bios(StringBuilder sb, MachineSpec spec)
    {
        var b = spec.Bios;
        Header(sb, "BIOS");
        Field(sb, "Fornecedor", b.Vendor);
        Field(sb, "Versão", b.Version);
        Field(sb, "Data", b.ReleaseDate);
        Field(sb, "Versão SMBIOS", b.SmbiosVersion);
        Field(sb, "Tipo de firmware", b.FirmwareType);
        Field(sb, "Tamanho da ROM", b.RomSizeBytes > 0 ? Format.Bytes(b.RomSizeBytes) : null);
        if (b.MajorRelease is not null)
        {
            Field(sb, "BIOS do sistema", $"{b.MajorRelease}.{b.MinorRelease ?? "?"}");
        }

        sb.AppendLine();
    }

    private static void Board(StringBuilder sb, MachineSpec spec)
    {
        var b = spec.Board;
        Header(sb, "PLACA-MÃE");
        Field(sb, "Fabricante", b.Manufacturer);
        Field(sb, "Modelo", b.Product);
        Field(sb, "Versão", b.Version);
        Field(sb, "Número de série", b.SerialNumber);
        Field(sb, "Asset tag", b.AssetTag);
        Field(sb, "Tipo", b.BoardType);
        Field(sb, "Chipset", b.Chipset);
        Field(sb, "Southbridge", b.Southbridge);

        if (b.Bridges.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(" Pontes PCI:");
            foreach (var bridge in b.Bridges)
            {
                var name = bridge.Name ?? "(não catalogada)";
                sb.AppendLine($"   {bridge.Class,-20} {bridge.VendorId}:{bridge.DeviceId}  {name}");
            }
        }

        sb.AppendLine();
    }

    private static void Cpu(StringBuilder sb, MachineSpec spec)
    {
        var c = spec.Cpu;
        Header(sb, "CPU");
        Field(sb, "Nome", c.Name);
        Field(sb, "Codename", c.Codename);
        Field(sb, "Socket", c.Socket);
        Field(sb, "Tecnologia", c.Technology);
        Field(sb, "Família", c.Family);
        Field(sb, "Modelo", c.Model);
        Field(sb, "Stepping", c.Stepping);
        Field(sb, "Ext. família", c.ExtFamily);
        Field(sb, "Ext. modelo", c.ExtModel);
        Field(sb, "Núcleos", c.Cores > 0 ? c.Cores.ToString(CultureInfo.InvariantCulture) : null);
        Field(sb, "Threads", c.Threads > 0 ? c.Threads.ToString(CultureInfo.InvariantCulture) : null);
        Field(sb, "Soquetes", c.Packages > 0 ? c.Packages.ToString(CultureInfo.InvariantCulture) : null);

        sb.AppendLine();
        Field(sb, "Core speed", Format.Mhz(c.CoreSpeedMhz));
        if (c.MinSpeedMhz is > 0 && c.MaxSpeedMhz is > 0 && c.MaxSpeedMhz - c.MinSpeedMhz > 50)
        {
            Field(sb, "  faixa observada", $"{Format.Num(c.MinSpeedMhz!.Value, "0")} – {Format.Num(c.MaxSpeedMhz!.Value, "0")} MHz");
        }

        Field(sb, "Max clock", Format.Mhz(c.MaxClockMhz));
        Field(sb, "Base clock", Format.Mhz(c.BaseClockMhz));
        Field(sb, "Bus speed", Format.Mhz(c.BusSpeedMhz));
        Field(sb, "Multiplicador", c.Multiplier is > 0 ? $"x {Format.Num(c.Multiplier.Value, "0.#")}" : null);
        Field(sb, "Tensão do núcleo", c.CoreVoltage is > 0 ? $"{Format.Num(c.CoreVoltage.Value, "0.000")} V" : null);

        if (!string.IsNullOrWhiteSpace(c.Instructions))
        {
            sb.AppendLine();
            sb.AppendLine(" Instruções:");
            foreach (var line in Wrap(c.Instructions, 74))
            {
                sb.AppendLine($"   {line}");
            }
        }

        if (c.Notes.Count > 0)
        {
            sb.AppendLine();
            foreach (var note in c.Notes)
            {
                sb.AppendLine($"   ({note})");
            }
        }

        sb.AppendLine();
    }

    private static void Caches(StringBuilder sb, MachineSpec spec)
    {
        if (spec.Cpu.Caches.Count == 0)
        {
            return;
        }

        Header(sb, "CACHES");
        foreach (var cache in spec.Cpu.Caches)
        {
            var details = new List<string> { Format.Bytes(cache.SizeBytes) };
            if (cache.Ways > 0)
            {
                details.Add($"{cache.Ways}-way");
            }

            if (cache.LineSize > 0)
            {
                details.Add($"{cache.LineSize} B/linha");
            }

            if (cache.Sharing > 1)
            {
                details.Add($"compartilhado por {cache.Sharing}");
            }

            Field(sb, cache.Display, string.Join(", ", details));
        }

        sb.AppendLine();
    }

    private static void Memory(StringBuilder sb, MachineSpec spec)
    {
        var m = spec.Memory;
        Header(sb, "MEMÓRIA");
        Field(sb, "Total", Format.Bytes(m.TotalBytes));
        Field(sb, "Capacidade máxima", m.MaxCapacityBytes > 0 ? Format.Bytes(m.MaxCapacityBytes) : null);
        Field(sb, "Slots", m.SlotCount > 0 ? $"{m.PopulatedSlots} de {m.SlotCount} ocupados" : null);
        Field(sb, "Canais", m.Channels > 0 ? m.Channels.ToString(CultureInfo.InvariantCulture) : null);
        Field(sb, "Tipo", m.Type);
        Field(sb, "Velocidade", DescribeSpeed(m.SpeedMts));
        Field(sb, "Formato", m.FormFactor);
        Field(sb, "ECC", m.Ecc ? "Sim" : "Não");
        sb.AppendLine();

        for (var i = 0; i < m.Modules.Count; i++)
        {
            var module = m.Modules[i];
            var channel = string.IsNullOrWhiteSpace(module.Channel) ? null : $"canal {module.Channel}";
            var title = string.Join(" · ", new[] { module.Locator ?? $"Módulo {i + 1}", channel }.Where(t => t is not null));
            sb.AppendLine($" Módulo {i + 1} — {title}");

            Field(sb, "  Banco", module.BankLocator, 30);
            Field(sb, "  Tamanho", Format.Bytes(module.SizeBytes), 30);
            Field(sb, "  Tipo", module.Type, 30);
            Field(sb, "  Formato", module.FormFactor, 30);
            Field(sb, "  Velocidade", DescribeSpeed(module.ConfiguredSpeedMts > 0 ? module.ConfiguredSpeedMts : module.SpeedMts), 30);
            if (module.ConfiguredSpeedMts > 0 && module.SpeedMts > 0 && module.ConfiguredSpeedMts != module.SpeedMts)
            {
                Field(sb, "  Velocidade nominal", DescribeSpeed(module.SpeedMts), 30);
            }

            Field(sb, "  Fabricante", module.Manufacturer, 30);
            Field(sb, "  Part number", module.PartNumber, 30);
            Field(sb, "  Número de série", module.SerialNumber, 30);
            Field(sb, "  Ranks", module.Rank > 0 ? module.Rank.ToString(CultureInfo.InvariantCulture) : null, 30);
            Field(sb, "  Tensão", module.Voltage is > 0 ? $"{Format.Num(module.Voltage.Value, "0.000")} V" : null, 30);
            Field(sb, "  Largura", module.DataWidth > 0 ? $"{module.DataWidth} bits" : null, 30);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(m.Note))
        {
            foreach (var line in Wrap(m.Note, 74))
            {
                sb.AppendLine($"   ({line})");
            }

            sb.AppendLine();
        }
    }

    private static void Graphics(StringBuilder sb, MachineSpec spec)
    {
        Header(sb, "GRÁFICOS");

        if (spec.Gpus.Count == 0)
        {
            sb.AppendLine(" Nenhum adaptador de vídeo identificado.");
            sb.AppendLine();
            return;
        }

        for (var i = 0; i < spec.Gpus.Count; i++)
        {
            var gpu = spec.Gpus[i];
            if (spec.Gpus.Count > 1)
            {
                sb.AppendLine($" Adaptador {i + 1}");
            }

            Field(sb, "Nome", gpu.Name);
            Field(sb, "Fabricante", gpu.Vendor);
            Field(sb, "IDs PCI", gpu.VendorId is null ? null : $"{gpu.VendorId}:{gpu.DeviceId}");
            Field(sb, "Processador", gpu.Processor);
            Field(sb, "Memória", gpu.MemoryBytes > 0 ? Format.Bytes(gpu.MemoryBytes) : null);
            Field(sb, "Barramento", gpu.MemoryBusBits is > 0 ? $"{gpu.MemoryBusBits} bits" : null);
            Field(sb, "Driver", gpu.DriverVersion);
            Field(sb, "Data do driver", gpu.DriverDate);
            Field(sb, "Resolução", gpu.Resolution);
            Field(sb, "Taxa de atualização", gpu.RefreshRate is > 0 ? $"{gpu.RefreshRate} Hz" : null);
            Field(sb, "Status", gpu.Status);
            sb.AppendLine();
        }
    }

    private static void Benchmark(StringBuilder sb, MachineSpec spec)
    {
        var bench = spec.Benchmark;
        if (bench is null)
        {
            Header(sb, "BENCHMARK");
            sb.AppendLine(" Nenhuma medição nesta execução. Use o comando 'bench'.");
            sb.AppendLine();
            return;
        }

        Header(sb, "BENCHMARK");
        Field(sb, "Executado em", bench.RunAt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
        Field(sb, "Threads", bench.Threads.ToString(CultureInfo.InvariantCulture));
        Field(sb, "Repetições", bench.Runs.ToString(CultureInfo.InvariantCulture));
        Field(sb, "Duração por teste", $"{bench.DurationMs} ms");
        sb.AppendLine();

        Field(sb, "Score single-thread", Format.Num(bench.SingleThreadScore, "0"));
        Field(sb, "Score multi-thread", Format.Num(bench.MultiThreadScore, "0"));
        Field(sb, "Escalabilidade", $"x {Format.Num(bench.Scaling, "0.00")}");
        sb.AppendLine();

        sb.AppendLine($" {"Subteste",-20} {"1 thread",12} {"N threads",12} {"Score 1T",10} {"Score NT",10}");
        sb.AppendLine($" {new string('-', 20)} {new string('-', 12)} {new string('-', 12)} {new string('-', 10)} {new string('-', 10)}");

        foreach (var single in bench.Single)
        {
            var multi = bench.Multi.FirstOrDefault(m => m.Name == single.Name);
            sb.AppendLine(
                $" {single.Name,-20} {single.DisplayValue,12} {multi?.DisplayValue ?? "—",12} " +
                $"{Format.Num(single.Score, "0"),10} {Format.Num(multi?.Score ?? 0, "0"),10}");
        }

        sb.AppendLine();
        sb.AppendLine(" O score do PcSpec não é comparável ao do CPU-Z: a carga e a normalização");
        sb.AppendLine(" são próprias. Use-o para comparar máquinas entre si, com a mesma versão.");

        if (bench.Notes.Count > 0)
        {
            sb.AppendLine();
            foreach (var note in bench.Notes)
            {
                sb.AppendLine($"   ({note})");
            }
        }

        sb.AppendLine();
    }

    private static string? DescribeSpeed(int mts) =>
        mts <= 0 ? null : $"{mts} MT/s ({Format.Num(mts / 2d, "0")} MHz)";

    private static void Header(StringBuilder sb, string title)
    {
        sb.AppendLine(title);
        sb.AppendLine(new string('-', 80));
    }

    private static void Field(StringBuilder sb, string label, string? value, int labelWidth = LabelWidth)
    {
        var text = Format.Or(value);
        var padded = label.Length + 1 >= labelWidth
            ? label + " "
            : (label + " ").PadRight(labelWidth, '.');

        sb.AppendLine($"{padded} {text}");
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();

        foreach (var word in words)
        {
            if (line.Length + word.Length + 1 > width && line.Length > 0)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }
}

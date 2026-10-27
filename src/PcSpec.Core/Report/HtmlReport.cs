using System.Globalization;
using System.Net;
using System.Text;
using PcSpec.Core.Model;

namespace PcSpec.Core.Report;

/// <summary>Relatório HTML autocontido (CSS embutido, sem recursos externos).</summary>
public static class HtmlReport
{
    public static string Build(MachineSpec spec)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine($"<title>PcSpec — {E(spec.System.MachineName ?? "relatório")}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        Hero(sb, spec);

        sb.AppendLine("<main>");
        SystemSection(sb, spec);
        BiosSection(sb, spec);
        BoardSection(sb, spec);
        CpuSection(sb, spec);
        CacheSection(sb, spec);
        MemorySection(sb, spec);
        GraphicsSection(sb, spec);
        sb.AppendLine("</main>");

        sb.AppendLine("<div class=\"wide\">");
        BenchmarkSection(sb, spec);
        DiagnosticsSection(sb, spec);
        sb.AppendLine("</div>");

        sb.AppendLine("<footer>");
        sb.AppendLine("Gerado por <strong>PcSpec</strong> — reimplementação independente inspirada no CPU-Z, " +
                      "usando apenas APIs públicas do Windows (CPUID, SMBIOS, WMI e registro).");
        sb.AppendLine("</footer>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static void Hero(StringBuilder sb, MachineSpec spec)
    {
        var cpu = spec.Cpu;
        var memory = spec.Memory;

        sb.AppendLine("<header class=\"hero\">");
        sb.AppendLine("<div class=\"brand\">PcSpec<span>0.1</span></div>");
        sb.AppendLine($"<h1>{E(spec.System.MachineName ?? "Computador")}</h1>");
        sb.AppendLine($"<p class=\"sub\">{E(spec.System.Manufacturer)} {E(spec.System.Model)} · " +
                      $"{E(spec.System.OsName)} {E(spec.System.OsArchitecture)}</p>");

        sb.AppendLine("<div class=\"chips\">");
        Chip(sb, "CPU", cpu.Name ?? "—");
        Chip(sb, "Núcleos", cpu.Cores > 0 ? $"{cpu.Cores}C / {cpu.Threads}T" : "—");
        Chip(sb, "Memória", Format.Bytes(memory.TotalBytes) + (memory.SpeedMts > 0 ? $" · {memory.SpeedMts} MT/s" : ""));
        Chip(sb, "Placa-mãe", spec.Board.Product ?? spec.Board.Manufacturer ?? "—");
        foreach (var gpu in spec.Gpus.Take(2))
        {
            Chip(sb, "GPU", gpu.Name ?? "—");
        }

        sb.AppendLine("</div>");
        sb.AppendLine($"<p class=\"stamp\">Coletado em {spec.CollectedAt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}</p>");
        sb.AppendLine("</header>");
    }

    private static void Chip(StringBuilder sb, string label, string value) =>
        sb.AppendLine($"<div class=\"chip\"><span>{E(label)}</span><strong>{E(value)}</strong></div>");

    private static void SystemSection(StringBuilder sb, MachineSpec spec)
    {
        var s = spec.System;
        Section(sb, "Sistema", () =>
        {
            Kv(sb, "Fabricante", s.Manufacturer);
            Kv(sb, "Modelo", s.Model);
            Kv(sb, "Família", s.Family);
            Kv(sb, "SKU", s.Sku);
            Kv(sb, "Número de série", s.SerialNumber);
            Kv(sb, "UUID", s.Uuid);
            Kv(sb, "Tipo de chassi", s.ChassisType);
            Kv(sb, "Sistema operacional", s.OsName);
            Kv(sb, "Versão", s.OsVersion);
            Kv(sb, "Arquitetura", s.OsArchitecture);
            Kv(sb, "Instalação", s.OsInstallDate);
            Kv(sb, "Memória total", Format.Bytes(s.TotalPhysicalMemoryBytes));
            Kv(sb, "Modo de boot", s.BootMode);
            Kv(sb, "Secure Boot", s.SecureBoot);
        });
    }

    private static void BiosSection(StringBuilder sb, MachineSpec spec)
    {
        var b = spec.Bios;
        Section(sb, "BIOS", () =>
        {
            Kv(sb, "Fornecedor", b.Vendor);
            Kv(sb, "Versão", b.Version);
            Kv(sb, "Data", b.ReleaseDate);
            Kv(sb, "Versão SMBIOS", b.SmbiosVersion);
            Kv(sb, "Tipo de firmware", b.FirmwareType);
            Kv(sb, "Tamanho da ROM", b.RomSizeBytes > 0 ? Format.Bytes(b.RomSizeBytes) : null);
        });
    }

    private static void BoardSection(StringBuilder sb, MachineSpec spec)
    {
        var b = spec.Board;
        Section(sb, "Placa-mãe", () =>
        {
            Kv(sb, "Fabricante", b.Manufacturer);
            Kv(sb, "Modelo", b.Product);
            Kv(sb, "Versão", b.Version);
            Kv(sb, "Número de série", b.SerialNumber);
            Kv(sb, "Tipo", b.BoardType);
            Kv(sb, "Chipset", b.Chipset);
            Kv(sb, "Southbridge", b.Southbridge);
        });

        if (b.Bridges.Count > 0)
        {
            sb.AppendLine("<div class=\"panel\"><h2>Pontes PCI</h2><table class=\"grid\">");
            sb.AppendLine("<thead><tr><th>Classe</th><th>Vendor:Device</th><th>Descrição</th></tr></thead><tbody>");
            foreach (var bridge in b.Bridges)
            {
                sb.AppendLine($"<tr><td>{E(bridge.Class)}</td><td class=\"mono\">{E(bridge.VendorId)}:{E(bridge.DeviceId)}</td>" +
                              $"<td>{E(bridge.Name ?? "(não catalogada)")}</td></tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }
    }

    private static void CpuSection(StringBuilder sb, MachineSpec spec)
    {
        var c = spec.Cpu;
        Section(sb, "CPU", () =>
        {
            Kv(sb, "Nome", c.Name);
            Kv(sb, "Codename", c.Codename);
            Kv(sb, "Socket", c.Socket);
            Kv(sb, "Tecnologia", c.Technology);
            Kv(sb, "Família", c.Family);
            Kv(sb, "Modelo", c.Model);
            Kv(sb, "Stepping", c.Stepping);
            Kv(sb, "Ext. família", c.ExtFamily);
            Kv(sb, "Ext. modelo", c.ExtModel);
            Kv(sb, "Núcleos", c.Cores > 0 ? c.Cores.ToString(CultureInfo.InvariantCulture) : null);
            Kv(sb, "Threads", c.Threads > 0 ? c.Threads.ToString(CultureInfo.InvariantCulture) : null);
            Kv(sb, "Core speed", Format.Mhz(c.CoreSpeedMhz));
            Kv(sb, "Max clock", Format.Mhz(c.MaxClockMhz));
            Kv(sb, "Bus speed", Format.Mhz(c.BusSpeedMhz));
            Kv(sb, "Multiplicador", c.Multiplier is > 0 ? $"x {Format.Num(c.Multiplier.Value, "0.#")}" : null);
            Kv(sb, "Tensão do núcleo", c.CoreVoltage is > 0 ? $"{Format.Num(c.CoreVoltage.Value, "0.000")} V" : null);
        });

        if (!string.IsNullOrWhiteSpace(c.Instructions))
        {
            sb.AppendLine("<div class=\"panel\"><h2>Instruções</h2>");
            sb.AppendLine($"<p class=\"instructions\">{E(c.Instructions)}</p></div>");
        }

        if (c.Notes.Count > 0)
        {
            Notes(sb, c.Notes);
        }
    }

    private static void CacheSection(StringBuilder sb, MachineSpec spec)
    {
        if (spec.Cpu.Caches.Count == 0)
        {
            return;
        }

        sb.AppendLine("<div class=\"panel\"><h2>Caches</h2><table class=\"grid\">");
        sb.AppendLine("<thead><tr><th>Nível</th><th>Tipo</th><th>Tamanho</th><th>Associatividade</th><th>Linha</th><th>Compartilhamento</th></tr></thead><tbody>");
        foreach (var cache in spec.Cpu.Caches)
        {
            sb.AppendLine($"<tr><td>{E(cache.Level)}</td><td>{E(cache.Type)}</td><td>{E(Format.Bytes(cache.SizeBytes))}</td>" +
                          $"<td>{(cache.Ways > 0 ? cache.Ways + "-way" : "—")}</td>" +
                          $"<td>{(cache.LineSize > 0 ? cache.LineSize + " B" : "—")}</td>" +
                          $"<td>{(cache.Sharing > 1 ? cache.Sharing + " threads" : "1 thread")}</td></tr>");
        }

        sb.AppendLine("</tbody></table></div>");
    }

    private static void MemorySection(StringBuilder sb, MachineSpec spec)
    {
        var m = spec.Memory;
        Section(sb, "Memória", () =>
        {
            Kv(sb, "Total", Format.Bytes(m.TotalBytes));
            Kv(sb, "Capacidade máxima", m.MaxCapacityBytes > 0 ? Format.Bytes(m.MaxCapacityBytes) : null);
            Kv(sb, "Slots", m.SlotCount > 0 ? $"{m.PopulatedSlots} de {m.SlotCount} ocupados" : null);
            Kv(sb, "Canais", m.Channels > 0 ? m.Channels.ToString(CultureInfo.InvariantCulture) : null);
            Kv(sb, "Tipo", m.Type);
            Kv(sb, "Velocidade", m.SpeedMts > 0 ? $"{m.SpeedMts} MT/s" : null);
            Kv(sb, "Formato", m.FormFactor);
            Kv(sb, "ECC", m.Ecc ? "Sim" : "Não");
        });

        if (m.Modules.Count > 0)
        {
            sb.AppendLine("<div class=\"panel\"><h2>Módulos</h2><table class=\"grid\">");
            sb.AppendLine("<thead><tr><th>Slot</th><th>Canal</th><th>Tamanho</th><th>Tipo</th><th>Velocidade</th>" +
                          "<th>Fabricante</th><th>Part number</th><th>Ranks</th></tr></thead><tbody>");
            foreach (var module in m.Modules)
            {
                var speed = module.ConfiguredSpeedMts > 0 ? module.ConfiguredSpeedMts : module.SpeedMts;
                sb.AppendLine($"<tr><td>{E(module.Locator)}</td><td>{E(module.Channel ?? "—")}</td>" +
                              $"<td>{E(Format.Bytes(module.SizeBytes))}</td><td>{E(module.Type)}</td>" +
                              $"<td>{(speed > 0 ? speed + " MT/s" : "—")}</td>" +
                              $"<td>{E(module.Manufacturer)}</td><td class=\"mono\">{E(module.PartNumber)}</td>" +
                              $"<td>{(module.Rank > 0 ? module.Rank.ToString(CultureInfo.InvariantCulture) : "—")}</td></tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }

        if (!string.IsNullOrWhiteSpace(m.Note))
        {
            Notes(sb, [m.Note]);
        }
    }

    private static void GraphicsSection(StringBuilder sb, MachineSpec spec)
    {
        if (spec.Gpus.Count == 0)
        {
            return;
        }

        foreach (var gpu in spec.Gpus)
        {
            Section(sb, $"Gráficos — {gpu.Name}", () =>
            {
                Kv(sb, "Fabricante", gpu.Vendor);
                Kv(sb, "IDs PCI", gpu.VendorId is null ? null : $"{gpu.VendorId}:{gpu.DeviceId}");
                Kv(sb, "Processador", gpu.Processor);
                Kv(sb, "Memória", gpu.MemoryBytes > 0 ? Format.Bytes(gpu.MemoryBytes) : null);
                Kv(sb, "Barramento", gpu.MemoryBusBits is > 0 ? $"{gpu.MemoryBusBits} bits" : null);
                Kv(sb, "Driver", gpu.DriverVersion);
                Kv(sb, "Data do driver", gpu.DriverDate);
                Kv(sb, "Resolução", gpu.Resolution);
                Kv(sb, "Taxa de atualização", gpu.RefreshRate is > 0 ? $"{gpu.RefreshRate} Hz" : null);
                Kv(sb, "Status", gpu.Status);
            });
        }
    }

    private static void BenchmarkSection(StringBuilder sb, MachineSpec spec)
    {
        var bench = spec.Benchmark;
        if (bench is null)
        {
            return;
        }

        sb.AppendLine("<div class=\"panel\"><h2>Benchmark</h2>");

        sb.AppendLine("<div class=\"scores\">");
        Score(sb, "Single-thread", bench.SingleThreadScore);
        Score(sb, "Multi-thread", bench.MultiThreadScore);
        Score(sb, "Escalabilidade", bench.Scaling, suffix: "x", format: "0.00");
        sb.AppendLine("</div>");

        sb.AppendLine("<table class=\"grid\">");
        sb.AppendLine("<thead><tr><th>Subteste</th><th>1 thread</th><th>N threads</th><th>Score 1T</th><th>Score NT</th><th>Barra</th></tr></thead><tbody>");

        var maxScore = bench.Multi.Count == 0 ? 1 : Math.Max(1, bench.Multi.Max(m => m.Score));
        foreach (var single in bench.Single)
        {
            var multi = bench.Multi.FirstOrDefault(m => m.Name == single.Name);
            var width = Math.Clamp(multi?.Score / maxScore * 100 ?? 0, 0, 100);

            sb.AppendLine($"<tr><td>{E(single.Name)}</td><td>{E(single.DisplayValue)}</td>" +
                          $"<td>{E(multi?.DisplayValue ?? "—")}</td>" +
                          $"<td class=\"mono\">{Format.Num(single.Score, "0")}</td>" +
                          $"<td class=\"mono\">{Format.Num(multi?.Score ?? 0, "0")}</td>" +
                          $"<td><div class=\"bar\"><span style=\"width:{Format.Num(width, "0.#")}%\"></span></div></td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p class=\"hint\">{bench.Threads} threads · {bench.Runs} repetições · {bench.DurationMs} ms por teste. " +
                      "O score do PcSpec não é comparável ao do CPU-Z.</p>");

        if (bench.Notes.Count > 0)
        {
            foreach (var note in bench.Notes)
            {
                sb.AppendLine($"<p class=\"hint\">{E(note)}</p>");
            }
        }

        sb.AppendLine("</div>");
    }

    private static void DiagnosticsSection(StringBuilder sb, MachineSpec spec)
    {
        if (spec.Diagnostics.Count == 0)
        {
            return;
        }

        sb.AppendLine("<div class=\"panel\"><h2>Diagnóstico</h2><ul class=\"diag\">");
        foreach (var item in spec.Diagnostics)
        {
            sb.AppendLine($"<li>{E(item)}</li>");
        }

        sb.AppendLine("</ul></div>");
    }

    private static void Score(StringBuilder sb, string label, double value, string suffix = "", string format = "0") =>
        sb.AppendLine($"<div class=\"score\"><span>{E(label)}</span><strong>{Format.Num(value, format)}{E(suffix)}</strong></div>");

    private static void Section(StringBuilder sb, string title, Action body)
    {
        sb.AppendLine($"<div class=\"panel\"><h2>{E(title)}</h2><table class=\"kv\"><tbody>");
        body();
        sb.AppendLine("</tbody></table></div>");
    }

    private static void Kv(StringBuilder sb, string label, string? value) =>
        sb.AppendLine($"<tr><th>{E(label)}</th><td>{E(Format.Or(value))}</td></tr>");

    private static void Notes(StringBuilder sb, IEnumerable<string> notes)
    {
        sb.AppendLine("<div class=\"panel small\">");
        foreach (var note in notes)
        {
            sb.AppendLine($"<p class=\"hint\">{E(note)}</p>");
        }

        sb.AppendLine("</div>");
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private const string Css = """
        :root {
          --bg: #0f1319;
          --panel: #161c24;
          --panel-2: #1a2029;
          --border: #232c37;
          --text: #e6edf3;
          --muted: #8b98a5;
          --accent: #4c9aff;
          --good: #4ade80;
        }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          padding: 0 0 48px;
          background: radial-gradient(1200px 600px at 20% -10%, #1b2634 0%, var(--bg) 55%);
          color: var(--text);
          font: 14px/1.55 "Segoe UI", system-ui, -apple-system, sans-serif;
        }
        .hero {
          padding: 40px 32px 28px;
          max-width: 1180px;
          margin: 0 auto;
        }
        .brand {
          font-size: 13px;
          letter-spacing: 3px;
          text-transform: uppercase;
          color: var(--accent);
          font-weight: 600;
        }
        .brand span {
          margin-left: 8px;
          color: var(--muted);
          font-weight: 400;
          letter-spacing: 1px;
        }
        h1 {
          margin: 10px 0 4px;
          font-size: 34px;
          font-weight: 650;
          letter-spacing: -0.5px;
        }
        .sub { margin: 0; color: var(--muted); font-size: 15px; }
        .stamp { margin: 18px 0 0; color: var(--muted); font-size: 12px; }
        .chips {
          display: flex;
          flex-wrap: wrap;
          gap: 10px;
          margin-top: 22px;
        }
        .chip {
          background: var(--panel);
          border: 1px solid var(--border);
          border-radius: 8px;
          padding: 10px 14px;
          min-width: 150px;
        }
        .chip span {
          display: block;
          font-size: 11px;
          letter-spacing: 1.2px;
          text-transform: uppercase;
          color: var(--muted);
        }
        .chip strong { font-size: 14px; font-weight: 600; }
        main {
          max-width: 1180px;
          margin: 0 auto;
          padding: 0 32px;
          columns: 2 360px;
          column-gap: 18px;
        }
        .panel {
          background: var(--panel);
          border: 1px solid var(--border);
          border-radius: 10px;
          padding: 18px 20px;
          margin: 0 0 18px;
          break-inside: avoid;
        }
        .panel.small { background: transparent; border-style: dashed; }
        .wide {
          max-width: 1180px;
          margin: 0 auto;
          padding: 0 32px;
        }
        .wide .panel { margin: 0 0 18px; }
        h2 {
          margin: 0 0 14px;
          font-size: 13px;
          letter-spacing: 1.6px;
          text-transform: uppercase;
          color: var(--accent);
          font-weight: 600;
        }
        table { width: 100%; border-collapse: collapse; }
        table.kv th {
          text-align: left;
          font-weight: 400;
          color: var(--muted);
          padding: 5px 12px 5px 0;
          width: 44%;
          vertical-align: top;
          white-space: nowrap;
        }
        table.kv td { padding: 5px 0; }
        table.kv tr + tr th, table.kv tr + tr td { border-top: 1px solid rgba(35, 44, 55, 0.55); }
        table.grid { font-size: 13px; }
        table.grid th {
          text-align: left;
          font-size: 11px;
          letter-spacing: 1px;
          text-transform: uppercase;
          color: var(--muted);
          font-weight: 600;
          padding: 0 10px 8px 0;
          border-bottom: 1px solid var(--border);
        }
        table.grid td { padding: 7px 10px 7px 0; border-bottom: 1px solid rgba(35, 44, 55, 0.55); }
        table.grid tr:last-child td { border-bottom: none; }
        .mono { font-family: "Cascadia Mono", Consolas, monospace; font-size: 12.5px; color: #c7d3e0; }
        .instructions { margin: 0; color: #c7d3e0; font-family: "Cascadia Mono", Consolas, monospace; font-size: 12.5px; line-height: 1.8; }
        .scores { display: flex; gap: 12px; margin-bottom: 18px; flex-wrap: wrap; }
        .score {
          flex: 1;
          min-width: 130px;
          background: var(--panel-2);
          border: 1px solid var(--border);
          border-radius: 8px;
          padding: 12px 14px;
        }
        .score span { display: block; font-size: 11px; letter-spacing: 1px; text-transform: uppercase; color: var(--muted); }
        .score strong { font-size: 24px; font-weight: 650; color: var(--good); }
        .bar { background: #0d1117; border-radius: 4px; height: 8px; width: 110px; overflow: hidden; }
        .bar span { display: block; height: 100%; background: linear-gradient(90deg, #2f6fd0, var(--accent)); border-radius: 4px; }
        .hint { color: var(--muted); font-size: 12px; margin: 12px 0 0; }
        ul.diag { margin: 0; padding-left: 18px; color: var(--muted); font-size: 12.5px; }
        footer {
          max-width: 1180px;
          margin: 28px auto 0;
          padding: 18px 32px 0;
          border-top: 1px solid var(--border);
          color: var(--muted);
          font-size: 12px;
        }
        @media (max-width: 720px) {
          main { columns: 1; padding: 0 18px; }
          .wide { padding: 0 18px; }
          .hero { padding: 28px 18px 20px; }
          h1 { font-size: 26px; }
        }
        """;
}

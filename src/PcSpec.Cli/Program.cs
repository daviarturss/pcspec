using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PcSpec.Core;
using PcSpec.Core.Bench;
using PcSpec.Core.Model;
using PcSpec.Core.Report;
using PcSpec.Core.Smbios;

Console.OutputEncoding = Encoding.UTF8;

var options = CliOptions.Parse(args);

if (options.ShowHelp)
{
    CliOptions.PrintHelp();
    return 0;
}

var benchmarkOptions = options.Command == "bench" || options.Bench
    ? new BenchmarkOptions
    {
        Runs = options.Runs,
        DurationMs = options.DurationMs,
        Threads = options.Threads,
        PinThreads = options.PinThreads,
    }
    : null;

var spec = MachineCollector.Collect(benchmarkOptions);

return options.Command switch
{
    "info" => PrintText(TextReport.Build(spec)),
    "system" => PrintText(TextReport.Build(spec, ReportSections.System | ReportSections.Bios | ReportSections.Diagnostics)),
    "cpu" => PrintText(TextReport.Build(spec, ReportSections.Cpu | ReportSections.Caches | ReportSections.Diagnostics)),
    "board" => PrintText(TextReport.Build(spec, ReportSections.Bios | ReportSections.Board | ReportSections.Diagnostics)),
    "memory" => PrintText(TextReport.Build(spec, ReportSections.Memory | ReportSections.Diagnostics)),
    "gpu" => PrintText(TextReport.Build(spec, ReportSections.Graphics | ReportSections.Diagnostics)),
    "bench" => PrintText(TextReport.Build(spec, ReportSections.Cpu | ReportSections.Benchmark | ReportSections.Diagnostics)),
    "report" => WriteReport(spec, options),
    "json" => WriteJson(spec, options),
    "smbios" => DumpSmbios(options),
    _ => Unknown(options.Command),
};

static int PrintText(string text)
{
    Console.Write(text);
    return 0;
}

static int WriteReport(MachineSpec spec, CliOptions options)
{
    var format = options.Format ?? ReportFormat.Text;
    var path = options.Output ?? ReportWriter.DefaultFileName(spec, format);
    var full = ReportWriter.Save(spec, path, format);

    Console.WriteLine($"Relatório gravado em {full}");
    Console.WriteLine($"Tamanho: {Format.Num(new FileInfo(full).Length / 1024d, "0.0")} KB");

    if (options.Open)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(full) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Não foi possível abrir o arquivo: {ex.Message}");
        }
    }

    return 0;
}

static int WriteJson(MachineSpec spec, CliOptions options)
{
    var json = JsonSerializer.Serialize(spec, JsonOptions.Value);

    if (options.Output is null)
    {
        Console.Write(json);
        return 0;
    }

    var full = Path.GetFullPath(options.Output);
    var directory = Path.GetDirectoryName(full);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(full, json, new UTF8Encoding(false));
    Console.WriteLine($"JSON gravado em {full}");
    return 0;
}

static int DumpSmbios(CliOptions options)
{
    var table = SmbiosTable.Read();
    if (table is null)
    {
        Console.Error.WriteLine("SMBIOS indisponível nesta máquina.");
        return 1;
    }

    Console.WriteLine($"SMBIOS {table.Version} (DMI revision {table.DmiRevision}) — {table.Structures.Count} estruturas");
    Console.WriteLine();

    foreach (var structure in table.Structures)
    {
        Console.WriteLine($"[{structure.Type,3}] handle=0x{structure.Handle:X4} tamanho={structure.Length}");

        if (options.Raw)
        {
            Console.WriteLine($"      bytes: {Convert.ToHexString(structure.Raw)}");
        }

        for (var i = 0; i < structure.Strings.Count; i++)
        {
            Console.WriteLine($"      string[{i + 1}] = \"{structure.Strings[i]}\"");
        }
    }

    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Comando desconhecido: {command}");
    Console.Error.WriteLine();
    CliOptions.PrintHelp();
    return 2;
}

internal sealed record CliOptions
{
    public string Command { get; init; } = "info";
    public bool ShowHelp { get; init; }
    public string? Output { get; init; }
    public ReportFormat? Format { get; init; }
    public bool Open { get; init; }
    public bool Raw { get; init; }
    public bool Bench { get; init; }
    public int Runs { get; init; } = 3;
    public int DurationMs { get; init; } = 400;
    public int? Threads { get; init; }
    public bool PinThreads { get; init; } = true;

    private static readonly string[] KnownCommands =
        ["info", "system", "cpu", "board", "memory", "gpu", "bench", "report", "json", "smbios"];

    public static CliOptions Parse(string[] args)
    {
        var command = "info";
        var showHelp = false;
        string? output = null;
        ReportFormat? format = null;
        var open = false;
        var raw = false;
        var bench = false;
        var runs = 3;
        var duration = 400;
        int? threads = null;
        var pin = true;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h" or "--help" or "help":
                    showHelp = true;
                    break;

                case "--out" or "-o" when i + 1 < args.Length:
                    output = args[++i];
                    break;

                case "--format" or "-f" when i + 1 < args.Length:
                    format = args[++i].ToLowerInvariant() switch
                    {
                        "html" or "htm" => ReportFormat.Html,
                        "txt" or "text" => ReportFormat.Text,
                        _ => format,
                    };
                    break;

                case "--open":
                    open = true;
                    break;

                case "--raw":
                    raw = true;
                    break;

                case "--bench":
                    bench = true;
                    break;

                case "--runs" when i + 1 < args.Length:
                    runs = ParseInt(args[++i], runs);
                    break;

                case "--duration" when i + 1 < args.Length:
                    duration = ParseInt(args[++i], duration);
                    break;

                case "--threads" or "-t" when i + 1 < args.Length:
                    threads = ParseInt(args[++i], Environment.ProcessorCount);
                    break;

                case "--no-pin":
                    pin = false;
                    break;

                default:
                    if (!arg.StartsWith('-'))
                    {
                        command = arg.ToLowerInvariant();
                    }

                    break;
            }
        }

        if (output is not null && format is null && command == "report")
        {
            format = ReportWriter.InferFormat(output);
        }

        return new CliOptions
        {
            Command = command,
            ShowHelp = showHelp,
            Output = output,
            Format = format,
            Open = open,
            Raw = raw,
            Bench = bench,
            Runs = Math.Clamp(runs, 1, 20),
            DurationMs = Math.Clamp(duration, 50, 10_000),
            Threads = threads,
            PinThreads = pin,
        };
    }

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    public static void PrintHelp()
    {
        Console.WriteLine("""
            pcspec — especificações de hardware do PC

            Uso:
              pcspec <comando> [opções]

            Comandos:
              info            Relatório completo em texto (padrão)
              system          Sistema operacional, chassi e memória total
              cpu             Processador: nome, família, caches, instruções, clocks
              board           Placa-mãe, BIOS, chipset e pontes PCI
              memory          Módulos de memória (SMBIOS)
              gpu             Adaptadores de vídeo
              bench           Benchmark single/multi-thread
              report          Gera relatório em arquivo (TXT ou HTML)
              json            Despeja tudo em JSON
              smbios          Lista as estruturas SMBIOS lidas do firmware
              help            Esta ajuda

            Opções:
              -o, --out <arquivo>      Caminho de saída (report/json)
              -f, --format <txt|html>  Formato do relatório
                  --open               Abre o relatório ao terminar
                  --raw                Inclui bytes crus no comando smbios
                  --bench              Inclui o benchmark em report/json
                  --runs <n>           Repetições do benchmark (padrão 3)
                  --duration <ms>      Duração por subteste (padrão 400)
              -t, --threads <n>        Threads no benchmark multi (padrão: todos)
                  --no-pin             Não fixa threads em núcleos

            Exemplos:
              pcspec
              pcspec cpu
              pcspec bench --runs 5 --duration 800
              pcspec report --format html --out relatório.html --open
              pcspec json -o machine.json
            """);
    }
}

internal static partial class JsonOptions
{
    public static readonly JsonSerializerOptions Value = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

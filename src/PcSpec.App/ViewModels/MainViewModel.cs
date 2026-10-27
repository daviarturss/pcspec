using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using PcSpec.Core;
using PcSpec.Core.Bench;
using PcSpec.Core.Model;
using PcSpec.Core.Report;

namespace PcSpec.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private MachineSpec? _spec;
    private bool _isBusy;
    private string _status = "Iniciando...";
    private string _machineName = "—";
    private string _osSummary = "—";
    private string _cpuSummary = "—";
    private string _memorySummary = "—";
    private string _boardSummary = "—";
    private string _gpuSummary = "—";
    private string _instructions = "—";
    private string _reportText = string.Empty;
    private string _singleScoreText = "—";
    private string _multiScoreText = "—";
    private string _scalingText = "—";
    private string _benchSummary = "Nenhuma medição nesta sessao. Use o botao Benchmark.";
    private bool _hasBenchmark;
    private bool _hasCpuNotes;
    private bool _hasDiagnostics;

    public MainViewModel()
    {
        RefreshCommand = new AsyncCommand(RefreshAsync, () => !IsBusy);
        BenchmarkCommand = new AsyncCommand(RunBenchmarkAsync, () => !IsBusy && _spec is not null);
        SaveTextCommand = new RelayCommand(() => Save(ReportFormat.Text), () => _spec is not null);
        SaveHtmlCommand = new RelayCommand(() => Save(ReportFormat.Html), () => _spec is not null);
        CopyReportCommand = new RelayCommand(CopyReport, () => _spec is not null && _reportText.Length > 0);

        _ = RefreshAsync();
    }

    public ObservableCollection<SpecRow> SystemRows { get; } = [];

    public ObservableCollection<SpecRow> BiosRows { get; } = [];

    public ObservableCollection<SpecRow> CpuRows { get; } = [];

    public ObservableCollection<SpecRow> CacheRows { get; } = [];

    public ObservableCollection<SpecRow> BoardRows { get; } = [];

    public ObservableCollection<SpecRow> BridgeRows { get; } = [];

    public ObservableCollection<SpecRow> MemoryRows { get; } = [];

    public ObservableCollection<SpecGroup> MemoryModules { get; } = [];

    public ObservableCollection<SpecGroup> GpuGroups { get; } = [];

    public ObservableCollection<BenchRow> BenchRows { get; } = [];

    public ObservableCollection<string> CpuNotes { get; } = [];

    public ObservableCollection<string> BenchNotes { get; } = [];

    public ObservableCollection<string> Diagnostics { get; } = [];

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand BenchmarkCommand { get; }

    public RelayCommand SaveTextCommand { get; }

    public RelayCommand SaveHtmlCommand { get; }

    public RelayCommand CopyReportCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(BusyText));
                RaiseCanExecuteChanged();
            }
        }
    }

    public string BusyText => IsBusy ? "trabalhando..." : string.Empty;

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string MachineName
    {
        get => _machineName;
        private set => SetProperty(ref _machineName, value);
    }

    public string OsSummary
    {
        get => _osSummary;
        private set => SetProperty(ref _osSummary, value);
    }

    public string CpuSummary
    {
        get => _cpuSummary;
        private set => SetProperty(ref _cpuSummary, value);
    }

    public string MemorySummary
    {
        get => _memorySummary;
        private set => SetProperty(ref _memorySummary, value);
    }

    public string BoardSummary
    {
        get => _boardSummary;
        private set => SetProperty(ref _boardSummary, value);
    }

    public string GpuSummary
    {
        get => _gpuSummary;
        private set => SetProperty(ref _gpuSummary, value);
    }

    public string Instructions
    {
        get => _instructions;
        private set => SetProperty(ref _instructions, value);
    }

    public string ReportText
    {
        get => _reportText;
        private set => SetProperty(ref _reportText, value);
    }

    public string SingleScoreText
    {
        get => _singleScoreText;
        private set => SetProperty(ref _singleScoreText, value);
    }

    public string MultiScoreText
    {
        get => _multiScoreText;
        private set => SetProperty(ref _multiScoreText, value);
    }

    public string ScalingText
    {
        get => _scalingText;
        private set => SetProperty(ref _scalingText, value);
    }

    public string BenchSummary
    {
        get => _benchSummary;
        private set => SetProperty(ref _benchSummary, value);
    }

    public bool HasBenchmark
    {
        get => _hasBenchmark;
        private set => SetProperty(ref _hasBenchmark, value);
    }

    public bool HasCpuNotes
    {
        get => _hasCpuNotes;
        private set => SetProperty(ref _hasCpuNotes, value);
    }

    public bool HasDiagnostics
    {
        get => _hasDiagnostics;
        private set => SetProperty(ref _hasDiagnostics, value);
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        Status = "Coletando especificações (CPUID, SMBIOS, WMI)...";

        try
        {
            var spec = await Task.Run(() => MachineCollector.Collect());
            Apply(spec);

            Status = spec.Diagnostics.Count == 0
                ? $"Coletado em {spec.CollectedAt:HH:mm:ss}"
                : $"Coletado em {spec.CollectedAt:HH:mm:ss} — {spec.Diagnostics.Count} aviso(s)";
        }
        catch (Exception ex)
        {
            Status = $"Falha na coleta: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunBenchmarkAsync()
    {
        if (_spec is null)
        {
            return;
        }

        IsBusy = true;
        Status = "Executando benchmark single e multi-thread...";

        try
        {
            var options = new BenchmarkOptions();
            var cpuName = _spec.Cpu.Name;
            var result = await Task.Run(() => BenchmarkRunner.Run(options, cpuName));

            Apply(_spec with { Benchmark = result });
            Status = $"Benchmark concluido: 1T {result.SingleThreadScore:0} / NT {result.MultiThreadScore:0} (x{result.Scaling:0.00})";
        }
        catch (Exception ex)
        {
            Status = $"Falha no benchmark: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(MachineSpec spec)
    {
        _spec = spec;

        MachineName = Format.Or(spec.System.MachineName);
        OsSummary = Join(spec.System.OsName, spec.System.OsVersion);
        CpuSummary = Format.Or(spec.Cpu.Name);
        MemorySummary = spec.Memory.TotalBytes > 0
            ? Format.Bytes(spec.Memory.TotalBytes) +
              (spec.Memory.SpeedMts > 0 ? $" · {spec.Memory.SpeedMts} MT/s" : string.Empty)
            : "—";
        BoardSummary = Join(spec.Board.Manufacturer, spec.Board.Product);
        GpuSummary = spec.Gpus.FirstOrDefault()?.Name ?? "—";

        Fill(SystemRows,
        [
            ("Fabricante", spec.System.Manufacturer),
            ("Modelo", spec.System.Model),
            ("Família", spec.System.Family),
            ("SKU", spec.System.Sku),
            ("Número de série", spec.System.SerialNumber),
            ("UUID", spec.System.Uuid),
            ("Tipo de chassi", spec.System.ChassisType),
            ("Nome da máquina", spec.System.MachineName),
            ("Sistema operacional", spec.System.OsName),
            ("Versão", spec.System.OsVersion),
            ("Arquitetura", spec.System.OsArchitecture),
            ("Instalação", spec.System.OsInstallDate),
            ("Memória total", Format.Bytes(spec.System.TotalPhysicalMemoryBytes)),
            ("Modo de boot", spec.System.BootMode),
            ("Secure Boot", spec.System.SecureBoot),
        ]);

        Fill(BiosRows,
        [
            ("Fornecedor", spec.Bios.Vendor),
            ("Versão", spec.Bios.Version),
            ("Data", spec.Bios.ReleaseDate),
            ("Versão SMBIOS", spec.Bios.SmbiosVersion),
            ("Tipo de firmware", spec.Bios.FirmwareType),
            ("Tamanho da ROM", spec.Bios.RomSizeBytes > 0 ? Format.Bytes(spec.Bios.RomSizeBytes) : null),
            ("BIOS do sistema", spec.Bios.MajorRelease is null ? null : $"{spec.Bios.MajorRelease}.{spec.Bios.MinorRelease ?? "?"}"),
        ]);

        Fill(CpuRows,
        [
            ("Nome", spec.Cpu.Name),
            ("Codename", spec.Cpu.Codename),
            ("Socket", spec.Cpu.Socket),
            ("Tecnologia", spec.Cpu.Technology),
            ("Família", spec.Cpu.Family),
            ("Modelo", spec.Cpu.Model),
            ("Stepping", spec.Cpu.Stepping),
            ("Ext. família", spec.Cpu.ExtFamily),
            ("Ext. modelo", spec.Cpu.ExtModel),
            ("Núcleos", spec.Cpu.Cores > 0 ? spec.Cpu.Cores.ToString() : null),
            ("Threads", spec.Cpu.Threads > 0 ? spec.Cpu.Threads.ToString() : null),
            ("Soquetes", spec.Cpu.Packages > 0 ? spec.Cpu.Packages.ToString() : null),
            ("Core speed", Format.Mhz(spec.Cpu.CoreSpeedMhz)),
            ("Max clock", Format.Mhz(spec.Cpu.MaxClockMhz)),
            ("Base clock", Format.Mhz(spec.Cpu.BaseClockMhz)),
            ("Bus speed", Format.Mhz(spec.Cpu.BusSpeedMhz)),
            ("Multiplicador", spec.Cpu.Multiplier is > 0 ? $"x {Format.Num(spec.Cpu.Multiplier.Value, "0.#")}" : null),
            ("Tensão do núcleo", spec.Cpu.CoreVoltage is > 0 ? $"{Format.Num(spec.Cpu.CoreVoltage.Value, "0.000")} V" : null),
        ]);

        Instructions = string.IsNullOrWhiteSpace(spec.Cpu.Instructions) ? "—" : spec.Cpu.Instructions;

        Fill(CacheRows, spec.Cpu.Caches.Select(cache =>
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
                details.Add($"compartilhado por {cache.Sharing} threads");
            }

            return (cache.Display, (string?)string.Join(" · ", details));
        }));

        Fill(BoardRows,
        [
            ("Fabricante", spec.Board.Manufacturer),
            ("Modelo", spec.Board.Product),
            ("Versão", spec.Board.Version),
            ("Número de série", spec.Board.SerialNumber),
            ("Asset tag", spec.Board.AssetTag),
            ("Tipo", spec.Board.BoardType),
            ("Chipset", spec.Board.Chipset),
            ("Southbridge", spec.Board.Southbridge),
        ]);

        Fill(BridgeRows, spec.Board.Bridges.Select(bridge =>
            ($"{bridge.VendorId}:{bridge.DeviceId}", (string?)$"{bridge.Class} - {bridge.Name ?? "(não catalogada)"}")));

        Fill(MemoryRows,
        [
            ("Total", Format.Bytes(spec.Memory.TotalBytes)),
            ("Capacidade máxima", spec.Memory.MaxCapacityBytes > 0 ? Format.Bytes(spec.Memory.MaxCapacityBytes) : null),
            ("Slots", spec.Memory.SlotCount > 0 ? $"{spec.Memory.PopulatedSlots} de {spec.Memory.SlotCount} ocupados" : null),
            ("Canais", spec.Memory.Channels > 0 ? spec.Memory.Channels.ToString() : null),
            ("Tipo", spec.Memory.Type),
            ("Velocidade", spec.Memory.SpeedMts > 0 ? $"{spec.Memory.SpeedMts} MT/s ({Format.Num(spec.Memory.SpeedMts / 2d, "0")} MHz)" : null),
            ("Formato", spec.Memory.FormFactor),
            ("ECC", spec.Memory.Ecc ? "Sim" : "Não"),
        ]);

        MemoryModules.Clear();
        foreach (var module in spec.Memory.Modules)
        {
            var speed = module.ConfiguredSpeedMts > 0 ? module.ConfiguredSpeedMts : module.SpeedMts;
            var title = string.Join(" · ", new[]
            {
                module.Locator ?? "Módulo",
                string.IsNullOrWhiteSpace(module.Channel) ? null : $"canal {module.Channel}",
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

            MemoryModules.Add(new SpecGroup(title,
            [
                new SpecRow("Banco", Format.Or(module.BankLocator)),
                new SpecRow("Tamanho", Format.Bytes(module.SizeBytes)),
                new SpecRow("Tipo", Format.Or(module.Type)),
                new SpecRow("Formato", Format.Or(module.FormFactor)),
                new SpecRow("Velocidade", speed > 0 ? $"{speed} MT/s" : "—"),
                new SpecRow("Fabricante", Format.Or(module.Manufacturer)),
                new SpecRow("Part number", Format.Or(module.PartNumber)),
                new SpecRow("Número de série", Format.Or(module.SerialNumber)),
                new SpecRow("Ranks", module.Rank > 0 ? module.Rank.ToString() : "—"),
                new SpecRow("Tensão", module.Voltage is > 0 ? $"{Format.Num(module.Voltage.Value, "0.000")} V" : "—"),
                new SpecRow("Largura", module.DataWidth > 0 ? $"{module.DataWidth} bits" : "—"),
            ]));
        }

        GpuGroups.Clear();
        foreach (var gpu in spec.Gpus)
        {
            GpuGroups.Add(new SpecGroup(gpu.Name ?? "Adaptador de vídeo",
            [
                new SpecRow("Fabricante", Format.Or(gpu.Vendor)),
                new SpecRow("IDs PCI", gpu.VendorId is null ? "—" : $"{gpu.VendorId}:{gpu.DeviceId}"),
                new SpecRow("Processador", Format.Or(gpu.Processor)),
                new SpecRow("Memória", gpu.MemoryBytes > 0 ? Format.Bytes(gpu.MemoryBytes) : "—"),
                new SpecRow("Barramento", gpu.MemoryBusBits is > 0 ? $"{gpu.MemoryBusBits} bits" : "—"),
                new SpecRow("Driver", Format.Or(gpu.DriverVersion)),
                new SpecRow("Data do driver", Format.Or(gpu.DriverDate)),
                new SpecRow("Resolução", Format.Or(gpu.Resolution)),
                new SpecRow("Taxa de atualização", gpu.RefreshRate is > 0 ? $"{gpu.RefreshRate} Hz" : "—"),
                new SpecRow("Status", Format.Or(gpu.Status)),
            ]));
        }

        CpuNotes.Clear();
        foreach (var note in spec.Cpu.Notes)
        {
            CpuNotes.Add(note);
        }

        Diagnostics.Clear();
        foreach (var item in spec.Diagnostics)
        {
            Diagnostics.Add(item);
        }

        ApplyBenchmark(spec.Benchmark);

        ReportText = TextReport.Build(spec);
        HasCpuNotes = CpuNotes.Count > 0;
        HasDiagnostics = Diagnostics.Count > 0;

        RaiseCanExecuteChanged();
    }

    private void ApplyBenchmark(BenchmarkResult? bench)
    {
        BenchRows.Clear();
        BenchNotes.Clear();
        HasBenchmark = bench is not null;

        if (bench is null)
        {
            SingleScoreText = "—";
            MultiScoreText = "—";
            ScalingText = "—";
            BenchSummary = "Nenhuma medição nesta sessao. Use o botao Benchmark.";
            return;
        }

        SingleScoreText = Format.Num(bench.SingleThreadScore, "0");
        MultiScoreText = Format.Num(bench.MultiThreadScore, "0");
        ScalingText = $"x {Format.Num(bench.Scaling, "0.00")}";

        var maxScore = bench.Multi.Count == 0 ? 1 : Math.Max(1, bench.Multi.Max(m => m.Score));

        foreach (var single in bench.Single)
        {
            var multi = bench.Multi.FirstOrDefault(m => m.Name == single.Name);
            BenchRows.Add(new BenchRow(
                single.Name,
                single.DisplayValue,
                multi?.DisplayValue ?? "—",
                Format.Num(single.Score, "0"),
                multi is null ? "—" : Format.Num(multi.Score, "0"),
                Math.Clamp((multi?.Score ?? 0) / maxScore * 100, 0, 100)));
        }

        foreach (var note in bench.Notes)
        {
            BenchNotes.Add(note);
        }

        BenchSummary = $"{bench.Threads} threads · {bench.Runs} repetições · {bench.DurationMs} ms por teste. " +
                       "O score do PcSpec não é comparável ao do CPU-Z.";
    }

    private void Save(ReportFormat format)
    {
        if (_spec is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Salvar relatório",
            FileName = ReportWriter.DefaultFileName(_spec, format),
            Filter = format == ReportFormat.Html ? "Pagina HTML (*.html)|*.html" : "Texto (*.txt)|*.txt",
            DefaultExt = format == ReportFormat.Html ? ".html" : ".txt",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var path = ReportWriter.Save(_spec, dialog.FileName, format);
            Status = $"Relatório salvo em {path}";
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = $"Não foi possível salvar: {ex.Message}";
        }
    }

    private void CopyReport()
    {
        try
        {
            Clipboard.SetText(ReportText);
            Status = "Relatório copiado para a área de transferência.";
        }
        catch (Exception ex)
        {
            Status = $"Não foi possível copiar: {ex.Message}";
        }
    }

    private static void Fill(ObservableCollection<SpecRow> target, IEnumerable<(string Label, string? Value)> rows)
    {
        target.Clear();
        foreach (var (label, value) in rows)
        {
            target.Add(new SpecRow(label, Format.Or(value)));
        }
    }

    private static string Join(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return text.Length == 0 ? "—" : text;
    }

    private void RaiseCanExecuteChanged()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        BenchmarkCommand.RaiseCanExecuteChanged();
        SaveTextCommand.RaiseCanExecuteChanged();
        SaveHtmlCommand.RaiseCanExecuteChanged();
        CopyReportCommand.RaiseCanExecuteChanged();
    }
}

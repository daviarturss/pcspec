namespace PcSpec.Core.Model;

public sealed record SystemSpec
{
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? Family { get; init; }
    public string? Sku { get; init; }
    public string? SerialNumber { get; init; }
    public string? Uuid { get; init; }
    public string? ChassisType { get; init; }
    public string? MachineName { get; init; }
    public string? OsName { get; init; }
    public string? OsVersion { get; init; }
    public string? OsBuild { get; init; }
    public string? OsArchitecture { get; init; }
    public string? OsInstallDate { get; init; }
    public long TotalPhysicalMemoryBytes { get; init; }
    public string? BootMode { get; init; }
    public string? SecureBoot { get; init; }
}

public sealed record BiosSpec
{
    public string? Vendor { get; init; }
    public string? Version { get; init; }
    public string? ReleaseDate { get; init; }
    public string? SmbiosVersion { get; init; }
    public string? FirmwareType { get; init; }
    public long RomSizeBytes { get; init; }
    public string? MajorRelease { get; init; }
    public string? MinorRelease { get; init; }
}

public sealed record BoardSpec
{
    public string? Manufacturer { get; init; }
    public string? Product { get; init; }
    public string? Version { get; init; }
    public string? SerialNumber { get; init; }
    public string? AssetTag { get; init; }
    public string? BoardType { get; init; }
    public string? ChipsetVendor { get; init; }
    public string? Chipset { get; init; }
    public string? Southbridge { get; init; }
    public IReadOnlyList<PciBridge> Bridges { get; init; } = [];
}

/// <summary>Ponte PCI identificada por vendor/device ID, com nome amigavel quando conhecido.</summary>
public sealed record PciBridge(string VendorId, string DeviceId, string Class, string? Name);

public sealed record CacheSpec
{
    public required string Level { get; init; }
    public required string Type { get; init; }
    public long SizeBytes { get; init; }
    public int Ways { get; init; }
    public int LineSize { get; init; }
    public int Sharing { get; init; }

    public string Display => $"{Level} {Type}".Trim();
}

public sealed record CpuSpec
{
    public string? Name { get; init; }
    public string? Vendor { get; init; }
    public string? VendorName { get; init; }
    public string? Codename { get; init; }
    public string? Socket { get; init; }
    public string? Package { get; init; }
    public string? Technology { get; init; }
    public string? Family { get; init; }
    public string? Model { get; init; }
    public string? Stepping { get; init; }
    public string? ExtFamily { get; init; }
    public string? ExtModel { get; init; }
    public int Cores { get; init; }
    public int Threads { get; init; }
    public int Packages { get; init; }
    public double? CoreSpeedMhz { get; init; }
    public double? MinSpeedMhz { get; init; }
    public double? MaxSpeedMhz { get; init; }
    public double? MaxClockMhz { get; init; }
    public double? BaseClockMhz { get; init; }
    public double? BusSpeedMhz { get; init; }
    public double? Multiplier { get; init; }
    public double? CoreVoltage { get; init; }
    public string? Instructions { get; init; }
    public IReadOnlyList<CacheSpec> Caches { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record MemoryModuleSpec
{
    public string? Locator { get; init; }
    public string? BankLocator { get; init; }
    public string? Channel { get; init; }
    public string? Manufacturer { get; init; }
    public string? PartNumber { get; init; }
    public string? SerialNumber { get; init; }
    public long SizeBytes { get; init; }
    public string? Type { get; init; }
    public string? FormFactor { get; init; }
    public int Rank { get; init; }
    public int SpeedMts { get; init; }
    public int ConfiguredSpeedMts { get; init; }
    public double? Voltage { get; init; }
    public int DataWidth { get; init; }
    public int TotalWidth { get; init; }
    public bool Ecc => TotalWidth > DataWidth && DataWidth > 0;
}

public sealed record MemorySpec
{
    public long TotalBytes { get; init; }
    public long MaxCapacityBytes { get; init; }
    public int SlotCount { get; init; }
    public int PopulatedSlots { get; init; }
    public int Channels { get; init; }
    public string? Type { get; init; }
    public int SpeedMts { get; init; }
    public string? FormFactor { get; init; }
    public bool Ecc { get; init; }
    public IReadOnlyList<MemoryModuleSpec> Modules { get; init; } = [];
    public string? Note { get; init; }
}

public sealed record GpuSpec
{
    public string? Name { get; init; }
    public string? Vendor { get; init; }
    public string? VendorId { get; init; }
    public string? DeviceId { get; init; }
    public string? Processor { get; init; }
    public long MemoryBytes { get; init; }
    public long? MemoryBusBits { get; init; }
    public string? DriverVersion { get; init; }
    public string? DriverDate { get; init; }
    public string? Resolution { get; init; }
    public int? RefreshRate { get; init; }
    public string? PnpId { get; init; }
    public string? Status { get; init; }
}

public sealed record MachineSpec
{
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.Now;
    public SystemSpec System { get; init; } = new();
    public BiosSpec Bios { get; init; } = new();
    public BoardSpec Board { get; init; } = new();
    public CpuSpec Cpu { get; init; } = new();
    public MemorySpec Memory { get; init; } = new();
    public IReadOnlyList<GpuSpec> Gpus { get; init; } = [];
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    public BenchmarkResult? Benchmark { get; init; }
}

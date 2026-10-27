using Microsoft.Win32;
using PcSpec.Core.Model;
using PcSpec.Core.Smbios;
using PcSpec.Core.Platform;

namespace PcSpec.Core.Cpu;

/// <summary>
/// Monta a aba CPU. Fonte primaria: instrução CPUID. Complementos:
/// <c>GetLogicalProcessorInformationEx</c> (topologia/caches),
/// <c>CallNtPowerInformation</c> (frequência corrente), SMBIOS tipo 4 (socket/tensão)
/// e WMI (frequência nominal de fabrica).
/// </summary>
public static class CpuInfoProvider
{
    private const string ProcessorKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

    public static CpuSpec Collect(SmbiosTable? smbios, ICollection<string> diagnostics)
    {
        var notes = new List<string>();
        var cpu = smbios?.First(4);

        if (!Cpuid.IsSupported)
        {
            notes.Add("CPUID indisponível: máquina não é x86/x64, usando apenas WMI/registro.");
            return CollectWithoutCpuid(cpu, diagnostics, notes);
        }

        var leaf0 = Cpuid.Call(0);
        var vendor = Cpuid.Vendor;
        var leaf1 = Cpuid.Call(1);

        var signature = Decode(leaf1.Eax);
        var (codename, technology) = CpuCatalog.Lookup(vendor, signature.Family, signature.Model);

        var brand = Format.Clean(Cpuid.BrandString);
        if (string.IsNullOrWhiteSpace(brand))
        {
            brand = RegistryReader.GetString(Registry.LocalMachine, ProcessorKey, "ProcessorNameString");
            notes.Add("Brand string do CPUID vazia; nome veio do registro.");
        }

        if (string.IsNullOrWhiteSpace(brand))
        {
            brand = Wmi.Text(Wmi.Query("SELECT Name FROM Win32_Processor", "Name").FirstOrDefault() ?? [], "Name");
        }

        var (cores, threads, packages) = Topology(notes);
        var caches = ReadCaches(notes);
        var (coreSpeed, minSpeed, maxSpeed) = ReadClocks(notes);
        var (baseClock, maxClock, busFromCpuid) = ReadNominalClocks(cpu, notes);

        var bus = busFromCpuid;
        double? multiplier = null;
        if (coreSpeed is > 0)
        {
            if (bus is > 0)
            {
                multiplier = Math.Round(coreSpeed.Value / bus.Value, 1);
            }
            else
            {
                // Sem folha 0x16: assume BCLK de 100 MHz (padrão em AM4/LGA1700) e
                // deriva o multiplicador. Marcado como inferência no relatório.
                var assumed = 100d;
                multiplier = Math.Round(coreSpeed.Value / assumed, 1);
                bus = multiplier is > 0 ? Math.Round(coreSpeed.Value / multiplier.Value, 2) : assumed;
                notes.Add("Bus speed derivada de 100 MHz (folha CPUID 0x16 ausente).");
            }
        }

        var socket = Format.Clean(cpu?.String(0x04))
                     ?? Wmi.Text(Wmi.Query("SELECT SocketDesignation FROM Win32_Processor", "SocketDesignation").FirstOrDefault() ?? [], "SocketDesignation");

        var voltage = ReadVoltage(cpu);
        if (voltage is null)
        {
            notes.Add("Tensão do núcleo não exposta pelo SMBIOS/WMI (exigiria MSR via driver).");
        }

        return new CpuSpec
        {
            Name = brand,
            Vendor = vendor,
            VendorName = VendorName(vendor),
            Codename = codename,
            Socket = socket,
            Technology = technology,
            Family = $"{signature.Family:X}h ({signature.Family})",
            Model = $"{signature.Model:X}h ({signature.Model})",
            Stepping = signature.Stepping.ToString(),
            ExtFamily = $"{signature.ExtFamily:X}h",
            ExtModel = $"{signature.ExtModel:X}h",
            Cores = cores,
            Threads = threads,
            Packages = packages,
            CoreSpeedMhz = coreSpeed,
            MinSpeedMhz = minSpeed,
            MaxSpeedMhz = maxSpeed,
            MaxClockMhz = maxClock,
            BaseClockMhz = baseClock,
            BusSpeedMhz = bus,
            Multiplier = multiplier,
            CoreVoltage = voltage,
            Instructions = InstructionFlags.Describe(),
            Caches = caches,
            Notes = notes,
        };
    }

    private static CpuSpec CollectWithoutCpuid(SmbiosStructure? cpu, ICollection<string> diagnostics, List<string> notes)
    {
        var row = Wmi.Query(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, CurrentClockSpeed, SocketDesignation FROM Win32_Processor",
            "Name", "NumberOfCores", "NumberOfLogicalProcessors", "MaxClockSpeed", "CurrentClockSpeed", "SocketDesignation").FirstOrDefault() ?? [];

        var cores = (int)Wmi.Number(row, "NumberOfCores");
        var threads = (int)Wmi.Number(row, "NumberOfLogicalProcessors");

        return new CpuSpec
        {
            Name = Wmi.Text(row, "Name") ?? Format.Clean(cpu?.String(0x10)),
            Vendor = Format.Clean(cpu?.String(0x07)),
            VendorName = SmbiosEnums.ProcessorFamily(cpu?.Word(0x06) ?? 0),
            Cores = cores > 0 ? cores : ProcessorTopology.PhysicalCores,
            Threads = threads > 0 ? threads : ProcessorTopology.LogicalProcessors,
            Packages = ProcessorTopology.Packages,
            MaxClockMhz = Wmi.Decimal(row, "MaxClockSpeed"),
            CoreSpeedMhz = Wmi.Decimal(row, "CurrentClockSpeed"),
            BaseClockMhz = RegistryReader.GetInt(Registry.LocalMachine, ProcessorKey, "~MHz"),
            Socket = Wmi.Text(row, "SocketDesignation"),
            Caches = ReadCaches(notes),
            Notes = notes,
        };
    }

    private static (int Cores, int Threads, int Packages) Topology(List<string> notes)
    {
        var cores = ProcessorTopology.PhysicalCores;
        var threads = ProcessorTopology.LogicalProcessors;
        var packages = ProcessorTopology.Packages;

        if (cores <= 0 || cores > threads)
        {
            notes.Add($"Topologia inconsistente (cores={cores}, threads={threads}); usando WMI.");
            var row = Wmi.Query(
                "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor",
                "NumberOfCores", "NumberOfLogicalProcessors").FirstOrDefault() ?? [];
            var wmiCores = (int)Wmi.Number(row, "NumberOfCores");
            var wmiThreads = (int)Wmi.Number(row, "NumberOfLogicalProcessors");
            cores = wmiCores > 0 ? wmiCores : Math.Max(1, threads);
            threads = wmiThreads > 0 ? wmiThreads : threads;
        }

        var (perf, eff) = ProcessorTopology.CoreClasses;
        if (eff > 0)
        {
            notes.Add($"Topologia hibrida: {perf} P-cores e {eff} E-cores.");
        }

        return (cores, threads, packages);
    }

    private static IReadOnlyList<CacheSpec> ReadCaches(List<string> notes)
    {
        var caches = new List<CacheSpec>();
        var seen = new HashSet<(int, int, long)>();

        // A folha 4 (Intel) existe em AMD mas devolve tipo 0, porque la ela e
        // reservada: o Zen+ expõe os caches em 0x8000001D. Por isso a ordem
        // depende do fabricante, e não apenas de a folha existir.
        var isAmd = Cpuid.Vendor.Contains("AMD", StringComparison.OrdinalIgnoreCase);
        uint[] candidates = isAmd
            ? [0x8000_001D, 4]
            : [4, 0x8000_001D];

        foreach (var leaf in candidates)
        {
            if (caches.Count > 0)
            {
                break;
            }

            if (!Cpuid.HasLeaf(leaf))
            {
                continue;
            }

            for (uint sub = 0; sub < 16; sub++)
            {
                var entry = Cpuid.Call(leaf, sub);
                var type = (int)(entry.Eax & 0x1F);
                if (type == 0)
                {
                    break;
                }

                var level = (int)((entry.Eax >> 5) & 0x7);
                var sharing = (int)((entry.Eax >> 14) & 0xFFF) + 1;
                var lineSize = (int)(entry.Ebx & 0xFFF) + 1;
                var partitions = (int)((entry.Ebx >> 12) & 0x3FF) + 1;
                var ways = (int)((entry.Ebx >> 22) & 0x3FF) + 1;
                var sets = (long)entry.Ecx + 1;
                var size = ways * (long)partitions * lineSize * sets;

                if (size <= 0 || size > 1L << 40)
                {
                    continue;
                }

                if (!seen.Add((level, type, size)))
                {
                    continue;
                }

                caches.Add(new CacheSpec
                {
                    Level = $"L{level}",
                    Type = type switch { 1 => "Data", 2 => "Instruction", 3 => "Unified", _ => "Cache" },
                    SizeBytes = size,
                    Ways = ways,
                    LineSize = lineSize,
                    Sharing = sharing,
                });
            }
        }

        if (caches.Count == 0 && ProcessorTopology.Caches.Count > 0)
        {
            notes.Add("Caches lidos de GetLogicalProcessorInformationEx (folhas CPUID ausentes).");
            foreach (var cache in ProcessorTopology.Caches)
            {
                caches.Add(new CacheSpec
                {
                    Level = $"L{cache.Level}",
                    Type = cache.Type switch { 1 => "Data", 2 => "Instruction", 3 => "Unified", _ => "Cache" },
                    SizeBytes = cache.Size,
                    LineSize = cache.LineSize,
                    Sharing = 1,
                });
            }
        }

        return caches
            .OrderBy(c => c.Level)
            .ThenByDescending(c => c.SizeBytes)
            .ToList();
    }

    private static (double? Average, double? Min, double? Max) ReadClocks(List<string> notes)
    {
        var readings = ProcessorPower.Read();
        if (readings.Count > 0)
        {
            return (
                Math.Round(readings.Average(r => r.CurrentMhz), 0),
                readings.Min(r => r.CurrentMhz),
                readings.Max(r => r.CurrentMhz));
        }

        var row = Wmi.Query("SELECT CurrentClockSpeed FROM Win32_Processor", "CurrentClockSpeed").FirstOrDefault() ?? [];
        var current = Wmi.Decimal(row, "CurrentClockSpeed");
        if (current > 0)
        {
            notes.Add("Frequência corrente veio do WMI (CallNtPowerInformation indisponível).");
            return (current, current, current);
        }

        return (null, null, null);
    }

    private static (double? BaseClock, double? MaxClock, double? Bus) ReadNominalClocks(SmbiosStructure? cpu, List<string> notes)
    {
        double? baseClock = null;
        double? maxClock = null;
        double? bus = null;

        // Folha 0x16 (Processor Frequency Information): base, máximo e BCLK em MHz.
        if (Cpuid.HasLeaf(0x16))
        {
            var freq = Cpuid.Call(0x16);
            if (freq.Eax is > 100 and < 10_000)
            {
                baseClock = freq.Eax;
            }

            if (freq.Ebx is > 100 and < 10_000)
            {
                maxClock = freq.Ebx;
            }

            if (freq.Ecx is > 50 and < 1_000)
            {
                bus = freq.Ecx;
            }
        }

        if (baseClock is null)
        {
            var nominal = RegistryReader.GetInt(Registry.LocalMachine, ProcessorKey, "~MHz");
            if (nominal is > 100)
            {
                baseClock = nominal;
            }
        }

        if (baseClock is null && cpu is not null)
        {
            var smbiosSpeed = cpu.Word(0x16);
            if (smbiosSpeed is > 100)
            {
                baseClock = smbiosSpeed;
            }
        }

        if (maxClock is null)
        {
            var row = Wmi.Query("SELECT MaxClockSpeed FROM Win32_Processor", "MaxClockSpeed").FirstOrDefault() ?? [];
            var wmiMax = Wmi.Decimal(row, "MaxClockSpeed");
            if (wmiMax > 0)
            {
                maxClock = wmiMax;
            }
        }

        if (bus is null && cpu is not null)
        {
            var smbiosBus = cpu.Word(0x12);
            if (smbiosBus is > 50 and < 1_000)
            {
                bus = smbiosBus;
            }
        }

        return (baseClock, maxClock, bus);
    }

    private static double? ReadVoltage(SmbiosStructure? cpu)
    {
        if (cpu is not null)
        {
            var raw = cpu.Byte(0x11);
            if ((raw & 0x80) != 0)
            {
                var volts = (raw & 0x7F) / 10d;
                if (volts > 0.3 && volts < 3.0)
                {
                    return Math.Round(volts, 3);
                }
            }
        }

        var row = Wmi.Query("SELECT CurrentVoltage FROM Win32_Processor", "CurrentVoltage").FirstOrDefault() ?? [];
        var decivolts = Wmi.Decimal(row, "CurrentVoltage");
        if (decivolts is > 0 and < 0x80)
        {
            return Math.Round(decivolts / 10d, 3);
        }

        return null;
    }

    private static string? VendorName(string vendor) => vendor switch
    {
        "AuthenticAMD" => "Advanced Micro Devices (AMD)",
        "GenuineIntel" => "Intel Corporation",
        "CentaurHauls" => "Centaur / VIA",
        "HygonGenuine" => "Hygon",
        _ => vendor,
    };

    private readonly record struct Signature(int Family, int Model, int Stepping, int ExtFamily, int ExtModel);

    /// <summary>Decodifica o registrador EAX da folha 1 conforme o manual da Intel/AMD.</summary>
    private static Signature Decode(uint eax)
    {
        var stepping = (int)(eax & 0xF);
        var model = (int)((eax >> 4) & 0xF);
        var family = (int)((eax >> 8) & 0xF);
        var extModel = (int)((eax >> 16) & 0xF);
        var extFamily = (int)((eax >> 20) & 0xFF);

        var baseFamily = family == 0xF ? family + extFamily : family;
        var baseModel = family is 0x6 or 0xF ? (extModel << 4) | model : model;

        return new Signature(baseFamily, baseModel, stepping, extFamily, extModel);
    }
}

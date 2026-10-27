using PcSpec.Core.Model;
using PcSpec.Core.Smbios;
using PcSpec.Core.Platform;

namespace PcSpec.Core.Memory;

/// <summary>
/// Monta a aba Memory a partir do SMBIOS (tipos 16, 17 e 19).
/// SPD real (timings, XMP/EXPO) exige SMBus/driver e fica fora do escopo:
/// o que o firmware expõe e a velocidade configurada e o tipo do módulo.
/// </summary>
public static class MemoryInfoProvider
{
    public static MemorySpec Collect(SmbiosTable? smbios, ICollection<string> diagnostics)
    {
        if (smbios is null)
        {
            diagnostics.Add("SMBIOS indisponível: memória lida apenas via WMI.");
            return FromWmi();
        }

        var array = smbios.First(16);
        var devices = smbios.OfType(17).ToList();

        var modules = new List<MemoryModuleSpec>();
        foreach (var device in devices)
        {
            var sizeBytes = ModuleSize(device);
            if (sizeBytes <= 0)
            {
                // Slot vazio (size = 0) aparece como "No Module Installed".
                continue;
            }

            var locator = Format.Clean(device.String(0x10));
            var speed = device.Word(0x15);
            var configured = device.Has(0x20) ? device.Word(0x20) : (ushort)0;

            modules.Add(new MemoryModuleSpec
            {
                Locator = locator,
                BankLocator = Format.Clean(device.String(0x11)),
                Channel = InferChannel(locator, Format.Clean(device.String(0x11))),
                Manufacturer = Format.Clean(device.String(0x17)),
                SerialNumber = Format.Clean(device.String(0x18)),
                PartNumber = Format.Clean(device.String(0x1A)),
                SizeBytes = sizeBytes,
                Type = SmbiosEnums.MemoryType(device.Byte(0x12)),
                FormFactor = SmbiosEnums.FormFactor(device.Byte(0x0E)),
                // Attributes (0x1B), bits 0-3: número de ranks (0 = desconhecido).
                Rank = device.Byte(0x1B) & 0x0F,
                SpeedMts = speed,
                ConfiguredSpeedMts = configured,
                Voltage = ModuleVoltage(device),
                DataWidth = device.Word(0x0A),
                TotalWidth = device.Word(0x08),
            });
        }

        var populated = modules.Count;
        var slotCount = array?.Word(0x0D) ?? populated;
        if (slotCount < populated)
        {
            slotCount = populated;
        }

        var total = modules.Sum(m => m.SizeBytes);
        var maxCapacity = array is null ? 0 : (long)array.Dword(0x07) * 1024;

        var dominant = modules
            .GroupBy(m => m.Type ?? "?")
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var speedDominant = modules.Count == 0
            ? 0
            : modules.Max(m => m.ConfiguredSpeedMts > 0 ? m.ConfiguredSpeedMts : m.SpeedMts);

        var channels = modules
            .Select(m => m.Channel)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Count();

        var ecc = modules.Any(m => m.Ecc);

        var notes = new List<string>();
        if (modules.Count > 0 && modules.All(m => m.ConfiguredSpeedMts == 0))
        {
            notes.Add("Velocidade configurada não exposta pelo SMBIOS; usando a velocidade nominal do módulo.");
        }

        notes.Add("Timings (CL/tRCD/tRP/tRAS) e perfis XMP/EXPO exigem leitura SPD por SMBus, que precisa de driver de kernel.");

        if (array is not null && array.Byte(0x06) is 0x04 or 0x05 or 0x06)
        {
            notes.Add($"Correção de erro do array: 0x{array.Byte(0x06):X2}.");
        }

        return new MemorySpec
        {
            TotalBytes = total,
            MaxCapacityBytes = maxCapacity,
            SlotCount = slotCount,
            PopulatedSlots = populated,
            Channels = channels,
            Type = dominant,
            SpeedMts = speedDominant,
            FormFactor = modules.FirstOrDefault()?.FormFactor,
            Ecc = ecc,
            Modules = modules,
            Note = string.Join(" ", notes),
        };
    }

    /// <summary>Size em MB (offset 0x0C) ou, quando 0x7FFF, o campo estendido de 0x1C.</summary>
    private static long ModuleSize(SmbiosStructure device)
    {
        var size = device.Word(0x0C);
        if (size == 0x7FFF)
        {
            var extended = device.Dword(0x1C) & 0x7FFF_FFFF;
            return extended * 1024L * 1024L;
        }

        if (size is 0 or 0xFFFF)
        {
            return 0;
        }

        return size * 1024L * 1024L;
    }

    private static double? ModuleVoltage(SmbiosStructure device)
    {
        // SMBIOS 3.3+: Configured Voltage em mV (offset 0x26).
        if (device.Has(0x26))
        {
            var millivolts = device.Word(0x26);
            if (millivolts is > 500 and < 3000)
            {
                return Math.Round(millivolts / 1000d, 3);
            }
        }

        return null;
    }

    /// <summary>Extrai o canal de rotulos como "ChannelA-DIMM0", "DIMM_A1" ou "BANK 0".</summary>
    private static string? InferChannel(string? locator, string? bank)
    {
        foreach (var text in new[] { locator, bank })
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var upper = text.ToUpperInvariant();

            if (upper.Contains("CHANNEL"))
            {
                var index = upper.IndexOf("CHANNEL", StringComparison.Ordinal) + 7;
                if (index < upper.Length && char.IsLetter(upper[index]))
                {
                    return upper[index].ToString();
                }

                if (index < upper.Length && char.IsDigit(upper[index]))
                {
                    return upper[index].ToString();
                }
            }

            if (upper.Contains("DIMM_") && upper.Length > 5)
            {
                var suffix = upper.Split('_').LastOrDefault();
                if (!string.IsNullOrEmpty(suffix) && char.IsLetter(suffix[0]))
                {
                    return suffix[0].ToString();
                }
            }
        }

        return null;
    }

    private static MemorySpec FromWmi()
    {
        var rows = Wmi.Query(
            "SELECT Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, SerialNumber, DeviceLocator, BankLabel, SMBIOSMemoryType, FormFactor, DataWidth, TotalWidth FROM Win32_PhysicalMemory",
            "Capacity", "Speed", "ConfiguredClockSpeed", "Manufacturer", "PartNumber", "SerialNumber",
            "DeviceLocator", "BankLabel", "SMBIOSMemoryType", "FormFactor", "DataWidth", "TotalWidth");

        var modules = rows.Select(row => new MemoryModuleSpec
        {
            Locator = Wmi.Text(row, "DeviceLocator"),
            BankLocator = Wmi.Text(row, "BankLabel"),
            Channel = InferChannel(Wmi.Text(row, "DeviceLocator"), Wmi.Text(row, "BankLabel")),
            Manufacturer = Wmi.Text(row, "Manufacturer"),
            PartNumber = Wmi.Text(row, "PartNumber"),
            SerialNumber = Wmi.Text(row, "SerialNumber"),
            SizeBytes = Wmi.Number(row, "Capacity"),
            Type = SmbiosEnums.MemoryType((byte)Wmi.Number(row, "SMBIOSMemoryType")),
            FormFactor = SmbiosEnums.FormFactor((byte)Wmi.Number(row, "FormFactor")),
            SpeedMts = (int)Wmi.Number(row, "Speed"),
            ConfiguredSpeedMts = (int)Wmi.Number(row, "ConfiguredClockSpeed"),
            DataWidth = (int)Wmi.Number(row, "DataWidth"),
            TotalWidth = (int)Wmi.Number(row, "TotalWidth"),
        }).ToList();

        return new MemorySpec
        {
            TotalBytes = modules.Sum(m => m.SizeBytes),
            SlotCount = modules.Count,
            PopulatedSlots = modules.Count,
            Channels = modules.Select(m => m.Channel).Where(c => c is not null).Distinct().Count(),
            Type = modules.GroupBy(m => m.Type).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault(),
            SpeedMts = modules.Count == 0 ? 0 : modules.Max(m => m.ConfiguredSpeedMts > 0 ? m.ConfiguredSpeedMts : m.SpeedMts),
            FormFactor = modules.FirstOrDefault()?.FormFactor,
            Ecc = modules.Any(m => m.Ecc),
            Modules = modules,
            Note = "Dados obtidos via WMI (Win32_PhysicalMemory).",
        };
    }
}

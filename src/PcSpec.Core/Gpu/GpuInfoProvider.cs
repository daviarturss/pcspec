using Microsoft.Win32;
using PcSpec.Core.Model;
using PcSpec.Core.Platform;

namespace PcSpec.Core.Gpu;

/// <summary>
/// Aba Graphics. WMI fornece nome/driver/resolução; o tamanho real de VRAM vem do
/// registro (HardwareInformation.qwMemorySize), porque AdapterRAM e uint32 e
/// satura em 4 GB em placas modernas.
/// </summary>
public static class GpuInfoProvider
{
    private const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public static IReadOnlyList<GpuSpec> Collect(ICollection<string> diagnostics)
    {
        var rows = Wmi.Query(
            "SELECT Name, VideoProcessor, AdapterCompatibility, AdapterRAM, DriverVersion, DriverDate, " +
            "VideoModeDescription, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, " +
            "PNPDeviceID, Status FROM Win32_VideoController",
            "Name", "VideoProcessor", "AdapterCompatibility", "AdapterRAM", "DriverVersion", "DriverDate",
            "VideoModeDescription", "CurrentHorizontalResolution", "CurrentVerticalResolution",
            "CurrentRefreshRate", "PNPDeviceID", "Status");

        if (rows.Count == 0)
        {
            diagnostics.Add("Win32_VideoController não retornou adaptadores.");
            return [];
        }

        var registry = ReadRegistryAdapters();
        var gpus = new List<GpuSpec>();

        foreach (var row in rows)
        {
            var name = Wmi.Text(row, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var pnp = Wmi.Text(row, "PNPDeviceID");
            var (vendorId, deviceId) = ParsePnp(pnp);

            var match = registry.FirstOrDefault(r =>
                !string.IsNullOrWhiteSpace(r.Description) &&
                (string.Equals(r.Description, name, StringComparison.OrdinalIgnoreCase) ||
                 name.Contains(r.Description, StringComparison.OrdinalIgnoreCase) ||
                 r.Description.Contains(name, StringComparison.OrdinalIgnoreCase)));

            var memory = match?.MemoryBytes ?? 0;
            if (memory == 0)
            {
                memory = Wmi.Number(row, "AdapterRAM");
            }

            var width = Wmi.Number(row, "CurrentHorizontalResolution");
            var height = Wmi.Number(row, "CurrentVerticalResolution");
            var refresh = Wmi.Number(row, "CurrentRefreshRate");

            gpus.Add(new GpuSpec
            {
                Name = name,
                Vendor = VendorName(vendorId) ?? Wmi.Text(row, "AdapterCompatibility"),
                VendorId = vendorId,
                DeviceId = deviceId,
                Processor = Wmi.Text(row, "VideoProcessor"),
                MemoryBytes = memory,
                MemoryBusBits = match?.BusWidth,
                DriverVersion = Wmi.Text(row, "DriverVersion"),
                DriverDate = Format.Date(Wmi.Date(row, "DriverDate")),
                Resolution = width > 0 && height > 0 ? $"{width} x {height}" : Wmi.Text(row, "VideoModeDescription"),
                RefreshRate = refresh > 0 ? (int)refresh : null,
                PnpId = pnp,
                Status = Wmi.Text(row, "Status"),
            });
        }

        if (gpus.Count > 1)
        {
            diagnostics.Add($"{gpus.Count} adaptadores de vídeo encontrados.");
        }

        return gpus;
    }

    private sealed record RegistryAdapter(string? Description, long MemoryBytes, long? BusWidth);

    private static List<RegistryAdapter> ReadRegistryAdapters()
    {
        var adapters = new List<RegistryAdapter>();

        foreach (var subKey in RegistryReader.GetSubKeyNames(Registry.LocalMachine, DisplayClass))
        {
            if (subKey.Length != 4 || !subKey.All(char.IsDigit))
            {
                continue;
            }

            var path = $@"{DisplayClass}\{subKey}";
            var description = RegistryReader.GetString(Registry.LocalMachine, path, "DriverDesc");

            long memory = 0;
            var qword = RegistryReader.GetValue(Registry.LocalMachine, path, "HardwareInformation.qwMemorySize");
            if (qword is long q && q > 0)
            {
                memory = q;
            }
            else
            {
                var raw = RegistryReader.GetValue(Registry.LocalMachine, path, "HardwareInformation.MemorySize");
                if (raw is byte[] bytes)
                {
                    memory = bytes.Length switch
                    {
                        8 => BitConverter.ToInt64(bytes, 0),
                        4 => BitConverter.ToUInt32(bytes, 0),
                        _ => 0,
                    };
                }
            }

            long? busWidth = null;
            var widthValue = RegistryReader.GetValue(Registry.LocalMachine, path, "HardwareInformation.BusWidth");
            if (widthValue is int width && width > 0)
            {
                busWidth = width;
            }

            if (description is not null || memory > 0)
            {
                adapters.Add(new RegistryAdapter(description, memory, busWidth));
            }
        }

        return adapters;
    }

    private static (string? VendorId, string? DeviceId) ParsePnp(string? pnp)
    {
        if (string.IsNullOrWhiteSpace(pnp))
        {
            return (null, null);
        }

        string? vendor = null;
        string? device = null;

        foreach (var part in pnp.Split('\\'))
        {
            foreach (var token in part.Split('&'))
            {
                if (token.StartsWith("VEN_", StringComparison.OrdinalIgnoreCase))
                {
                    vendor = token[4..].ToUpperInvariant();
                }
                else if (token.StartsWith("DEV_", StringComparison.OrdinalIgnoreCase))
                {
                    device = token[4..].ToUpperInvariant();
                }
            }
        }

        return (vendor, device);
    }

    private static string? VendorName(string? vendorId) => vendorId switch
    {
        "10DE" => "NVIDIA",
        "1002" or "1022" => "AMD",
        "8086" => "Intel",
        "1414" => "Microsoft",
        "15AD" => "VMware",
        "80EE" => "Oracle VirtualBox",
        "1234" => "QEMU / Bochs",
        "1AF4" => "Red Hat (virtio)",
        _ => null,
    };
}

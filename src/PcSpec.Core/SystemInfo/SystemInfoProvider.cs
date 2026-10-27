using Microsoft.Win32;
using PcSpec.Core.Model;
using PcSpec.Core.Smbios;
using PcSpec.Core.Platform;

namespace PcSpec.Core.SystemInfo;

/// <summary>Dados da máquina e do sistema operacional (aba About/System).</summary>
public static class SystemInfoProvider
{
    public static SystemSpec Collect(SmbiosTable? smbios, ICollection<string> diagnostics)
    {
        var system = smbios?.First(1);
        var chassis = smbios?.First(3);

        var computer = Wmi.Query(
            "SELECT Manufacturer, Model, TotalPhysicalMemory, SystemFamily, SystemSKUNumber FROM Win32_ComputerSystem",
            "Manufacturer", "Model", "TotalPhysicalMemory", "SystemFamily", "SystemSKUNumber").FirstOrDefault() ?? [];

        var os = Wmi.Query(
            "SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate FROM Win32_OperatingSystem",
            "Caption", "Version", "BuildNumber", "OSArchitecture", "InstallDate").FirstOrDefault() ?? [];

        var build = Wmi.Text(os, "BuildNumber") ?? RegistryReader.GetString(
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "CurrentBuildNumber");

        var displayVersion = RegistryReader.GetString(
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "DisplayVersion");

        var productName = RegistryReader.GetString(
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "ProductName");

        var osName = Wmi.Text(os, "Caption") ?? productName;
        if (osName is not null && build is not null && int.TryParse(build, out var buildNumber) && buildNumber >= 22000)
        {
            // O registro ainda reporta "Windows 10" em builds 11.x.
            osName = osName.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
            productName = productName?.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
        }

        var version = Wmi.Text(os, "Version");
        var osVersion = string.Join(" ", new[] { displayVersion ?? version, build is null ? null : $"(build {build})" }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        var totalMemory = Wmi.Number(computer, "TotalPhysicalMemory");
        if (totalMemory <= 0)
        {
            totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }

        var secureBoot = ReadSecureBoot();

        if (smbios is null)
        {
            diagnostics.Add("SMBIOS indisponível: fabricante/modelo vieram do WMI.");
        }

        return new SystemSpec
        {
            Manufacturer = Format.Clean(system?.String(0x04)) ?? Wmi.Text(computer, "Manufacturer"),
            Model = Format.Clean(system?.String(0x05)) ?? Wmi.Text(computer, "Model"),
            Family = Format.Clean(system?.String(0x1A)) ?? Wmi.Text(computer, "SystemFamily"),
            Sku = Format.Clean(system?.String(0x19)) ?? Wmi.Text(computer, "SystemSKUNumber"),
            SerialNumber = Format.Clean(system?.String(0x07)),
            Uuid = FormatUuid(system),
            ChassisType = chassis is null ? null : SmbiosEnums.ChassisType((byte)(chassis.Byte(0x05) & 0x7F)),
            MachineName = Environment.MachineName,
            OsName = osName,
            OsVersion = osVersion,
            OsBuild = build,
            OsArchitecture = Wmi.Text(os, "OSArchitecture") ?? System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            OsInstallDate = Format.Date(Wmi.Date(os, "InstallDate")),
            TotalPhysicalMemoryBytes = totalMemory,
            BootMode = BootMode(),
            SecureBoot = secureBoot,
        };
    }

    /// <summary>UUID do SMBIOS com os tres primeiros campos em little-endian.</summary>
    private static string? FormatUuid(SmbiosStructure? system)
    {
        if (system is null || system.Length < 0x18)
        {
            return null;
        }

        var raw = system.Raw.Slice(0x08, 16).ToArray();
        if (raw.All(b => b == 0) || raw.All(b => b == 0xFF))
        {
            return null;
        }

        return $"{raw[3]:X2}{raw[2]:X2}{raw[1]:X2}{raw[0]:X2}-" +
               $"{raw[5]:X2}{raw[4]:X2}-" +
               $"{raw[7]:X2}{raw[6]:X2}-" +
               $"{raw[8]:X2}{raw[9]:X2}-" +
               $"{raw[10]:X2}{raw[11]:X2}{raw[12]:X2}{raw[13]:X2}{raw[14]:X2}{raw[15]:X2}";
    }

    private static string? BootMode()
    {
        try
        {
            if (Interop.NativeMethods.GetFirmwareType(out var type))
            {
                return type switch
                {
                    1 => "BIOS (legado)",
                    2 => "UEFI",
                    _ => null,
                };
            }
        }
        catch
        {
            // Ignorado.
        }

        return null;
    }

    private static string? ReadSecureBoot()
    {
        var value = RegistryReader.GetInt(
            Registry.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\SecureBoot\State",
            "UEFISecureBootEnabled");

        return value switch
        {
            1 => "Habilitado",
            0 => "Desabilitado",
            _ => null,
        };
    }
}

using Microsoft.Win32;
using PcSpec.Core.Model;
using PcSpec.Core.Platform;

namespace PcSpec.Core.Board;

/// <summary>
/// Identificação de chipset/southbridge por IDs PCI. A classe do dispositivo
/// (PCI\CC_0600 = host bridge, CC_0601 = ISA/LPC bridge) vem de
/// <c>CompatibleIDs</c> na subchave de instância do Enum\PCI, o que dispensa
/// SetupAPI e não exige privilégio.
/// </summary>
public static class PciInventory
{
    private const string EnumPci = @"SYSTEM\CurrentControlSet\Enum\PCI";

    public sealed record Device(
        string VendorId,
        string DeviceId,
        string Class,
        string Description,
        string? Location)
    {
        /// <summary>Endereço "bus/dev/fun" quando o driver informa LocationInformation.</summary>
        public string? Address => ParseLocation(Location);

        private static string? ParseLocation(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            var open = location.IndexOf('(');
            var close = location.LastIndexOf(')');
            if (open < 0 || close <= open)
            {
                return null;
            }

            var parts = location[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                return null;
            }

            return $"{parts[0]}:{parts[1]}.{parts[2]}";
        }
    }

    public static IReadOnlyList<Device> Read()
    {
        var devices = new List<Device>();

        foreach (var deviceKey in RegistryReader.GetSubKeyNames(Registry.LocalMachine, EnumPci))
        {
            var parts = deviceKey.Split('&');
            if (parts.Length < 2)
            {
                continue;
            }

            var vendor = parts[0].StartsWith("VEN_", StringComparison.OrdinalIgnoreCase) ? parts[0][4..] : null;
            var device = parts[1].StartsWith("DEV_", StringComparison.OrdinalIgnoreCase) ? parts[1][4..] : null;
            if (vendor is null || device is null)
            {
                continue;
            }

            // Os valores (DeviceDesc, CompatibleIDs, LocationInformation) ficam na
            // subchave de instância, não na chave do device ID.
            var instances = RegistryReader.GetSubKeyNames(Registry.LocalMachine, $@"{EnumPci}\{deviceKey}");
            if (instances.Count == 0)
            {
                continue;
            }

            var path = $@"{EnumPci}\{deviceKey}\{instances[0]}";
            var compatible = RegistryReader.GetValue(Registry.LocalMachine, path, "CompatibleIDs") as string[];

            var pciClass = compatible?
                .Select(id => id.StartsWith("PCI\\CC_", StringComparison.OrdinalIgnoreCase) ? id[7..] : null)
                .FirstOrDefault(id => id is not null && id.Length >= 4);

            if (pciClass is null)
            {
                continue;
            }

            devices.Add(new Device(
                vendor.ToUpperInvariant(),
                device.ToUpperInvariant(),
                pciClass[..4],
                CleanDescription(RegistryReader.GetString(Registry.LocalMachine, path, "DeviceDesc")) ?? string.Empty,
                RegistryReader.GetString(Registry.LocalMachine, path, "LocationInformation")));
        }

        return devices;
    }

    /// <summary>DeviceDesc vem como "@pci.inf,%pci_dev_...%;Nome real" em muitos drivers.</summary>
    private static string? CleanDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var separator = raw.LastIndexOf(';');
        var text = separator >= 0 ? raw[(separator + 1)..] : raw;
        return Format.Clean(text);
    }

    public static Device? HostBridge(IReadOnlyList<Device> devices) =>
        devices.FirstOrDefault(d => d.Class == "0600");

    public static Device? LpcBridge(IReadOnlyList<Device> devices) =>
        devices.FirstOrDefault(d => d.Class == "0601");

    public static Device? IdeBridge(IReadOnlyList<Device> devices) =>
        devices.FirstOrDefault(d => d.Class is "0101" or "0106");
}

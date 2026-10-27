using Microsoft.Win32;
using PcSpec.Core.Model;
using PcSpec.Core.Smbios;
using PcSpec.Core.Platform;

namespace PcSpec.Core.Board;

/// <summary>Monta a aba Mainboard: placa-mãe (SMBIOS tipo 2), BIOS (tipo 0) e chipset (PCI).</summary>
public static class BoardInfoProvider
{
    public static (BoardSpec Board, BiosSpec Bios) Collect(SmbiosTable? smbios, ICollection<string> diagnostics)
    {
        var board = smbios?.First(2);
        var bios = smbios?.First(0);

        var pci = PciInventory.Read();
        if (pci.Count == 0)
        {
            diagnostics.Add("Inventario PCI vazio: chipset e southbridge ficam sem identificação.");
        }

        var host = PciInventory.HostBridge(pci);
        var lpc = PciInventory.LpcBridge(pci);
        var family = ChipsetCatalog.ChipsetFamily(pci);

        var hostName = host is null ? null : ChipsetCatalog.HostBridgeName(host.VendorId, host.DeviceId);
        var lpcName = lpc is null ? null : ChipsetCatalog.LpcBridgeName(lpc.VendorId, lpc.DeviceId);

        var chipset = family
            ?? (host is null
                ? null
                : hostName ?? $"{ChipsetCatalog.VendorName(host.VendorId)} (VEN_{host.VendorId}&DEV_{host.DeviceId})");

        var southbridge = lpc is null
            ? null
            : lpcName ?? $"{ChipsetCatalog.VendorName(lpc.VendorId)} (VEN_{lpc.VendorId}&DEV_{lpc.DeviceId})";

        var boardSpec = new BoardSpec
        {
            Manufacturer = Format.Clean(board?.String(0x04)) ?? Wmi.Text(WmiBaseBoard(), "Manufacturer"),
            Product = Format.Clean(board?.String(0x05)) ?? Wmi.Text(WmiBaseBoard(), "Product"),
            Version = Format.Clean(board?.String(0x06)),
            SerialNumber = Format.Clean(board?.String(0x07)),
            AssetTag = Format.Clean(board?.String(0x08)),
            BoardType = board is null ? null : SmbiosEnums.BoardType(board.Byte(0x0D)),
            ChipsetVendor = host is null ? null : ChipsetCatalog.VendorName(host.VendorId),
            Chipset = chipset,
            Southbridge = southbridge,
            Bridges = pci
                .Where(d => d.Class is "0600" or "0601" or "0604")
                .Select(d => new PciBridge(
                    d.VendorId,
                    d.DeviceId,
                    d.Class switch
                    {
                        "0600" => "Host bridge",
                        "0601" => "ISA/LPC bridge",
                        "0604" => "PCI-to-PCI bridge",
                        _ => d.Class,
                    },
                    d.Class switch
                    {
                        "0600" => ChipsetCatalog.HostBridgeName(d.VendorId, d.DeviceId),
                        "0601" => ChipsetCatalog.LpcBridgeName(d.VendorId, d.DeviceId),
                        _ => Format.Clean(d.Description),
                    }))
                .Take(24)
                .ToList(),
        };

        var firmware = FirmwareType();
        var romSize = bios is not null ? (long)(bios.Byte(0x09) + 1) * 64 * 1024 : 0;

        var biosSpec = new BiosSpec
        {
            Vendor = Format.Clean(bios?.String(0x04)),
            Version = Format.Clean(bios?.String(0x05)),
            ReleaseDate = Format.Clean(bios?.String(0x08)),
            SmbiosVersion = smbios is null ? null : smbios.Version,
            FirmwareType = firmware,
            RomSizeBytes = romSize,
            MajorRelease = bios is not null && bios.Has(0x14) && bios.Byte(0x14) != 0xFF ? bios.Byte(0x14).ToString() : null,
            MinorRelease = bios is not null && bios.Has(0x15) && bios.Byte(0x15) != 0xFF ? bios.Byte(0x15).ToString() : null,
        };

        // Fallback: BIOS via WMI quando o firmware não expõe SMBIOS.
        if (biosSpec.Vendor is null || biosSpec.Version is null)
        {
            var row = Wmi.Query("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS",
                "Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate").FirstOrDefault() ?? [];

            biosSpec = biosSpec with
            {
                Vendor = biosSpec.Vendor ?? Wmi.Text(row, "Manufacturer"),
                Version = biosSpec.Version ?? Wmi.Text(row, "SMBIOSBIOSVersion"),
                ReleaseDate = biosSpec.ReleaseDate ?? Format.Date(Wmi.Date(row, "ReleaseDate")),
            };
        }

        return (boardSpec, biosSpec);
    }

    private static Dictionary<string, object?> WmiBaseBoard() =>
        Wmi.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Manufacturer", "Product").FirstOrDefault() ?? [];

    private static string? FirmwareType()
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
            // Ignorado: campo opcional.
        }

        return null;
    }
}

namespace PcSpec.Core.Board;

/// <summary>
/// Tabela curada de host bridges e LPC/ISA bridges. Cobre os chipsets que aparecem
/// em máquinas reais; o que não estiver aqui e exibido como vendor/device ID cru.
/// </summary>
public static class ChipsetCatalog
{
    private static readonly Dictionary<(string Vendor, string Device), string> HostBridges = new()
    {
        // AMD - família 17h / 19h / 1Ah
        [("1022", "1450")] = "AMD 17h Models 00h-0Fh Root Complex",
        [("1022", "1440")] = "AMD 19h Root Complex",
        [("1022", "1448")] = "AMD 19h (Vermeer) Root Complex",
        [("1022", "1480")] = "AMD Starship/Matisse Root Complex",
        [("1022", "14B5")] = "AMD 17h/19h Root Complex",
        [("1022", "14D8")] = "AMD 19h/1Ah Root Complex",
        [("1022", "1630")] = "AMD Renoir/Cezanne Root Complex",
        [("1022", "15D0")] = "AMD Raven Ridge Root Complex",
        [("1022", "15E8")] = "AMD Raven2 Root Complex",
        [("1022", "14A0")] = "AMD 19h (Milan) Root Complex",
        [("1022", "1530")] = "AMD Stoney Ridge Root Complex",
        [("1022", "1570")] = "AMD 17h Models 20h-2Fh Root Complex",

        // Intel - série 100 a 800
        [("8086", "3E30")] = "Intel 8th/9th Gen Core Host Bridge",
        [("8086", "3EC2")] = "Intel 8th Gen Core Host Bridge",
        [("8086", "3F00")] = "Intel Xeon E5 v4 Host Bridge",
        [("8086", "5910")] = "Intel 6th/7th Gen Core Host Bridge",
        [("8086", "5918")] = "Intel 6th Gen Core Host Bridge",
        [("8086", "9B33")] = "Intel 10th Gen Core Host Bridge",
        [("8086", "A700")] = "Intel 11th Gen Core Host Bridge",
        [("8086", "4660")] = "Intel 12th/13th Gen Core Host Bridge",
        [("8086", "4640")] = "Intel 12th Gen Core Host Bridge",
        [("8086", "7D00")] = "Intel 13th/14th Gen Core Host Bridge",
    };

    private static readonly Dictionary<(string Vendor, string Device), string> LpcBridges = new()
    {
        // AMD FCH / Promontory
        [("1022", "790E")] = "AMD FCH LPC Bridge",
        [("1022", "780E")] = "AMD FCH LPC Bridge (Hudson)",
        [("1022", "43C9")] = "AMD 400 Series Chipset LPC Bridge",
        [("1022", "57A4")] = "AMD 500 Series Chipset LPC Bridge",

        // Intel PCH
        [("8086", "A300")] = "Intel 600/700 Series PCH LPC",
        [("8086", "A301")] = "Intel 600/700 Series PCH LPC",
        [("8086", "7A04")] = "Intel 700 Series PCH LPC",
        [("8086", "5182")] = "Intel 8th/9th Gen PCH LPC",
        [("8086", "9D84")] = "Intel 300 Series PCH LPC",
        [("8086", "A082")] = "Intel 400 Series PCH LPC",
        [("8086", "4385")] = "Intel 100/200 Series PCH LPC",
        [("8086", "8C4E")] = "Intel 8 Series PCH LPC",
        [("8086", "8CC4")] = "Intel 9 Series PCH LPC",
        [("8086", "7A87")] = "Intel 700 Series PCH LPC",
    };

    private static readonly HashSet<string> Amd500 = ["57A3", "57A4", "57AD", "57B3", "57B4", "57B5", "57B6", "57B7", "57B9"];
    private static readonly HashSet<string> Amd400 = ["43C6", "43C7", "43C8", "43C9", "43D5", "43EB", "43EC"];
    private static readonly HashSet<string> Amd300 = ["43B3", "43B4", "43B5", "43B9", "43BA", "43BB", "43BC", "43BD"];

    /// <summary>
    /// Família comercial do chipset. Os IDs de 300/400/500 series se sobrepõem em
    /// parte, por isso a checagem usa listas explicitas em vez de prefixo: um
    /// A320 (300-series) também expõe 43B3/43B4.
    /// </summary>
    public static string? ChipsetFamily(IEnumerable<PciInventory.Device> devices)
    {
        var amd = devices.Where(d => d.VendorId == "1022").Select(d => d.DeviceId).ToHashSet();

        if (amd.Overlaps(Amd500))
        {
            return "AMD 500-series";
        }

        if (amd.Overlaps(Amd400))
        {
            return "AMD 400-series";
        }

        if (amd.Overlaps(Amd300))
        {
            return "AMD 300-series";
        }

        var intel = devices.Where(d => d.VendorId == "8086").Select(d => d.DeviceId).ToList();

        if (intel.Any(id => id.StartsWith("7A", StringComparison.Ordinal)))
        {
            return "Intel 700-series";
        }

        if (intel.Any(id => id.StartsWith("A3", StringComparison.Ordinal)))
        {
            return "Intel 600/700-series";
        }

        if (intel.Any(id => id.StartsWith("A0", StringComparison.Ordinal)))
        {
            return "Intel 400/500-series";
        }

        if (intel.Any(id => id.StartsWith("9D", StringComparison.Ordinal) || id.StartsWith("A1", StringComparison.Ordinal)))
        {
            return "Intel 300-series";
        }

        if (intel.Any(id => id.StartsWith("43", StringComparison.Ordinal) || id.StartsWith("A2", StringComparison.Ordinal)))
        {
            return "Intel 100/200-series";
        }

        return null;
    }

    public static string? HostBridgeName(string vendorId, string deviceId) =>
        HostBridges.GetValueOrDefault((vendorId, deviceId));

    public static string? LpcBridgeName(string vendorId, string deviceId) =>
        LpcBridges.GetValueOrDefault((vendorId, deviceId));

    public static string VendorName(string vendorId) => vendorId switch
    {
        "1022" => "AMD",
        "8086" => "Intel",
        "10DE" => "NVIDIA",
        "1002" => "AMD/ATI",
        "1D17" => "Zhaoxin",
        "1A03" => "ASPEED",
        _ => $"VEN_{vendorId}",
    };
}

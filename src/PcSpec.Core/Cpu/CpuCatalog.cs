namespace PcSpec.Core.Cpu;

/// <summary>
/// Tradução (família, modelo) -> codename e litografia. E uma tabela curada:
/// quando não ha correspondência o campo fica nulo em vez de arriscar um palpite.
/// </summary>
public static class CpuCatalog
{
    public static (string? Codename, string? Technology) Lookup(string? vendor, int family, int model) =>
        vendor switch
        {
            "AuthenticAMD" => Amd(family, model),
            "GenuineIntel" => Intel(family, model),
            _ => (null, null),
        };

    private static (string?, string?) Amd(int family, int model) => family switch
    {
        0x17 => model switch
        {
            0x01 => ("Zen (Summit Ridge / Naples)", "14 nm"),
            0x08 => ("Zen+ (Pinnacle Ridge)", "12 nm"),
            0x11 => ("Zen (Raven Ridge)", "14 nm"),
            0x18 => ("Zen+ (Picasso)", "12 nm"),
            0x20 => ("Zen (Dali)", "14 nm"),
            0x30 or 0x31 => ("Zen 2 (Rome / Castle Peak)", "7 nm"),
            0x60 => ("Zen 2 (Renoir)", "7 nm"),
            0x68 => ("Zen 2 (Lucienne)", "7 nm"),
            0x71 => ("Zen 2 (Matisse)", "7 nm"),
            0x90 => ("Zen 2 (Van Gogh)", "7 nm"),
            0xA0 => ("Zen 2 (Mendocino)", "6 nm"),
            _ => ("Zen / Zen+ / Zen 2", null),
        },
        0x19 => model switch
        {
            0x00 or 0x01 => ("Zen 3 (Milan)", "7 nm"),
            0x08 => ("Zen 3 (Chagall)", "7 nm"),
            0x21 => ("Zen 3 (Vermeer)", "7 nm"),
            0x40 or 0x44 => ("Zen 3+ (Rembrandt)", "6 nm"),
            0x50 => ("Zen 3 (Cezanne)", "7 nm"),
            0x61 => ("Zen 3+ (Rembrandt)", "6 nm"),
            0x74 or 0x75 => ("Zen 4 (Phoenix)", "4 nm"),
            0x10 or 0x11 => ("Zen 4 (Genoa / Bergamo)", "5 nm"),
            0x17 or 0x18 => ("Zen 4 (Storm Peak)", "5 nm"),
            _ => ("Zen 3 / Zen 4", null),
        },
        0x1A => ("Zen 5 (Granite Ridge / Strix Point / Turin)", "4 nm"),
        0x1F => ("Zen 5 (Turin)", "3 nm"),
        0x15 => ("Zen (Bulldozer / Excavator)", "28 nm"),
        0x16 => ("Zen (Jaguar / Puma)", "28 nm"),
        _ => (null, null),
    };

    private static (string?, string?) Intel(int family, int model) => family switch
    {
        0x06 => model switch
        {
            0x3C or 0x3F or 0x45 or 0x46 => ("Haswell", "22 nm"),
            0x3D or 0x47 or 0x4F or 0x56 => ("Broadwell", "14 nm"),
            0x4E or 0x5E or 0x55 => ("Skylake", "14 nm"),
            0x8E or 0x9E => ("Kaby Lake / Coffee Lake", "14 nm"),
            0x66 => ("Cannon Lake", "10 nm"),
            0x6A or 0x6C => ("Ice Lake", "10 nm"),
            0x7D or 0x7E => ("Ice Lake", "10 nm"),
            0x86 or 0x96 or 0x9C => ("Tremont / Elkhart Lake", "10 nm"),
            0x8C or 0x8D => ("Tiger Lake", "10 nm"),
            0x97 or 0x9A => ("Alder Lake", "Intel 7"),
            0xB7 or 0xBA or 0xBF => ("Raptor Lake", "Intel 7"),
            0xAA or 0xAC => ("Meteor Lake", "Intel 4"),
            0xB5 or 0xAF => ("Meteor Lake / Arrow Lake", "Intel 4"),
            0xC7 => ("Arrow Lake", "TSMC N3B"),
            0xBD => ("Lunar Lake", "TSMC N3B"),
            0xCC or 0xCF => ("Sapphire Rapids / Emerald Rapids", "Intel 7"),
            0x0F or 0x16 => ("Skylake / Kaby Lake", "14 nm"),
            0x1A or 0x1E or 0x1F => ("Nehalem / Lynnfield / Bloomfield", "45 nm"),
            0x2A or 0x2D => ("Sandy Bridge", "32 nm"),
            0x3A or 0x3E => ("Ivy Bridge", "22 nm"),
            _ => (null, null),
        },
        _ => (null, null),
    };
}

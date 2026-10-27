using System.Globalization;

namespace PcSpec.Core;

/// <summary>Formatação consistente para CLI, relatório e UI.</summary>
public static class Format
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(long value)
    {
        if (value <= 0)
        {
            return "—";
        }

        double size = value;
        var unit = 0;
        while (size >= 1024 && unit < ByteUnits.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return size.ToString(size >= 100 ? "0" : "0.##", CultureInfo.InvariantCulture) + " " + ByteUnits[unit];
    }

    public static string Mhz(double? value) =>
        value is > 0 ? value.Value.ToString("0.#", CultureInfo.InvariantCulture) + " MHz" : "—";

    public static string Ghz(double? value) =>
        value is > 0 ? (value.Value / 1000d).ToString("0.00", CultureInfo.InvariantCulture) + " GHz" : "—";

    public static string Rate(double gigabytesPerSecond) =>
        gigabytesPerSecond > 0
            ? gigabytesPerSecond.ToString("0.00", CultureInfo.InvariantCulture) + " GB/s"
            : "—";

    public static string Number(double? value, string format = "0.#") =>
        value is null ? "—" : value.Value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>Formatação numerica sempre com ponto decimal, independente do idioma.</summary>
    public static string Num(double value, string format = "0.#") =>
        value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>Valor textual ou travessão, para não deixar campos vazios no relatório.</summary>
    public static string Or(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    public static string Percent(double? value) =>
        value is null ? "—" : value.Value.ToString("0.0", CultureInfo.InvariantCulture) + " %";

    public static string Date(DateTime? value) =>
        value is null ? "—" : value.Value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

    /// <summary>Converte uma data WMI (formato DMTF, ex. 20200101000000.000000+000) em DateTime.</summary>
    public static DateTime? ParseWmiDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8)
        {
            return null;
        }

        var digits = raw.Length >= 14 ? raw[..14] : raw[..8].PadRight(8, '0');
        if (DateTime.TryParseExact(
                digits[..8] + (digits.Length >= 14 ? digits[8..14] : "000000"),
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>Normaliza o texto vindo do firmware (remove espacos e caracteres de controle).</summary>
    public static string? Clean(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        var trimmed = new string(raw.Where(c => !char.IsControl(c) || c == '\t').ToArray()).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

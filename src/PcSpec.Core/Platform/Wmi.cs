using System.Globalization;
using System.Management;

namespace PcSpec.Core.Platform;

/// <summary>
/// Camada fina sobre WMI. Materializa as linhas em dicionarios para não manter
/// objetos COM vivos e para que uma propriedade ausente não derrube a consulta.
/// </summary>
public static class Wmi
{
    public static bool IsAvailable { get; private set; } = true;

    public static List<Dictionary<string, object?>> Query(string wql, params string[] properties)
    {
        var rows = new List<Dictionary<string, object?>>();

        if (!IsAvailable)
        {
            return rows;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            searcher.Options.Timeout = TimeSpan.FromSeconds(20);
            searcher.Options.ReturnImmediately = true;

            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                {
                    try
                    {
                        row[property] = item[property];
                    }
                    catch
                    {
                        row[property] = null;
                    }
                }

                rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            Diagnostics.Add($"WMI indisponível ({ex.GetType().Name}: {ex.Message.Trim()})");
        }

        return rows;
    }

    public static List<string> Diagnostics { get; } = [];

    public static string? Text(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        var text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        return Format.Clean(text);
    }

    public static long Number(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    public static double Decimal(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    public static DateTime? Date(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is not null
            ? Format.ParseWmiDate(value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture))
            : null;
}

using Microsoft.Win32;

namespace PcSpec.Core.Platform;

/// <summary>Acesso somente-leitura ao registro, sempre tolerante a ausencia de chave.</summary>
public static class RegistryReader
{
    public static object? GetValue(RegistryKey root, string path, string name)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            return key?.GetValue(name);
        }
        catch
        {
            return null;
        }
    }

    public static string? GetString(RegistryKey root, string path, string name) =>
        Format.Clean(GetValue(root, path, name) as string);

    public static int? GetInt(RegistryKey root, string path, string name)
    {
        var value = GetValue(root, path, name);
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<string> GetSubKeyNames(RegistryKey root, string path)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            return key?.GetSubKeyNames() ?? [];
        }
        catch
        {
            return [];
        }
    }
}

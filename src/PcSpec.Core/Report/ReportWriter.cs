using System.Text;
using PcSpec.Core.Model;

namespace PcSpec.Core.Report;

public enum ReportFormat
{
    Text,
    Html,
}

public static class ReportWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string Build(MachineSpec spec, ReportFormat format) => format switch
    {
        ReportFormat.Html => HtmlReport.Build(spec),
        _ => TextReport.Build(spec),
    };

    /// <summary>
    /// Grava o relatório e devolve o caminho absoluto. O formato e inferido da
    /// extensão quando <paramref name="format"/> não é informado.
    /// </summary>
    public static string Save(MachineSpec spec, string path, ReportFormat? format = null)
    {
        var resolved = format ?? InferFormat(path);
        var full = Path.GetFullPath(path);

        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(full, Build(spec, resolved), Utf8NoBom);
        return full;
    }

    public static ReportFormat InferFormat(string path) =>
        Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".htm", StringComparison.OrdinalIgnoreCase)
            ? ReportFormat.Html
            : ReportFormat.Text;

    /// <summary>Nome padrão do arquivo: pcspec-AAAAMMDD-HHmm.txt/.html.</summary>
    public static string DefaultFileName(MachineSpec spec, ReportFormat format)
    {
        var extension = format == ReportFormat.Html ? "html" : "txt";
        return $"pcspec-{spec.CollectedAt.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture)}.{extension}";
    }
}

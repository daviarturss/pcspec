namespace PcSpec.App.ViewModels;

/// <summary>Uma linha rotulo/valor exibida nas abas.</summary>
public sealed record SpecRow(string Label, string Value);

/// <summary>Conjunto de linhas com titulo, usado para módulos de memória e placas de vídeo.</summary>
public sealed record SpecGroup(string Title, IReadOnlyList<SpecRow> Rows);

/// <summary>Linha da tabela de benchmark, com a barra normalizada pelo maior score.</summary>
public sealed record BenchRow(
    string Name,
    string SingleText,
    string MultiText,
    string SingleScoreText,
    string MultiScoreText,
    double BarPercent);

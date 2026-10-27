namespace PcSpec.Core.Model;

/// <summary>
/// Resultado de um subteste do benchmark. <see cref="Value"/> fica na unidade declarada
/// e <see cref="Score"/> e o valor normalizado pela referência de calibracao.
/// </summary>
public sealed record BenchSubtest(
    string Name,
    string Unit,
    double Value,
    double Reference,
    double Score)
{
    public string DisplayValue => Unit switch
    {
        "Mops/s" => $"{Value:0} Mops/s",
        "GB/s" => $"{Value:0.00} GB/s",
        _ => $"{Value:0.##} {Unit}",
    };
}

public sealed record BenchThreadResult(
    string Mode,
    IReadOnlyList<BenchSubtest> Subtests,
    double Score)
{
    public static BenchThreadResult Empty(string mode) => new(mode, [], 0);
}

public sealed record BenchmarkResult
{
    public DateTimeOffset RunAt { get; init; } = DateTimeOffset.Now;
    public int Threads { get; init; }
    public int Runs { get; init; }
    public int DurationMs { get; init; }
    public double SingleThreadScore { get; init; }
    public double MultiThreadScore { get; init; }
    public double Scaling { get; init; }
    public IReadOnlyList<BenchSubtest> Single { get; init; } = [];
    public IReadOnlyList<BenchSubtest> Multi { get; init; } = [];
    public string? CpuName { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
}

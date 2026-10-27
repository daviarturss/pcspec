using System.Diagnostics;
using System.Runtime.CompilerServices;
using PcSpec.Core.Interop;
using PcSpec.Core.Model;

namespace PcSpec.Core.Bench;

public sealed record BenchmarkOptions
{
    public int Runs { get; init; } = 3;
    public int DurationMs { get; init; } = 400;
    public int WarmupMs { get; init; } = 150;
    public int? Threads { get; init; }
    public bool PinThreads { get; init; } = true;
}

/// <summary>
/// Benchmark próprio do PcSpec. Mede quatro cargas (inteiro, ponto flutuante,
/// banda de memória e SHA-256) em uma thread e em todas as threads, e normaliza
/// cada resultado por uma referência fixa documentada.
///
/// O score NÃO é comparável ao do CPU-Z: a carga e a normalização são diferentes.
/// </summary>
public static class BenchmarkRunner
{
    public static BenchmarkResult Run(BenchmarkOptions? options = null, string? cpuName = null)
    {
        options ??= new BenchmarkOptions();
        var threads = Math.Clamp(options.Threads ?? Environment.ProcessorCount, 1, 256);
        var notes = new List<string>();

        var originalPriority = Process.GetCurrentProcess().PriorityClass;
        try
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        }
        catch
        {
            notes.Add("Não foi possível elevar a prioridade do processo.");
        }

        try
        {
            var single = MeasureThreads(1, options, notes);
            var multi = threads == 1 ? single : MeasureThreads(threads, options, notes);

            var singleScore = GeometricMean(single.Select(s => s.Score));
            var multiScore = GeometricMean(multi.Select(s => s.Score));

            return new BenchmarkResult
            {
                RunAt = DateTimeOffset.Now,
                Threads = threads,
                Runs = options.Runs,
                DurationMs = options.DurationMs,
                SingleThreadScore = Math.Round(singleScore, 0),
                MultiThreadScore = Math.Round(multiScore, 0),
                Scaling = singleScore > 0 ? Math.Round(multiScore / singleScore, 2) : 0,
                Single = single,
                Multi = multi,
                CpuName = cpuName,
                Notes = notes,
            };
        }
        finally
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass = originalPriority;
            }
            catch
            {
                // Restauracao best-effort.
            }
        }
    }

    private static IReadOnlyList<BenchSubtest> MeasureThreads(int threadCount, BenchmarkOptions options, List<string> notes)
    {
        var results = new List<BenchSubtest>();

        foreach (var prototype in Workload.All)
        {
            var best = double.MinValue;
            BenchSubtest? bestResult = null;

            for (var run = 0; run < Math.Max(1, options.Runs); run++)
            {
                var (ops, seconds) = threadCount == 1
                    ? RunSingle(prototype, options)
                    : RunMulti(prototype, threadCount, options, notes);

                if (seconds <= 0)
                {
                    continue;
                }

                var value = ToDisplayUnit(prototype.Unit, ops / seconds);
                if (value > best)
                {
                    best = value;
                    bestResult = new BenchSubtest(
                        prototype.Name,
                        prototype.Unit,
                        Math.Round(value, prototype.Unit == "GB/s" ? 2 : 0),
                        prototype.Reference,
                        Math.Round(value / prototype.Reference * 1000, 0));
                }
            }

            if (bestResult is not null)
            {
                results.Add(bestResult);
            }
        }

        return results;
    }

    private static double ToDisplayUnit(string unit, double opsPerSecond) => unit switch
    {
        "Mops/s" => opsPerSecond / 1e6,
        "GB/s" => opsPerSecond / 1e9,
        _ => opsPerSecond,
    };

    private static (double Ops, double Seconds) RunSingle(Workload prototype, BenchmarkOptions options)
    {
        var workload = prototype.Create();
        var chunk = Calibrate(workload, options.WarmupMs);

        var stopwatch = Stopwatch.StartNew();
        double ops = 0;
        while (stopwatch.Elapsed.TotalMilliseconds < options.DurationMs)
        {
            ops += workload.Run(chunk);
        }

        stopwatch.Stop();
        return (ops, stopwatch.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Executa a carga em N threads. Cada worker mede a própria janela de trabalho
    /// e o resultado usa a maior delas: se o coordenador for escalonado com atraso,
    /// a janela continua sendo a do trabalho real, e não a do <c>Sleep</c>.
    ///
    /// Os workers NÃO recebem prioridade acima do coordenador. Com prioridade
    /// "Highest" em todas as threads logicas, o coordenador fica faminto de CPU e
    /// a janela medida sai menor que o trabalho executado (foi um bug real aqui:
    /// 10,7 s de trabalho reportados como 0,24 s).
    /// </summary>
    private static (double Ops, double Seconds) RunMulti(
        Workload prototype,
        int threadCount,
        BenchmarkOptions options,
        List<string> notes)
    {
        using var start = new ManualResetEventSlim(false);
        using var stop = new ManualResetEventSlim(false);
        using var ready = new CountdownEvent(threadCount);

        var workers = new Worker[threadCount];
        var threads = new Thread[threadCount];
        var pin = options.PinThreads && threadCount <= 64;

        for (var i = 0; i < threadCount; i++)
        {
            var worker = new Worker(prototype.Create(), i, pin);
            workers[i] = worker;

            threads[i] = new Thread(() => worker.Loop(options, start, stop, ready))
            {
                IsBackground = true,
                Name = $"pcspec-bench-{i}",
            };
            threads[i].Start();
        }

        // Todos calibrados: a janela de medição comeca agora.
        ready.Wait();
        start.Set();

        Thread.Sleep(Math.Max(1, options.DurationMs));
        stop.Set();

        foreach (var thread in threads)
        {
            if (!thread.Join(TimeSpan.FromSeconds(30)))
            {
                notes.Add("Alguma thread do benchmark não terminou no prazo; resultado pode estar subestimado.");
            }
        }

        var ops = workers.Sum(w => w.Ops);
        var seconds = workers.Max(w => w.LoopSeconds);

        if (workers.Any(w => !w.Finished))
        {
            notes.Add("Alguma thread do benchmark terminou por exceção; resultado pode estar subestimado.");
        }

        return (ops, seconds);
    }

    /// <summary>Descobre um tamanho de bloco que leva ~25 ms, para reduzir ruido de medição.</summary>
    private static long Calibrate(Workload workload, int warmupMs)
    {
        var chunk = workload.InitialChunk;

        var warmup = Stopwatch.StartNew();
        while (warmup.Elapsed.TotalMilliseconds < Math.Max(20, warmupMs))
        {
            workload.Run(chunk);
        }

        var probe = Stopwatch.StartNew();
        workload.Run(chunk);
        probe.Stop();

        if (probe.Elapsed.TotalMilliseconds > 0.05)
        {
            var scaled = chunk * (25.0 / probe.Elapsed.TotalMilliseconds);
            chunk = (long)Math.Clamp(scaled, workload.InitialChunk, workload.InitialChunk * 512L);
        }

        return Math.Max(1, chunk);
    }

    private static double GeometricMean(IEnumerable<double> values)
    {
        var list = values.Where(v => v > 0).ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var logSum = list.Sum(v => Math.Log(v));
        return Math.Exp(logSum / list.Count);
    }

    private sealed class Worker(Workload workload, int index, bool pin)
    {
        public double Ops { get; private set; }
        public double LoopSeconds { get; private set; }
        public bool Finished { get; private set; }

        public void Loop(BenchmarkOptions options, ManualResetEventSlim start, ManualResetEventSlim stop, CountdownEvent ready)
        {
            try
            {
                if (pin && index is >= 0 and < 64)
                {
                    try
                    {
                        NativeMethods.SetThreadAffinityMask(NativeMethods.GetCurrentThread(), (UIntPtr)(1UL << index));
                    }
                    catch
                    {
                        // Sem pinagem: segue com escalonamento normal.
                    }
                }

                var chunk = Calibrate(workload, options.WarmupMs);
                ready.Signal();

                start.Wait();

                var stopwatch = Stopwatch.StartNew();
                while (!stop.IsSet)
                {
                    Ops += workload.Run(chunk);
                }

                stopwatch.Stop();
                LoopSeconds = stopwatch.Elapsed.TotalSeconds;
                Finished = true;
            }
            catch
            {
                try
                {
                    ready.Signal();
                }
                catch
                {
                    // Ignorado: o CountdownEvent pode já ter chegado a zero.
                }

                Finished = true;
            }
        }
    }
}

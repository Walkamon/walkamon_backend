namespace BLL.Options;

public sealed class StepTrackingBenchmarkOptions
{
    public const string SectionName = "StepTrackingBenchmark";

    // Benchmark artifacts are additionally guarded by the ASP.NET Core
    // Development environment during DI registration.
    public bool Enabled { get; set; }
    public string ArtifactDirectory { get; set; } =
        "artifacts/step-tracking-benchmark";
    public string JsonlFileName { get; set; } = "step-benchmark.jsonl";
    public string CsvFileName { get; set; } = "step-benchmark-summary.csv";
}

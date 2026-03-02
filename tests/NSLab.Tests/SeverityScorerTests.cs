using System.Globalization;
using System.Text.Json;
using NSLab.Cli;
using NSLab.Core;

namespace NSLab.Tests;

public class SeverityScorerTests
{
    [Fact]
    public void Severity_OK_For_Clean_Data()
    {
        var rows = new List<TimeseriesRow>
        {
            new(0.0, 1.0, 2.0, 1.0, 0.0, 0.01, 0.2, "OK"),
            new(0.1, 1.2, 3.0, 1.5, 0.0, 0.01, 0.2, "OK"),
            new(0.2, 1.3, 4.0, 2.0, 0.0, 0.01, 0.2, "OK")
        };

        var result = SeverityScorerV0.Compute(rows);

        var expected = 2.0 + (0.1 * (2.0 - 1.0)) + (0.01 * 4.0);
        Assert.Equal(HealthStatus.OK, result.HealthStatus);
        Assert.Equal(expected, result.Severity, 12);
    }

    [Fact]
    public void Severity_WARN_For_WARN_Row()
    {
        var rows = new List<TimeseriesRow>
        {
            new(0.0, 1.0, 1.0, 0.5, 0.0, 0.01, 0.2, "OK"),
            new(0.1, 1.0, 1.2, 0.6, 0.0, 0.01, 0.2, "WARN")
        };

        var result = SeverityScorerV0.Compute(rows);

        Assert.Equal(HealthStatus.WARN_NUMERIC, result.HealthStatus);
    }

    [Fact]
    public void Severity_FAIL_For_FAIL_Row()
    {
        var rows = new List<TimeseriesRow>
        {
            new(0.0, 1.0, 1.0, 0.5, 0.0, 0.01, 0.2, "OK"),
            new(0.1, 1.0, 1.2, 0.6, 0.0, 0.01, 0.2, "FAIL")
        };

        var result = SeverityScorerV0.Compute(rows);

        Assert.Equal(HealthStatus.FAIL_NAN, result.HealthStatus);
        Assert.Equal(0.0, result.Severity);
    }

    [Fact]
    public async Task Summary_Uses_SeverityScorer()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir = CreateTempDirectory();

        try
        {
            var runExitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, runExitCode);

            var summarizeExitCode = await CliApp.BuildRootCommand().Parse(["summarize", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, summarizeExitCode);

            using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, RunFolderSpec.SummaryJson)));
            var maxOmega = summary.RootElement.GetProperty("max_omega_inf").GetDouble();
            var severity = summary.RootElement.GetProperty("severity_score_v0").GetDouble();
            var status = summary.RootElement.GetProperty("status").GetString();

            Assert.NotEqual(maxOmega, severity);
            Assert.Equal(HealthStatus.OK, status);
        }
        finally
        {
            DeleteDirectory(runDir);
        }
    }

    [Fact]
    public async Task Deterministic_Severity()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir1 = CreateTempDirectory();
        var runDir2 = CreateTempDirectory();

        try
        {
            var exitCode1 = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir1]).InvokeAsync();
            var exitCode2 = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir2]).InvokeAsync();

            Assert.Equal(ExitCodes.Ok, exitCode1);
            Assert.Equal(ExitCodes.Ok, exitCode2);

            using var summary1 = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir1, RunFolderSpec.SummaryJson)));
            using var summary2 = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir2, RunFolderSpec.SummaryJson)));

            var severity1 = summary1.RootElement.GetProperty("severity_score_v0").GetDouble();
            var severity2 = summary2.RootElement.GetProperty("severity_score_v0").GetDouble();

            Assert.Equal(severity1, severity2);
        }
        finally
        {
            DeleteDirectory(runDir1);
            DeleteDirectory(runDir2);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSLab.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nslab-test-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}

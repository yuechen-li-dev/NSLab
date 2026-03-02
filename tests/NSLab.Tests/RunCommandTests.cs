using System.Globalization;
using System.Text.Json;
using NSLab.Cli;
using NSLab.Core;

namespace NSLab.Tests;

public class RunCommandTests
{
    [Fact]
    public async Task RunCommand_Creates_Full_EvidencePack()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir = CreateTempDirectory();

        try
        {
            var exitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();

            Assert.Equal(ExitCodes.Ok, exitCode);
            Assert.True(File.Exists(Path.Combine(runDir, RunFolderSpec.MetadataJson)));
            Assert.True(File.Exists(Path.Combine(runDir, RunFolderSpec.SummaryJson)));
            var timeseriesPath = Path.Combine(runDir, RunFolderSpec.TimeseriesCsv);
            Assert.True(File.Exists(timeseriesPath));
            Assert.Equal(101, File.ReadLines(timeseriesPath).Count());
        }
        finally
        {
            DeleteDirectory(runDir);
        }
    }

    [Fact]
    public async Task RunCommand_Deterministic_Output_For_Same_Input()
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

            Assert.Equal(
                summary1.RootElement.GetProperty("run_id").GetString(),
                summary2.RootElement.GetProperty("run_id").GetString());
            Assert.Equal(
                summary1.RootElement.GetProperty("max_omega_inf").GetDouble(),
                summary2.RootElement.GetProperty("max_omega_inf").GetDouble());
            Assert.Equal(
                summary1.RootElement.GetProperty("t_at_max_omega_inf").GetDouble(),
                summary2.RootElement.GetProperty("t_at_max_omega_inf").GetDouble());
        }
        finally
        {
            DeleteDirectory(runDir1);
            DeleteDirectory(runDir2);
        }
    }

    [Fact]
    public async Task RunCommand_InvalidScenario_Returns_ValidationError()
    {
        var scenarioPath = Path.Combine(Path.GetTempPath(), "nslab-invalid-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".json");
        var runDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(scenarioPath, """
{
  "schema_version": "scenario_v0"
}
""");

            var exitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();

            Assert.Equal(ExitCodes.ValidationError, exitCode);
            Assert.False(File.Exists(Path.Combine(runDir, RunFolderSpec.MetadataJson)));
        }
        finally
        {
            if (File.Exists(scenarioPath))
            {
                File.Delete(scenarioPath);
            }

            DeleteDirectory(runDir);
        }
    }

    [Fact]
    public async Task RunCommand_Uses_Exact_Json_Text_For_RunId()
    {
        var sourceScenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var json1 = File.ReadAllText(sourceScenarioPath);
        using var doc = JsonDocument.Parse(json1);
        var json2 = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });

        var scenarioPath1 = Path.Combine(Path.GetTempPath(), "nslab-scenario-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + "-1.json");
        var scenarioPath2 = Path.Combine(Path.GetTempPath(), "nslab-scenario-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + "-2.json");
        var runDir1 = CreateTempDirectory();
        var runDir2 = CreateTempDirectory();

        try
        {
            File.WriteAllText(scenarioPath1, json1);
            File.WriteAllText(scenarioPath2, json2);

            var exitCode1 = await CliApp.BuildRootCommand().Parse(["run", scenarioPath1, "--out", runDir1]).InvokeAsync();
            var exitCode2 = await CliApp.BuildRootCommand().Parse(["run", scenarioPath2, "--out", runDir2]).InvokeAsync();

            Assert.Equal(ExitCodes.Ok, exitCode1);
            Assert.Equal(ExitCodes.Ok, exitCode2);

            using var summary1 = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir1, RunFolderSpec.SummaryJson)));
            using var summary2 = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir2, RunFolderSpec.SummaryJson)));

            Assert.NotEqual(
                summary1.RootElement.GetProperty("run_id").GetString(),
                summary2.RootElement.GetProperty("run_id").GetString());
        }
        finally
        {
            if (File.Exists(scenarioPath1))
            {
                File.Delete(scenarioPath1);
            }

            if (File.Exists(scenarioPath2))
            {
                File.Delete(scenarioPath2);
            }

            DeleteDirectory(runDir1);
            DeleteDirectory(runDir2);
        }
    }

    [Fact]
    public async Task Timeseries_Rows_Are_Strictly_OK_Status()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir = CreateTempDirectory();

        try
        {
            var exitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, exitCode);

            var lines = File.ReadLines(Path.Combine(runDir, RunFolderSpec.TimeseriesCsv)).Skip(1);
            foreach (var line in lines)
            {
                Assert.EndsWith(",OK", line, StringComparison.Ordinal);
            }
        }
        finally
        {
            DeleteDirectory(runDir);
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

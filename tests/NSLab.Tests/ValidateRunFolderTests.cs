using System.Diagnostics;
using System.Globalization;
using NSLab.Cli;
using NSLab.Core;

namespace NSLab.Tests;

public class ValidateRunFolderTests
{
    [Fact]
    public async Task ValidateRunFolder_Ok_For_Run_Output()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir = CreateTempDirectory();

        try
        {
            var runExitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, runExitCode);

            var (exitCode, output) = await InvokeCli("validate", runDir);
            Assert.Equal(ExitCodes.Ok, exitCode);
            Assert.Contains("OK", output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(runDir);
        }
    }

    [Fact]
    public async Task ValidateRunFolder_Fails_On_Bad_Timeseries_Header()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir = CreateTempDirectory();

        try
        {
            var runExitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, runExitCode);

            var timeseriesPath = Path.Combine(runDir, RunFolderSpec.TimeseriesCsv);
            var lines = File.ReadAllLines(timeseriesPath);
            lines[0] = "bad,header";
            File.WriteAllLines(timeseriesPath, lines);

            var (exitCode, output) = await InvokeCli("validate", runDir);
            Assert.Equal(ExitCodes.ValidationError, exitCode);
            Assert.Contains("/timeseries/header", output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(runDir);
        }
    }

    [Fact]
    public async Task Summarize_Prints_Deterministic_Line()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir = CreateTempDirectory();

        try
        {
            var runExitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, runExitCode);

            var (exitCode1, output1) = await InvokeCli("summarize", runDir);
            var (exitCode2, output2) = await InvokeCli("summarize", runDir);

            Assert.Equal(ExitCodes.Ok, exitCode1);
            Assert.Equal(ExitCodes.Ok, exitCode2);
            Assert.Equal(output1, output2);
        }
        finally
        {
            DeleteDirectory(runDir);
        }
    }

    [Fact]
    public async Task EnsureSummary_Recomputes_When_Missing()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir = CreateTempDirectory();

        try
        {
            var runExitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, runExitCode);

            File.Delete(Path.Combine(runDir, RunFolderSpec.SummaryJson));

            var (summarizeExitCode, _) = await InvokeCli("summarize", runDir);
            Assert.Equal(ExitCodes.Ok, summarizeExitCode);
            Assert.True(File.Exists(Path.Combine(runDir, RunFolderSpec.SummaryJson)));

            var (validateExitCode, validateOutput) = await InvokeCli("validate", runDir);
            Assert.Equal(ExitCodes.Ok, validateExitCode);
            Assert.Contains("OK", validateOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(runDir);
        }
    }

    [Fact]
    public async Task EnsureSummary_Does_Not_Change_When_Present()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var runDir = CreateTempDirectory();

        try
        {
            var runExitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, runExitCode);

            var summaryPath = Path.Combine(runDir, RunFolderSpec.SummaryJson);
            var before = await File.ReadAllTextAsync(summaryPath);

            var (summarizeExitCode, _) = await InvokeCli("summarize", runDir);
            Assert.Equal(ExitCodes.Ok, summarizeExitCode);

            var after = await File.ReadAllTextAsync(summaryPath);
            Assert.Equal(before, after);
        }
        finally
        {
            DeleteDirectory(runDir);
        }
    }

    private static async Task<(int ExitCode, string Output)> InvokeCli(string command, string path)
    {
        var cliDllPath = typeof(CliApp).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            Arguments = $"\"{cliDllPath}\" {command} \"{path}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var stdOut = await process!.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, $"{stdOut}\n{stdErr}");
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

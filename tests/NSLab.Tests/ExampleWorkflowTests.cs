using System.Diagnostics;
using System.Globalization;
using NSLab.Cli;
using NSLab.Core;

namespace NSLab.Tests;

public class ExampleWorkflowTests
{
    [Fact]
    public async Task ExampleWorkflow_Run_Validate_Summarize()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_sweepable.json");
        Assert.True(File.Exists(scenarioPath));

        var runDir = CreateTempDirectory();

        try
        {
            var (validateScenarioExitCode, _) = await InvokeCli("validate", scenarioPath);
            Assert.Equal(ExitCodes.Ok, validateScenarioExitCode);

            var runExitCode = await CliApp.BuildRootCommand().Parse(["run", scenarioPath, "--out", runDir]).InvokeAsync();
            Assert.Equal(ExitCodes.Ok, runExitCode);

            var (validateRunExitCode, validateRunOutput) = await InvokeCli("validate", runDir);
            Assert.Equal(ExitCodes.Ok, validateRunExitCode);
            Assert.Contains("OK", validateRunOutput, StringComparison.Ordinal);

            var (summarizeExitCode, summarizeOutput) = await InvokeCli("summarize", runDir);
            Assert.Equal(ExitCodes.Ok, summarizeExitCode);
            Assert.Contains("run_id=", summarizeOutput, StringComparison.Ordinal);
            Assert.Contains("severity=", summarizeOutput, StringComparison.Ordinal);

            Assert.True(File.Exists(Path.Combine(runDir, RunFolderSpec.MetadataJson)));
            Assert.True(File.Exists(Path.Combine(runDir, RunFolderSpec.SummaryJson)));
            Assert.True(File.Exists(Path.Combine(runDir, RunFolderSpec.TimeseriesCsv)));
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

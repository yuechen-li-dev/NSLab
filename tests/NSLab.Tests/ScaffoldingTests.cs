using System.Diagnostics;
using NSLab.Core;

namespace NSLab.Tests;

public class ScaffoldingTests
{
    [Fact]
    public void SchemaVersions_Constants_AreExact()
    {
        Assert.Equal("nslab.scenario.v0", SchemaVersions.ScenarioV0);
        Assert.Equal("nslab.summary.v0", SchemaVersions.SummaryV0);
        Assert.Equal("nslab.evidence.v0", SchemaVersions.EvidenceV0);
    }

    [Fact]
    public void RunFolderSpec_Filenames_AreExact()
    {
        Assert.Equal("metadata.json", RunFolderSpec.MetadataJson);
        Assert.Equal("summary.json", RunFolderSpec.SummaryJson);
        Assert.Equal("timeseries.csv", RunFolderSpec.TimeseriesCsv);
        Assert.Equal("spectra.csv", RunFolderSpec.SpectraCsv);
        Assert.Equal("snapshots", RunFolderSpec.SnapshotsDir);
        Assert.Equal("log.txt", RunFolderSpec.LogTxt);
    }

    [Fact]
    public void ExitCodes_AreExact()
    {
        Assert.Equal(0, ExitCodes.Ok);
        Assert.Equal(2, ExitCodes.ValidationError);
        Assert.Equal(3, ExitCodes.RuntimeError);
    }

    [Fact]
    public async Task Cli_Help_Includes_Commands()
    {
        var cliDllPath = typeof(NSLab.Cli.CliApp).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            Arguments = $"\"{cliDllPath}\" --help",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var stdOut = await process!.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        var output = $"{stdOut}\n{stdErr}";
        Assert.Contains("run", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("summarize", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validate", output, StringComparison.OrdinalIgnoreCase);
    }
}

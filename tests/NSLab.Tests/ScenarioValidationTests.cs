using System.Diagnostics;
using NSLab.Core;

namespace NSLab.Tests;

public class ScenarioValidationTests
{
    [Fact]
    public void Scenario_ParseAndValidate_Minimal_Valid()
    {
        var json = ValidScenarioJson();

        var (scenario, result) = ScenarioV0Parser.ParseAndValidate(json);

        Assert.True(result.IsValid);
        Assert.NotNull(scenario);
    }

    [Fact]
    public void Scenario_Fails_On_UnknownField()
    {
        var json = ValidScenarioJson("\n  \"extra\": 1");

        var (scenario, result) = ScenarioV0Parser.ParseAndValidate(json);

        Assert.Null(scenario);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "UnknownField");
    }

    [Fact]
    public void Scenario_Fails_On_WrongSchemaVersion()
    {
        var json = ValidScenarioJson().Replace("nslab.scenario.v0", "nslab.scenario.v1", StringComparison.Ordinal);

        var (scenario, result) = ScenarioV0Parser.ParseAndValidate(json);

        Assert.Null(scenario);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "SchemaVersionMismatch");
    }

    [Fact]
    public void Scenario_Fails_On_BadTimestamp()
    {
        var json = ValidScenarioJson().Replace("2026-03-01T00:00:00Z", "2026-03-01T00:00:00", StringComparison.Ordinal);

        var (scenario, result) = ScenarioV0Parser.ParseAndValidate(json);

        Assert.Null(scenario);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "InvalidValue" && issue.Path == "/experiment/utc_timestamp");
    }

    [Fact]
    public async Task Cli_Validate_Returns_0_On_Valid_File()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, ValidScenarioJson());

        try
        {
            var (exitCode, output) = await InvokeCliValidate(file);
            Assert.Equal(0, exitCode);
            Assert.Contains("OK", output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Cli_Validate_Returns_2_On_Invalid_File()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, ValidScenarioJson().Replace("\"solver\": \"null\",\n", string.Empty, StringComparison.Ordinal));

        try
        {
            var (exitCode, output) = await InvokeCliValidate(file);
            Assert.Equal(2, exitCode);
            Assert.Contains("MissingField", output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static async Task<(int ExitCode, string Output)> InvokeCliValidate(string scenarioPath)
    {
        var cliDllPath = typeof(NSLab.Cli.CliApp).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            Arguments = $"\"{cliDllPath}\" validate \"{scenarioPath}\"",
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

    private static string ValidScenarioJson(string? extraTopLevelProperty = null)
    {
        var extra = string.IsNullOrEmpty(extraTopLevelProperty) ? string.Empty : $",{extraTopLevelProperty}";
        return $$"""
{
  "schema_version": "nslab.scenario.v0",
  "experiment": {
    "llm_name": "ExampleLLM",
    "run_number": 1,
    "utc_timestamp": "2026-03-01T00:00:00Z"
  },
  "solver": "null",
  "domain": {
    "type": "periodic_box",
    "L": [6.283185307179586, 6.283185307179586]
  },
  "resolution": {
    "N": [128, 128]
  },
  "time": {
    "t_end": 1.0,
    "cfl": 0.4,
    "max_steps": 200000
  },
  "physics": {
    "nu": 0.001
  },
  "initial_condition": {
    "type": "smooth_random_vorticity_bandlimited",
    "params": {
      "k0": 6,
      "k1": 12,
      "amplitude": 1.0
    }
  },
  "numerics": {
    "advection": "spectral_dealiased_2_3",
    "integrator": "rk3"
  },
  "outputs": {
    "timeseries_dt": 0.01,
    "snapshots": []
  },
  "seed": 1337{{extra}}
}
""";
    }
}

using System.Globalization;
using System.Text.Json;
using NSLab.Core;

namespace NSLab.Tests;

public class EvidencePackTests
{
    [Fact]
    public void EvidenceSchemaVersions_AreConsistent_With_SchemaVersions()
    {
        Assert.Equal(SchemaVersions.SummaryV0, EvidenceSchemaVersions.SummaryV0);
        Assert.Equal(SchemaVersions.EvidenceV0, EvidenceSchemaVersions.EvidencePackV0);
    }

    [Fact]
    public void Timeseries_Header_IsExact()
    {
        Assert.Equal("t,E,Z,omega_inf,div_l2,dt,cfl,status", TimeseriesCsv.HeaderExact);
    }

    [Fact]
    public void EvidencePackWriter_Creates_Required_Files_And_Dirs()
    {
        var runDir = Path.Combine(Path.GetTempPath(), "nslab-test-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            EvidencePackWriter.InitializeRunFolder(runDir);

            Assert.True(Directory.Exists(runDir));
            Assert.True(Directory.Exists(Path.Combine(runDir, RunFolderSpec.SnapshotsDir)));

            var timeseriesPath = Path.Combine(runDir, RunFolderSpec.TimeseriesCsv);
            Assert.True(File.Exists(timeseriesPath));
            var firstLine = File.ReadLines(timeseriesPath).First();
            Assert.Equal(TimeseriesCsv.HeaderExact, firstLine);
        }
        finally
        {
            if (Directory.Exists(runDir))
            {
                Directory.Delete(runDir, true);
            }
        }
    }

    [Fact]
    public void EvidencePackWriter_Writes_Metadata_And_Summary_With_Traceability()
    {
        var scenarioPath = Path.Combine(FindRepoRoot(), "examples", "scenario_minimal.json");
        var jsonText = File.ReadAllText(scenarioPath);
        var (scenario, result) = ScenarioV0Parser.ParseAndValidate(jsonText);
        Assert.True(result.IsValid);
        Assert.NotNull(scenario);

        var runId = RunId.FromScenarioJson(jsonText);
        var info = new EvidenceRunInfo(
            runId,
            "null",
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            123,
            "test",
            "OK",
            null);

        var runDir = Path.Combine(Path.GetTempPath(), "nslab-test-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            EvidencePackWriter.InitializeRunFolder(runDir);
            EvidencePackWriter.WriteMetadata(runDir, scenario!, info);
            var summary = EvidencePackWriter.CreateEmptySummary(scenario!, info);
            EvidencePackWriter.WriteSummary(runDir, scenario!, summary);

            using var metadataJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, RunFolderSpec.MetadataJson)));
            using var summaryJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, RunFolderSpec.SummaryJson)));

            Assert.Equal(EvidenceSchemaVersions.MetadataV0, metadataJson.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal(EvidenceSchemaVersions.SummaryV0, summaryJson.RootElement.GetProperty("schema_version").GetString());

            Assert.Equal(runId, metadataJson.RootElement.GetProperty("run_id").GetString());
            Assert.Equal(runId, summaryJson.RootElement.GetProperty("run_id").GetString());
            Assert.Equal("null", metadataJson.RootElement.GetProperty("solver_id").GetString());
            Assert.Equal("null", summaryJson.RootElement.GetProperty("solver_id").GetString());

            var metadataExperiment = metadataJson.RootElement.GetProperty("scenario").GetProperty("experiment");
            var summaryExperiment = summaryJson.RootElement.GetProperty("experiment");
            Assert.Equal("ExampleLLM", metadataExperiment.GetProperty("llm_name").GetString());
            Assert.Equal(1, metadataExperiment.GetProperty("run_number").GetInt32());
            Assert.Equal("2026-03-01T00:00:00Z", metadataExperiment.GetProperty("utc_timestamp").GetString());
            Assert.Equal("ExampleLLM", summaryExperiment.GetProperty("llm_name").GetString());
            Assert.Equal(1, summaryExperiment.GetProperty("run_number").GetInt32());
            Assert.Equal("2026-03-01T00:00:00Z", summaryExperiment.GetProperty("utc_timestamp").GetString());

            Assert.True(metadataJson.RootElement.GetProperty("defaults_expanded").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(runDir))
            {
                Directory.Delete(runDir, true);
            }
        }
    }

    [Fact]
    public void Timeseries_AppendRow_Writes_Invariant_Format()
    {
        var csvPath = Path.Combine(Path.GetTempPath(), "nslab-test-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".csv");
        try
        {
            TimeseriesCsv.WriteHeader(csvPath);
            TimeseriesCsv.AppendRow(csvPath, new TimeseriesRow(1.25, 2.5, 3.75, 4.125, 0.5, 0.01, 0.4, "OK"));

            var lastLine = File.ReadLines(csvPath).Last();
            var fields = lastLine.Split(',');

            Assert.Equal(8, fields.Length);
            Assert.Equal("OK", fields[7]);
            for (var i = 0; i < 7; i++)
            {
                Assert.DoesNotContain(',', fields[i]);
            }
        }
        finally
        {
            if (File.Exists(csvPath))
            {
                File.Delete(csvPath);
            }
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
}

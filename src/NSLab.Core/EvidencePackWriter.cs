using System.Text.Json;

namespace NSLab.Core;

public static class EvidencePackWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null
    };

    public static void InitializeRunFolder(string runDir)
    {
        Directory.CreateDirectory(runDir);
        Directory.CreateDirectory(Path.Combine(runDir, RunFolderSpec.SnapshotsDir));
        TimeseriesCsv.WriteHeader(Path.Combine(runDir, RunFolderSpec.TimeseriesCsv));
    }

    public static void WriteMetadata(string runDir, ScenarioV0 scenario, EvidenceRunInfo info)
    {
        var metadata = new MetadataV0Document(
            EvidenceSchemaVersions.MetadataV0,
            EvidenceSchemaVersions.EvidencePackV0,
            info.RunId,
            info.SolverId,
            info.Status,
            info.Reason ?? string.Empty,
            info.StartedUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            info.DurationMs,
            info.Platform,
            scenario.SchemaVersion,
            scenario,
            true);

        var json = JsonSerializer.Serialize(metadata, WriteOptions);
        File.WriteAllText(Path.Combine(runDir, RunFolderSpec.MetadataJson), json);
    }

    public static void WriteSummary(string runDir, ScenarioV0 scenario, SummaryV0Document summary)
    {
        _ = scenario;
        var json = JsonSerializer.Serialize(summary, WriteOptions);
        File.WriteAllText(Path.Combine(runDir, RunFolderSpec.SummaryJson), json);
    }

    public static SummaryV0Document CreateEmptySummary(ScenarioV0 scenario, EvidenceRunInfo info)
    {
        return new SummaryV0Document(
            EvidenceSchemaVersions.SummaryV0,
            scenario.Experiment,
            string.Empty,
            info.RunId,
            info.SolverId,
            info.Status,
            info.Reason ?? string.Empty,
            0,
            0,
            0,
            0,
            0);
    }
}

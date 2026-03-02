using System.Globalization;
using System.Text.Json;

namespace NSLab.Core;

public static class EvidencePackSummarizer
{
    public static SummaryV0Document ComputeSummaryFromTimeseries(string runDir)
    {
        var metadataPath = Path.Combine(runDir, RunFolderSpec.MetadataJson);
        var timeseriesPath = Path.Combine(runDir, RunFolderSpec.TimeseriesCsv);

        using var metadataDoc = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var metadata = metadataDoc.RootElement;

        var runId = metadata.GetProperty("run_id").GetString() ?? string.Empty;
        var solverId = metadata.GetProperty("solver_id").GetString() ?? string.Empty;
        var scenario = metadata.GetProperty("scenario");
        var experiment = scenario.GetProperty("experiment");
        var trace = new ExperimentTrace(
            experiment.GetProperty("llm_name").GetString() ?? string.Empty,
            experiment.GetProperty("run_number").GetInt32(),
            experiment.GetProperty("utc_timestamp").GetString() ?? string.Empty);

        var lines = File.ReadAllLines(timeseriesPath);
        if (lines.Length < 2)
        {
            throw new InvalidDataException("Timeseries must include at least one data row.");
        }

        if (!string.Equals(lines[0], TimeseriesCsv.HeaderExact, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Timeseries header mismatch.");
        }

        var maxOmegaInf = double.MinValue;
        var tAtMaxOmegaInf = 0d;
        var maxZ = double.MinValue;
        var maxE = double.MinValue;
        var rows = new List<TimeseriesRow>();

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var parts = lines[i].Split(',');
            if (parts.Length != 8)
            {
                throw new InvalidDataException("Timeseries row must have 8 columns.");
            }

            var t = double.Parse(parts[0], CultureInfo.InvariantCulture);
            var e = double.Parse(parts[1], CultureInfo.InvariantCulture);
            var z = double.Parse(parts[2], CultureInfo.InvariantCulture);
            var omegaInf = double.Parse(parts[3], CultureInfo.InvariantCulture);
            var divL2 = double.Parse(parts[4], CultureInfo.InvariantCulture);
            var dt = double.Parse(parts[5], CultureInfo.InvariantCulture);
            var cfl = double.Parse(parts[6], CultureInfo.InvariantCulture);
            var rowStatus = parts[7];

            rows.Add(new TimeseriesRow(t, e, z, omegaInf, divL2, dt, cfl, rowStatus));

            if (omegaInf > maxOmegaInf)
            {
                maxOmegaInf = omegaInf;
                tAtMaxOmegaInf = t;
            }

            if (z > maxZ)
            {
                maxZ = z;
            }

            if (e > maxE)
            {
                maxE = e;
            }
        }

        if (maxOmegaInf == double.MinValue)
        {
            throw new InvalidDataException("Timeseries must include at least one non-empty data row.");
        }

        var (severity, healthStatus) = SeverityScorerV0.Compute(rows);
        var reason = string.Equals(healthStatus, HealthStatus.OK, StringComparison.Ordinal)
            ? string.Empty
            : healthStatus;

        return new SummaryV0Document(
            EvidenceSchemaVersions.SummaryV0,
            trace,
            string.Empty,
            runId,
            solverId,
            healthStatus,
            reason,
            maxOmegaInf,
            tAtMaxOmegaInf,
            maxZ,
            maxE,
            severity);
    }

    public static SummaryV0Document EnsureSummary(string runDir)
    {
        var summaryPath = Path.Combine(runDir, RunFolderSpec.SummaryJson);
        if (File.Exists(summaryPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(summaryPath));
                if (doc.RootElement.TryGetProperty("schema_version", out var schema)
                    && schema.ValueKind == JsonValueKind.String
                    && string.Equals(schema.GetString(), EvidenceSchemaVersions.SummaryV0, StringComparison.Ordinal))
                {
                    var existing = JsonSerializer.Deserialize<SummaryV0Document>(doc.RootElement.GetRawText(), ScenarioJsonOptionsFactory.CreateStrict());
                    if (existing is not null)
                    {
                        return existing;
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        var summary = ComputeSummaryFromTimeseries(runDir);
        using var metadataDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, RunFolderSpec.MetadataJson)));
        var scenario = JsonSerializer.Deserialize<ScenarioV0>(metadataDoc.RootElement.GetProperty("scenario").GetRawText(), ScenarioJsonOptionsFactory.CreateStrict())
            ?? throw new InvalidDataException("Scenario is invalid in metadata.");
        EvidencePackWriter.WriteSummary(runDir, scenario, summary);
        return summary;
    }
}

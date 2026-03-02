using System.Globalization;
using System.Text.Json;

namespace NSLab.Core;

public static class EvidencePackValidator
{
    private static readonly string[] UtcFormats =
    {
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"
    };

    public static ValidationResult ValidateRunFolder(string runDir)
    {
        var issues = new List<ValidationIssue>();

        if (!Directory.Exists(runDir))
        {
            issues.Add(new ValidationIssue("InvalidValue", "Run folder does not exist.", "/run_dir"));
            return new ValidationResult(false, issues);
        }

        var metadataPath = Path.Combine(runDir, RunFolderSpec.MetadataJson);
        var summaryPath = Path.Combine(runDir, RunFolderSpec.SummaryJson);
        var timeseriesPath = Path.Combine(runDir, RunFolderSpec.TimeseriesCsv);
        var snapshotsPath = Path.Combine(runDir, RunFolderSpec.SnapshotsDir);

        RequireFile(metadataPath, "/metadata", issues);
        RequireFile(summaryPath, "/summary", issues);
        RequireFile(timeseriesPath, "/timeseries", issues);

        if (File.Exists(snapshotsPath))
        {
            issues.Add(new ValidationIssue("InvalidValue", "Snapshots path must be a directory when present.", "/snapshots"));
        }
        else if (Directory.Exists(snapshotsPath) is false && Path.Exists(snapshotsPath))
        {
            issues.Add(new ValidationIssue("InvalidValue", "Snapshots path must be a directory when present.", "/snapshots"));
        }

        ValidateMetadata(metadataPath, issues);
        ValidateSummary(summaryPath, issues);
        ValidateTimeseries(timeseriesPath, issues);

        return new ValidationResult(issues.Count == 0, issues);
    }

    private static void RequireFile(string path, string pointer, List<ValidationIssue> issues)
    {
        if (!File.Exists(path))
        {
            issues.Add(new ValidationIssue("MissingField", "Required file is missing.", pointer));
        }
    }

    private static void ValidateMetadata(string metadataPath, List<ValidationIssue> issues)
    {
        if (!File.Exists(metadataPath))
        {
            return;
        }

        JsonDocument? metadata = null;
        try
        {
            metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue("JsonParseError", ex.Message, "/metadata"));
            return;
        }

        using (metadata)
        {
            var root = metadata.RootElement;
            ValidateSchemaVersion(root, "schema_version", EvidenceSchemaVersions.MetadataV0, "/metadata/schema_version", issues);
            ValidateSchemaVersion(root, "evidence_schema_version", EvidenceSchemaVersions.EvidencePackV0, "/metadata/evidence_schema_version", issues);

            var required = new[]
            {
                "run_id", "solver_id", "status", "started_utc", "duration_ms", "platform",
                "scenario_schema_version", "scenario", "defaults_expanded"
            };

            foreach (var key in required)
            {
                if (!root.TryGetProperty(key, out _))
                {
                    issues.Add(new ValidationIssue("MissingField", "Required field is missing.", $"/metadata/{key}"));
                }
            }
        }
    }

    private static void ValidateSummary(string summaryPath, List<ValidationIssue> issues)
    {
        if (!File.Exists(summaryPath))
        {
            return;
        }

        JsonDocument? summary = null;
        try
        {
            summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue("JsonParseError", ex.Message, "/summary"));
            return;
        }

        using (summary)
        {
            var root = summary.RootElement;
            ValidateSchemaVersion(root, "schema_version", EvidenceSchemaVersions.SummaryV0, "/summary/schema_version", issues);

            var required = new[]
            {
                "run_id", "solver_id", "status", "reason", "max_omega_inf", "t_at_max_omega_inf",
                "max_Z", "max_E", "severity_score_v0", "experiment", "block"
            };

            foreach (var key in required)
            {
                if (!root.TryGetProperty(key, out _))
                {
                    issues.Add(new ValidationIssue("MissingField", "Required field is missing.", $"/summary/{key}"));
                }
            }

            if (root.TryGetProperty("experiment", out var experiment) && experiment.ValueKind == JsonValueKind.Object)
            {
                ValidateExperimentTrace(experiment, "/summary/experiment", issues);
            }
            else if (root.TryGetProperty("experiment", out _))
            {
                issues.Add(new ValidationIssue("InvalidValue", "Experiment must be an object.", "/summary/experiment"));
            }
        }
    }

    private static void ValidateExperimentTrace(JsonElement experiment, string basePath, List<ValidationIssue> issues)
    {
        if (!experiment.TryGetProperty("llm_name", out var llmName))
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", $"{basePath}/llm_name"));
        }
        else if (llmName.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(llmName.GetString()))
        {
            issues.Add(new ValidationIssue("InvalidValue", "Value must be non-empty.", $"{basePath}/llm_name"));
        }

        if (!experiment.TryGetProperty("run_number", out var runNumber))
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", $"{basePath}/run_number"));
        }
        else if (runNumber.ValueKind != JsonValueKind.Number || !runNumber.TryGetInt32(out var n) || n < 1)
        {
            issues.Add(new ValidationIssue("InvalidValue", "Value must be >= 1.", $"{basePath}/run_number"));
        }

        if (!experiment.TryGetProperty("utc_timestamp", out var timestamp))
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", $"{basePath}/utc_timestamp"));
        }
        else if (timestamp.ValueKind != JsonValueKind.String || !IsValidUtcTimestamp(timestamp.GetString()))
        {
            issues.Add(new ValidationIssue("InvalidValue", "Value must be ISO 8601 UTC with Z suffix.", $"{basePath}/utc_timestamp"));
        }
    }

    private static bool IsValidUtcTimestamp(string? value)
    {
        if (value is null)
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(value, UtcFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ts))
        {
            return false;
        }

        return ts.Offset == TimeSpan.Zero && value.EndsWith("Z", StringComparison.Ordinal);
    }

    private static void ValidateSchemaVersion(JsonElement root, string propertyName, string expected, string path, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var actual))
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", path));
            return;
        }

        if (actual.ValueKind != JsonValueKind.String || !string.Equals(actual.GetString(), expected, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue("SchemaVersionMismatch", $"Expected '{expected}'.", path));
        }
    }

    private static void ValidateTimeseries(string timeseriesPath, List<ValidationIssue> issues)
    {
        if (!File.Exists(timeseriesPath))
        {
            return;
        }

        var lines = File.ReadAllLines(timeseriesPath);
        if (lines.Length == 0)
        {
            issues.Add(new ValidationIssue("InvalidValue", "Timeseries must include header and at least one row.", "/timeseries/header"));
            return;
        }

        if (!string.Equals(lines[0], TimeseriesCsv.HeaderExact, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue("InvalidValue", "Timeseries header mismatch.", "/timeseries/header"));
        }

        if (lines.Length < 2)
        {
            issues.Add(new ValidationIssue("InvalidValue", "Timeseries must include at least one data row.", "/timeseries/rows"));
            return;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                issues.Add(new ValidationIssue("InvalidValue", "Data row must be non-empty.", $"/timeseries/rows/{i}"));
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length != 8)
            {
                issues.Add(new ValidationIssue("InvalidValue", "Data row must contain exactly 8 fields.", $"/timeseries/rows/{i}"));
                continue;
            }

            var status = parts[7];
            if (!string.Equals(status, "OK", StringComparison.Ordinal)
                && !string.Equals(status, "WARN", StringComparison.Ordinal)
                && !string.Equals(status, "FAIL", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue("InvalidValue", "Status must be OK WARN or FAIL.", $"/timeseries/rows/{i}/status"));
            }
        }
    }
}

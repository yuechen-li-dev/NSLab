using System.Text.Json.Serialization;

namespace NSLab.Core;

public sealed record EvidenceRunInfo(
    string RunId,
    string SolverId,
    DateTimeOffset StartedUtc,
    int DurationMs,
    string Platform,
    string Status,
    string? Reason
);

public sealed record SummaryV0Document(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("experiment")] ExperimentTrace Experiment,
    [property: JsonPropertyName("block")] string Block,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("solver_id")] string SolverId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("max_omega_inf")] double MaxOmegaInf,
    [property: JsonPropertyName("t_at_max_omega_inf")] double TAtMaxOmegaInf,
    [property: JsonPropertyName("max_Z")] double MaxZ,
    [property: JsonPropertyName("max_E")] double MaxE,
    [property: JsonPropertyName("severity_score_v0")] double SeverityScoreV0
);

public sealed record MetadataV0Document(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("evidence_schema_version")] string EvidenceSchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("solver_id")] string SolverId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("started_utc")] string StartedUtc,
    [property: JsonPropertyName("duration_ms")] int DurationMs,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("scenario_schema_version")] string ScenarioSchemaVersion,
    [property: JsonPropertyName("scenario")] ScenarioV0 Scenario,
    [property: JsonPropertyName("defaults_expanded")] bool DefaultsExpanded
);

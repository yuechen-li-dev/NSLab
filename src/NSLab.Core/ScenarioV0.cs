using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSLab.Core;

public sealed record ScenarioV0(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("experiment")] ExperimentTrace Experiment,
    [property: JsonPropertyName("solver")] string Solver,
    [property: JsonPropertyName("domain")] DomainSpec Domain,
    [property: JsonPropertyName("resolution")] ResolutionSpec Resolution,
    [property: JsonPropertyName("time")] TimeSpec Time,
    [property: JsonPropertyName("physics")] PhysicsSpec Physics,
    [property: JsonPropertyName("initial_condition")] InitialConditionSpec InitialCondition,
    [property: JsonPropertyName("numerics")] NumericsSpec Numerics,
    [property: JsonPropertyName("outputs")] OutputsSpec Outputs,
    [property: JsonPropertyName("seed")] int Seed
);

public sealed record ExperimentTrace(
    [property: JsonPropertyName("llm_name")] string LlmName,
    [property: JsonPropertyName("run_number")] int RunNumber,
    [property: JsonPropertyName("utc_timestamp")] string UtcTimestamp
);

public sealed record DomainSpec(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("L")] double[] L
);

public sealed record ResolutionSpec(
    [property: JsonPropertyName("N")] int[] N
);

public sealed record TimeSpec(
    [property: JsonPropertyName("t_end")] double TEnd,
    [property: JsonPropertyName("cfl")] double Cfl,
    [property: JsonPropertyName("max_steps")] int MaxSteps
);

public sealed record PhysicsSpec(
    [property: JsonPropertyName("nu")] double Nu
);

public sealed record InitialConditionSpec(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("params")] JsonElement Params
);

public sealed record NumericsSpec(
    [property: JsonPropertyName("advection")] string Advection,
    [property: JsonPropertyName("integrator")] string Integrator
);

public sealed record OutputsSpec(
    [property: JsonPropertyName("timeseries_dt")] double TimeseriesDt,
    [property: JsonPropertyName("snapshots")] double[] Snapshots
);

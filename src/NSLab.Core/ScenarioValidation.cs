using System.Globalization;
using System.Text.Json;

namespace NSLab.Core;

public static class ScenarioJsonOptionsFactory
{
    public static JsonSerializerOptions CreateStrict()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false
        };
    }
}

public sealed record ValidationIssue(string Code, string Message, string Path);

public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues);

public static class ScenarioV0Validator
{
    private static readonly string[] UtcFormats =
    {
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"
    };

    public static ValidationResult Validate(ScenarioV0 scenario)
    {
        var issues = new List<ValidationIssue>();

        if (scenario.SchemaVersion is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/schema_version"));
        }
        else if (!string.Equals(scenario.SchemaVersion, SchemaVersions.ScenarioV0, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue("SchemaVersionMismatch", $"Expected '{SchemaVersions.ScenarioV0}'.", "/schema_version"));
        }

        if (scenario.Experiment is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/experiment"));
        }
        else
        {
            if (scenario.Experiment.LlmName is null)
            {
                issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/experiment/llm_name"));
            }
            else if (string.IsNullOrWhiteSpace(scenario.Experiment.LlmName))
            {
                issues.Add(new ValidationIssue("InvalidValue", "Value must be non-empty.", "/experiment/llm_name"));
            }

            if (scenario.Experiment.RunNumber < 1)
            {
                issues.Add(new ValidationIssue("InvalidValue", "Value must be >= 1.", "/experiment/run_number"));
            }

            if (scenario.Experiment.UtcTimestamp is null)
            {
                issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/experiment/utc_timestamp"));
            }
            else if (!DateTimeOffset.TryParseExact(
                         scenario.Experiment.UtcTimestamp,
                         UtcFormats,
                         CultureInfo.InvariantCulture,
                         DateTimeStyles.AssumeUniversal,
                         out var timestamp)
                     || timestamp.Offset != TimeSpan.Zero
                     || !scenario.Experiment.UtcTimestamp.EndsWith("Z", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue("InvalidValue", "Value must be ISO 8601 UTC with Z suffix.", "/experiment/utc_timestamp"));
            }
        }

        ValidateNonEmptyString(issues, scenario.Solver, "/solver");

        if (scenario.Domain is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/domain"));
        }
        else
        {
            ValidateNonEmptyString(issues, scenario.Domain.Type, "/domain/type");

            if (scenario.Domain.L is null)
            {
                issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/domain/L"));
            }
            else
            {
                if (scenario.Domain.L.Length < 1)
                {
                    issues.Add(new ValidationIssue("InvalidValue", "Array length must be >= 1.", "/domain/L"));
                }

                for (var i = 0; i < scenario.Domain.L.Length; i++)
                {
                    if (scenario.Domain.L[i] <= 0)
                    {
                        issues.Add(new ValidationIssue("InvalidValue", "Elements must be > 0.", $"/domain/L/{i}"));
                    }
                }
            }
        }

        if (scenario.Resolution is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/resolution"));
        }
        else if (scenario.Resolution.N is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/resolution/N"));
        }
        else
        {
            if (scenario.Resolution.N.Length < 1)
            {
                issues.Add(new ValidationIssue("InvalidValue", "Array length must be >= 1.", "/resolution/N"));
            }

            for (var i = 0; i < scenario.Resolution.N.Length; i++)
            {
                if (scenario.Resolution.N[i] < 4)
                {
                    issues.Add(new ValidationIssue("InvalidValue", "Elements must be >= 4.", $"/resolution/N/{i}"));
                }
            }
        }

        if (scenario.Time is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/time"));
        }
        else
        {
            if (scenario.Time.TEnd <= 0)
            {
                issues.Add(new ValidationIssue("InvalidValue", "Value must be > 0.", "/time/t_end"));
            }

            if (scenario.Time.Cfl <= 0 || scenario.Time.Cfl > 1)
            {
                issues.Add(new ValidationIssue("InvalidValue", "Value must be > 0 and <= 1.", "/time/cfl"));
            }

            if (scenario.Time.MaxSteps < 1)
            {
                issues.Add(new ValidationIssue("InvalidValue", "Value must be >= 1.", "/time/max_steps"));
            }
        }

        if (scenario.Physics is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/physics"));
        }
        else if (scenario.Physics.Nu < 0)
        {
            issues.Add(new ValidationIssue("InvalidValue", "Value must be >= 0.", "/physics/nu"));
        }

        if (scenario.InitialCondition is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/initial_condition"));
        }
        else
        {
            ValidateNonEmptyString(issues, scenario.InitialCondition.Type, "/initial_condition/type");

            if (scenario.InitialCondition.Params.ValueKind == JsonValueKind.Undefined)
            {
                issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/initial_condition/params"));
            }
        }

        if (scenario.Numerics is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/numerics"));
        }
        else
        {
            ValidateNonEmptyString(issues, scenario.Numerics.Advection, "/numerics/advection");
            ValidateNonEmptyString(issues, scenario.Numerics.Integrator, "/numerics/integrator");
        }

        if (scenario.Outputs is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/outputs"));
        }
        else
        {
            var tEnd = scenario.Time?.TEnd ?? 0;

            if (scenario.Outputs.TimeseriesDt <= 0 || (tEnd > 0 && scenario.Outputs.TimeseriesDt > tEnd))
            {
                issues.Add(new ValidationIssue("InvalidValue", "Value must be > 0 and <= /time/t_end.", "/outputs/timeseries_dt"));
            }

            if (scenario.Outputs.Snapshots is null)
            {
                issues.Add(new ValidationIssue("MissingField", "Required field is missing.", "/outputs/snapshots"));
            }
            else
            {
                for (var i = 0; i < scenario.Outputs.Snapshots.Length; i++)
                {
                    var value = scenario.Outputs.Snapshots[i];
                    if (value < 0 || (tEnd > 0 && value > tEnd))
                    {
                        issues.Add(new ValidationIssue("InvalidValue", "Elements must be within [0, /time/t_end].", $"/outputs/snapshots/{i}"));
                    }
                }
            }
        }

        return new ValidationResult(issues.Count == 0, issues);
    }

    private static void ValidateNonEmptyString(List<ValidationIssue> issues, string? value, string path)
    {
        if (value is null)
        {
            issues.Add(new ValidationIssue("MissingField", "Required field is missing.", path));
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new ValidationIssue("InvalidValue", "Value must be non-empty.", path));
        }
    }
}

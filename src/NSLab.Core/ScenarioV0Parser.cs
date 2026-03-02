using System.Text.Json;

namespace NSLab.Core;

public static class ScenarioV0Parser
{
    private static readonly Dictionary<string, HashSet<string>> AllowedChildren = new(StringComparer.Ordinal)
    {
        [""] = new HashSet<string>(new[]
        {
            "schema_version", "experiment", "solver", "domain", "resolution", "time", "physics",
            "initial_condition", "numerics", "outputs", "seed"
        }, StringComparer.Ordinal),
        ["/experiment"] = new HashSet<string>(new[] { "llm_name", "run_number", "utc_timestamp" }, StringComparer.Ordinal),
        ["/domain"] = new HashSet<string>(new[] { "type", "L" }, StringComparer.Ordinal),
        ["/resolution"] = new HashSet<string>(new[] { "N" }, StringComparer.Ordinal),
        ["/time"] = new HashSet<string>(new[] { "t_end", "cfl", "max_steps" }, StringComparer.Ordinal),
        ["/physics"] = new HashSet<string>(new[] { "nu" }, StringComparer.Ordinal),
        ["/initial_condition"] = new HashSet<string>(new[] { "type", "params" }, StringComparer.Ordinal),
        ["/numerics"] = new HashSet<string>(new[] { "advection", "integrator" }, StringComparer.Ordinal),
        ["/outputs"] = new HashSet<string>(new[] { "timeseries_dt", "snapshots" }, StringComparer.Ordinal)
    };

    public static (ScenarioV0? Scenario, ValidationResult Result) ParseAndValidate(string jsonText)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonText, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationResult(false, new[]
            {
                new ValidationIssue("JsonParseError", ex.Message, "")
            }));
        }

        using (document)
        {
            var issues = new List<ValidationIssue>();

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue("InvalidValue", "Root JSON value must be an object.", ""));
                return (null, new ValidationResult(false, issues));
            }

            FindUnknownFields(document.RootElement, string.Empty, issues);

            ScenarioV0? scenario;
            try
            {
                scenario = JsonSerializer.Deserialize<ScenarioV0>(document.RootElement.GetRawText(), ScenarioJsonOptionsFactory.CreateStrict());
            }
            catch (JsonException ex)
            {
                issues.Add(new ValidationIssue("JsonParseError", ex.Message, ""));
                return (null, new ValidationResult(false, issues));
            }

            if (scenario is null)
            {
                issues.Add(new ValidationIssue("JsonParseError", "Failed to deserialize scenario.", ""));
                return (null, new ValidationResult(false, issues));
            }

            var validation = ScenarioV0Validator.Validate(scenario);
            if (!validation.IsValid)
            {
                issues.AddRange(validation.Issues);
            }

            if (issues.Count > 0)
            {
                return (null, new ValidationResult(false, issues));
            }

            return (scenario, new ValidationResult(true, Array.Empty<ValidationIssue>()));
        }
    }

    private static void FindUnknownFields(JsonElement element, string path, List<ValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!AllowedChildren.TryGetValue(path, out var allowedAtPath))
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!allowedAtPath.Contains(property.Name))
            {
                var unknownPath = $"{path}/{property.Name}";
                issues.Add(new ValidationIssue("UnknownField", "Unknown JSON property.", unknownPath));
                continue;
            }

            var childPath = $"{path}/{property.Name}";
            if (AllowedChildren.ContainsKey(childPath))
            {
                FindUnknownFields(property.Value, childPath, issues);
            }
        }
    }
}

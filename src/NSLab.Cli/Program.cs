using System.CommandLine;
using System.Diagnostics;
using NSLab.Core;

namespace NSLab.Cli;

public static class CliApp
{
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("nslab");

        root.Subcommands.Add(CreateRunCommand());
        root.Subcommands.Add(CreateSummarizeCommand());
        root.Subcommands.Add(CreateValidateCommand());

        return root;
    }

    private static Command CreateRunCommand()
    {
        var scenarioPathArgument = new Argument<string>("scenario.json");
        var outOption = new Option<string>("--out") { Required = true };
        var command = new Command("run");
        command.Arguments.Add(scenarioPathArgument);
        command.Options.Add(outOption);

        command.SetAction(parseResult =>
        {
            var scenarioPath = parseResult.GetValue(scenarioPathArgument)!;
            var runDir = parseResult.GetValue(outOption)!;

            var scenarioJsonText = File.ReadAllText(scenarioPath);
            var (scenario, validation) = ScenarioV0Parser.ParseAndValidate(scenarioJsonText);
            if (!validation.IsValid)
            {
                foreach (var issue in validation.Issues)
                {
                    Console.WriteLine($"{issue.Code} {issue.Path} {issue.Message}");
                }

                return ExitCodes.ValidationError;
            }

            var runId = RunId.FromScenarioJson(scenarioJsonText);
            EvidencePackWriter.InitializeRunFolder(runDir);

            var startedUtc = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var solverResult = NullSolver.Execute(scenario!);
            stopwatch.Stop();

            var timeseriesPath = Path.Combine(runDir, RunFolderSpec.TimeseriesCsv);
            foreach (var row in solverResult.Rows)
            {
                TimeseriesCsv.AppendRow(timeseriesPath, row);
            }

            var (severity, healthStatus) = SeverityScorerV0.Compute(solverResult.Rows);
            var summaryReason = string.Equals(healthStatus, HealthStatus.OK, StringComparison.Ordinal)
                ? string.Empty
                : healthStatus;

            var info = new EvidenceRunInfo(
                runId,
                "null",
                startedUtc,
                (int)stopwatch.ElapsedMilliseconds,
                Environment.OSVersion.ToString(),
                HealthStatus.OK,
                null);

            var summary = new SummaryV0Document(
                EvidenceSchemaVersions.SummaryV0,
                scenario!.Experiment,
                string.Empty,
                runId,
                "null",
                healthStatus,
                summaryReason,
                solverResult.MaxOmegaInf,
                solverResult.TAtMaxOmegaInf,
                solverResult.MaxZ,
                solverResult.MaxE,
                severity);

            EvidencePackWriter.WriteMetadata(runDir, scenario, info);
            EvidencePackWriter.WriteSummary(runDir, scenario, summary);

            return ExitCodes.Ok;
        });

        return command;
    }

    private static Command CreateSummarizeCommand()
    {
        var runDirArgument = new Argument<string>("run_dir");
        var command = new Command("summarize");
        command.Arguments.Add(runDirArgument);

        command.SetAction(parseResult =>
        {
            var runDir = parseResult.GetValue(runDirArgument)!;
            try
            {
                var summary = EvidencePackSummarizer.EnsureSummary(runDir);
                Console.WriteLine(
                    $"run_id={summary.RunId} solver_id={summary.SolverId} status={summary.Status} severity={summary.SeverityScoreV0:G17} max_omega_inf={summary.MaxOmegaInf:G17} t_at_max_omega_inf={summary.TAtMaxOmegaInf:G17}");
                return ExitCodes.Ok;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InvalidValue /run_dir {ex.Message}");
                return ExitCodes.ValidationError;
            }
        });

        return command;
    }

    private static Command CreateValidateCommand()
    {
        var pathArgument = new Argument<string>("scenario.json");
        var command = new Command("validate");
        command.Arguments.Add(pathArgument);

        command.SetAction(parseResult =>
        {
            var inputPath = parseResult.GetValue(pathArgument)!;

            if (inputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var jsonText = File.ReadAllText(inputPath);

                var (_, result) = ScenarioV0Parser.ParseAndValidate(jsonText);
                if (result.IsValid)
                {
                    Console.WriteLine("OK");
                    return ExitCodes.Ok;
                }

                foreach (var issue in result.Issues)
                {
                    Console.WriteLine($"{issue.Code} {issue.Path} {issue.Message}");
                }

                return ExitCodes.ValidationError;
            }

            var runResult = EvidencePackValidator.ValidateRunFolder(inputPath);
            if (runResult.IsValid)
            {
                Console.WriteLine("OK");
                return ExitCodes.Ok;
            }

            foreach (var issue in runResult.Issues)
            {
                Console.WriteLine($"{issue.Code} {issue.Path} {issue.Message}");
            }

            return ExitCodes.ValidationError;
        });

        return command;
    }
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await CliApp.BuildRootCommand().Parse(args).InvokeAsync();
    }
}

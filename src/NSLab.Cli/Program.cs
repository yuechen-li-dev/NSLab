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
        root.Subcommands.Add(CreateStubCommand("summarize"));
        root.Subcommands.Add(CreateValidateCommand());

        return root;
    }

    private static Command CreateStubCommand(string name)
    {
        var command = new Command(name);
        command.SetAction(_ =>
        {
            Console.WriteLine("Not implemented");
            return ExitCodes.RuntimeError;
        });

        return command;
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

            var info = new EvidenceRunInfo(
                runId,
                "null",
                startedUtc,
                (int)stopwatch.ElapsedMilliseconds,
                Environment.OSVersion.ToString(),
                "OK",
                null);

            var summary = new SummaryV0Document(
                EvidenceSchemaVersions.SummaryV0,
                scenario!.Experiment,
                runId,
                "null",
                "OK",
                string.Empty,
                solverResult.MaxOmegaInf,
                solverResult.TAtMaxOmegaInf,
                solverResult.MaxZ,
                solverResult.MaxE,
                solverResult.MaxOmegaInf);

            EvidencePackWriter.WriteMetadata(runDir, scenario, info);
            EvidencePackWriter.WriteSummary(runDir, scenario, summary);

            return ExitCodes.Ok;
        });

        return command;
    }

    private static Command CreateValidateCommand()
    {
        var scenarioPathArgument = new Argument<string>("scenario.json");
        var command = new Command("validate");
        command.Arguments.Add(scenarioPathArgument);

        command.SetAction(parseResult =>
        {
            var scenarioPath = parseResult.GetValue(scenarioPathArgument);
            var jsonText = File.ReadAllText(scenarioPath!);

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

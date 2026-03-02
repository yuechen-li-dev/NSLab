using NSLab.Core;
using System.CommandLine;

namespace NSLab.Cli;

public static class CliApp
{
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("nslab");

        root.Subcommands.Add(CreateStubCommand("run"));
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

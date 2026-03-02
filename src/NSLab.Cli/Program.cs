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
        root.Subcommands.Add(CreateStubCommand("validate"));

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
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await CliApp.BuildRootCommand().Parse(args).InvokeAsync();
    }
}

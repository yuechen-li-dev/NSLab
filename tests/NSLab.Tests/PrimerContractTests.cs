using System.Text.RegularExpressions;

namespace NSLab.Tests;

public class PrimerContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string PrimerPath = Path.Combine(RepoRoot, "PRIMER.md");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NSLab.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root containing NSLab.sln.");
    }

    private static string ReadPrimer()
    {
        Assert.True(File.Exists(PrimerPath), $"Expected PRIMER.md at repository root: {PrimerPath}");
        return File.ReadAllText(PrimerPath);
    }

    [Fact]
    public void Primer_Exists_And_Not_Trivial()
    {
        var content = ReadPrimer();
        var lines = File.ReadAllLines(PrimerPath);

        Assert.InRange(lines.Length, 120, 260);
        Assert.True(content.Length > 2000, "PRIMER.md must be substantial and operationally complete.");
    }

    [Fact]
    public void Primer_Contains_All_Required_Headings()
    {
        var content = ReadPrimer();

        var requiredHeadings = new[]
        {
            "# NSLab PRIMER",
            "## Contract overview",
            "## Inputs scenario.json",
            "## Outputs evidence pack",
            "## Severity and health flags",
            "## Explore vs verify policy",
            "## Refinement rules",
            "## Anti false positive rules",
            "## Standard runbook",
            "## Failure handling",
            "## Minimal acceptance drill"
        };

        foreach (var heading in requiredHeadings)
        {
            Assert.Contains(heading, content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Primer_Contains_All_Required_Literals()
    {
        var content = ReadPrimer();

        var requiredLiterals = new[]
        {
            "nslab validate",
            "nslab run",
            "nslab summarize",
            "llm_name",
            "run_number",
            "utc_timestamp",
            "severity_score_v0",
            "max_omega_inf",
            "Do not trust apparent blow up without refinement checks.",
            "If validate fails do not proceed."
        };

        foreach (var literal in requiredLiterals)
        {
            Assert.Contains(literal, content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Primer_Has_Exactly_Two_Fenced_Code_Blocks()
    {
        var content = ReadPrimer();

        var fenceCount = Regex.Matches(content, "```", RegexOptions.CultureInvariant).Count;
        Assert.Equal(4, fenceCount);

        Assert.Matches(new Regex("```json\\s", RegexOptions.CultureInvariant), content);
        var hasShellFence = Regex.IsMatch(content, "```(sh|bash)\\s", RegexOptions.CultureInvariant);
        Assert.True(hasShellFence, "Expected one shell fenced block labeled sh or bash.");
    }

    [Fact]
    public void Primer_Contains_Scenario_Template_With_Traceability()
    {
        var content = ReadPrimer();
        var jsonBlockMatch = Regex.Match(content, "```json\\s*(?<body>[\\s\\S]*?)```", RegexOptions.CultureInvariant);

        Assert.True(jsonBlockMatch.Success, "Expected a json code block in PRIMER.md.");
        var jsonBlock = jsonBlockMatch.Groups["body"].Value;

        Assert.Contains("llm_name", jsonBlock, StringComparison.Ordinal);
        Assert.Contains("run_number", jsonBlock, StringComparison.Ordinal);
        Assert.Contains("utc_timestamp", jsonBlock, StringComparison.Ordinal);
        Assert.Contains("schema_version", jsonBlock, StringComparison.Ordinal);
        Assert.Contains("solver", jsonBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Primer_Contains_Command_Sequence()
    {
        var content = ReadPrimer();
        var shellBlockMatch = Regex.Match(content, "```(?:sh|bash)\\s*(?<body>[\\s\\S]*?)```", RegexOptions.CultureInvariant);

        Assert.True(shellBlockMatch.Success, "Expected a shell code block in PRIMER.md.");
        var shellBlock = shellBlockMatch.Groups["body"].Value;

        Assert.Contains("nslab validate", shellBlock, StringComparison.Ordinal);
        Assert.Contains("nslab run", shellBlock, StringComparison.Ordinal);
        Assert.Contains("nslab summarize", shellBlock, StringComparison.Ordinal);
    }
}

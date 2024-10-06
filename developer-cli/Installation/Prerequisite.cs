using System.Diagnostics;
using System.Text.RegularExpressions;
using PlatformPlatform.DeveloperCli.Utilities;
using Spectre.Console;

namespace PlatformPlatform.DeveloperCli.Installation;

public abstract record Prerequisite
{
    public static readonly Prerequisite Dotnet = new CommandLineToolPrerequisite("dotnet", "dotnet", new Version(8, 0, 300));
    public static readonly Prerequisite Docker = new CommandLineToolPrerequisite("docker", "Docker", new Version(27, 1, 1));
    public static readonly Prerequisite Node = new CommandLineToolPrerequisite("node", "NodeJS", new Version(22, 3, 0));
    public static readonly Prerequisite AzureCli = new CommandLineToolPrerequisite("az", "Azure CLI", new Version(2, 63));
    public static readonly Prerequisite GithubCli = new CommandLineToolPrerequisite("gh", "GitHub CLI", new Version(2, 55));
    public static readonly Prerequisite Aspire = new DotnetWorkloadPrerequisite("aspire", "Aspire", new Version(8, 2, 0));

    protected abstract bool IsValid();

    public static void Ensure(params Prerequisite[] prerequisites)
    {
        var failedPrerequisiteCount = prerequisites.Count(p => !p.IsValid());
        if (failedPrerequisiteCount > 0)
        {
            Environment.Exit(1);
        }
    }
}

file sealed record CommandLineToolPrerequisite(string Command, string DisplayName, Version MinVersion) : Prerequisite
{
    protected override bool IsValid()
    {
        // Check if the command line tool is installed
        var checkOutput = ProcessHelper.StartProcess(new ProcessStartInfo
            {
                FileName = Configuration.IsWindows ? "where" : "which",
                Arguments = Command,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        );

        var possibleFileLocations = checkOutput.Split(Environment.NewLine);

        if (string.IsNullOrWhiteSpace(checkOutput) || !possibleFileLocations.Any() || !File.Exists(possibleFileLocations[0]))
        {
            AnsiConsole.MarkupLine($"[red]{DisplayName} of minimum version {MinVersion} should be installed.[/]");

            return false;
        }

        // Get the version of the command line tool
        var output = ProcessHelper.StartProcess(new ProcessStartInfo
            {
                FileName = Configuration.IsWindows ? "cmd.exe" : "/bin/bash",
                Arguments = Configuration.IsWindows ? $"/c {Command} --version" : $"-c \"{Command} --version\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        );

        var versionRegex = new Regex(@"\d+\.\d+\.\d+(\.\d+)?");
        var match = versionRegex.Match(output);
        if (match.Success)
        {
            var version = Version.Parse(match.Value);
            if (version >= MinVersion) return true;
            AnsiConsole.MarkupLine(
                $"[red]Please update '[bold]{DisplayName}[/]' from version [bold]{version}[/] to [bold]{MinVersion}[/] or later.[/]"
            );

            return false;
        }

        // If the version could not be determined please change the logic here to check for the correct version
        AnsiConsole.MarkupLine(
            $"[red]Command '[bold]{Command}[/]' is installed but version could not be determined. Please update the CLI to check for correct version.[/]"
        );

        return false;
    }
}

file sealed record DotnetWorkloadPrerequisite(string WorkloadName, string DisplayName, Version MinVersion) : Prerequisite
{
    protected override bool IsValid()
    {
        var output = ProcessHelper.StartProcess("dotnet workload list", redirectOutput: true);

        if (!output.Contains(WorkloadName))
        {
            AnsiConsole.MarkupLine(
                $"[red].NET '[bold]{DisplayName}[/]' should be installed. Please run '[bold]dotnet workload update[/]' and then '[bold]dotnet workload install {WorkloadName}[/]'.[/]"
            );

            return false;
        }

        /*
           The output is on the form:

           Installed Workload Id      Manifest Version      Installation Source
           --------------------------------------------------------------------
           aspire                     8.0.2/8.0.100         SDK 8.0.300

           Use `dotnet workload search` to find additional workloads to install.
         */
        var versionRegex = new Regex($@"{WorkloadName}\s+(\d+\.\d+\.\d+(\.\d+)?)/");
        var match = versionRegex.Match(output);
        if (match.Success)
        {
            var versionString = match.Groups[1].Value;
            var version = Version.Parse(versionString);
            if (version >= MinVersion) return true;
            AnsiConsole.MarkupLine(
                $"[red].NET workload '[bold]{DisplayName}[/]' is version [bold]{version}[/] but version [bold]{MinVersion}[/] is required. Please run '[bold]dotnet workload update[/]'.[/]"
            );

            return false;
        }

        // If the version could not be determined please change the logic here to check for the correct version
        AnsiConsole.MarkupLine(
            $"[red]The workload '[bold]{WorkloadName}[/]' is installed but version could not be determined. Please update the CLI to check for correct version.[/]"
        );

        return false;
    }
}
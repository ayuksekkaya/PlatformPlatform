---
trigger: glob
globs: developer-cli/Commands/*.cs
description: Rules for implementing Developer CLI commands
---

# Developer CLI Rules

Carefully follow these instructions when implementing and extending the custom Developer CLI commands.

## Implementation

1. Command Structure:
   - Create one file per command in the `developer-cli/Commands` directory.
   - Name the file with the `Command` iherit from `System.CommandLine.Command` base class.
   - Provide a concise description in the constructor that explains the command's purpose.
   - Define all command options using `AddOption()` in the constructor.
   - Implement the command's logic in a private `Execute` method.
   - Use static methods where appropriate for better organization of functionality.

2. Command Options:
   - Use double-dash (`--`) for long option names and single-dash (`-`) for abbreviations.
   - Provide clear, concise descriptions for all options.
   - Use consistent naming across commands for similar options (e.g., `--solution-name` and `-s`).
   - Define option types explicitly (e.g., `Option<bool>`, `Option<string?>`).
   - For positional arguments, include both positional and named options (e.g., `["<solution-name>", "--solution-name", "-s"]`).
   - Set default values where appropriate using lambda expressions.

3. Prerequisites and Dependencies:
   - Always check for required dependencies at the beginning of the `Execute` method.
   - Use `Prerequisite.Ensure()` to verify that required tools are installed.
   - Common prerequisites include `Prerequisite.Dotnet` and `Prerequisite.Node`.

4. Process Execution:
   - Use `ProcessHelper` for all external process execution.
   - For simple process execution, use `ProcessHelper.StartProcess()`.
   - For processes that need shell features, use `ProcessHelper.StartProcessWithSystemShell()`.
   - Specify the working directory as the second parameter when needed.
   - Handle process execution errors appropriately.

5. Error Handling:
   - Use `try/catch` blocks to handle exceptions.
   - Display error messages using `AnsiConsole.MarkupLine()` with appropriate color formatting.
   - Use `Environment.Exit(1)` to exit with a non-zero status code on errors.
   - Do not throw exceptions; instead, handle them and exit gracefully.
   - Provide clear, actionable error messages to the user.

6. Console Output:
   - Use `Spectre.Console.AnsiConsole` for all console output.
   - Use color coding for different types of messages:
     - `[blue]` for informational messages
     - `[green]` for success messages
     - `[yellow]` for warnings
     - `[red]` for errors
   - Format output consistently across all commands.
   - Use tables, panels, or other Spectre.Console features for complex output.

7. Command Registration:
   - Set the command handler in the constructor using `CommandHandler.Create<>()`.
   - Match the handler parameters with the command options.
   - Use nullable types for optional parameters.

8. Utility Classes:
   - Use existing utility classes from the `developer-cli/Utilities` directory.
   - Only create new utility classes for truly generic functionality that can be used across multiple commands.
   - Place command-specific helper methods as private methods within the command class.

9. Performance Tracking:
   - Use `Stopwatch` to track execution time for long-running operations.
   - Display timing information to the user for better feedback.
   - Format timing information consistently using extension methods like `.Format()`.

10. Self-Contained Implementation:
    - Keep each command implementation self-contained in a single file.
    - Avoid dependencies between command implementations.
    - Extract shared functionality to utility classes only when necessary.

## Examples

```csharp
// ✅ DO: Use clear option naming, prerequisite checks, AnsiConsole, ProcessHelper
public class BuildCommand : Command
{
    public BuildCommand() : base("build", "Builds the solution")
    {
        AddOption(new Option<string?>(["<solution-name>", "--solution-name", "-s"], "The solution to build")); // ✅ DO: Consistent option naming
        AddOption(new Option<bool>(["--verbose", "-v"], () => false, "Enable verbose output"));
        Handler = CommandHandler.Create<string?, bool>(Execute);
    }

    private static void Execute(string? solutionName, bool verbose)
    {
        Prerequisite.Ensure(Prerequisite.Dotnet); // ✅ DO: Check prerequisites

        if (string.IsNullOrEmpty(solutionName))
        {
            AnsiConsole.MarkupLine("[red]Error: Solution name is required[/]");
            Environment.Exit(1); // ✅ DO: Exit on error
        }

        try
        {
            AnsiConsole.MarkupLine("[blue]Building solution...[/]"); // ✅ DO: Use AnsiConsole

            ProcessHelper.StartProcess($"dotnet build {solutionName}"); // ✅ DO: Use ProcessHelper
            AnsiConsole.MarkupLine("[green]Build completed successfully[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            Environment.Exit(1);
        }
    }
}

public class BadBuildCommand : Command
{
    public BadBuildCommand() : base("bad-build", "Bad build command")
    {
        // ❌ DON'T: Extract options to a variable
        var option = new Option<string?>(["-file-name", "--f"], "The name of the solution to process") // ❌ DON'T: Inconsistent option naming and wrong use of -- and -
        AddOption(option); 
        Handler = CommandHandler.Create<string>(Execute);
    }
    private static int Execute(string file)
    {
        // ❌ DON'T: Skip prerequisite checks
        if (string.IsNullOrEmpty(file)) throw new ArgumentException("File required"); // ❌ DON'T: Throw exceptions
        Console.WriteLine("Building..."); // ❌ DON'T: Use Console.WriteLine
        var process = System.Diagnostics.Process.Start("dotnet", $"build {file}"); // ❌ DON'T: Use Process.Start directly
        process.WaitForExit();
        return process.ExitCode; // ❌ DON'T: Return exit code instead of Environment.Exit
    }
}
```

## Troubleshooting

The CLI is self compiling, so to build you just have to run [CLI_ALIAS]. Somethimes you will get errors like:

```bash
Failed to publish new CLI. Please run 'dotnet run' to fix. Could not load file or assembly 'System.IO.Pipelines, 
Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'. The system cannot find the 
```

Just retry the command and it should work.


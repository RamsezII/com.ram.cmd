# com.ram.cmd

Command lines for Shitstorm using Microsoft System.CommandLine 2.0.12.

Declare commands, typed arguments/options and their C# actions. System.CommandLine handles tokenization, quoted values, validation, help and completion suggestions. It invokes registered actions; runtime C# compilation remains the job of Roslyn/CSharpScript.

## Example

```csharp
using _CMD_;
using System.CommandLine;
using System.IO;

var output = new StringWriter();
var errors = new StringWriter();
var shell = new Shell("Game commands", output, errors);

var message = new Argument<string>("message");
var count = new Option<int>("--count", "-n")
{
    Description = "Number of repetitions",
    DefaultValueFactory = _ => 1
};
var echo = new Command("echo", "Print a message") { message, count };
echo.SetAction(result =>
{
    for (int i = 0; i < result.GetValue(count); i++)
        shell.Configuration.Output.WriteLine(result.GetValue(message));
});
shell.Root.Subcommands.Add(echo);

int exitCode = shell.Execute("echo \"hello world\" --count 2");
UnityEngine.Debug.Log(output.ToString());

output.GetStringBuilder().Clear();
shell.Execute("echo --help");
UnityEngine.Debug.Log(output.ToString());

shell.Execute("echo hello --count nope");
UnityEngine.Debug.Log(errors.ToString());
```

`Execute` returns `0` on success and a nonzero code for invalid input. Invalid input does not invoke the command action. Exceptions thrown by actions propagate to the caller, preserving their stack trace.

`Configuration.Output` and `Configuration.Error` receive library help and diagnostics. Actions should use these writers too when their output belongs in the same terminal. This does not redirect `Console.WriteLine` globally. Without supplied writers, the shell uses `Console.Out` and `Console.Error`. The caller owns the supplied writers; clear or dispose them as appropriate.

## Parsing and completion

```csharp
var parsed = shell.Parse("echo hello --count nope");
foreach (var error in parsed.Errors)
    UnityEngine.Debug.Log(error.Message);

foreach (var completion in shell.GetCompletions("echo --c"))
    UnityEngine.Debug.Log(completion.Label);
```

`Parse` and `GetCompletions` never execute actions. An optional character position selects the completion cursor. Connecting these diagnostics and suggestions to the in-game editor/TAB UI is a separate step.

For asynchronous actions, use `SetAction((result, cancellationToken) => ...)` and `await shell.ExecuteAsync(text, cancellationToken)`. Do not block the Unity thread while waiting for an asynchronous action. The shell disables process termination hooks because it is hosted inside the game.

## Dependency

The official NuGet package's `netstandard2.0` DLL is included in `Runtime/Plugins/System.CommandLine`; Unity does not provide this namespace automatically. No NuGet manager or generated `.csproj` changes are required. The plugin is explicitly referenced by `ram._CMD_.asmdef`. Another assembly using `Command`, `Option<T>` or other System.CommandLine types must add `System.CommandLine.dll` to its own explicit precompiled references.

The package declares a System.Memory dependency. This Unity project's .NET Standard 2.1/CoreCLR runtime provides the corresponding APIs; no duplicate System.Memory.dll is bundled here. See the plugin README for its origin and license.

System.CommandLine parses individual command lines. Shell operators such as pipes, redirects or command sequences need their own execution semantics if added later.

Validation: `Shell.cs` compiles as C# 9 against .NET Standard 2.1 with Unity 7000.0.0a8's bundled compiler/reference assembly. A standalone .NET 10 smoke run checked quoting, typed values, aliases/defaults, invalid input, help output, diagnostics, completions, action exceptions, asynchronous execution and cancellation. Live Editor import/Console and Play Mode have not been verified; the open Editor's Pipeline server was unavailable.

- [Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [Official NuGet package](https://www.nuget.org/packages/System.CommandLine/2.0.12)

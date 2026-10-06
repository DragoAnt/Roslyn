using System.Diagnostics;
using System.Text;

namespace DragoAnt.Roslyn.Shared.Consumer.Tests;

public sealed record DotNetResult(string Command, int ExitCode, string Output)
{
    public DotNetResult EnsureSuccess() =>
        ExitCode == 0 ? this : throw new InvalidOperationException($"'dotnet {Command}' exited with {ExitCode}:\n{Output}");
}

internal static class DotNet
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    /// <summary>Runs the dotnet CLI in <paramref name="workingDirectory"/> without the MSBuild state of the test host's own run.</summary>
    public static async Task<DotNetResult> RunAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var key in start.Environment.Keys.Where(IsInheritedBuildState).ToList())
        {
            start.Environment.Remove(key);
        }

        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        var output = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        process.WaitForExit();
        lock (output)
        {
            return new DotNetResult(string.Join(' ', arguments), process.ExitCode, output.ToString());
        }
    }

    private static bool IsInheritedBuildState(string key) =>
        key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("DOTNET_HOST_PATH", StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("NUGET_", StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("TESTINGPLATFORM", StringComparison.OrdinalIgnoreCase);

    private static void Append(StringBuilder output, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }
}

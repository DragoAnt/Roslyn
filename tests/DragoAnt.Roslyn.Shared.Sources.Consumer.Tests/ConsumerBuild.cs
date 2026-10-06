using System.Globalization;

namespace DragoAnt.Roslyn.Shared.Consumer.Tests;

/// <summary>
/// Packs <c>DragoAnt.Roslyn.Shared.Sources</c> from this checkout into a local feed, then builds the fixture under
/// <c>Consumer/</c> in a temporary folder: a code fix, an analyzer package carrying it, a generator package and an app
/// that references both packages. Everything it writes stays in that folder, which is deleted afterwards.
/// </summary>
public sealed class ConsumerBuild : IAsyncLifetime
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "dragoant-roslyn-consumer", Guid.NewGuid().ToString("N")[..12]);

    public string SharedVersion { get; } = "1.0.0-consumer." + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    public string Feed => Path.Combine(Root, "feed");

    public string Consumer => Path.Combine(Root, "consumer");

    public string Packages => Path.Combine(Root, "packages");

    public DotNetResult SharedPack { get; private set; } = null!;

    public DotNetResult CodeFixBuild { get; private set; } = null!;

    public DotNetResult AnalyzerPack { get; private set; } = null!;

    public DotNetResult GeneratorPack { get; private set; } = null!;

    public DotNetResult AppBuild { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Root);
        CopyDirectory(Path.Combine(AppContext.BaseDirectory, "Consumer"), Consumer);
        File.Copy(Path.Combine(RepositoryRoot, "global.json"), Path.Combine(Root, "global.json"));
        await File.WriteAllTextAsync(Path.Combine(Root, ".editorconfig"), "root = true\n", cancellationToken);

        SharedPack = (await DotNet.RunAsync(RepositoryRoot, cancellationToken,
            "pack", Path.Combine(RepositoryRoot, "src", "DragoAnt.Roslyn.Shared.Sources", "DragoAnt.Roslyn.Shared.Sources.csproj"),
            "-c", "Release", "-o", Feed, "--artifacts-path", Path.Combine(Root, "artifacts"),
            $"-p:Version={SharedVersion}", "--disable-build-servers", "-nologo")).EnsureSuccess();

        CodeFixBuild = (await Consume(cancellationToken, "build", "Contoso.Rules.CodeFixes")).EnsureSuccess();
        AnalyzerPack = (await Consume(cancellationToken, "pack", "Contoso.Rules.Analyzers")).EnsureSuccess();
        GeneratorPack = (await Consume(cancellationToken, "pack", "Contoso.Rules.Generators")).EnsureSuccess();
        AppBuild = (await Consume(cancellationToken, "build", "Contoso.App")).EnsureSuccess();
    }

    public Task<DotNetResult> Consume(CancellationToken cancellationToken, string command, string project, params string[] extra) =>
        DotNet.RunAsync(Consumer, cancellationToken,
        [
            command, project, "-nologo",
            .. command == "msbuild" ? ["-p:Configuration=Release", "-nodeReuse:false"] : new[] { "-c", "Release", "--disable-build-servers" },
            $"-p:DragoAntRoslynSharedSourcesVersion={SharedVersion}",
            $"-p:RestorePackagesPath={Packages}",
            $"-p:RestoreAdditionalProjectFallbackFolders={GlobalPackagesFolder}",
            .. extra,
        ]);

    public string Output(string project) =>
        Path.Combine(Consumer, project, "bin", "Release", "netstandard2.0", project + ".dll");

    public ValueTask DisposeAsync()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ValueTask.CompletedTask;
    }

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string GlobalPackagesFolder { get; } =
        Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DragoAnt.Roslyn.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"DragoAnt.Roslyn.slnx not found above {AppContext.BaseDirectory}");
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}

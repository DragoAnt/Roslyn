namespace DragoAnt.Roslyn.Shared.Tests;

internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string SharedProject => Path.Combine(Root, "src", "DragoAnt.Roslyn.Shared.Sources");

    public static string SharedSources => Path.Combine(SharedProject, "src");

    public static IReadOnlyList<string> SharedSourceFiles() =>
        Directory.GetFiles(SharedSources, "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToList();

    private static string FindRoot()
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
}

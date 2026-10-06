using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DragoAnt.Roslyn.Shared.Consumer.Tests;

public sealed class ConsumerBuildTests(ConsumerBuild build) : IClassFixture<ConsumerBuild>
{
    private const string SharedPackageId = "DragoAnt.Roslyn.Shared.Sources";

    [Fact]
    public void TheSharedPackage_ShipsSourcesAndTargetsOnly()
    {
        using var package = ZipFile.OpenRead(Path.Combine(build.Feed, $"{SharedPackageId}.{build.SharedVersion}.nupkg"));
        var entries = package.Entries.Select(e => e.FullName).ToList();

        entries.Should().Contain($"build/{SharedPackageId}.targets")
            .And.Contain(e => e.StartsWith("src/Common/", StringComparison.Ordinal))
            .And.Contain(e => e.StartsWith("src/Generators/", StringComparison.Ordinal))
            .And.Contain(e => e.StartsWith("src/CodeFixes/", StringComparison.Ordinal))
            .And.NotContain(e => e.StartsWith("lib/", StringComparison.Ordinal) || e.EndsWith(".dll", StringComparison.Ordinal));
        Nuspec(package).Should().Contain("<developmentDependency>true</developmentDependency>").And.NotContain("<dependencies>");
    }

    [Theory]
    [InlineData("Contoso.Rules.Analyzers", new[] { "Common" })]
    [InlineData("Contoso.Rules.Generators", new[] { "Common", "Generators" })]
    [InlineData("Contoso.Rules.CodeFixes", new[] { "Common", "CodeFixes" })]
    public async Task EachRole_CompilesOnlyItsParts_AsVisibleDaSharedLinks(string project, string[] parts)
    {
        var items = await SharedCompileItems(project);

        items.Should().NotBeEmpty();
        items.Select(i => i.Part).Distinct().Should().BeEquivalentTo(parts);
        items.Should().OnlyContain(i => i.Link.StartsWith(".da.shared/" + i.Part + "/", StringComparison.Ordinal) && i.Visible == "true");
    }

    [Theory]
    [InlineData("Contoso.Rules.Analyzers", "SyntaxHelpers", "EquatableArray`1", "LiteralRewriter")]
    [InlineData("Contoso.Rules.Generators", "EquatableArray`1", "LiteralRewriter", null)]
    [InlineData("Contoso.Rules.CodeFixes", "LiteralRewriter", "EquatableArray`1", null)]
    public void EachAssembly_CarriesItsOwnInternalCopy(string project, string present, string absent, string? alsoAbsent)
    {
        var types = SharedTypes(build.Output(project));

        types.Should().Contain(t => t.Name == present)
            .And.Contain(t => t.Name == "DiagnosticHelpers")
            .And.NotContain(t => t.Name == absent);
        if (alsoAbsent is not null)
        {
            types.Should().NotContain(t => t.Name == alsoAbsent);
        }

        types.Should().OnlyContain(t => !t.IsPublic);
    }

    [Fact]
    public void TheAnalyzerPackage_CarriesBothRoslynAssemblies_AndNoDependency()
    {
        using var package = ZipFile.OpenRead(Path.Combine(build.Feed, "Contoso.Rules.Analyzers.1.0.0.nupkg"));

        package.Entries.Select(e => e.FullName).Should().Contain("analyzers/dotnet/cs/Contoso.Rules.Analyzers.dll")
            .And.Contain("analyzers/dotnet/cs/Contoso.Rules.CodeFixes.dll")
            .And.NotContain(e => e.StartsWith("src/", StringComparison.Ordinal) || e.StartsWith("lib/", StringComparison.Ordinal));
        Nuspec(package).Should().NotContain(SharedPackageId).And.NotContain("<dependency ");
    }

    [Fact]
    public void TheGeneratorPackage_HasNoDependency()
    {
        using var package = ZipFile.OpenRead(Path.Combine(build.Feed, "Contoso.Rules.Generators.1.0.0.nupkg"));

        Nuspec(package).Should().NotContain(SharedPackageId).And.NotContain("<dependency ");
    }

    [Fact]
    public void TheApp_NeverSeesTheSharedPackage()
    {
        var assets = File.ReadAllText(Path.Combine(build.Consumer, "Contoso.App", "obj", "project.assets.json"));

        assets.Should().Contain("Contoso.Rules.Analyzers/1.0.0")
            .And.NotContainEquivalentOf(SharedPackageId);
    }

    [Fact]
    public void TheApp_RunsTheAnalyzerAndTheGenerator_OnTheSharedCode()
    {
        build.AppBuild.Output.Should().MatchRegex(@"Program\.cs\(12,52\): warning CTR001: Path 'orders total' contains a space")
            .And.NotContain("AD0001")
            .And.NotContain("CTR900")
            .And.NotContain("CS8032");
    }

    [Fact]
    public void TheHeader_KeepsTheConsumersOwnRules_OffTheSharedFiles()
    {
        var flagged = Regex.Matches(build.AnalyzerPack.Output, @"([^\s\\/]+\.cs)\(\d+,\d+\): warning IDE0160")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        flagged.Should().Equal("PathLiteralAnalyzer.cs");
    }

    [Fact]
    public async Task AnUnknownPart_IsReported()
    {
        var result = await build.Consume(TestContext.Current.CancellationToken, "build", "Contoso.Rules.Analyzers", "-p:DragoAntRoslynSharedParts=Generatorz", "--no-restore");

        result.EnsureSuccess().Output.Should().Contain("warning DARS001").And.Contain("'Generatorz'");
    }

    private async Task<List<SharedItem>> SharedCompileItems(string project)
    {
        var result = (await build.Consume(TestContext.Current.CancellationToken, "msbuild", project, "-getItem:Compile")).EnsureSuccess();
        var json = result.Output[result.Output.IndexOf('{', StringComparison.Ordinal)..];
        using var document = JsonDocument.Parse(json);

        return
        [
            .. document.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray()
                .Where(i => i.TryGetProperty("DragoAntRoslynSharedPart", out _))
                .Select(i => new SharedItem(
                    i.GetProperty("DragoAntRoslynSharedPart").GetString()!,
                    i.GetProperty("Link").GetString()!,
                    i.GetProperty("Visible").GetString()!)),
        ];
    }

    private static List<(string Name, bool IsPublic)> SharedTypes(string assembly)
    {
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        return
        [
            .. reader.TypeDefinitions.Select(reader.GetTypeDefinition)
                .Where(t => reader.GetString(t.Namespace) == "DragoAnt.Roslyn.Shared")
                .Select(t => (reader.GetString(t.Name), (t.Attributes & System.Reflection.TypeAttributes.VisibilityMask) == System.Reflection.TypeAttributes.Public)),
        ];
    }

    private static string Nuspec(ZipArchive package)
    {
        using var reader = new StreamReader(package.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open());
        return reader.ReadToEnd();
    }

    private sealed record SharedItem(string Part, string Link, string Visible);
}

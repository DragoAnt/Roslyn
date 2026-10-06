using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DragoAnt.Roslyn.Shared.Tests;

internal static class TestCompilation
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray());

    private static readonly ImmutableDictionary<string, ReportDiagnostic> UnifiedReferenceWarnings =
        ImmutableDictionary<string, ReportDiagnostic>.Empty
            .Add("CS1701", ReportDiagnostic.Suppress)
            .Add("CS1702", ReportDiagnostic.Suppress);

    public static CSharpCompilation Create(
        IEnumerable<SyntaxTree> trees, NullableContextOptions nullable = NullableContextOptions.Enable) =>
        CSharpCompilation.Create(
            "TestAssembly",
            trees,
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: nullable)
                .WithSpecificDiagnosticOptions(UnifiedReferenceWarnings));

    public static CSharpCompilation Create(params string[] sources) =>
        Create(sources.Select((source, i) => CSharpSyntaxTree.ParseText(source, path: $"Source{i}.cs")));

    public static IEnumerable<Diagnostic> Errors(this Compilation compilation) =>
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

    public static T Single<T>(this Compilation compilation, Func<T, bool>? predicate = null)
        where T : SyntaxNode =>
        compilation.SyntaxTrees.SelectMany(t => t.GetRoot().DescendantNodes().OfType<T>()).Single(predicate ?? (_ => true));

    public static LiteralExpressionSyntax Literal(this Compilation compilation, string value) =>
        compilation.Single<LiteralExpressionSyntax>(l => l.Token.ValueText == value);
}

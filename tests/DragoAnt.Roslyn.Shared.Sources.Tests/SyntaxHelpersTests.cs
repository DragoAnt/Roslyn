using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DragoAnt.Roslyn.Shared.Tests;

public sealed class SyntaxHelpersTests
{
    private const string Declarations = """
        namespace Contoso.Paths
        {
            [System.AttributeUsage(System.AttributeTargets.All)]
            public sealed class PathAttribute : System.Attribute
            {
                public PathAttribute(string path, string tag = "", params string[] extra) { }
                public string Name { get; set; } = "";
            }

            public static class Outer
            {
                public sealed class Inner<T> : System.Attribute { }
            }

            public static class Api
            {
                public static void Select(int depth, string path) { }
            }
        }
        """;

    [Theory]
    [InlineData("""Contoso.Paths.Api.Select(1, "a.b");""", "a.b", "path")]
    [InlineData("""Contoso.Paths.Api.Select(path: "a.c", depth: 1);""", "a.c", "path")]
    [InlineData("""new Contoso.Paths.PathAttribute("a.d");""", "a.d", "path")]
    public void GetTargetParameter_FindsTheCallParameter(string statement, string literal, string expected)
    {
        var compilation = TestCompilation.Create(Declarations, $"class Use {{ void M() {{ {statement} }} }}");
        var target = compilation.Literal(literal);

        var parameter = SyntaxHelpers.GetTargetParameter(compilation.GetSemanticModel(target.SyntaxTree), target);

        parameter!.Name.Should().Be(expected);
    }

    [Theory]
    [InlineData("""[Contoso.Paths.Path("x.a")]""", "x.a", "path")]
    [InlineData("""[Contoso.Paths.Path("x.b", "tag-b")]""", "tag-b", "tag")]
    [InlineData("""[Contoso.Paths.Path("x.c", tag: "tag-c")]""", "tag-c", "tag")]
    [InlineData("""[Contoso.Paths.Path("x.d", "t", "extra-1", "extra-2")]""", "extra-2", "extra")]
    public void GetTargetParameter_FindsTheAttributeParameter(string attribute, string literal, string expected)
    {
        var compilation = TestCompilation.Create(Declarations, $"{attribute} class Use {{ }}");
        var target = compilation.Literal(literal);

        var parameter = SyntaxHelpers.GetTargetParameter(compilation.GetSemanticModel(target.SyntaxTree), target);

        parameter!.Name.Should().Be(expected);
    }

    [Theory]
    [InlineData("""[Contoso.Paths.Path("x", Name = "property")] class Use { }""", "property")]
    [InlineData("""class Use { string F = "field"; }""", "field")]
    [InlineData("""[Contoso.Paths.Missing("unresolved")] class Use { }""", "unresolved")]
    public void GetTargetParameter_IsNullWhenNoParameterTakesTheValue(string source, string literal)
    {
        var compilation = TestCompilation.Create(Declarations, source);
        var target = compilation.Literal(literal);

        SyntaxHelpers.GetTargetParameter(compilation.GetSemanticModel(target.SyntaxTree), target).Should().BeNull();
    }

    [Theory]
    [InlineData("""const string A = "a"; string M() => A + ".b";""", true, "a.b")]
    [InlineData("""string M() => nameof(M);""", true, "M")]
    [InlineData("""string M() => string.Concat("a", "b");""", false, null)]
    public void TryGetConstantString_ReadsCompileTimeConstants(string members, bool expected, string? value)
    {
        var compilation = TestCompilation.Create($"class Use {{ {members} }}");
        var expression = compilation.Single<ArrowExpressionClauseSyntax>().Expression;

        var found = SyntaxHelpers.TryGetConstantString(compilation.GetSemanticModel(expression.SyntaxTree), expression, out var text);

        found.Should().Be(expected);
        text.Should().Be(value);
    }

    [Fact]
    public void HasAttribute_MatchesBySymbolAndByMetadataName()
    {
        var compilation = TestCompilation.Create(
            Declarations, """[Contoso.Paths.Path("p"), Contoso.Paths.Outer.Inner<int>] class Use { } class Bare { }""");
        var use = compilation.GetTypeByMetadataName("Use")!;
        var bare = compilation.GetTypeByMetadataName("Bare")!;
        var path = compilation.GetTypeByMetadataName("Contoso.Paths.PathAttribute")!;

        SyntaxHelpers.HasAttribute(use, path).Should().BeTrue();
        SyntaxHelpers.HasAttribute(bare, path).Should().BeFalse();
        SyntaxHelpers.HasAttribute(use, "Contoso.Paths.PathAttribute").Should().BeTrue();
        SyntaxHelpers.HasAttribute(use, "Contoso.Paths.Outer+Inner`1").Should().BeTrue();
        SyntaxHelpers.HasAttribute(use, "Contoso.Paths.Inner`1").Should().BeFalse();
        SyntaxHelpers.HasAttribute(bare, "Contoso.Paths.PathAttribute").Should().BeFalse();
    }

    [Theory]
    [InlineData("Contoso.Paths.PathAttribute")]
    [InlineData("Contoso.Paths.Outer+Inner`1")]
    public void GetFullMetadataName_RoundTripsThroughGetTypeByMetadataName(string metadataName)
    {
        var compilation = TestCompilation.Create(Declarations);
        var type = compilation.GetTypeByMetadataName(metadataName)!;

        SyntaxHelpers.GetFullMetadataName(type).Should().Be(metadataName);
    }

    [Fact]
    public void GetFullMetadataName_OfAGlobalType_HasNoNamespace()
    {
        var compilation = TestCompilation.Create("class Global { }");

        SyntaxHelpers.GetFullMetadataName(compilation.GetTypeByMetadataName("Global")!).Should().Be("Global");
    }

    [Fact]
    public void NullSemanticModel_Throws()
    {
        var compilation = TestCompilation.Create("""class Use { string F = "x"; }""");
        var literal = compilation.Literal("x");

        FluentActions.Invoking(() => SyntaxHelpers.GetTargetParameter(null!, literal)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => SyntaxHelpers.TryGetConstantString(null!, literal, out _)).Should().Throw<ArgumentNullException>();
    }
}

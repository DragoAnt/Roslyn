using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DragoAnt.Roslyn.Shared.Tests;

public sealed class LiteralRewriterTests
{
    private const string Source = """
        class Use
        {
            string F = /* keep */ "card.number:last4" /* trailing */;
        }
        """;

    [Fact]
    public void ReplaceStringLiteral_ReplacesTheValueAndKeepsTrivia()
    {
        var root = CSharpSyntaxTree.ParseText(Source).GetRoot();
        var literal = root.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();

        var rewritten = LiteralRewriter.ReplaceStringLiteral(root, literal, "card.number:Last4");

        rewritten.ToFullString().Should().Contain("""/* keep */ "card.number:Last4" /* trailing */;""");
    }

    [Fact]
    public void ReplaceStringLiteral_EscapesTheNewValue()
    {
        var root = CSharpSyntaxTree.ParseText(Source).GetRoot();
        var literal = root.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();

        var rewritten = LiteralRewriter.ReplaceStringLiteral(root, literal, "say \"hi\"\n");

        var replaced = rewritten.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();
        replaced.Token.ValueText.Should().Be("say \"hi\"\n");
        replaced.Token.Text.Should().Be("\"say \\\"hi\\\"\\n\"");
    }

    [Fact]
    public async Task ReplaceStringLiteralAsync_ReturnsTheChangedDocument()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.CurrentSolution
            .AddProject("Use", "Use", LanguageNames.CSharp)
            .AddDocument("Use.cs", SourceText.From(Source));
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var literal = root!.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();

        var changed = await LiteralRewriter.ReplaceStringLiteralAsync(document, literal, "card.number:Last4", TestContext.Current.CancellationToken);

        (await changed.GetTextAsync(TestContext.Current.CancellationToken)).ToString()
            .Should().Contain("\"card.number:Last4\"").And.NotContain("last4\"");
        (await document.GetTextAsync(TestContext.Current.CancellationToken)).ToString().Should().Contain("last4");
    }
}

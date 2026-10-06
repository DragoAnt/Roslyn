using System.Collections.Immutable;
using System.Threading.Tasks;
using DragoAnt.Roslyn.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Contoso.Rules;

[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class PathSpaceCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["CTR001"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(context.Span, getInnermostNodeForTie: true) is not LiteralExpressionSyntax literal)
        {
            return;
        }

        var fixedValue = literal.Token.ValueText.Replace(' ', '.');
        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace spaces with dots",
                ct => LiteralRewriter.ReplaceStringLiteralAsync(context.Document, literal, fixedValue, ct),
                "CTR001"),
            context.Diagnostics);
    }
}

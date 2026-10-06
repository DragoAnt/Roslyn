using System.Collections.Immutable;
using DragoAnt.Roslyn.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Contoso.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PathLiteralAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        "CTR001", "Path contains a space", "Path '{0}' contains a space", "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Fault = new(
        "CTR900", "Analyzer fault", "{0}: {1}", "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, Fault];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            DiagnosticHelpers.Guarded<SyntaxNodeAnalysisContext>(Fault, c => c.ReportDiagnostic, Analyze),
            SyntaxKind.StringLiteralExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (SyntaxHelpers.GetTargetParameter(context.SemanticModel, literal)?.Name != "path")
        {
            return;
        }

        var space = literal.Token.ValueText.IndexOf(' ');
        if (space >= 0)
        {
            context.ReportDiagnostic(DiagnosticHelpers.Create(Rule, LocationHelpers.InStringLiteral(literal, space, 1), literal.Token.ValueText));
        }
    }
}

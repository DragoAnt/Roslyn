using System;
using System.Linq;
using DragoAnt.Roslyn.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Contoso.Rules;

[Generator(LanguageNames.CSharp)]
public sealed class NamesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var names = context.SyntaxProvider
            .CreateSyntaxProvider((node, _) => node is ClassDeclarationSyntax, (c, _) => ((ClassDeclarationSyntax)c.Node).Identifier.ValueText)
            .Collect()
            .Select((items, _) => items.OrderBy(n => n, StringComparer.Ordinal).ToEquatableArray());

        context.RegisterSourceOutput(names, (spc, model) => spc.AddSource(
            "GeneratedNames.g.cs",
            $"namespace Contoso.App {{ internal static class GeneratedNames {{ public const string All = \"{string.Join(",", model)}\"; }} }}"));
    }
}

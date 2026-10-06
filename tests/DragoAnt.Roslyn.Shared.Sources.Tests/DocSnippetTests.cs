using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DragoAnt.Roslyn.Shared.Tests;

public sealed partial class DocSnippetTests
{
    private static readonly string[] Documents = ["README.md", "docs/consuming.md"];

    public static TheoryData<string, int> Snippets()
    {
        var data = new TheoryData<string, int>();
        foreach (var document in Documents)
        {
            for (var i = 0; i < CSharpBlocks(document).Count; i++)
            {
                data.Add(document, i);
            }
        }

        return data;
    }

    [Fact]
    public void TheReadme_HasACSharpExample() => CSharpBlocks("README.md").Should().NotBeEmpty();

    [Theory]
    [MemberData(nameof(Snippets))]
    public void EveryCSharpBlock_CompilesAgainstTheSharedSources(string document, int index)
    {
        var snippet = CSharpSyntaxTree.ParseText(CSharpBlocks(document)[index], path: $"{document}#{index}.cs");
        var shared = RepositoryPaths.SharedSourceFiles().Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f));

        var compilation = TestCompilation.Create(shared.Append(snippet));

        compilation.Errors().Should().BeEmpty();
    }

    private static List<string> CSharpBlocks(string document) =>
        [.. Fence().Matches(File.ReadAllText(Path.Combine(RepositoryPaths.Root, document))).Select(m => m.Groups[1].Value)];

    [GeneratedRegex(@"```csharp\r?\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex Fence();
}

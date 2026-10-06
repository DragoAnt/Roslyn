using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DragoAnt.Roslyn.Shared.Tests;

public sealed class EquatableArrayTests
{
    [Fact]
    public void ArraysWithTheSameItems_AreEqual()
    {
        var a = new EquatableArray<string>(["x", "y"]);
        var b = new[] { "x", "y" }.ToEquatableArray();

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.Equals((object)b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Theory]
    [InlineData(new[] { "x" }, new[] { "y" })]
    [InlineData(new[] { "x" }, new[] { "x", "y" })]
    [InlineData(new[] { "x", "y" }, new[] { "y", "x" })]
    public void ArraysWithDifferentItems_AreNotEqual(string[] left, string[] right)
    {
        var a = new EquatableArray<string>(left);
        var b = new EquatableArray<string>(right);

        (a == b).Should().BeFalse();
        a.Equals((object)"x").Should().BeFalse();
    }

    [Fact]
    public void Default_IsTheEmptyArray()
    {
        var empty = default(EquatableArray<int>);

        empty.Should().Equal(EquatableArray<int>.Empty);
        (empty == new EquatableArray<int>([])).Should().BeTrue();
        (empty == ImmutableArray<int>.Empty.ToEquatableArray()).Should().BeTrue();
        (empty == default(ImmutableArray<int>).ToEquatableArray()).Should().BeTrue();
        empty.IsEmpty.Should().BeTrue();
        empty.Count.Should().Be(0);
        empty.AsImmutableArray().Should().BeEmpty();
        empty.GetHashCode().Should().Be(new EquatableArray<int>([]).GetHashCode());
    }

    [Fact]
    public void TheInputArray_IsCopied()
    {
        var items = new[] { 1, 2 };
        var array = new EquatableArray<int>(items);

        items[0] = 9;

        array[0].Should().Be(1);
        array.Should().Equal(1, 2);
    }

    [Fact]
    public void ItemsAreReadByIndexEnumerationAndImmutableArray()
    {
        var array = ImmutableArray.Create(3, 4, 5).ToEquatableArray();

        array.Count.Should().Be(3);
        array.IsEmpty.Should().BeFalse();
        array[2].Should().Be(5);
        array.AsImmutableArray().Should().Equal(3, 4, 5);
        ((System.Collections.IEnumerable)array).Cast<int>().Should().Equal(3, 4, 5);
        FluentActions.Invoking(() => array[3]).Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void NullItems_CompareAndHash()
    {
        var a = new EquatableArray<string>([null!, "b"]);
        var b = new EquatableArray<string>([null!, "b"]);

        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void AnIncrementalModelBuiltFromIt_IsCachedWhenAnUnrelatedFileChanges()
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new NamesGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        var compilation = TestCompilation.Create("class Alpha { } class Beta { }", "class Other { }");

        driver = driver.RunGenerators(compilation);
        var changed = compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Last(), CSharpSyntaxTree.ParseText("class Other { int F; }", path: "Source1.cs"));
        var result = driver.RunGenerators(changed).GetRunResult().Results.Single();

        result.TrackedSteps[NamesGenerator.Step].SelectMany(s => s.Outputs).Select(o => o.Reason)
            .Should().OnlyContain(r => r == IncrementalStepRunReason.Cached || r == IncrementalStepRunReason.Unchanged);
        result.GeneratedSources.Single().SourceText.ToString().Should().Contain("Alpha,Beta");
    }

    private sealed class NamesGenerator : IIncrementalGenerator
    {
        public const string Step = "Names";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var names = context.SyntaxProvider
                .CreateSyntaxProvider((node, _) => node is ClassDeclarationSyntax, (c, _) => ((ClassDeclarationSyntax)c.Node).Identifier.ValueText)
                .Where(name => name != "Other")
                .Collect()
                .Select((items, _) => items.Sort(StringComparer.Ordinal).ToEquatableArray())
                .WithTrackingName(Step);

            context.RegisterSourceOutput(names, (spc, model) =>
                spc.AddSource("Names.g.cs", $"// {string.Join(",", model)}"));
        }
    }
}

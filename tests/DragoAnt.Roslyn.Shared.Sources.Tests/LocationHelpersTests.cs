using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DragoAnt.Roslyn.Shared.Tests;

public sealed class LocationHelpersTests
{
    [Fact]
    public void InStringLiteral_PointsInsideAPlainLiteral()
    {
        var compilation = TestCompilation.Create("class Use\n{\n    string F = \"orders[x].total\";\n}");
        var literal = compilation.Literal("orders[x].total");

        var location = LocationHelpers.InStringLiteral(literal, 7, 1);

        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("x");
        location.GetLineSpan().StartLinePosition.Should().Be(new LinePosition(2, 23));
    }

    [Theory]
    [InlineData("""string F = "a\tb";""", "a\tb")]
    [InlineData("""string F = @"a.b";""", "a.b")]
    [InlineData("""string F = "a.b";""", "a.b", 2, 5)]
    [InlineData("""string F = "a.b";""", "a.b", -1, 1)]
    public void InStringLiteral_FallsBackToTheWholeLiteral(string member, string value, int offset = 0, int length = 1)
    {
        var compilation = TestCompilation.Create($"class Use {{ {member} }}");
        var literal = compilation.Literal(value);

        var location = LocationHelpers.InStringLiteral(literal, offset, length);

        location.SourceSpan.Should().Be(literal.Span);
    }

    [Fact]
    public void InText_ComputesLineAndColumn()
    {
        var text = SourceText.From("{\n  \"tag\": \"Sha1\"\n}");

        var location = LocationHelpers.InText("observer.json", text, 12, 4);

        location.GetLineSpan().Path.Should().Be("observer.json");
        location.GetLineSpan().StartLinePosition.Should().Be(new LinePosition(1, 10));
        location.SourceSpan.Should().Be(new TextSpan(12, 4));
    }

    [Theory]
    [InlineData(-5, 3, 0, 3)]
    [InlineData(10, 50, 10, 6)]
    [InlineData(99, 1, 16, 0)]
    public void InText_ClampsTheSpanToTheText(int start, int length, int expectedStart, int expectedLength)
    {
        var text = SourceText.From("0123456789abcdef");

        var location = LocationHelpers.InText("file.txt", text, start, length);

        location.SourceSpan.Should().Be(new TextSpan(expectedStart, expectedLength));
    }

    [Fact]
    public void InText_NullText_Throws() =>
        FluentActions.Invoking(() => LocationHelpers.InText("f", null!, 0, 0)).Should().Throw<ArgumentNullException>();

    [Fact]
    public void InAdditionalText_UsesTheFilePathAndText()
    {
        var file = new InMemoryAdditionalText("settings/observer.yaml", "rules:\n  - path: a.b\n");

        var location = LocationHelpers.InAdditionalText(file, 17, 3);

        location.GetLineSpan().Path.Should().Be("settings/observer.yaml");
        location.GetLineSpan().StartLinePosition.Should().Be(new LinePosition(1, 10));
    }

    [Fact]
    public void InAdditionalText_WithoutText_IsNone()
    {
        var file = new InMemoryAdditionalText("missing.json", null);

        LocationHelpers.InAdditionalText(file, 0, 1).Should().Be(Location.None);
    }

    private sealed class InMemoryAdditionalText(string path, string? content) : AdditionalText
    {
        public override string Path => path;

        public override SourceText? GetText(CancellationToken cancellationToken = default) =>
            content is null ? null : SourceText.From(content);
    }
}

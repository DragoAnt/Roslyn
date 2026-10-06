using Microsoft.CodeAnalysis;

namespace DragoAnt.Roslyn.Shared.Tests;

public sealed class DiagnosticHelpersTests
{
    private static readonly DiagnosticDescriptor Rule = new(
        "TST001", "Test rule", "Value '{0}' is not valid", "Tests", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Fault = new(
        "TST900", "Analyzer fault", "{0}: {1}", "Tests", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    [Fact]
    public void Create_WithoutLocation_UsesNone()
    {
        var diagnostic = DiagnosticHelpers.Create(Rule, null, "x");

        diagnostic.Location.Should().Be(Location.None);
        diagnostic.GetMessage().Should().Be("Value 'x' is not valid");
    }

    [Fact]
    public void Create_KeepsTheLocation()
    {
        var location = Location.Create("a.json", default, default);

        DiagnosticHelpers.Create(Rule, location, "y").Location.Should().Be(location);
    }

    [Fact]
    public void Guard_ReportsAnExceptionAsTheFaultDiagnostic()
    {
        var reported = new List<Diagnostic>();

        DiagnosticHelpers.Guard(Fault, reported.Add, () => throw new InvalidOperationException("boom"));

        reported.Should().ContainSingle()
            .Which.GetMessage().Should().Be("System.InvalidOperationException: boom");
        reported[0].Location.Should().Be(Location.None);
    }

    [Fact]
    public void Guard_RunsTheBodyAndReportsNothingOnSuccess()
    {
        var reported = new List<Diagnostic>();
        var ran = false;

        DiagnosticHelpers.Guard(Fault, reported.Add, () => ran = true);

        ran.Should().BeTrue();
        reported.Should().BeEmpty();
    }

    [Fact]
    public void Guard_LetsCancellationThrough()
    {
        var reported = new List<Diagnostic>();

        FluentActions.Invoking(() => DiagnosticHelpers.Guard(Fault, reported.Add, () => throw new OperationCanceledException()))
            .Should().Throw<OperationCanceledException>();
        reported.Should().BeEmpty();
    }

    [Fact]
    public void Guard_WithoutReport_Throws() =>
        FluentActions.Invoking(() => DiagnosticHelpers.Guard(Fault, null!, () => { })).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Guarded_WrapsACallbackWithTheContextsReport()
    {
        var context = new FakeContext();
        var callback = DiagnosticHelpers.Guarded<FakeContext>(Fault, c => c.Report, c => throw new FormatException(c.Name));

        callback(context);

        context.Reported.Should().ContainSingle().Which.GetMessage().Should().Be("System.FormatException: ctx");
    }

    private sealed class FakeContext
    {
        public string Name => "ctx";

        public List<Diagnostic> Reported { get; } = [];

        public void Report(Diagnostic diagnostic) => Reported.Add(diagnostic);
    }
}

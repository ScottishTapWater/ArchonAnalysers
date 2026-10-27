using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using ArchonAnalysers.Analysers.ARCHON004;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON004;

public class ForbiddenNamespaceReferencesAnalyserPerformanceTests
{
    [Fact]
    public async Task LargeConfiguredTree_CompletesWithExpectedDiagnostics()
    {
        StringBuilder source = new("namespace Target { public class Secret; } namespace Source {");
        for (int index = 0; index < 500; index++)
        {
            source.Append("public class Type").Append(index)
                .Append(" { public Target.Secret? Value; }");
        }
        source.Append('}');

        CSharpCompilation compilation = CSharpCompilation.Create(
            "PerformanceProject",
            [CSharpSyntaxTree.ParseText(source.ToString())],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AnalyzerOptions options = new(
            ImmutableArray.Create<AdditionalText>(new TestSettingsFile(
                "archon_004.forbidden_namespace_references = Source->Target")),
            new TestOptionsProvider());
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            [new ForbiddenNamespaceReferencesAnalyser()],
            options);

        Stopwatch stopwatch = Stopwatch.StartNew();
        ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        stopwatch.Stop();

        Assert.Equal(500, diagnostics.Count(diagnostic => diagnostic.Id == ForbiddenNamespaceReferencesAnalyser.DiagnosticId));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Analyzer took {stopwatch.Elapsed}.");
    }

    private sealed class TestSettingsFile : AdditionalText
    {
        private readonly SourceText _text;

        public TestSettingsFile(string contents) => _text = SourceText.From(contents);

        public override string Path => "/.archonanalysers";
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    private sealed class TestOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions SettingsOptions = new TestSettingsOptions();

        public override AnalyzerConfigOptions GlobalOptions => SettingsOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => SettingsOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => SettingsOptions;
    }

    private sealed class TestSettingsOptions : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = key == "build_metadata.AdditionalFiles.ArchonSettings" ? "true" : string.Empty;
            return value.Length > 0;
        }
    }
}

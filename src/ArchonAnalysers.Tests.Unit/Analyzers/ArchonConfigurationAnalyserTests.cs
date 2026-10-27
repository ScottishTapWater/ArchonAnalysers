using System.Collections.Immutable;
using ArchonAnalysers.Analysers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers;

public class ArchonConfigurationAnalyserTests
{
    [Theory]
    [InlineData("not a setting", "Expected 'key = value'")]
    [InlineData("unknown.setting = value", "Unknown setting")]
    [InlineData("archon_001.internal_namespace_slugs = Internal\narchon_001.internal_namespace_slugs = Private", "Duplicate setting")]
    public async Task InvalidSettingsFile_ReportsError(string contents, string expectedMessage)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyseAsync(contents);

        Diagnostic error = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(ArchonConfigurationAnalyser.InvalidSettingsFileId, error.Id),
            () => Assert.Equal(DiagnosticSeverity.Error, error.Severity),
            () => Assert.Equal("/.archonanalysers", error.Location.GetLineSpan().Path),
            () => Assert.Contains(expectedMessage, error.GetMessage()));
    }

    [Fact]
    public async Task CommentsAndRecognisedSettings_AreAccepted()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyseAsync(
            "# Project rules\n\narchon_001.internal_namespace_slugs = Private\narchon_004.forbidden_namespace_references = Domain->Infrastructure");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task LegacyEditorConfigSetting_ReportsMigrationError()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyseAsync(
            "archon_001.internal_namespace_slugs = Private",
            "archon_001.internal_namespace_slugs", "Private");

        Diagnostic error = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(ArchonConfigurationAnalyser.LegacyEditorConfigId, error.Id),
            () => Assert.Contains("archon_001.internal_namespace_slugs", error.GetMessage()));
    }

    [Fact]
    public async Task SeverityInEditorConfig_DoesNotReportMigrationError()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyseAsync(
            "archon_001.internal_namespace_slugs = Private",
            "dotnet_diagnostic.ARCHON001.severity", "warning");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UnmarkedAdditionalFile_IsIgnored()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyseAsync(
            "unknown.setting = value", markFile: false);

        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyseAsync(
        string contents, string? editorConfigKey = null, string? editorConfigValue = null,
        bool markFile = true)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestProject",
            [CSharpSyntaxTree.ParseText("public class TestClass;")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AnalyzerOptions options = new(
            [new TestSettingsFile(contents)],
            new TestOptionsProvider(editorConfigKey, editorConfigValue, markFile));
        CompilationWithAnalyzers analysed = compilation.WithAnalyzers(
            [new ArchonConfigurationAnalyser()], options);
        return await analysed.GetAnalyzerDiagnosticsAsync();
    }

    private sealed class TestSettingsFile : AdditionalText
    {
        private readonly string _contents;

        public TestSettingsFile(string contents) => _contents = contents;

        public override string Path => "/.archonanalysers";
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(_contents);
    }

    private sealed class TestOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestOptions _options;

        public TestOptionsProvider(string? key, string? value, bool markFile) => _options = new(key, value, markFile);

        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText file) => _options;
    }

    private sealed class TestOptions : AnalyzerConfigOptions
    {
        private readonly string? _key;
        private readonly string? _value;
        private readonly bool _markFile;

        public TestOptions(string? key, string? value, bool markFile)
        {
            _key = key;
            _value = value;
            _markFile = markFile;
        }

        public override bool TryGetValue(string requestedKey, out string result)
        {
            if (requestedKey == "build_metadata.AdditionalFiles.ArchonSettings" && _markFile)
            {
                result = "true";
                return true;
            }

            if (requestedKey == _key && _value != null)
            {
                result = _value;
                return true;
            }

            result = string.Empty;
            return false;
        }
    }
}

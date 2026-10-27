using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ArchonAnalysers.Analysers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArchonConfigurationAnalyser : DiagnosticAnalyzer
{
    public const string InvalidSettingsFileId = "ARCHON005";
    public const string LegacyEditorConfigId = "ARCHON006";

    internal static readonly DiagnosticDescriptor InvalidSettingsFile = new(
        InvalidSettingsFileId, "Invalid Archon settings file", "{0}", "Architecture",
        DiagnosticSeverity.Error, isEnabledByDefault: true,
        customTags: ["CompilationEnd"]);

    private static readonly DiagnosticDescriptor LegacyEditorConfig = new(
        LegacyEditorConfigId, "Move Archon setting to its settings file",
        "Move '{0}' from .editorconfig to the file named by <ArchonSettingsFile>", "Architecture",
        DiagnosticSeverity.Error, isEnabledByDefault: true,
        customTags: ["CompilationEnd"]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [InvalidSettingsFile, LegacyEditorConfig];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyseConfiguration);
    }

    private static void AnalyseConfiguration(CompilationAnalysisContext context)
    {
        ArchonSettings settings = ArchonSettings.Read(context.Options, context.CancellationToken);
        foreach (Diagnostic error in settings.Errors)
        {
            context.ReportDiagnostic(error);
        }

        foreach (string key in ArchonSettings.Keys)
        {
            if (context.Compilation.SyntaxTrees.Any(tree =>
                context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree).TryGetValue(key, out _)))
            {
                context.ReportDiagnostic(Diagnostic.Create(LegacyEditorConfig, Location.None, key));
            }
        }
    }
}

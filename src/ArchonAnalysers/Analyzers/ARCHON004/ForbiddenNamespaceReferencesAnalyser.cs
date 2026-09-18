using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ArchonAnalysers.Analyzers.ARCHON004;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForbiddenNamespaceReferencesAnalyser : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCHON004";
    public const string EditorConfigKey = "archon_004.forbidden_namespace_references";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Namespace contains a forbidden reference",
        "Namespace '{0}' cannot reference '{1}' because references to namespace '{2}' are forbidden",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Prevents code in configured source namespaces from depending on types or imports in configured target namespaces.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            NamespaceReferenceCompilationState state = new(startContext.Compilation);
            startContext.RegisterSemanticModelAction(modelContext => AnalyzeSemanticModel(modelContext, state));
        });
    }

    private static void AnalyzeSemanticModel(
        SemanticModelAnalysisContext context,
        NamespaceReferenceCompilationState state)
    {
        SyntaxTree tree = context.SemanticModel.SyntaxTree;
        NamespaceRuleSet rules = state.GetRules(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        if (rules.IsEmpty)
        {
            return;
        }

        NamespaceReferenceWalker walker = new(
            context.SemanticModel,
            rules,
            state,
            context.ReportDiagnostic,
            context.CancellationToken);
        walker.Analyze(tree.GetRoot(context.CancellationToken));
    }
}

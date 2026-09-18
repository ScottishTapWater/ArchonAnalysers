using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchonAnalysers.Analyzers.ARCHON004;

internal static class NamespaceImportAnalyser
{
    public static void Analyze(
        PendingImport import,
        SemanticModel semanticModel,
        NamespaceRuleSet ruleSet,
        IReadOnlyList<string> fileNamespaces,
        NamespaceReferenceCompilationState compilationState,
        NamespaceReferenceDiagnosticReporter reporter,
        CancellationToken cancellationToken)
    {
        UsingDirectiveSyntax directive = import.Directive;
        IAliasSymbol? alias = semanticModel.GetDeclaredSymbol(directive, cancellationToken);
        ISymbol? symbol = alias?.Target;
        if (symbol == null && directive.Name != null)
        {
            symbol = semanticModel.GetSymbolInfo(directive.Name, cancellationToken).Symbol;
        }

        if (symbol is not INamespaceSymbol && symbol is not ITypeSymbol)
        {
            return;
        }

        string[] importedNamespaces = symbol switch
        {
            INamespaceSymbol @namespace => [ReferencedTypeCollector.GetNamespaceName(@namespace)],
            ITypeSymbol type => ReferencedTypeCollector.GetConstituentNamedTypes(type)
                .Select(item => ReferencedTypeCollector.GetNamespaceName(item.ContainingNamespace))
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            _ => []
        };
        if (importedNamespaces.Length == 0)
        {
            return;
        }

        IEnumerable<string> sourceNamespaces = import.NamespaceScope != null
            ? [import.NamespaceScope]
            : directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                ? compilationState.GetGlobalNamespaces()
                : fileNamespaces;

        Location location = directive.Name?.GetLocation() ?? directive.GetLocation();
        string display = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        string identity = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        foreach (NamespaceRule rule in ruleSet.Rules)
        {
            string? actualSource = sourceNamespaces.FirstOrDefault(source => NamespaceRuleSet.Matches(source, rule.Source));
            if (actualSource != null && importedNamespaces.Any(target => NamespaceRuleSet.Matches(target, rule.Target)))
            {
                reporter.ReportImport(location, actualSource, display, identity, rule);
            }
        }
    }
}

internal readonly struct PendingImport
{
    public PendingImport(UsingDirectiveSyntax directive, string? namespaceScope)
    {
        Directive = directive;
        NamespaceScope = namespaceScope;
    }

    public UsingDirectiveSyntax Directive { get; }
    public string? NamespaceScope { get; }
}

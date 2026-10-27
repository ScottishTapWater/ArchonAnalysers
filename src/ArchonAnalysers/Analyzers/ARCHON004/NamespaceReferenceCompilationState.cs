using Microsoft.CodeAnalysis;

namespace ArchonAnalysers.Analysers.ARCHON004;

internal sealed class NamespaceReferenceCompilationState
{
    private readonly NamespaceRuleSet _rules;
    private readonly Compilation _compilation;
    private readonly Lazy<string[]> _globalNamespaces;

    public NamespaceReferenceCompilationState(Compilation compilation, string? configuredRules)
    {
        _compilation = compilation;
        _globalNamespaces = new(() => GetNamespaces(_compilation.Assembly.GlobalNamespace).ToArray());
        _rules = NamespaceRuleSet.Parse(configuredRules);
    }

    public NamespaceRuleSet GetRules() => _rules;

    public string[] GetGlobalNamespaces() => _globalNamespaces.Value;

    private static IEnumerable<string> GetNamespaces(INamespaceSymbol namespaceSymbol)
    {
        if (namespaceSymbol.GetTypeMembers().Length > 0)
        {
            yield return ReferencedTypeCollector.GetNamespaceName(namespaceSymbol);
        }

        foreach (INamespaceSymbol child in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (string name in GetNamespaces(child))
            {
                yield return name;
            }
        }
    }
}

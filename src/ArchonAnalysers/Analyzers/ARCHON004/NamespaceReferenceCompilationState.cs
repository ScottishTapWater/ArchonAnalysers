using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ArchonAnalysers.Analyzers.ARCHON004;

internal sealed class NamespaceReferenceCompilationState
{
    private readonly ConcurrentDictionary<string, NamespaceRuleSet> _ruleSets = new(StringComparer.Ordinal);
    private readonly Compilation _compilation;
    private readonly Lazy<string[]> _globalNamespaces;

    public NamespaceReferenceCompilationState(Compilation compilation)
    {
        _compilation = compilation;
        _globalNamespaces = new(() => GetNamespaces(_compilation.Assembly.GlobalNamespace).ToArray());
    }

    public NamespaceRuleSet GetRules(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(ForbiddenNamespaceReferencesAnalyser.EditorConfigKey, out string? value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return NamespaceRuleSet.Empty;
        }

        return _ruleSets.GetOrAdd(value, NamespaceRuleSet.Parse);
    }

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

using System.Text.RegularExpressions;

namespace ArchonAnalysers.Analyzers.ARCHON004;

internal sealed class NamespaceRuleSet
{
    private static readonly Regex DirectionalRulePattern = new(
        @"^\s*(?<source>.+?)\s*->\s*(?<target>.+?)\s*$",
        RegexOptions.Compiled);

    public static readonly NamespaceRuleSet Empty = new([]);

    private NamespaceRuleSet(NamespaceRule[] rules) => Rules = rules;

    public NamespaceRule[] Rules { get; }
    public bool IsEmpty => Rules.Length == 0;

    public static NamespaceRuleSet Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Empty;
        }

        NamespaceRule[] rules = value!.Split(',')
            .Select(item => DirectionalRulePattern.Match(item.Trim()))
            .Where(match => match.Success)
            .Select(match => new NamespaceRule(
                match.Groups["source"].Value.Trim(),
                match.Groups["target"].Value.Trim()))
            .Where(rule => rule.Source.Length > 0 && rule.Target.Length > 0)
            .ToArray();

        return rules.Length == 0 ? Empty : new(rules);
    }

    public NamespaceRule[] ForSource(string sourceNamespace) =>
        Rules.Where(rule => Matches(sourceNamespace, rule.Source)).ToArray();

    public static bool Matches(string actualNamespace, string configuredNamespace) =>
        string.Equals(actualNamespace, configuredNamespace, StringComparison.Ordinal) ||
        (actualNamespace.Length > configuredNamespace.Length &&
         actualNamespace.StartsWith(configuredNamespace, StringComparison.Ordinal) &&
         actualNamespace[configuredNamespace.Length] == '.');
}

internal readonly struct NamespaceRule
{
    public NamespaceRule(string source, string target)
    {
        Source = source;
        Target = target;
    }

    public string Source { get; }
    public string Target { get; }
}

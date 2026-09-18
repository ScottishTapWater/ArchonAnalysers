using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ArchonAnalysers.Analyzers.ARCHON004;

internal sealed class NamespaceReferenceDiagnosticReporter
{
    private readonly Action<Diagnostic> _reportDiagnostic;
    private readonly HashSet<DiagnosticKey> _reported = [];

    public NamespaceReferenceDiagnosticReporter(Action<Diagnostic> reportDiagnostic) =>
        _reportDiagnostic = reportDiagnostic;

    public void AnalyzeSymbol(
        ISymbol? symbol,
        IAliasSymbol? alias,
        Location location,
        TypeScope scope,
        bool recurseDirectType = true,
        bool includeMethodTypeArguments = true)
    {
        symbol = alias?.Target ?? symbol;
        if (symbol == null)
        {
            return;
        }

        switch (symbol)
        {
            case ITypeSymbol type:
                AnalyzeType(type, location, scope, recurseDirectType);
                break;
            case IMethodSymbol method:
                AnalyzeType(method.ContainingType, location, scope);
                AnalyzeType(method.ReturnType, location, scope);
                if (includeMethodTypeArguments)
                {
                    foreach (ITypeSymbol argument in method.TypeArguments)
                    {
                        AnalyzeType(argument, location, scope);
                    }
                }
                break;
            case IPropertySymbol property:
                AnalyzeType(property.ContainingType, location, scope);
                AnalyzeType(property.Type, location, scope);
                break;
            case IFieldSymbol field:
                AnalyzeType(field.ContainingType, location, scope);
                AnalyzeType(field.Type, location, scope);
                break;
            case IEventSymbol @event:
                AnalyzeType(@event.ContainingType, location, scope);
                AnalyzeType(@event.Type, location, scope);
                break;
            case ILocalSymbol local:
                AnalyzeType(local.Type, location, scope);
                break;
            case IParameterSymbol parameter:
                AnalyzeType(parameter.Type, location, scope);
                break;
        }
    }

    public void AnalyzeType(ITypeSymbol? type, Location location, TypeScope scope, bool recurse = true)
    {
        IEnumerable<INamedTypeSymbol> namedTypes = recurse
            ? ReferencedTypeCollector.GetConstituentNamedTypes(type)
            : type is INamedTypeSymbol named && named.TypeKind != TypeKind.Error ? [named] : [];

        foreach (INamedTypeSymbol namedType in namedTypes)
        {
            string targetNamespace = ReferencedTypeCollector.GetNamespaceName(namedType.ContainingNamespace);
            if (targetNamespace.Length == 0)
            {
                continue;
            }

            string? identity = null;
            string? display = null;
            foreach (NamespaceRule rule in scope.Rules)
            {
                if (!NamespaceRuleSet.Matches(targetNamespace, rule.Target))
                {
                    continue;
                }

                identity ??= namedType.WithNullableAnnotation(NullableAnnotation.None)
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                display ??= namedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                Report(location, scope.SourceNamespace,
                    display,
                    identity, rule, includeRule: false);
            }
        }
    }

    public void ReportImport(
        Location location,
        string sourceNamespace,
        string display,
        string identity,
        NamespaceRule rule) =>
        Report(location, sourceNamespace, display, identity, rule, includeRule: true);

    private void Report(
        Location location,
        string sourceNamespace,
        string display,
        string identity,
        NamespaceRule rule,
        bool includeRule)
    {
        DiagnosticKey key = new(
            location.SourceSpan,
            sourceNamespace,
            identity,
            includeRule ? rule.Source : string.Empty,
            includeRule ? rule.Target : string.Empty);

        if (_reported.Add(key))
        {
            _reportDiagnostic(Diagnostic.Create(
                ForbiddenNamespaceReferencesAnalyser.Rule,
                location,
                sourceNamespace,
                display,
                rule.Target));
        }
    }
}

internal readonly struct TypeScope
{
    public TypeScope(string sourceNamespace, NamespaceRule[] rules)
    {
        SourceNamespace = sourceNamespace;
        Rules = rules;
    }

    public string SourceNamespace { get; }
    public NamespaceRule[] Rules { get; }
    public bool IsApplicable => Rules.Length > 0;
}

internal readonly struct DiagnosticKey : IEquatable<DiagnosticKey>
{
    private readonly TextSpan _span;
    private readonly string _source;
    private readonly string _identity;
    private readonly string _ruleSource;
    private readonly string _ruleTarget;

    public DiagnosticKey(TextSpan span, string source, string identity, string ruleSource, string ruleTarget)
    {
        _span = span;
        _source = source;
        _identity = identity;
        _ruleSource = ruleSource;
        _ruleTarget = ruleTarget;
    }

    public bool Equals(DiagnosticKey other) =>
        _span.Equals(other._span) &&
        string.Equals(_source, other._source, StringComparison.Ordinal) &&
        string.Equals(_identity, other._identity, StringComparison.Ordinal) &&
        string.Equals(_ruleSource, other._ruleSource, StringComparison.Ordinal) &&
        string.Equals(_ruleTarget, other._ruleTarget, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DiagnosticKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = _span.GetHashCode();
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_source);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_identity);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_ruleSource);
            return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_ruleTarget);
        }
    }
}

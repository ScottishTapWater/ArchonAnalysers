using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ArchonAnalysers.Analysers.ARCHON001;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class InternalsAreInternalAnalyser : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCHON001";
    private const string Category = "Architecture";

    private static readonly LocalizableString Title = "Types in internal namespaces should be internal or private";
    private static readonly LocalizableString MessageFormat = "Type {0} should be internal or private due to being in namespace {1} but is {2}";

    private static readonly LocalizableString Description =
        "This rule validates that all types in defined internal namespaces have either internal or private access modifiers, excluding compiler generated types.";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    private const string SettingsKey = "archon_001.internal_namespace_slugs";
    private const string DefaultNamespaceSlugs = "Internal";

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            ArchonSettings settings = ArchonSettings.Read(startContext.Options, startContext.CancellationToken);
            startContext.RegisterSymbolAction(symbolContext => AnalyzeSymbol(symbolContext, settings), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext obj, ArchonSettings settings)
    {
        string[] slugs = GetNamespaceSlugs(settings);
        string pattern = BuildNamespacePattern(slugs);

        INamespaceSymbol? symbolNamespace = obj.Symbol.ContainingNamespace;

        if (SymbolIsInIrrelevantNamespace(symbolNamespace, pattern))
        {
            return;
        }

        ISymbol symbol = obj.Symbol;

        if (SymbolIsMaskedByContainingType(symbol))
        {
            return;
        }

        if (!SymbolHasProblematicAccessibility(symbol))
        {
            return;
        }

        CreateDiagnosticForProblematicSymbol(symbol, obj);
    }

    private static string[] GetNamespaceSlugs(ArchonSettings settings)
    {
        string configValue = settings.GetValue(SettingsKey) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return configValue
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
        }

        return DefaultNamespaceSlugs.Split(',');
    }

    private static string BuildNamespacePattern(string[] slugs)
    {
        if (slugs.Length == 0)
        {
            return string.Empty;
        }

        if (slugs.Length == 1)
        {
            return $@"^(?:\w+\.)*(?<Slug>{Regex.Escape(slugs[0])})(?:\.\w+)*$";
        }

        IEnumerable<string> escapedSlugs = slugs.Select(Regex.Escape);
        string alternation = string.Join("|", escapedSlugs);
        return $@"^(?:\w+\.)*(?<Slug>{alternation})(?:\.\w+)*$";
    }

    private static void CreateDiagnosticForProblematicSymbol(ISymbol symbol, SymbolAnalysisContext obj)
    {
        SyntaxNode? syntaxNode = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

        // BaseTypeDeclarationSyntax gets everything including enums (TypeDeclarationSyntax misses enums)
        // Technically MemberDeclarationSyntax alone would work here but that would match properties and stuff
        // which, while filtered out by SymbolKind.NamedType would be misleading
        if (syntaxNode is not (MemberDeclarationSyntax or DelegateDeclarationSyntax))
        {
            return; // Something's gone wrong
        }

        SyntaxTokenList modifiers = syntaxNode switch
        {
            BaseTypeDeclarationSyntax btds => btds.Modifiers,
            DelegateDeclarationSyntax dds => dds.Modifiers,
            _ => default
        };

        SyntaxToken? problematicModifier = modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword));

        if (problematicModifier is null)
        {
            return;
        }

        Location location = problematicModifier.Value.GetLocation();

        Diagnostic diagnostic = Diagnostic.Create(Rule, location, obj.Symbol.Name, obj.Symbol.ContainingNamespace.ToDisplayString(),
            obj.Symbol.DeclaredAccessibility.ToString());
        obj.ReportDiagnostic(diagnostic);
    }

    private static bool SymbolHasProblematicAccessibility(ISymbol symbol) => symbol.DeclaredAccessibility is  (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal);

    private static bool SymbolIsMaskedByContainingType(ISymbol symbol)
    {
        INamedTypeSymbol? parent = symbol.ContainingType;

        // Top level, unmasked
        if (parent is null)
        {
            return false;
        }

        // Masked
        if (parent.DeclaredAccessibility is Accessibility.Internal or Accessibility.Private or Accessibility.ProtectedAndInternal)
        {
            return true;
        }

        return SymbolIsMaskedByContainingType(parent);
    }


    private static bool SymbolIsInIrrelevantNamespace(INamespaceSymbol? symbolNamespace, string pattern) => symbolNamespace is null
                                                                                             || symbolNamespace.IsGlobalNamespace
                                                                                             || string.IsNullOrEmpty(pattern)
                                                                                             || !Regex.IsMatch(symbolNamespace.ToDisplayString(), pattern);
}

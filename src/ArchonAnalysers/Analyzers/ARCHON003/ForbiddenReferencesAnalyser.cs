using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ArchonAnalysers.Analysers.ARCHON003;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ForbiddenReferencesAnalyser : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCHON003";
    private const string Category = "Architecture";

    private readonly struct ForbiddenRule
    {
        public string Source { get; }
        public string Target { get; }

        public ForbiddenRule(string source, string target)
        {
            Source = source;
            Target = target;
        }
    }

    private static readonly LocalizableString Title = "Project contains forbidden assembly reference";
    private static readonly LocalizableString MessageFormat = "Assembly '{0}' cannot reference '{1}'. This reference is forbidden by architectural rules.";

    private static readonly LocalizableString Description =
        "This rule validates that projects do not reference assemblies that are forbidden by architectural constraints defined in the Archon settings file. This enforces clean architecture and dependency inversion principles at compile time.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description,
        customTags: ["CompilationEnd"]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    private const string SettingsKey = "archon_003.forbidden_references";
    private static readonly Regex DirectionalRulePattern = new(@"^\s*(?<source>.+?)\s*->\s*(?<target>.+?)\s*$", RegexOptions.Compiled);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        ArchonSettings settings = ArchonSettings.Read(context.Options, context.CancellationToken);
        ForbiddenRule[] allRules = GetForbiddenRules(settings);
        if (allRules.Length == 0)
        {
            return;
        }

        string currentAssembly = context.Compilation.AssemblyName ?? string.Empty;

        // Filter rules that apply to current assembly
        ForbiddenRule[] applicableRules = allRules
            .Where(rule => string.Equals(rule.Source, currentAssembly, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (applicableRules.Length == 0)
        {
            return;
        }

        // Check each reference against applicable rules
        foreach (MetadataReference reference in context.Compilation.References)
        {
            string? assemblyName = GetAssemblyName(reference, context.Compilation);
            if (assemblyName == null || !applicableRules.Any(rule => string.Equals(rule.Target, assemblyName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("ForbiddenAssembly", assemblyName);

            Diagnostic diagnostic = Diagnostic.Create(
                Rule,
                Location.None,
                properties.ToImmutable(),
                currentAssembly,
                assemblyName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static ForbiddenRule[] GetForbiddenRules(ArchonSettings settings)
    {
        string configValue = settings.GetValue(SettingsKey) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configValue))
        {
            return [];
        }


        ForbiddenRule[] rules = configValue.Split(',')
            .Select(rule => rule.Trim())
            .Where(trimmed => !string.IsNullOrWhiteSpace(trimmed))
            .Select(trimmed => DirectionalRulePattern.Match(trimmed))
            .Where(match => match.Success)
            .Select(match => new { match, source = NormalizeAssemblyName(match.Groups["source"].Value) })
            .Select(t => new { t, target = NormalizeAssemblyName(t.match.Groups["target"].Value) })
            .Where(t => !string.IsNullOrWhiteSpace(t.t.source) && !string.IsNullOrWhiteSpace(t.target))
            .Select(t => new ForbiddenRule(t.t.source, t.target))
            .ToArray();

        return rules;
    }


    private static string NormalizeAssemblyName(string name)
    {
        // Strip .dll extension if present
        return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
    }

    private static string? GetAssemblyName(MetadataReference reference, Compilation compilation)
    {
        try
        {
            // Use the compilation to resolve the reference to a symbol
            ISymbol? symbol = compilation.GetAssemblyOrModuleSymbol(reference);

            if (symbol is IAssemblySymbol assemblySymbol)
            {
                return assemblySymbol.Name;
            }
        }
        catch
        {
            // If we can't read the assembly name, skip it
        }
        return null;
    }

}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ArchonAnalysers.Analyzers.ARCHON004;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PackableProjectReferenceAnalyser : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCHON004";
    private const string Category = "Architecture";

    private const string IsPackableKey = "build_property.IsPackable";
    private const string ProjectRefPackabilityMapKey = "build_property._ArchonProjectRefPackabilityMap";

    private static readonly LocalizableString Title = "Packable project should not reference another packable project";
    private static readonly LocalizableString MessageFormat = "Project '{0}' is packable and references packable project '{1}'. Use PackageReference instead of ProjectReference for packable dependencies.";

    private static readonly LocalizableString Description =
        "This rule enforces that packable NuGet projects within a solution reference each other via PackageReference instead of ProjectReference. This ensures proper dependency management and versioning for published packages.";

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

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        SyntaxTree? syntaxTree = context.Compilation.SyntaxTrees.FirstOrDefault();
        if (syntaxTree == null)
        {
            return;
        }

        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);

        if (!IsCurrentProjectPackable(options))
        {
            return;
        }

        Dictionary<string, bool> packableReferences = GetPackableProjectReferences(options);
        if (packableReferences.Count == 0)
        {
            return;
        }

        string currentAssembly = context.Compilation.AssemblyName ?? string.Empty;

        foreach (MetadataReference reference in context.Compilation.References)
        {
            string? assemblyName = GetAssemblyName(reference, context.Compilation);
            if (assemblyName == null)
            {
                continue;
            }

            if (packableReferences.TryGetValue(assemblyName, out bool isPackable) && isPackable)
            {
                Diagnostic diagnostic = Diagnostic.Create(
                    Rule,
                    Location.None,
                    currentAssembly,
                    assemblyName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsCurrentProjectPackable(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(IsPackableKey, out string? isPackableValue))
        {
            return false;
        }

        return string.Equals(isPackableValue, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, bool> GetPackableProjectReferences(AnalyzerConfigOptions options)
    {
        Dictionary<string, bool> result = new(StringComparer.OrdinalIgnoreCase);

        if (!options.TryGetValue(ProjectRefPackabilityMapKey, out string? mapValue) ||
            string.IsNullOrWhiteSpace(mapValue))
        {
            return result;
        }

        string[] entries = mapValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string entry in entries)
        {
            string trimmed = entry.Trim();
            int pipeIndex = trimmed.LastIndexOf('|');
            if (pipeIndex <= 0 || pipeIndex >= trimmed.Length - 1)
            {
                continue;
            }

            string assemblyName = trimmed.Substring(0, pipeIndex);
            string isPackableStr = trimmed.Substring(pipeIndex + 1);
            bool isPackable = string.Equals(isPackableStr, "true", StringComparison.OrdinalIgnoreCase);

            result[assemblyName] = isPackable;
        }

        return result;
    }

    private static string? GetAssemblyName(MetadataReference reference, Compilation compilation)
    {
        try
        {
            ISymbol? symbol = compilation.GetAssemblyOrModuleSymbol(reference);

            if (symbol is IAssemblySymbol assemblySymbol)
            {
                return assemblySymbol.Name;
            }
        }
        catch
        {
            // Ignored
        }
        return null;
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ArchonAnalysers.Analysers;

internal sealed class ArchonSettings
{
    internal static readonly ImmutableArray<string> Keys =
    [
        "archon_001.internal_namespace_slugs",
        "archon_002.public_namespace_slugs",
        "archon_003.forbidden_references",
        "archon_004.forbidden_namespace_references"
    ];

    private const string SettingsMetadataKey = "build_metadata.AdditionalFiles.ArchonSettings";
    private readonly Dictionary<string, string> _values;

    private ArchonSettings(Dictionary<string, string> values, ImmutableArray<Diagnostic> errors)
    {
        _values = values;
        Errors = errors;
    }

    public ImmutableArray<Diagnostic> Errors { get; }

    public string? GetValue(string key) => _values.TryGetValue(key, out string? value) ? value : null;

    public static ArchonSettings Read(AnalyzerOptions options, CancellationToken cancellationToken)
    {
        AdditionalText[] files = options.AdditionalFiles
            .Where(file => IsSettingsFile(file, options.AnalyzerConfigOptionsProvider))
            .ToArray();

        if (files.Length == 0)
        {
            return new(new(StringComparer.OrdinalIgnoreCase), []);
        }

        if (files.Length > 1)
        {
            return new(new(StringComparer.OrdinalIgnoreCase),
                [Diagnostic.Create(ArchonConfigurationAnalyser.InvalidSettingsFile, Location.None,
                    "More than one Archon settings file was supplied")]);
        }

        AdditionalText file = files[0];
        SourceText? text = file.GetText(cancellationToken);
        if (text == null)
        {
            return new(new(StringComparer.OrdinalIgnoreCase),
                [Diagnostic.Create(ArchonConfigurationAnalyser.InvalidSettingsFile, Location.None,
                    $"Could not read '{file.Path}'")]);
        }

        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        ImmutableArray<Diagnostic>.Builder errors = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (TextLine line in text.Lines)
        {
            string content = line.ToString().Trim();
            if (content.Length == 0 || content.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int equals = content.IndexOf('=');
            string key = equals < 0 ? content : content.Substring(0, equals).Trim();
            Location location = Location.Create(file.Path, line.Span, text.Lines.GetLinePositionSpan(line.Span));
            if (equals < 0 || key.Length == 0)
            {
                errors.Add(Diagnostic.Create(ArchonConfigurationAnalyser.InvalidSettingsFile, location,
                    "Expected 'key = value'"));
            }
            else if (!Keys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(Diagnostic.Create(ArchonConfigurationAnalyser.InvalidSettingsFile, location,
                    $"Unknown setting '{key}'"));
            }
            else if (values.ContainsKey(key))
            {
                errors.Add(Diagnostic.Create(ArchonConfigurationAnalyser.InvalidSettingsFile, location,
                    $"Duplicate setting '{key}'"));
            }
            else
            {
                values.Add(key, content.Substring(equals + 1).Trim());
            }
        }

        return new(errors.Count == 0 ? values : new(StringComparer.OrdinalIgnoreCase), errors.ToImmutable());
    }

    private static bool IsSettingsFile(AdditionalText file, AnalyzerConfigOptionsProvider provider)
    {
        AnalyzerConfigOptions fileOptions = provider.GetOptions(file);
        return fileOptions.TryGetValue(SettingsMetadataKey, out string? marked) &&
            string.Equals(marked, "true", StringComparison.OrdinalIgnoreCase);
    }

}

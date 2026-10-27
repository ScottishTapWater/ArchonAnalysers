using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace ArchonAnalysers.Tests.Unit.Analysers;

internal static class ArchonSettingsTest
{
    public static void Add<TAnalyzer>(CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> test, string contents)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        test.TestState.AdditionalFiles.Add(("/.archonanalysers", contents));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", """
            is_global = true
            build_metadata.AdditionalFiles.ArchonSettings = true
            """));
    }
}

using ArchonAnalysers.Analysers.ARCHON002;
using ArchonAnalysers.FixProviders.ARCHON002;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON002;

public class PublicsArePublicFixProviderTests
{
    [Theory]
    [InlineData("struct")]
    [InlineData("interface")]
    [InlineData("record")]
    [InlineData("enum")]
    [InlineData("class")]
    public async Task FixChangesInternalTypeDeclarationsToPublic(string typeKeyword)
    {
        string testCode = $$"""
                            namespace TestApp.Public;
                            {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} {{typeKeyword}} MyType;
                            """;

        string fixedCode = $$"""
                             namespace TestApp.Public;
                             public {{typeKeyword}} MyType;
                             """;

        CSharpCodeFixTest<PublicsArePublicAnalyser, PublicsArePublicFixProvider, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            FixedCode = fixedCode
        };

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FixChangesInternalDelegateToPublic()
    {
        const string testCode = $$"""
                                  namespace TestApp.Public;
                                  {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} delegate void MyDelegate();
                                  """;

        const string fixedCode = """
                                 namespace TestApp.Public;
                                 public delegate void MyDelegate();
                                 """;

        CSharpCodeFixTest<PublicsArePublicAnalyser, PublicsArePublicFixProvider, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            FixedCode = fixedCode
        };

        await test.RunAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("abstract", "class")]
    [InlineData("sealed", "class")]
    [InlineData("static", "class")]
    [InlineData("readonly", "struct")]
    public async Task FixPreservesNonAccessibilityModifiers(string additionalModifiers, string typeDeclaration)
    {
        string testCode = $$"""
                            namespace TestApp.Public;
                            {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} {{additionalModifiers}} {{typeDeclaration}} MyType;
                            """;

        string fixedCode = $$"""
                             namespace TestApp.Public;
                             public {{additionalModifiers}} {{typeDeclaration}} MyType;
                             """;

        CSharpCodeFixTest<PublicsArePublicAnalyser, PublicsArePublicFixProvider, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            FixedCode = fixedCode
        };

        await test.RunAsync(CancellationToken.None);
    }
}

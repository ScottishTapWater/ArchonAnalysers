using ArchonAnalysers.Analysers.ARCHON001;
using ArchonAnalysers.FixProviders.ARCHON001;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON001;

public class InternalsAreInternalFixProviderTests
{
    [Theory]
    [InlineData("struct")]
    [InlineData("interface")]
    [InlineData("record")]
    [InlineData("enum")]
    [InlineData("class")]
    public async Task FixChangesPublicTypeDeclarationsToInternal(string typeKeyword)
    {
        string testCode = $$"""
                            namespace TestApp.Internal;
                            {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} {{typeKeyword}} MyType;
                            """;

        string fixedCode = $$"""
                             namespace TestApp.Internal;
                             internal {{typeKeyword}} MyType;
                             """;

        CSharpCodeFixTest<InternalsAreInternalAnalyser, InternalsAreInternalFixProvider, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            FixedCode = fixedCode
        };

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FixChangesPublicDelegateToInternal()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} delegate void MyDelegate();
                                  """;

        const string fixedCode = """
                                 namespace TestApp.Internal;
                                 internal delegate void MyDelegate();
                                 """;

        CSharpCodeFixTest<InternalsAreInternalAnalyser, InternalsAreInternalFixProvider, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            FixedCode = fixedCode
        };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FixChangesProtectedInternalNestedClassToInternal()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                  {
                                      {|{{InternalsAreInternalAnalyser.DiagnosticId}}:protected|} internal class MyClass;
                                  }
                                  """;

        const string fixedCode = $$"""
                                   namespace TestApp.Internal;
                                   {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                   {
                                       internal class MyClass;
                                   }
                                   """;

        // This is brittle as fuck
        DiagnosticResult expectedRemainingDiagnostic = DiagnosticResult.CompilerError(InternalsAreInternalAnalyser.DiagnosticId).WithSpan(2, 1, 2, 7)
            .WithArguments("OuterClass", "TestApp.Internal", "Public");

        CSharpCodeFixTest<InternalsAreInternalAnalyser, InternalsAreInternalFixProvider, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            CodeActionEquivalenceKey = $"{InternalsAreInternalAnalyser.DiagnosticId}:[58..67)", // This is brittle as fuck
            FixedState = { ExpectedDiagnostics = { expectedRemainingDiagnostic }}
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
                            namespace TestApp.Internal;
                            {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} {{additionalModifiers}} {{typeDeclaration}} MyType;
                            """;

        string fixedCode = $$"""
                             namespace TestApp.Internal;
                             internal {{additionalModifiers}} {{typeDeclaration}} MyType;
                             """;

        CSharpCodeFixTest<InternalsAreInternalAnalyser, InternalsAreInternalFixProvider, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            FixedCode = fixedCode
        };

        await test.RunAsync(CancellationToken.None);
    }
}


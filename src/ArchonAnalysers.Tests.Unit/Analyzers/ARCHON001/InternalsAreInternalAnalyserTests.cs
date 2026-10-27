using ArchonAnalysers.Analysers.ARCHON001;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON001;

public class InternalsAreInternalAnalyserTests
{
    [Fact]
    public async Task DiagnosticAppearsOnPublicClass()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class MyClass;
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnProtectedClassNestedInsidePublicClass()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                  {
                                        {|{{InternalsAreInternalAnalyser.DiagnosticId}}:protected|} class MyClass;
                                  }
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnInternalClassNestedInsidePublicClass()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                  {
                                    internal class MyClass;
                                  }
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnProtectedClassNestedInsideInternalClass()
    {
        const string testCode = """
                                namespace TestApp.Internal;
                                internal class OuterClass
                                {
                                      protected class MyClass;
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnProtectedInternalClassNestedInsideInternalClass()
    {
        const string testCode = """
                                namespace TestApp.Internal;
                                internal class OuterClass
                                {
                                    protected internal class MyClass;
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NoDiagnosticOnPublicClassesNotInInternalNamespace()
    {
        const string testCode = """
                                namespace TestApp.Public;
                                public class MyClass;
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnProtectedInternalClassNestedInsidePublicClass()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                  {
                                      {|{{InternalsAreInternalAnalyser.DiagnosticId}}:protected|} internal class MyClass;
                                  }
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnInternalClass()
    {
        const string testCode = """
                                namespace TestApp.Internal;
                                internal class MyClass;
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnShieldedNestedPublicClass()
    {
        const string testCode = """
                                namespace TestApp.Internal;
                                internal class OuterClass
                                {
                                    public class MyClass;
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnPrivateNestedClass()
    {
        const string testCode = $$"""
                                namespace TestApp.Internal;
                                {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                {
                                    private class MyClass;
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnPrivateProtectedNestedClass()
    {
        const string testCode = $$"""
                                namespace TestApp.Internal;
                                {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                {
                                    private protected class MyClass;
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnPublicRecord()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} record MyRecord;
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnPublicStruct()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} struct MyStruct;
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnPublicInterface()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} interface IMyInterface;
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnPublicEnum()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} enum MyEnum;
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnPublicClassesNestedInsideInternalClass()
    {
        const string testCode = """
                                namespace TestApp.Internal;
                                internal class OuterClass
                                {
                                    public class MiddleClass
                                    {
                                        public class InnerClass;
                                    }
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnAllPublicClassChain()
    {
        const string testCode = $$"""
                                namespace TestApp.Internal;
                                {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                {
                                    {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class MiddleClass
                                    {
                                        {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class InnerClass;
                                    }
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnPublicClassesInChainUntilMasked()
    {
        const string testCode = $$"""
                                namespace TestApp.Internal;
                                {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OutermostClass
                                {
                                    {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                    {
                                        internal class MiddleClass
                                        {
                                            public class InnerClass;
                                        }
                                    }
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnAllLevelsWhenProtectedInMiddle()
    {
        const string testCode = $$"""
                                namespace TestApp.Internal;
                                {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class OuterClass
                                {
                                    {|{{InternalsAreInternalAnalyser.DiagnosticId}}:protected|} class MiddleClass
                                    {
                                        {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class InnerClass;
                                    }
                                }
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnPublicClassInNestedInternalNamespace()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal.SubNamespace;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class MyClass;
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnPublicDelegate()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} delegate void MyDelegate();
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnInternalDelegate()
    {
        const string testCode = """
                                namespace TestApp.Internal;
                                internal delegate void MyDelegate();
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticAppearsOnPublicGenericClass()
    {
        const string testCode = $$"""
                                  namespace TestApp.Internal;
                                  {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class MyClass<T>;
                                  """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DiagnosticDoesntAppearOnInternalGenericClass()
    {
        const string testCode = """
                                namespace TestApp.Internal;
                                internal class MyClass<T>;
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NoDiagnosticOnPublicClassInPublicNamespaceContainingInternalFragment()
    {
        const string testCode = """
                                namespace TestApp.Public.InternalStuff;
                                public class MyClass;
                                """;
        CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }
}

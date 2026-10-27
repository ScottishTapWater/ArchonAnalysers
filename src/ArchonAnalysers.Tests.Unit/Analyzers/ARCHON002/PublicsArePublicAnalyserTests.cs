using ArchonAnalysers.Analysers.ARCHON002;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON002;

public class PublicsArePublicAnalyserTests
{
	[Fact]
	public async Task DiagnosticAppearsOnInternalClass()
	{
		const string testCode = $$"""
		                          namespace TestApp.Public;
		                          {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} class MyClass;
		                          """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticDoesntAppearOnPublicClass()
	{
		const string testCode = """
		                        namespace TestApp.Public;
		                        public class MyClass;
		                        """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticDoesntAppearOnNestedClassRegardlessOfAccessibility()
	{
		const string testCode = """
		                        namespace TestApp.Public;
		                        public class OuterClass
		                        {
		                            private class PrivateNested;
		                            protected class ProtectedNested;
		                            internal class InternalNested;
		                            protected internal class ProtectedInternalNested;
		                            private protected class PrivateProtectedNested;
		                            public class PublicNested;
		                        }
		                        """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticDoesntAppearInNonPublicNamespace()
	{
		const string testCode = """
		                        namespace TestApp.Internal;
		                        internal class MyClass;
		                        """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticAppearsOnPublicRecord()
	{
		const string testCode = $$"""
		                          namespace TestApp.Public;
		                          {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} record MyRecord;
		                          """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticAppearsOnPublicStruct()
	{
		const string testCode = $$"""
		                          namespace TestApp.Public;
		                          {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} struct MyStruct;
		                          """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticAppearsOnPublicInterface()
	{
		const string testCode = $$"""
		                          namespace TestApp.Public;
		                          {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} interface IMyInterface;
		                          """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticAppearsOnPublicEnum()
	{
		const string testCode = $$"""
		                          namespace TestApp.Public;
		                          {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} enum MyEnum;
		                          """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

    [Fact]
    public async Task NoDiagnosticOnInternalClassInPublicNamespaceContainingSectionWithInternalSlugFragment()
    {
        const string testCode = $$"""
                                  namespace TestApp.Public.InternalStuff;
                                  {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} class MyClass;
                                  """;
        CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
        await test.RunAsync(CancellationToken.None);
    }

	[Fact]
	public async Task DiagnosticAppearsOnInternalClassInNestedPublicNamespace()
	{
		const string testCode = $$"""
		                          namespace TestApp.Public.SubNamespace;
		                          {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} class MyClass;
		                          """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticAppearsOnInternalDelegate()
	{
		const string testCode = $$"""
		                          namespace TestApp.Public;
		                          {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} delegate void MyDelegate();
		                          """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticDoesntAppearOnPublicDelegate()
	{
		const string testCode = """
		                        namespace TestApp.Public;
		                        public delegate void MyDelegate();
		                        """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticAppearsOnInternalGenericClass()
	{
		const string testCode = $$"""
		                          namespace TestApp.Public;
		                          {|{{PublicsArePublicAnalyser.DiagnosticId}}:internal|} class MyClass<T>;
		                          """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiagnosticDoesntAppearOnPublicGenericClass()
	{
		const string testCode = """
		                        namespace TestApp.Public;
		                        public class MyClass<T>;
		                        """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NoDiagnosticOnInternalClassInInternalNamespaceContainingPublicFragment()
	{
		const string testCode = """
		                        namespace TestApp.Internal.PublicStuff;
		                        internal class MyClass;
		                        """;
		CSharpAnalyzerTest<PublicsArePublicAnalyser, DefaultVerifier> test = new() { TestCode = testCode };
		await test.RunAsync(CancellationToken.None);
	}
}

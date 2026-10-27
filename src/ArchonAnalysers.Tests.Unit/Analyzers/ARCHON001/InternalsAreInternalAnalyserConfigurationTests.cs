using ArchonAnalysers.Analysers.ARCHON001;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON001;

public class InternalsAreInternalAnalyserConfigurationTests
{
	[Fact]
	public async Task CustomSingleSlug_TriggersOnConfiguredNamespace()
	{
		const string testCode = $$"""
		                          namespace TestApp.Private;
		                          {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class MyClass;
		                          """;

		const string settingsFile = """
		                            archon_001.internal_namespace_slugs = Private
		                            """;

		CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new()
		{
			TestCode = testCode
		};

		ArchonSettingsTest.Add(test, settingsFile);
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task CustomSingleSlug_DoesNotTriggerOnDefaultNamespace()
	{
		const string testCode = """
		                        namespace TestApp.Internal;
		                        public class MyClass;
		                        """;

		const string settingsFile = """
		                            archon_001.internal_namespace_slugs = Private
		                            """;

		CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new()
		{
			TestCode = testCode
		};

		ArchonSettingsTest.Add(test, settingsFile);
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task MultipleSlugs_TriggersOnAllConfiguredNamespaces()
	{
		const string testCode = $$"""
		                          namespace TestApp.Private
		                          {
		                              {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class PrivateClass;
		                          }

		                          namespace TestApp.Hidden
		                          {
		                              {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class HiddenClass;
		                          }

		                          namespace TestApp.Secret
		                          {
		                              {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class SecretClass;
		                          }
		                          """;

		const string settingsFile = """
		                            archon_001.internal_namespace_slugs = Private, Hidden, Secret
		                            """;

		CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new()
		{
			TestCode = testCode
		};

		ArchonSettingsTest.Add(test, settingsFile);
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task MultipleSlugs_DoesNotTriggerOnNonConfiguredNamespace()
	{
		const string testCode = """
		                        namespace TestApp.Public;
		                        public class MyClass;
		                        """;

		const string settingsFile = """
		                            archon_001.internal_namespace_slugs = Private, Hidden
		                            """;

		CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new()
		{
			TestCode = testCode
		};

		ArchonSettingsTest.Add(test, settingsFile);
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NoConfiguration_UsesDefaultInternalSlug()
	{
		const string testCode = $$"""
		                          namespace TestApp.Internal;
		                          {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class MyClass;
		                          """;

		// No settings file added - should use default "Internal"
		CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new()
		{
			TestCode = testCode
		};

		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task EmptyConfiguration_UsesDefaultInternalSlug()
	{
		const string testCode = $$"""
		                          namespace TestApp.Internal;
		                          {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class MyClass;
		                          """;

		const string settingsFile = """
		                            archon_001.internal_namespace_slugs =
		                            """;

		CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new()
		{
			TestCode = testCode
		};

		ArchonSettingsTest.Add(test, settingsFile);
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task WhitespaceInConfiguration_IsProperlyTrimmed()
	{
		const string testCode = $$"""
		                          namespace TestApp.Private
		                          {
		                              {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class PrivateClass;
		                          }

		                          namespace TestApp.Hidden
		                          {
		                              {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class HiddenClass;
		                          }
		                          """;

		const string settingsFile = """
		                            archon_001.internal_namespace_slugs = Private ,  Hidden  ,   Internal
		                            """;

		CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new()
		{
			TestCode = testCode
		};

		ArchonSettingsTest.Add(test, settingsFile);
		await test.RunAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SlugWithSpecialRegexCharacters_IsProperlyEscaped()
	{
		const string testCode = $$"""
		                          namespace TestApp.Internal.Impl;
		                          {|{{InternalsAreInternalAnalyser.DiagnosticId}}:public|} class MyClass;
		                          """;

		const string settingsFile = """
		                            archon_001.internal_namespace_slugs = Internal.Impl
		                            """;

		CSharpAnalyzerTest<InternalsAreInternalAnalyser, DefaultVerifier> test = new()
		{
			TestCode = testCode
		};

		ArchonSettingsTest.Add(test, settingsFile);
		await test.RunAsync(CancellationToken.None);
	}
}

using ArchonAnalysers.Analyzers.ARCHON004;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using static ArchonAnalysers.Tests.Unit.Analyzers.ARCHON004.NamespaceReferenceTest;

namespace ArchonAnalysers.Tests.Unit.Analyzers.ARCHON004;

public class ForbiddenNamespaceReferencesAnalyserTests
{
    [Fact]
    public async Task FieldType_InForbiddenNamespace_ReportsAtTypeName()
    {
        await RunAsync(
            """
            namespace MyApp.Infrastructure { public class Database; }
            namespace MyApp.Domain { public class Service { private Infrastructure.[|Database|]? database; } }
            """,
            "MyApp.Domain->MyApp.Infrastructure");
    }

    [Fact]
    public async Task ChildNamespaces_OnBothSides_AreIncluded()
    {
        await RunAsync(
            """
            namespace MyApp.Infrastructure.Data { public class Database; }
            namespace MyApp.Domain.Services { public class Service { public Infrastructure.Data.[|Database|] Get() => [|new|](); } }
            """,
            "MyApp.Domain->MyApp.Infrastructure");
    }

    [Fact]
    public async Task Matching_IsDirectionalCaseSensitiveAndSegmentAware()
    {
        await RunAsync(
            """
            namespace MyApp.Infrastructure { public class Database; }
            namespace MyApp.DomainModels
            {
                public class One { public MyApp.Infrastructure.Database? Value; }
            }
            namespace myapp.domain
            {
                public class Two { public MyApp.Infrastructure.Database? Value; }
            }
            """,
            "MyApp.Domain->MyApp.Infrastructure");
    }

    [Fact]
    public async Task ConstructedGeneric_CannotHideForbiddenType()
    {
        await RunAsync(
            """
            using System.Collections.Generic;
            namespace Target.Child { public class Secret; }
            namespace Source { public class Service { public List<Target.Child.[|Secret|]> Values = [|[]|]; } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task InferredReturnType_IsReportedAtInvocation()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers { public static class Factory { public static Target.Secret Create() => new(); } }
            namespace Source { public class Service { public void M() { var value = Helpers.Factory.[|Create|](); } } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task TargetTypedConstruction_IsReportedAtNewKeyword()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Source { public class Service { public void M() { Target.[|Secret|] value = [|new|](); } } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task MemberDeclaredOnForbiddenType_IsReportedOnce()
    {
        await RunAsync(
            """
            namespace Target { public static class Api { public static void Execute() { } } }
            namespace Source { public class Service { public void M() => Target.Api.[|Execute|](); } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task ExplicitConstruction_IsNotDuplicatedByInitializerInference()
    {
        await RunAsync(
            "namespace Target { public class Secret; } namespace Source { public class Service { public void M() { var value = new Target.[|Secret|](); } } }",
            "Source->Target");
    }

    [Fact]
    public async Task ConditionalAndNestedMemberResults_AreAnalyzed()
    {
        await RunAsync(
            """
            using Allowed;
            namespace Target { public class Secret; }
            namespace Allowed
            {
                public class Api { public Target.Secret? ForbiddenProperty => null; }
                public static class Extensions { public static object Continue(this Target.Secret value) => new(); }
            }
            namespace Source
            {
                public class Service
                {
                    public object? One(Allowed.Api api) => api?.[|ForbiddenProperty|];
                    public object Two(Allowed.Api api) => api.[|ForbiddenProperty|]!.Continue();
                }
            }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task AttributeArguments_AreAnalyzedIndependentlyOfAttributeType()
    {
        await RunAsync(
            """
            using System;
            namespace Allowed { public class MarkAttribute : Attribute { public MarkAttribute(Type type) { } } }
            namespace Target { public class Secret; }
            namespace Source { [Allowed.Mark(typeof(Target.[|Secret|]))] public class Service; }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task PredefinedKeywordType_IsAnalyzedSemantically()
    {
        await RunAsync(
            "namespace Source { public class Service { public [|string|]? Value; } }",
            "Source->System");
    }

    [Fact]
    public async Task EnclosingGenericArguments_CannotHideForbiddenType()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers
            {
                public class Outer<T> { public class Inner; }
                public static class Factory { public static Outer<Target.Secret>.Inner Create() => new(); }
            }
            namespace Source { public class Service { public void M() { var value = Helpers.Factory.[|Create|](); } } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task TransparentWrappers_PreserveContextualInferredType()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers { public static class Api { public static void Take(Target.Secret value) { } } }
            namespace Source
            {
                public class Service
                {
                    public void M()
                    {
                        Helpers.Api.Take(([|null|]));
                        Helpers.Api.Take(([|default|])!);
                    }
                }
            }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task IdentifierConvertedToForbiddenType_IsReportedAtIdentifier()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers
            {
                public class Input { public static implicit operator Target.Secret(Input value) => new(); }
                public static class Api { public static void Take(Target.Secret value) { } }
            }
            namespace Source { public class Service { public void M(Helpers.Input input) => Helpers.Api.Take([|input|]); } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task ExplicitConstructionStillChecksDistinctContextualConversion()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers
            {
                public class Input { public static implicit operator Target.Secret(Input value) => new(); }
                public static class Api { public static void Take(Target.Secret value) { } }
            }
            namespace Source
            {
                public class Service
                {
                    public void M()
                    {
                        Helpers.Api.Take([|new Helpers.Input()|]);
                        Helpers.Api.Take(([|new Helpers.Input()|]));
                    }
                }
            }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task IndexerReturningForbiddenType_IsAnalyzedInChain()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers { public class Values { public Target.Secret this[int index] => new(); } }
            namespace Source { public class Service { public int M(Helpers.Values values) => [|values[0]|].GetHashCode(); } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task ConditionalIndexerReturningForbiddenType_IsAnalyzed()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers { public class Values { public Target.Secret this[int index] => new(); } }
            namespace Source { public class Service { public bool M(Helpers.Values? values) => values?[|[0]|] == null; } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task TransparentWrapperAroundExplicitConstruction_DoesNotDuplicate()
    {
        await RunAsync(
            "namespace Target { public class Secret; } namespace Source { public class Service { public void M() { var value = (new Target.[|Secret|]()); } } }",
            "Source->Target");
    }

    [Fact]
    public async Task InferredForeachLocalType_IsAnalyzedAtVar()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers
            {
                public class Collection { public Enumerator GetEnumerator() => new(); }
                public class Enumerator
                {
                    public Target.Secret Current => new();
                    public bool MoveNext() => false;
                }
            }
            namespace Source
            {
                public class Service
                {
                    public void M(Helpers.Collection collection)
                    {
                        foreach ([|var|] item in collection) { }
                    }
                }
            }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task ExplicitGenericArgument_IsReportedOnlyAtTypeSyntax()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Helpers { public static class Api { public static void Execute<T>() { } } }
            namespace Source { public class Service { public void M() => Helpers.Api.Execute<Target.[|Secret|]>(); } }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task MetadataReferenceType_IsAnalyzed()
    {
        MetadataReference reference = CreateReference(
            "TargetAssembly",
            "namespace External.Target { public class Secret; } namespace External.Api { public static class Factory { public static External.Target.Secret Create() => new(); } }");

        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test = Create(
            "namespace Source { public class Service { public object M() => External.Api.Factory.[|Create|](); } }",
            "Source->External.Target",
            CompilerDiagnostics.None);
        test.TestState.AdditionalReferences.Add(reference);
        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UnresolvedSymbol_IsIgnored()
    {
        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test = Create(
            "namespace Source { public class Service { public Missing.Type? Value; } }",
            "Source->Missing",
            CompilerDiagnostics.None);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NestedAndDelegateTypes_UseTheirDeclaredNamespace()
    {
        await RunAsync(
            """
            namespace Target { public class Secret; }
            namespace Source
            {
                public class Outer { public class Inner { public Target.[|Secret|]? Value; } }
                public delegate Target.[|Secret|] Factory(Target.[|Secret|] value);
            }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task EveryDeclaredTypeKindAndPartialDeclarations_AreAnalyzed()
    {
        await RunAsync(
            """
            using System;
            namespace System.Runtime.CompilerServices { public sealed class IsExternalInit; }
            namespace Target { public class Secret; public class MarkAttribute : Attribute; }
            namespace Source
            {
                public class ClassType { public Target.[|Secret|]? Value; }
                public record RecordType(Target.[|Secret|] Value);
                public struct StructType { public Target.[|Secret|]? Value; }
                public record struct RecordStructType(Target.[|Secret|] Value);
                public interface InterfaceType { Target.[|Secret|] Get(); }
                [Target.[|Mark|]] public enum EnumType { Value }
                public partial class PartialType { public Target.[|Secret|]? First; }
                public partial class PartialType { public Target.[|Secret|]? Second; }
            }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task BaseInterfaceConstraintAttributeCastPatternTypeofAndNameof_AreAnalyzed()
    {
        await RunAsync(
            """
            using System;
            namespace Target
            {
                public interface IMarker { }
                public class Base { public static int Value; }
                public class Constraint { }
                public class MarkAttribute : Attribute { }
            }
            namespace Source
            {
                [Target.[|Mark|]]
                public class Service<T> : Target.[|Base|], Target.[|IMarker|] where T : Target.[|Constraint|]
                {
                    public bool M(object value) => value is Target.[|Constraint|] &&
                        typeof(Target.[|Constraint|]) != null && nameof(Target.Base.[|Value|]) != null;
                }
            }
            """,
            "Source->Target");
    }

}

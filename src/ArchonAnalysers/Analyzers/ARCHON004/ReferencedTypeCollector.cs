using Microsoft.CodeAnalysis;

namespace ArchonAnalysers.Analyzers.ARCHON004;

internal static class ReferencedTypeCollector
{
    public static IEnumerable<INamedTypeSymbol> GetConstituentNamedTypes(ITypeSymbol? type)
    {
        if (type == null || type.TypeKind == TypeKind.Error)
        {
            yield break;
        }

        if (type is IArrayTypeSymbol array)
        {
            foreach (INamedTypeSymbol constituent in GetConstituentNamedTypes(array.ElementType))
            {
                yield return constituent;
            }
            yield break;
        }

        if (type is IPointerTypeSymbol pointer)
        {
            foreach (INamedTypeSymbol constituent in GetConstituentNamedTypes(pointer.PointedAtType))
            {
                yield return constituent;
            }
            yield break;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            yield break;
        }

        yield return namedType;
        if (namedType.ContainingType != null)
        {
            foreach (INamedTypeSymbol constituent in GetConstituentNamedTypes(namedType.ContainingType))
            {
                yield return constituent;
            }
        }

        foreach (ITypeSymbol typeArgument in namedType.TypeArguments)
        {
            foreach (INamedTypeSymbol constituent in GetConstituentNamedTypes(typeArgument))
            {
                yield return constituent;
            }
        }
    }

    public static string GetNamespaceName(INamespaceSymbol? namespaceSymbol) =>
        namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace
            ? string.Empty
            : namespaceSymbol.ToDisplayString();
}

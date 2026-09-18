using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchonAnalysers.Analyzers.ARCHON004;

internal static class NamespaceReferenceLocations
{
    public static bool ShouldAnalyzeSimpleName(SimpleNameSyntax name, bool isAttributeName)
    {
        return !(isAttributeName ||
            name.Identifier.Text == "var" ||
            name.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == name ||
            name.Parent is MemberBindingExpressionSyntax ||
            name.Parent is QualifiedNameSyntax qualifiedName && qualifiedName.Right != name ||
            name.Parent is AliasQualifiedNameSyntax aliasQualified && aliasQualified.Name != name);
    }

    public static bool IsInferredReferenceContext(ExpressionSyntax expression) => expression.Parent switch
    {
        EqualsValueClauseSyntax equalsValue => equalsValue.Value == expression,
        ArrowExpressionClauseSyntax arrow => arrow.Expression == expression,
        ReturnStatementSyntax returnStatement => returnStatement.Expression == expression,
        AssignmentExpressionSyntax assignment => assignment.Right == expression,
        ArgumentSyntax argument => argument.Expression == expression,
        _ => false
    };

    public static bool HasExplicitTypeSyntax(ExpressionSyntax expression)
    {
        ExpressionSyntax unwrapped = UnwrapTransparentExpression(expression);
        return unwrapped is ObjectCreationExpressionSyntax or CastExpressionSyntax or
            DefaultExpressionSyntax or ArrayCreationExpressionSyntax or
            StackAllocArrayCreationExpressionSyntax ||
            unwrapped is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AsExpression);
    }

    public static ExpressionSyntax UnwrapTransparentExpression(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    continue;
                case CheckedExpressionSyntax checkedExpression:
                    expression = checkedExpression.Expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    public static Location GetInvocationLocation(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.GetLocation(),
        MemberBindingExpressionSyntax memberBinding => memberBinding.Name.GetLocation(),
        ElementBindingExpressionSyntax elementBinding => elementBinding.GetLocation(),
        SimpleNameSyntax simpleName => simpleName.GetLocation(),
        _ => invocation.GetLocation()
    };

    public static bool HasExplicitTypeArguments(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        GenericNameSyntax => true,
        MemberAccessExpressionSyntax { Name: GenericNameSyntax } => true,
        MemberBindingExpressionSyntax { Name: GenericNameSyntax } => true,
        _ => false
    };

    public static Location GetExpressionLocation(ExpressionSyntax expression) => expression switch
    {
        InvocationExpressionSyntax invocation => GetInvocationLocation(invocation),
        ImplicitObjectCreationExpressionSyntax creation => creation.NewKeyword.GetLocation(),
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.GetLocation(),
        MemberBindingExpressionSyntax memberBinding => memberBinding.Name.GetLocation(),
        ElementBindingExpressionSyntax elementBinding => elementBinding.GetLocation(),
        IdentifierNameSyntax identifier => identifier.GetLocation(),
        ConditionalAccessExpressionSyntax conditional => GetExpressionLocation(conditional.WhenNotNull),
        ParenthesizedExpressionSyntax parenthesized => GetExpressionLocation(parenthesized.Expression),
        PostfixUnaryExpressionSyntax postfix => GetExpressionLocation(postfix.Operand),
        _ => expression.GetLocation()
    };

    public static Location GetRightmostNameLocation(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => GetRightmostNameLocation(qualified.Right),
        AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.GetLocation(),
        _ => name.GetLocation()
    };
}

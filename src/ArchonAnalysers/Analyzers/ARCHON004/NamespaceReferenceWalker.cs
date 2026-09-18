using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchonAnalysers.Analyzers.ARCHON004;

internal sealed class NamespaceReferenceWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly NamespaceRuleSet _ruleSet;
    private readonly NamespaceReferenceCompilationState _compilationState;
    private readonly NamespaceReferenceDiagnosticReporter _reporter;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<string, TypeScope> _scopes = new(StringComparer.Ordinal);
    private readonly List<PendingImport> _imports = [];
    private readonly List<string> _fileNamespaces = [];
    private readonly HashSet<string> _seenFileNamespaces = new(StringComparer.Ordinal);

    private TypeScope? _currentTypeScope;
    private string? _currentNamespace;
    private bool _visitingAttributeName;

    public NamespaceReferenceWalker(
        SemanticModel semanticModel,
        NamespaceRuleSet ruleSet,
        NamespaceReferenceCompilationState compilationState,
        Action<Diagnostic> reportDiagnostic,
        CancellationToken cancellationToken)
    {
        _semanticModel = semanticModel;
        _ruleSet = ruleSet;
        _compilationState = compilationState;
        _reporter = new(reportDiagnostic);
        _cancellationToken = cancellationToken;
    }

    public void Analyze(SyntaxNode root)
    {
        Visit(root);
        foreach (PendingImport import in _imports)
        {
            NamespaceImportAnalyser.Analyze(
                import,
                _semanticModel,
                _ruleSet,
                _fileNamespaces,
                _compilationState,
                _reporter,
                _cancellationToken);
        }
    }

    public override void Visit(SyntaxNode? node)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (node is ExpressionSyntax expression && CurrentScope is { } scope &&
            NamespaceReferenceLocations.IsInferredReferenceContext(expression))
        {
            if (NamespaceReferenceLocations.HasExplicitTypeSyntax(expression))
            {
                AnalyzeConvertedExpressionType(expression, scope);
            }
            else
            {
                AnalyzeExpressionType(expression, scope);
            }
        }

        base.Visit(node);
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node) =>
        _imports.Add(new(node, _currentNamespace));

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node) =>
        VisitNamespace(node, () => base.VisitNamespaceDeclaration(node));

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node) =>
        VisitNamespace(node, () => base.VisitFileScopedNamespaceDeclaration(node));

    public override void VisitClassDeclaration(ClassDeclarationSyntax node) => VisitType(node, () => base.VisitClassDeclaration(node));
    public override void VisitStructDeclaration(StructDeclarationSyntax node) => VisitType(node, () => base.VisitStructDeclaration(node));
    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => VisitType(node, () => base.VisitInterfaceDeclaration(node));
    public override void VisitRecordDeclaration(RecordDeclarationSyntax node) => VisitType(node, () => base.VisitRecordDeclaration(node));
    public override void VisitEnumDeclaration(EnumDeclarationSyntax node) => VisitType(node, () => base.VisitEnumDeclaration(node));
    public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node) => VisitType(node, () => base.VisitDelegateDeclaration(node));

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        AnalyzeSimpleName(node);
        base.VisitIdentifierName(node);
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        AnalyzeSimpleName(node);
        base.VisitGenericName(node);
    }

    public override void VisitPredefinedType(PredefinedTypeSyntax node)
    {
        if (CurrentScope is { } scope)
        {
            _reporter.AnalyzeType(
                _semanticModel.GetTypeInfo(node, _cancellationToken).Type,
                node.GetLocation(),
                scope,
                recurse: false);
        }
        base.VisitPredefinedType(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (CurrentScope is { } scope)
        {
            _reporter.AnalyzeSymbol(
                _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol,
                alias: null,
                NamespaceReferenceLocations.GetInvocationLocation(node),
                scope,
                includeMethodTypeArguments: !NamespaceReferenceLocations.HasExplicitTypeArguments(node));
            AnalyzeExpressionType(node, scope);
        }
        base.VisitInvocationExpression(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        if (CurrentScope is { } scope)
        {
            _reporter.AnalyzeSymbol(
                _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol,
                alias: null,
                node.NewKeyword.GetLocation(),
                scope);
            AnalyzeExpressionType(node, scope);
        }
        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (CurrentScope is { } scope && node.Parent is not InvocationExpressionSyntax)
        {
            ISymbol? symbol = _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol;
            if (node.Parent is not MemberAccessExpressionSyntax || symbol is not ITypeSymbol and not INamespaceSymbol)
            {
                _reporter.AnalyzeSymbol(
                    symbol,
                    alias: null,
                    node.Name.GetLocation(),
                    scope,
                    includeMethodTypeArguments: node.Name is not GenericNameSyntax);
            }

            if (_semanticModel.GetOperation(node, _cancellationToken) != null)
            {
                AnalyzeExpressionType(node, scope);
            }
        }
        base.VisitMemberAccessExpression(node);
    }

    public override void VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        if (CurrentScope is { } scope && node.Parent is not InvocationExpressionSyntax)
        {
            _reporter.AnalyzeSymbol(
                _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol,
                alias: null,
                node.Name.GetLocation(),
                scope,
                includeMethodTypeArguments: node.Name is not GenericNameSyntax);
            AnalyzeExpressionType(node, scope);
        }
        base.VisitMemberBindingExpression(node);
    }

    public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        AnalyzeElement(node, node.GetLocation());
        base.VisitElementAccessExpression(node);
    }

    public override void VisitElementBindingExpression(ElementBindingExpressionSyntax node)
    {
        AnalyzeElement(node, node.GetLocation());
        base.VisitElementBindingExpression(node);
    }

    public override void VisitAttribute(AttributeSyntax node)
    {
        if (CurrentScope is { } scope)
        {
            _reporter.AnalyzeSymbol(
                _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol,
                alias: null,
                NamespaceReferenceLocations.GetRightmostNameLocation(node.Name),
                scope);
        }
        bool previous = _visitingAttributeName;
        _visitingAttributeName = true;
        Visit(node.Name);
        _visitingAttributeName = previous;
        Visit(node.ArgumentList);
    }

    private TypeScope? CurrentScope => _currentTypeScope is { IsApplicable: true } scope ? scope : null;

    private void VisitNamespace(BaseNamespaceDeclarationSyntax node, Action visitChildren)
    {
        string? previous = _currentNamespace;
        _currentNamespace = ReferencedTypeCollector.GetNamespaceName(
            _semanticModel.GetDeclaredSymbol(node, _cancellationToken) as INamespaceSymbol);
        visitChildren();
        _currentNamespace = previous;
    }

    private void VisitType(MemberDeclarationSyntax node, Action visitChildren)
    {
        TypeScope? previous = _currentTypeScope;
        INamedTypeSymbol? symbol = _semanticModel.GetDeclaredSymbol(node, _cancellationToken) as INamedTypeSymbol;
        if (symbol != null && symbol.TypeKind != TypeKind.Error)
        {
            string sourceNamespace = ReferencedTypeCollector.GetNamespaceName(symbol.ContainingNamespace);
            if (_seenFileNamespaces.Add(sourceNamespace))
            {
                _fileNamespaces.Add(sourceNamespace);
            }

            if (!_scopes.TryGetValue(sourceNamespace, out TypeScope scope))
            {
                scope = new(sourceNamespace, _ruleSet.ForSource(sourceNamespace));
                _scopes.Add(sourceNamespace, scope);
            }
            _currentTypeScope = scope;
        }

        visitChildren();
        _currentTypeScope = previous;
    }

    private void AnalyzeSimpleName(SimpleNameSyntax node)
    {
        if (CurrentScope is not { } scope)
        {
            return;
        }

        if (node.Identifier.Text == "var" &&
            node.Parent is ForEachStatementSyntax forEach && forEach.Type == node)
        {
            _reporter.AnalyzeSymbol(
                _semanticModel.GetDeclaredSymbol(forEach, _cancellationToken),
                alias: null,
                node.GetLocation(),
                scope);
        }

        if (!NamespaceReferenceLocations.ShouldAnalyzeSimpleName(node, _visitingAttributeName))
        {
            return;
        }

        IAliasSymbol? alias = _semanticModel.GetAliasInfo(node, _cancellationToken);
        _reporter.AnalyzeSymbol(
            _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol,
            alias,
            node.GetLocation(),
            scope,
            recurseDirectType: alias != null,
            includeMethodTypeArguments: node is not GenericNameSyntax);

        if (_semanticModel.GetOperation(node, _cancellationToken) != null)
        {
            AnalyzeExpressionType(node, scope);
        }
    }

    private void AnalyzeElement(ExpressionSyntax node, Location location)
    {
        if (CurrentScope is not { } scope)
        {
            return;
        }
        _reporter.AnalyzeSymbol(
            _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol,
            alias: null,
            location,
            scope);
        AnalyzeExpressionType(node, scope);
    }

    private void AnalyzeExpressionType(ExpressionSyntax expression, TypeScope scope)
    {
        TypeInfo typeInfo = _semanticModel.GetTypeInfo(expression, _cancellationToken);
        Location location = NamespaceReferenceLocations.GetExpressionLocation(expression);
        _reporter.AnalyzeType(typeInfo.Type, location, scope);
        if (!SymbolEqualityComparer.Default.Equals(typeInfo.Type, typeInfo.ConvertedType))
        {
            _reporter.AnalyzeType(typeInfo.ConvertedType, location, scope);
        }
    }

    private void AnalyzeConvertedExpressionType(ExpressionSyntax expression, TypeScope scope)
    {
        TypeInfo typeInfo = _semanticModel.GetTypeInfo(expression, _cancellationToken);
        if (!SymbolEqualityComparer.Default.Equals(typeInfo.Type, typeInfo.ConvertedType))
        {
            _reporter.AnalyzeType(
                typeInfo.ConvertedType,
                NamespaceReferenceLocations.GetExpressionLocation(expression),
                scope);
        }
    }
}

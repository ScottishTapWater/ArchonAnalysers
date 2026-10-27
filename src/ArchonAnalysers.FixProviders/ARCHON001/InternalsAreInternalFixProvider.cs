using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchonAnalysers.Analysers.ARCHON001;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchonAnalysers.FixProviders.ARCHON001;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InternalsAreInternalFixProvider))]
[Shared]
public class InternalsAreInternalFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [InternalsAreInternalAnalyser.DiagnosticId];

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic? diagnostic = context.Diagnostics.FirstOrDefault();

        if (diagnostic == null)
        {
            return Task.CompletedTask;
        }

        CodeAction action = CodeAction.Create("Make Type Internal", ct => CreateChangedDocument(context, ct), diagnostic.Id + ":" + diagnostic.Location.SourceSpan);
        context.RegisterCodeFix(action, diagnostic);

        return Task.CompletedTask;
    }


    private static async Task<Document> CreateChangedDocument(CodeFixContext context, CancellationToken ct)
    {
        SyntaxNode? syntaxRoot = await context.Document.GetSyntaxRootAsync(ct);

        if (syntaxRoot == null)
        {
            return context.Document;
        }

        SyntaxToken troublesomeToken = syntaxRoot.FindToken(context.Span.Start);


        SyntaxNode? parent = troublesomeToken.Parent;

        if (parent is not MemberDeclarationSyntax memberDeclarationSyntax)
        {
            return context.Document;
        }

        SyntaxNode newNode = RemoveTroublesomeSiblings(memberDeclarationSyntax);
        SyntaxNode newRoot = syntaxRoot.ReplaceNode(parent, newNode);
        Document newDocument = context.Document.WithSyntaxRoot(newRoot);

        return newDocument;
    }

    private static MemberDeclarationSyntax RemoveTroublesomeSiblings(MemberDeclarationSyntax declarationSyntax)
    {
        SyntaxTokenList validTokens = new(declarationSyntax.Modifiers.Where(m => !m.IsKind(SyntaxKind.PublicKeyword) && !m.IsKind(SyntaxKind.ProtectedKeyword) && !m.IsKind(SyntaxKind.InternalKeyword)));

        // Safely get trivia from first modifier, or fall back to declaration's leading trivia
        SyntaxToken? firstModifier = declarationSyntax.Modifiers.FirstOrDefault();
        SyntaxToken internalToken = firstModifier.HasValue
            ? SyntaxFactory.Token(SyntaxKind.InternalKeyword).WithTriviaFrom(firstModifier.Value)
            : SyntaxFactory.Token(declarationSyntax.GetLeadingTrivia(), SyntaxKind.InternalKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));

        // Insert internal at the start for conventional modifier ordering
        validTokens = validTokens.Insert(0, internalToken);

        MemberDeclarationSyntax newNode = declarationSyntax.WithModifiers(validTokens);
        return newNode;
    }

    public sealed override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
}

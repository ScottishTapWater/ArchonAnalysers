using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ArchonAnalysers.Analysers.ARCHON003;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace ArchonAnalysers.FixProviders.ARCHON003;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ForbiddenReferencesFixProvider))]
[Shared]
public class ForbiddenReferencesFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [ForbiddenReferencesAnalyser.DiagnosticId];

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic? diagnostic = context.Diagnostics.FirstOrDefault();

        if (diagnostic == null || !diagnostic.Properties.TryGetValue("ForbiddenAssembly", out string? forbiddenAssembly) || string.IsNullOrEmpty(forbiddenAssembly))
        {
            return Task.CompletedTask;
        }

        CodeAction action = CodeAction.Create(
            $"Remove project reference to {forbiddenAssembly}",
            ct => RemoveProjectReferenceAsync(context.Document, forbiddenAssembly!, ct),
            diagnostic.Id + ":" + forbiddenAssembly);

        context.RegisterCodeFix(action, diagnostic);

        return Task.CompletedTask;
    }

    internal static Task<Solution> RemoveProjectReferenceAsync(Document document, string forbiddenAssembly, CancellationToken ct)
    {
#pragma warning disable RS1035 // File IO is allowed in CodeFixProviders
        try
        {
            Project project = document.Project;
            string? projectDir = Path.GetDirectoryName(project.FilePath);

            if (string.IsNullOrEmpty(projectDir))
            {
                return Task.FromResult(project.Solution);
            }

            string[] csprojFiles = Directory.GetFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly);

            if (csprojFiles.Length == 0)
            {
                return Task.FromResult(project.Solution);
            }

            string csprojPath = csprojFiles[0];
            XDocument doc = XDocument.Load(csprojPath);

            // Find ProjectReference elements matching the forbidden assembly
            List<XElement> toRemove = doc.Descendants("ProjectReference")
                .Where(pr =>
                {
                    string? include = pr.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(include))
                    {
                        return false;
                    }

                    // Extract project name from path: ..\Domain\Domain.csproj -> Domain
                    string projectName = Path.GetFileNameWithoutExtension(include);
                    return string.Equals(projectName, forbiddenAssembly, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            // Remove matching elements
            foreach (XElement element in toRemove)
            {
                element.Remove();
            }

            // Save back to file
            doc.Save(csprojPath);

            // Return unchanged solution (file modified on disk)
            return Task.FromResult(project.Solution);
        }
        catch
        {
            // Don't crash the IDE if anything goes wrong
            return Task.FromResult(document.Project.Solution);
        }
#pragma warning restore RS1035
    }

    public sealed override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
}

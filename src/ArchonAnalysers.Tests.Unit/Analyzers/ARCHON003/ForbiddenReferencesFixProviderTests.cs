using System.Xml.Linq;
using ArchonAnalysers.FixProviders.ARCHON003;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON003;

public class ForbiddenReferencesFixProviderTests : IDisposable
{
    private readonly string _tempDirectory;

    public ForbiddenReferencesFixProviderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ARCHON003_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task RemovesMatchingProjectReference()
    {
        // Arrange: Create .csproj with forbidden reference
        string csprojPath = CreateCsproj("Contracts",
            "../Domain/Domain.csproj",
            "../Infrastructure/Infrastructure.csproj");

        (AdhocWorkspace workspace, Document document) = CreateMinimalWorkspace(csprojPath);

        // Act: Call fix provider method directly
        await ForbiddenReferencesFixProvider.RemoveProjectReferenceAsync(
            document, "Domain", CancellationToken.None);

        // Assert: Verify .csproj was modified
        XDocument doc = XDocument.Load(csprojPath);
        List<string?> refs = doc.Descendants("ProjectReference")
            .Select(pr => pr.Attribute("Include")?.Value)
            .ToList();

        Assert.Single(refs);
        Assert.Equal("../Infrastructure/Infrastructure.csproj", refs[0]);
    }

    [Fact]
    public async Task MatchingIsCaseInsensitive()
    {
        // Arrange
        string csprojPath = CreateCsproj("Contracts",
            "../Domain/Domain.csproj");

        (AdhocWorkspace workspace, Document document) = CreateMinimalWorkspace(csprojPath);

        // Act: Try to remove "domain" (lowercase) when reference is "Domain" (titlecase)
        await ForbiddenReferencesFixProvider.RemoveProjectReferenceAsync(
            document, "domain", CancellationToken.None);

        // Assert: Reference should be removed due to case-insensitive matching
        XDocument doc = XDocument.Load(csprojPath);
        List<XElement> projectRefs = doc.Descendants("ProjectReference").ToList();

        Assert.Empty(projectRefs);
    }

    [Fact]
    public async Task PreservesOtherReferences()
    {
        // Arrange
        string csprojPath = CreateCsproj("Contracts",
            "../Domain/Domain.csproj",
            "../Application/Application.csproj",
            "../Infrastructure/Infrastructure.csproj");

        (AdhocWorkspace workspace, Document document) = CreateMinimalWorkspace(csprojPath);

        // Act: Remove only Domain
        await ForbiddenReferencesFixProvider.RemoveProjectReferenceAsync(
            document, "Domain", CancellationToken.None);

        // Assert: Other references should remain
        XDocument doc = XDocument.Load(csprojPath);
        List<string?> refs = doc.Descendants("ProjectReference")
            .Select(pr => pr.Attribute("Include")?.Value)
            .ToList();

        Assert.Equal(2, refs.Count);
        Assert.Contains("../Application/Application.csproj", refs);
        Assert.Contains("../Infrastructure/Infrastructure.csproj", refs);
    }

    [Fact]
    public async Task HandlesNonExistentReferenceGracefully()
    {
        // Arrange
        string csprojPath = CreateCsproj("Contracts",
            "../Infrastructure/Infrastructure.csproj");

        (AdhocWorkspace workspace, Document document) = CreateMinimalWorkspace(csprojPath);

        // Act: Try to remove a reference that doesn't exist
        await ForbiddenReferencesFixProvider.RemoveProjectReferenceAsync(
            document, "Domain", CancellationToken.None);

        // Assert: Should not crash, file should remain unchanged
        XDocument doc = XDocument.Load(csprojPath);
        List<string?> refs = doc.Descendants("ProjectReference")
            .Select(pr => pr.Attribute("Include")?.Value)
            .ToList();

        Assert.Single(refs);
        Assert.Contains("../Infrastructure/Infrastructure.csproj", refs);
    }

    [Fact]
    public async Task HandlesMissingCsprojFile()
    {
        // Arrange: Create workspace but no actual .csproj file
        string nonExistentPath = Path.Combine(_tempDirectory, "NonExistent.csproj");
        (AdhocWorkspace workspace, Document document) = CreateMinimalWorkspace(nonExistentPath);

        // Act: Should not crash due to catch block in fix provider
        Solution result = await ForbiddenReferencesFixProvider.RemoveProjectReferenceAsync(
            document, "Domain", CancellationToken.None);

        // Assert: No exception thrown, solution unchanged
        Assert.NotNull(result);
        Assert.Equal(document.Project.Solution, result);
    }

    [Theory]
    [InlineData("../Domain/Domain.csproj", "Domain")]
    [InlineData("../../Shared/Common/Common.csproj", "Common")]
    [InlineData("Domain.csproj", "Domain")]
    [InlineData("../MyProject.Name/MyProject.Name.csproj", "MyProject.Name")]
    public async Task HandlesVariousPathFormats(string projectReferencePath, string expectedAssemblyName)
    {
        // Arrange
        string csprojPath = CreateCsproj("TestProject", projectReferencePath);
        (AdhocWorkspace workspace, Document document) = CreateMinimalWorkspace(csprojPath);

        // Act
        await ForbiddenReferencesFixProvider.RemoveProjectReferenceAsync(
            document, expectedAssemblyName, CancellationToken.None);

        // Assert: Reference should be removed
        XDocument doc = XDocument.Load(csprojPath);
        List<XElement> projectRefs = doc.Descendants("ProjectReference").ToList();

        Assert.Empty(projectRefs);
    }

    private string CreateCsproj(string projectName, params string[] projectRefs)
    {
        string path = Path.Combine(_tempDirectory, $"{projectName}.csproj");
        XDocument doc = new(
            new XElement("Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",
                    new XElement("TargetFramework", "netstandard2.0")),
                new XElement("ItemGroup",
                    projectRefs.Select(pr =>
                        new XElement("ProjectReference",
                            new XAttribute("Include", pr))))));
        doc.Save(path);
        return path;
    }

    private (AdhocWorkspace workspace, Document document) CreateMinimalWorkspace(string csprojPath)
    {
        AdhocWorkspace workspace = new();
        ProjectInfo projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            filePath: csprojPath);

        Project project = workspace.AddProject(projectInfo);
        Document document = workspace.AddDocument(
            project.Id,
            "Test.cs",
            SourceText.From("namespace Test { }"));

        return (workspace, document);
    }
}

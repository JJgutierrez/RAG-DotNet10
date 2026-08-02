using System;
using System.IO;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Domain.Entities;
using EventArgs.RagPrototype.Infrastructure.Extractors;
using Xunit;

namespace EventArgs.RagPrototype.Tests.UnitTests;

public class ExtractorUnitTests
{
    [Fact]
    public async Task MarkdownExtractor_CanExtractContentAndHash()
    {
        var extractor = new MarkdownExtractor();
        Assert.True(extractor.CanHandle("document.md"));
        Assert.False(extractor.CanHandle("document.txt"));

        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tempPath, "# Heading\nMarkdown test content for RAG prototype.");

        try
        {
            var extracted = await extractor.ExtractAsync(tempPath);
            Assert.NotNull(extracted);
            Assert.Equal(SourceType.File, extracted.SourceType);
            Assert.Contains("Markdown test content", extracted.FullText);
            Assert.Equal(64, extracted.ContentHash.Length);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task TextExtractor_CanExtractContentAndHash()
    {
        var extractor = new TextExtractor();
        Assert.True(extractor.CanHandle("notes.txt"));
        Assert.False(extractor.CanHandle("notes.pdf"));

        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempPath, "Plain text document content for local retrieval.");

        try
        {
            var extracted = await extractor.ExtractAsync(tempPath);
            Assert.NotNull(extracted);
            Assert.Equal(SourceType.File, extracted.SourceType);
            Assert.Contains("Plain text document", extracted.FullText);
            Assert.Equal(64, extracted.ContentHash.Length);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void PdfExtractor_CanHandlePdfExtensionOnly()
    {
        var extractor = new PdfExtractor();
        Assert.True(extractor.CanHandle("manual.pdf"));
        Assert.False(extractor.CanHandle("manual.docx"));
    }
}

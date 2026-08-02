using System;
using System.IO;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Application.Services;
using EventArgs.RagPrototype.Infrastructure.AI;
using EventArgs.RagPrototype.Infrastructure.Extractors;
using EventArgs.RagPrototype.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventArgs.RagPrototype.Tests.UnitTests;

public class IdempotentIngestionTests
{
    [Fact]
    public async Task IngestFile_TwiceWithSameContent_SkipsSecondIngestion()
    {
        var config = new ConfigurationBuilder().Build();
        var store = new PgVectorStore(config, NullLogger<PgVectorStore>.Instance);
        var chunker = new TextChunker();
        var extractors = new[] { new MarkdownExtractor() };
        var embedder = new OllamaEmbeddingGeneratorService(null!, config, NullLogger<OllamaEmbeddingGeneratorService>.Instance);
        var coordinator = new IngestionCoordinator(extractors, chunker, embedder, store, NullLogger<IngestionCoordinator>.Instance);

        string tempPath = Path.Combine(Path.GetTempPath(), $"idempotent_{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tempPath, "# Idempotency Test Document\nIdempotent ingestion must skip unchanged files.");

        try
        {
            // First Ingestion
            var result1 = await coordinator.IngestFileAsync(tempPath);
            Assert.False(result1.SkippedUnchanged);
            Assert.True(result1.ChunksIngested > 0);

            // Second Ingestion (same content hash)
            var result2 = await coordinator.IngestFileAsync(tempPath);
            Assert.True(result2.SkippedUnchanged);
            Assert.Equal(0, result2.ChunksIngested);
            Assert.Equal(result1.ContentHash, result2.ContentHash);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}

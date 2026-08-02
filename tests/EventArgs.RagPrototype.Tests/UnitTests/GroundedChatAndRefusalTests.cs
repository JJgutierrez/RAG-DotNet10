using System;
using System.IO;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Application.Models;
using EventArgs.RagPrototype.Application.Services;
using EventArgs.RagPrototype.Infrastructure.AI;
using EventArgs.RagPrototype.Infrastructure.Extractors;
using EventArgs.RagPrototype.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventArgs.RagPrototype.Tests.UnitTests;

public class GroundedChatAndRefusalTests
{
    [Fact]
    public async Task ChatService_ReturnsRefusal_WhenNoRelevantContextInStore()
    {
        var config = new ConfigurationBuilder().Build();
        var store = new PgVectorStore(config, NullLogger<PgVectorStore>.Instance);
        var chunker = new TextChunker();
        var embedder = new OllamaEmbeddingGeneratorService(null!, config, NullLogger<OllamaEmbeddingGeneratorService>.Instance);
        var chatClient = new OllamaChatClientService(null!, config, NullLogger<OllamaChatClientService>.Instance);
        var chatService = new GroundedChatService(embedder, store, chatClient, NullLogger<GroundedChatService>.Instance);

        // Submit query to empty vector store
        var request = new GroundedChatRequest("What is the security boundary?", TopK: 5, RelevanceThreshold: 0.70, MinEvidenceCount: 1);
        var response = await chatService.ProcessChatQueryAsync(request);

        Assert.NotNull(response);
        Assert.True(response.RefusalState.IsRefused);
        Assert.Equal("NoRelevantContext", response.RefusalState.Reason);
        Assert.Null(response.Answer);
        Assert.Empty(response.Citations);
    }

    [Fact]
    public async Task IngestionAndChat_ReturnsGroundedAnswerAndCitations_WhenEvidenceExists()
    {
        var config = new ConfigurationBuilder().Build();
        var store = new PgVectorStore(config, NullLogger<PgVectorStore>.Instance);
        var chunker = new TextChunker();
        var extractors = new[] { new MarkdownExtractor() };
        var embedder = new OllamaEmbeddingGeneratorService(null!, config, NullLogger<OllamaEmbeddingGeneratorService>.Instance);
        var chatClient = new OllamaChatClientService(null!, config, NullLogger<OllamaChatClientService>.Instance);

        var coordinator = new IngestionCoordinator(extractors, chunker, embedder, store, NullLogger<IngestionCoordinator>.Instance);
        var chatService = new GroundedChatService(embedder, store, chatClient, NullLogger<GroundedChatService>.Instance);

        // Ingest sample document
        string tempFile = Path.Combine(Path.GetTempPath(), "test_doc.md");
        await File.WriteAllTextAsync(tempFile, "# Security Policy\nAll operations run inside a local offline boundary with explicit refusal rules.");

        try
        {
            var ingestResult = await coordinator.IngestFileAsync(tempFile);
            Assert.False(ingestResult.SkippedUnchanged);
            Assert.True(ingestResult.ChunksIngested > 0);

            // Query with negative threshold to match synthetic deterministic vectors in test
            var request = new GroundedChatRequest("What is the security policy?", TopK: 3, RelevanceThreshold: -1.0, MinEvidenceCount: 1);
            var chatResponse = await chatService.ProcessChatQueryAsync(request);

            Assert.NotNull(chatResponse);
            Assert.False(chatResponse.RefusalState.IsRefused);
            Assert.NotNull(chatResponse.Answer);
            Assert.NotEmpty(chatResponse.Citations);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ChatService_ReturnsRefusal_WhenEvidenceCountBelowMinimum()
    {
        var config = new ConfigurationBuilder().Build();
        var store = new PgVectorStore(config, NullLogger<PgVectorStore>.Instance);
        var chunker = new TextChunker();
        var extractors = new[] { new MarkdownExtractor() };
        var embedder = new OllamaEmbeddingGeneratorService(null!, config, NullLogger<OllamaEmbeddingGeneratorService>.Instance);
        var chatClient = new OllamaChatClientService(null!, config, NullLogger<OllamaChatClientService>.Instance);

        var coordinator = new IngestionCoordinator(extractors, chunker, embedder, store, NullLogger<IngestionCoordinator>.Instance);
        var chatService = new GroundedChatService(embedder, store, chatClient, NullLogger<GroundedChatService>.Instance);

        string tempFile = Path.Combine(Path.GetTempPath(), $"single_chunk_{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tempFile, "Single short line of context.");

        try
        {
            await coordinator.IngestFileAsync(tempFile);

            // Require MinEvidenceCount: 5 when only 1 chunk exists -> Expect InsufficientEvidence
            var request = new GroundedChatRequest("What is in the file?", TopK: 3, RelevanceThreshold: -1.0, MinEvidenceCount: 5);
            var response = await chatService.ProcessChatQueryAsync(request);

            Assert.NotNull(response);
            Assert.True(response.RefusalState.IsRefused);
            Assert.Equal("InsufficientEvidence", response.RefusalState.Reason);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

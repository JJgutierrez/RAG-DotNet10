using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Domain.Abstractions;
using EventArgs.RagPrototype.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EventArgs.RagPrototype.Application.Services;

public record IngestionResult(
    string SourceId,
    bool SkippedUnchanged,
    int ChunksIngested,
    string ContentHash,
    string? ErrorDetails = null
);

public class IngestionCoordinator
{
    private readonly IEnumerable<IDocumentExtractor> _extractors;
    private readonly TextChunker _chunker;
    private readonly IEmbeddingGeneratorService _embeddingGenerator;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<IngestionCoordinator> _logger;

    public IngestionCoordinator(
        IEnumerable<IDocumentExtractor> extractors,
        TextChunker chunker,
        IEmbeddingGeneratorService embeddingGenerator,
        IVectorStore vectorStore,
        ILogger<IngestionCoordinator> logger)
    {
        _extractors = extractors;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var extractor = _extractors.FirstOrDefault(e => e.CanHandle(filePath));
        if (extractor == null)
        {
            _logger.LogWarning("No suitable extractor found for file {FilePath}", filePath);
            return new IngestionResult(filePath, false, 0, string.Empty, $"Unsupported file type: {Path.GetExtension(filePath)}");
        }

        try
        {
            var extracted = await extractor.ExtractAsync(filePath, cancellationToken);
            var existingHash = await _vectorStore.GetSourceContentHashAsync(extracted.SourceId, cancellationToken);

            if (string.Equals(existingHash, extracted.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("File {SourceId} is unchanged (SHA-256 match). Skipping re-ingestion.", extracted.SourceId);
                return new IngestionResult(extracted.SourceId, true, 0, extracted.ContentHash);
            }

            _logger.LogInformation("Processing changed/new file {SourceId}...", extracted.SourceId);

            var rawChunks = _chunker.ChunkText(extracted.FullText);
            if (rawChunks.Count == 0)
            {
                _logger.LogWarning("File {SourceId} produced 0 text chunks.", extracted.SourceId);
                return new IngestionResult(extracted.SourceId, false, 0, extracted.ContentHash);
            }

            var embeddings = await _embeddingGenerator.GenerateEmbeddingsAsync(rawChunks, cancellationToken);

            var domainChunks = new List<IngestedChunk>();
            for (int i = 0; i < rawChunks.Count; i++)
            {
                domainChunks.Add(new IngestionDocumentChunkBuilder()
                    .WithSource(extracted.SourceId, extracted.SourceType, extracted.LocationOrTableName, extracted.RecordKey)
                    .WithChunkIndex(i)
                    .WithText(rawChunks[i])
                    .WithEmbedding(embeddings[i])
                    .Build());
            }

            await _vectorStore.DeleteChunksForSourceAsync(extracted.SourceId, cancellationToken);
            await _vectorStore.SaveChunksAsync(domainChunks, extracted.ContentHash, cancellationToken);

            _logger.LogInformation("Successfully ingested {Count} chunks for source {SourceId}.", domainChunks.Count, extracted.SourceId);
            return new IngestionResult(extracted.SourceId, false, domainChunks.Count, extracted.ContentHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest file {FilePath}", filePath);
            return new IngestionResult(filePath, false, 0, string.Empty, ex.Message);
        }
    }
}

public class IngestionDocumentChunkBuilder
{
    private readonly IngestedChunk _chunk = new();

    public IngestionDocumentChunkBuilder WithSource(string sourceId, SourceType sourceType, string location, string? recordKey)
    {
        _chunk.SourceId = sourceId;
        _chunk.SourceType = sourceType;
        _chunk.DocumentOrTableName = location;
        _chunk.RecordKey = recordKey;
        return this;
    }

    public IngestionDocumentChunkBuilder WithChunkIndex(int index)
    {
        _chunk.ChunkIndex = index;
        return this;
    }

    public IngestionDocumentChunkBuilder WithText(string text)
    {
        _chunk.Text = text;
        return this;
    }

    public IngestionDocumentChunkBuilder WithEmbedding(float[] embedding)
    {
        _chunk.Embedding = embedding;
        return this;
    }

    public IngestedChunk Build()
    {
        _chunk.UtcTimestamp = DateTime.UtcNow;
        return _chunk;
    }
}

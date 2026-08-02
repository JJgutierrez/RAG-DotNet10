using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Application.Models;
using EventArgs.RagPrototype.Domain.Abstractions;
using EventArgs.RagPrototype.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EventArgs.RagPrototype.Application.Services;

public class GroundedChatService
{
    private readonly IEmbeddingGeneratorService _embeddingGenerator;
    private readonly IVectorStore _vectorStore;
    private readonly IChatClientService _chatClient;
    private readonly ILogger<GroundedChatService> _logger;

    public GroundedChatService(
        IEmbeddingGeneratorService embeddingGenerator,
        IVectorStore vectorStore,
        IChatClientService chatClient,
        ILogger<GroundedChatService> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<GroundedChatResponse> ProcessChatQueryAsync(
        GroundedChatRequest request,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[{CorrelationId}] Processing chat request: '{Prompt}' (TopK={TopK}, Threshold={Threshold}, MinCount={MinCount})",
            correlationId, request.Prompt, request.TopK, request.RelevanceThreshold, request.MinEvidenceCount);

        try
        {
            // Step 1: Generate Query Embedding
            var queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(request.Prompt, cancellationToken);

            // Step 2: Vector Similarity Search
            var searchResults = await _vectorStore.SearchSimilarAsync(
                queryVector,
                request.TopK,
                minSimilarityThreshold: -1.0, // Fetch candidates first across cosine range [-1, 1]
                cancellationToken: cancellationToken);

            stopwatch.Stop();
            long latencyMs = stopwatch.ElapsedMilliseconds;

            if (searchResults.Count == 0)
            {
                _logger.LogWarning("[{CorrelationId}] Refused: Zero chunks retrieved from vector store.", correlationId);
                return new GroundedChatResponse(
                    correlationId,
                    Answer: null,
                    RefusalState: new RefusalState(true, "NoRelevantContext"),
                    Citations: Array.Empty<Citation>(),
                    RetrievalSummary: new RetrievalSummary(0, 0.0, latencyMs));
            }

            double highestScore = searchResults.Max(r => r.SimilarityScore);

            // Step 3: Threshold Filtering
            var relevantResults = searchResults
                .Where(r => r.SimilarityScore >= request.RelevanceThreshold)
                .OrderByDescending(r => r.SimilarityScore)
                .ToList();

            if (relevantResults.Count == 0)
            {
                _logger.LogWarning("[{CorrelationId}] Refused: Highest score ({Score:F3}) below threshold ({Threshold:F3}).",
                    correlationId, highestScore, request.RelevanceThreshold);

                return new GroundedChatResponse(
                    correlationId,
                    Answer: null,
                    RefusalState: new RefusalState(true, "NoRelevantContext"),
                    Citations: Array.Empty<Citation>(),
                    RetrievalSummary: new RetrievalSummary(searchResults.Count, highestScore, latencyMs));
            }

            if (relevantResults.Count < request.MinEvidenceCount)
            {
                _logger.LogWarning("[{CorrelationId}] Refused: Relevant chunk count ({Count}) < minEvidenceCount ({MinCount}).",
                    correlationId, relevantResults.Count, request.MinEvidenceCount);

                return new GroundedChatResponse(
                    correlationId,
                    Answer: null,
                    RefusalState: new RefusalState(true, "InsufficientEvidence"),
                    Citations: Array.Empty<Citation>(),
                    RetrievalSummary: new RetrievalSummary(searchResults.Count, highestScore, latencyMs));
            }

            // Step 4: Construct Citations & Grounding Prompt Context
            var citations = relevantResults.Select(r => new Citation(
                SourceId: r.Chunk.SourceId,
                SourceType: r.Chunk.SourceType,
                LocationName: r.Chunk.DocumentOrTableName,
                RecordKey: r.Chunk.RecordKey,
                ChunkIndex: r.Chunk.ChunkIndex,
                RelevanceScore: Math.Round(r.SimilarityScore, 4)
            )).ToList();

            var contextBuilder = new StringBuilder();
            for (int i = 0; i < relevantResults.Count; i++)
            {
                var chunk = relevantResults[i].Chunk;
                contextBuilder.AppendLine($"--- EVIDENCE BLOCK [{i + 1}] ---");
                contextBuilder.AppendLine($"Source: {chunk.DocumentOrTableName} (ID: {chunk.SourceId}, Index: {chunk.ChunkIndex})");
                contextBuilder.AppendLine($"Content:\n{chunk.Text}");
                contextBuilder.AppendLine();
            }

            // Step 5: Call LLM with Strict Grounding Prompt
            var answerText = await _chatClient.GenerateGroundedAnswerAsync(
                request.Prompt,
                contextBuilder.ToString(),
                cancellationToken);

            _logger.LogInformation("[{CorrelationId}] Answer successfully generated with {CitationCount} citations.",
                correlationId, citations.Count);

            return new GroundedChatResponse(
                correlationId,
                Answer: answerText,
                RefusalState: new RefusalState(false, null),
                Citations: citations,
                RetrievalSummary: new RetrievalSummary(searchResults.Count, highestScore, latencyMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Dependency failure occurred while processing query.", correlationId);

            return new GroundedChatResponse(
                correlationId,
                Answer: null,
                RefusalState: new RefusalState(true, "DependencyFailure"),
                Citations: Array.Empty<Citation>(),
                RetrievalSummary: new RetrievalSummary(0, 0.0, stopwatch.ElapsedMilliseconds));
        }
    }
}

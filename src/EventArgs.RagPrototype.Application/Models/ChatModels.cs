using System.Collections.Generic;
using EventArgs.RagPrototype.Domain.ValueObjects;

namespace EventArgs.RagPrototype.Application.Models;

public record GroundedChatRequest(
    string Prompt,
    int TopK = 5,
    double RelevanceThreshold = 0.70,
    int MinEvidenceCount = 1
);

public record RefusalState(
    bool IsRefused,
    string? Reason
);

public record RetrievalSummary(
    int TotalChunksRetrieved,
    double HighestRelevanceScore,
    long LatencyMs
);

public record GroundedChatResponse(
    string CorrelationId,
    string? Answer,
    RefusalState RefusalState,
    IReadOnlyList<Citation> Citations,
    RetrievalSummary RetrievalSummary
);

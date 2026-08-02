using EventArgs.RagPrototype.Domain.Entities;

namespace EventArgs.RagPrototype.Domain.ValueObjects;

public record Citation(
    string SourceId,
    SourceType SourceType,
    string LocationName,
    string? RecordKey,
    int ChunkIndex,
    double RelevanceScore
);

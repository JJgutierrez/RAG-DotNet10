using System;

namespace EventArgs.RagPrototype.Domain.Entities;

public class IngestedChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceId { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public string DocumentOrTableName { get; set; } = string.Empty;
    public string? RecordKey { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DateTime UtcTimestamp { get; set; } = DateTime.UtcNow;
}

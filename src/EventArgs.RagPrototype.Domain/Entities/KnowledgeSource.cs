namespace EventArgs.RagPrototype.Domain.Entities;

public enum SourceType
{
    File = 0,
    DatabaseRecord = 1
}

public class KnowledgeSource
{
    public string Id { get; set; } = string.Empty;
    public SourceType Type { get; set; }
    public string LocationOrTableName { get; set; } = string.Empty;
    public string? RecordKey { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTime UtcIngestedAt { get; set; }
}

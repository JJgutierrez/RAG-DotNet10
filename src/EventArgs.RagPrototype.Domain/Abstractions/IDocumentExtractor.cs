using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Domain.Entities;

namespace EventArgs.RagPrototype.Domain.Abstractions;

public record ExtractedDocument(
    string SourceId,
    SourceType SourceType,
    string LocationOrTableName,
    string? RecordKey,
    string FullText,
    string ContentHash
);

public interface IDocumentExtractor
{
    bool CanHandle(string filePath);
    Task<ExtractedDocument> ExtractAsync(string filePath, CancellationToken cancellationToken = default);
}

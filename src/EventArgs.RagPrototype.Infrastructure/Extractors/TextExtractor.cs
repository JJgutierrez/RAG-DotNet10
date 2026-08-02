using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Domain.Abstractions;
using EventArgs.RagPrototype.Domain.Entities;
using EventArgs.RagPrototype.Domain.ValueObjects;

namespace EventArgs.RagPrototype.Infrastructure.Extractors;

public class TextExtractor : IDocumentExtractor
{
    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return string.Equals(ext, ".txt", System.StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ExtractedDocument> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var fileName = Path.GetFileName(filePath);
        var hash = HashFingerprint.ComputeSha256(content);

        return new ExtractedDocument(
            SourceId: filePath,
            SourceType: SourceType.File,
            LocationOrTableName: fileName,
            RecordKey: null,
            FullText: content,
            ContentHash: hash);
    }
}

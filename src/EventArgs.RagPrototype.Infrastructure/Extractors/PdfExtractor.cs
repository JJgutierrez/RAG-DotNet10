using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Domain.Abstractions;
using EventArgs.RagPrototype.Domain.Entities;
using EventArgs.RagPrototype.Domain.ValueObjects;
using UglyToad.PdfPig;

namespace EventArgs.RagPrototype.Infrastructure.Extractors;

public class PdfExtractor : IDocumentExtractor
{
    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ExtractedDocument> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var fileName = Path.GetFileName(filePath);

        using (var pdf = PdfDocument.Open(filePath))
        {
            foreach (var page in pdf.GetPages())
            {
                sb.AppendLine($"--- Page {page.Number} ---");
                sb.AppendLine(page.Text);
            }
        }

        var fullText = sb.ToString();
        var hash = HashFingerprint.ComputeSha256(fullText);

        return Task.FromResult(new ExtractedDocument(
            SourceId: filePath,
            SourceType: SourceType.File,
            LocationOrTableName: fileName,
            RecordKey: null,
            FullText: fullText,
            ContentHash: hash));
    }
}

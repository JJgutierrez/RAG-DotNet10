using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Domain.Entities;

namespace EventArgs.RagPrototype.Domain.Abstractions;

public record VectorSearchResult(
    IngestedChunk Chunk,
    double Distance,
    double SimilarityScore // 1.0 - Cosine Distance
);

public interface IVectorStore
{
    Task EnsureSchemaCreatedAsync(CancellationToken cancellationToken = default);
    Task<string?> GetSourceContentHashAsync(string sourceId, CancellationToken cancellationToken = default);
    Task DeleteChunksForSourceAsync(string sourceId, CancellationToken cancellationToken = default);
    Task SaveChunksAsync(IEnumerable<IngestedChunk> chunks, string contentHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topK,
        double minSimilarityThreshold = -1.0,
        CancellationToken cancellationToken = default);
}

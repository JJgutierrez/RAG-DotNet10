using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventArgs.RagPrototype.Domain.Abstractions;

public interface IEmbeddingGeneratorService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}

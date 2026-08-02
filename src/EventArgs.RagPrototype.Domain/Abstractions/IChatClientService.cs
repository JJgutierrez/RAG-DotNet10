using System.Threading;
using System.Threading.Tasks;

namespace EventArgs.RagPrototype.Domain.Abstractions;

public interface IChatClientService
{
    Task<string> GenerateGroundedAnswerAsync(string prompt, string contextBlock, CancellationToken cancellationToken = default);
}

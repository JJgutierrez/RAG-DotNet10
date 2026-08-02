using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace EventArgs.RagPrototype.Infrastructure.AI;

public class OllamaEmbeddingGeneratorService : IEmbeddingGeneratorService
{
    private readonly IOllamaApiClient _ollamaClient;
    private readonly string _modelName;
    private readonly ILogger<OllamaEmbeddingGeneratorService> _logger;
    private const int DefaultDimension = 768;

    public OllamaEmbeddingGeneratorService(
        IOllamaApiClient ollamaClient,
        IConfiguration configuration,
        ILogger<OllamaEmbeddingGeneratorService> logger)
    {
        _ollamaClient = ollamaClient;
        _modelName = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_ollamaClient != null)
            {
                var req = new OllamaSharp.Models.EmbedRequest { Model = _modelName, Input = new List<string> { text } };
                var response = await _ollamaClient.EmbedAsync(req, cancellationToken);
                if (response?.Embeddings != null && response.Embeddings.Count > 0)
                {
                    var floatList = response.Embeddings[0];
                    return floatList.Select(d => (float)d).ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama embedding generation failed or unavailable. Falling back to deterministic vector embedding.");
        }

        return GenerateDeterministicVector(text, DefaultDimension);
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        var results = new List<float[]>();

        foreach (var text in textList)
        {
            var vector = await GenerateEmbeddingAsync(text, cancellationToken);
            results.Add(vector);
        }

        return results;
    }

    /// <summary>
    /// Computes a normalized 768-dim deterministic vector based on SHA-256 seed for offline/test determinism.
    /// </summary>
    public static float[] GenerateDeterministicVector(string text, int dimensions = DefaultDimension)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        var vector = new float[dimensions];

        var rng = new Random(BitConverter.ToInt32(hash, 0));
        double sumSq = 0;

        for (int i = 0; i < dimensions; i++)
        {
            float val = (float)(rng.NextDouble() * 2.0 - 1.0);
            vector[i] = val;
            sumSq += val * val;
        }

        // L2 normalize
        float norm = (float)Math.Sqrt(sumSq);
        if (norm > 0)
        {
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }
}

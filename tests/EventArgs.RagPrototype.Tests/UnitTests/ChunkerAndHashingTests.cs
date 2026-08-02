using System.Linq;
using EventArgs.RagPrototype.Application.Services;
using EventArgs.RagPrototype.Domain.ValueObjects;
using Xunit;

namespace EventArgs.RagPrototype.Tests.UnitTests;

public class ChunkerAndHashingTests
{
    [Fact]
    public void TextChunker_SplitsText_WithConfiguredOverlap()
    {
        var options = new TextChunkerOptions { MaxChunkSizeWords = 10, ChunkOverlapWords = 2 };
        var chunker = new TextChunker(options);

        string text = "Word1 Word2 Word3 Word4 Word5 Word6 Word7 Word8 Word9 Word10 Word11 Word12 Word13 Word14 Word15";
        var chunks = chunker.ChunkText(text);

        Assert.NotNull(chunks);
        Assert.True(chunks.Count > 1);
        Assert.Contains("Word1", chunks[0]);
    }

    [Fact]
    public void HashFingerprint_ComputesDeterministicSha256()
    {
        string input = "EventArgs LLC Production RAG Prototype Specification";
        string hash1 = HashFingerprint.ComputeSha256(input);
        string hash2 = HashFingerprint.ComputeSha256(input);

        Assert.NotNull(hash1);
        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1, hash2);
    }
}

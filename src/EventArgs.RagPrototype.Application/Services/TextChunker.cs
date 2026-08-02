using System;
using System.Collections.Generic;
using System.Linq;

namespace EventArgs.RagPrototype.Application.Services;

public class TextChunkerOptions
{
    public int MaxChunkSizeWords { get; set; } = 250;
    public int ChunkOverlapWords { get; set; } = 40;
}

public class TextChunker
{
    private readonly TextChunkerOptions _options;

    public TextChunker(TextChunkerOptions? options = null)
    {
        _options = options ?? new TextChunkerOptions();
    }

    public List<string> ChunkText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return new List<string>();

        var chunks = new List<string>();
        int index = 0;
        int step = Math.Max(1, _options.MaxChunkSizeWords - _options.ChunkOverlapWords);

        while (index < words.Length)
        {
            var chunkWords = words.Skip(index).Take(_options.MaxChunkSizeWords);
            var chunkText = string.Join(" ", chunkWords);
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                chunks.Add(chunkText);
            }

            index += step;
            if (index >= words.Length)
                break;
        }

        return chunks;
    }
}

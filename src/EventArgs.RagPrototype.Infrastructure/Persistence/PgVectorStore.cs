using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Domain.Abstractions;
using EventArgs.RagPrototype.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventArgs.RagPrototype.Infrastructure.Persistence;

public class PgVectorStore : IVectorStore
{
    private readonly string _connectionString;
    private readonly ILogger<PgVectorStore> _logger;

    // In-memory backing store for local testing & deterministic evaluation when DB container is not active
    private static readonly ConcurrentDictionary<Guid, IngestedChunk> InMemoryChunks = new();
    private static readonly ConcurrentDictionary<string, string> SourceHashes = new();

    public PgVectorStore(IConfiguration configuration, ILogger<PgVectorStore> logger)
    {
        _connectionString = configuration.GetConnectionString("PostgresVector") 
            ?? "Host=localhost;Port=5432;Database=ragdb;Username=raguser;Password=ragpassword";
        _logger = logger;
    }

    public async Task EnsureSchemaCreatedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE EXTENSION IF NOT EXISTS vector;
                CREATE TABLE IF NOT EXISTS knowledge_sources (
                    source_id TEXT PRIMARY KEY,
                    content_hash TEXT NOT NULL,
                    ingested_at TIMESTAMP WITH TIME ZONE NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ingested_chunks (
                    id UUID PRIMARY KEY,
                    source_id TEXT NOT NULL,
                    source_type INT NOT NULL,
                    document_name TEXT NOT NULL,
                    record_key TEXT,
                    chunk_index INT NOT NULL,
                    text_content TEXT NOT NULL,
                    embedding vector(768) NOT NULL,
                    utc_timestamp TIMESTAMP WITH TIME ZONE NOT NULL
                );
            ";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("PostgreSQL vector schema ensured successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize PostgreSQL schema directly. Operating in memory-backed mode.");
        }
    }

    public async Task<string?> GetSourceContentHashAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT content_hash FROM knowledge_sources WHERE source_id = @sid;";
            cmd.Parameters.AddWithValue("sid", sourceId);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                return (string)result;
            }
        }
        catch
        {
            // Fallback to in-memory store
        }

        SourceHashes.TryGetValue(sourceId, out var hash);
        return hash;
    }

    public async Task DeleteChunksForSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ingested_chunks WHERE source_id = @sid; DELETE FROM knowledge_sources WHERE source_id = @sid;";
            cmd.Parameters.AddWithValue("sid", sourceId);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Fallback
        }

        var toRemove = InMemoryChunks.Where(kvp => kvp.Value.SourceId == sourceId).Select(kvp => kvp.Key).ToList();
        foreach (var key in toRemove)
        {
            InMemoryChunks.TryRemove(key, out _);
        }
        SourceHashes.TryRemove(sourceId, out _);
    }

    public async Task SaveChunksAsync(IEnumerable<IngestedChunk> chunks, string contentHash, CancellationToken cancellationToken = default)
    {
        var chunkList = chunks.ToList();
        if (chunkList.Count == 0) return;

        string sourceId = chunkList[0].SourceId;

        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var tx = await conn.BeginTransactionAsync(cancellationToken);

            using (var cmdSource = conn.CreateCommand())
            {
                cmdSource.Transaction = tx;
                cmdSource.CommandText = @"
                    INSERT INTO knowledge_sources (source_id, content_hash, ingested_at)
                    VALUES (@sid, @hash, @now)
                    ON CONFLICT (source_id) DO UPDATE SET content_hash = EXCLUDED.content_hash, ingested_at = EXCLUDED.ingested_at;";
                cmdSource.Parameters.AddWithValue("sid", sourceId);
                cmdSource.Parameters.AddWithValue("hash", contentHash);
                cmdSource.Parameters.AddWithValue("now", DateTime.UtcNow);
                await cmdSource.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var chunk in chunkList)
            {
                using var cmdChunk = conn.CreateCommand();
                cmdChunk.Transaction = tx;
                cmdChunk.CommandText = @"
                    INSERT INTO ingested_chunks (id, source_id, source_type, document_name, record_key, chunk_index, text_content, embedding, utc_timestamp)
                    VALUES (@id, @sid, @stype, @doc, @rkey, @cidx, @text, @vec::vector, @ts);";
                cmdChunk.Parameters.AddWithValue("id", chunk.Id);
                cmdChunk.Parameters.AddWithValue("sid", chunk.SourceId);
                cmdChunk.Parameters.AddWithValue("stype", (int)chunk.SourceType);
                cmdChunk.Parameters.AddWithValue("doc", chunk.DocumentOrTableName);
                cmdChunk.Parameters.AddWithValue("rkey", (object?)chunk.RecordKey ?? DBNull.Value);
                cmdChunk.Parameters.AddWithValue("cidx", chunk.ChunkIndex);
                cmdChunk.Parameters.AddWithValue("text", chunk.Text);
                cmdChunk.Parameters.AddWithValue("vec", $"[{string.Join(",", chunk.Embedding)}]");
                cmdChunk.Parameters.AddWithValue("ts", chunk.UtcTimestamp);

                await cmdChunk.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            // Fallback to in-memory store
        }

        foreach (var chunk in chunkList)
        {
            InMemoryChunks[chunk.Id] = chunk;
        }
        SourceHashes[sourceId] = contentHash;
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topK,
        double minSimilarityThreshold = 0.0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, source_id, source_type, document_name, record_key, chunk_index, text_content, utc_timestamp,
                       (1 - (embedding <=> @qvec::vector)) AS similarity_score,
                       (embedding <=> @qvec::vector) AS distance
                FROM ingested_chunks
                ORDER BY distance ASC
                LIMIT @topk;";
            cmd.Parameters.AddWithValue("qvec", $"[{string.Join(",", queryEmbedding)}]");
            cmd.Parameters.AddWithValue("topk", topK);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var results = new List<VectorSearchResult>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var score = Convert.ToDouble(reader["similarity_score"]);
                if (score >= minSimilarityThreshold)
                {
                    var chunk = new IngestedChunk
                    {
                        Id = reader.GetGuid(0),
                        SourceId = reader.GetString(1),
                        SourceType = (SourceType)reader.GetInt32(2),
                        DocumentOrTableName = reader.GetString(3),
                        RecordKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ChunkIndex = reader.GetInt32(5),
                        Text = reader.GetString(6),
                        UtcTimestamp = reader.GetDateTime(7)
                    };
                    results.Add(new VectorSearchResult(chunk, Convert.ToDouble(reader["distance"]), score));
                }
            }

            if (results.Count > 0)
                return results;
        }
        catch
        {
            // Fallback to in-memory cosine similarity search
        }

        // Cosine similarity search over in-memory chunks
        var searchList = new List<VectorSearchResult>();
        foreach (var chunk in InMemoryChunks.Values)
        {
            double sim = ComputeCosineSimilarity(queryEmbedding, chunk.Embedding);
            double dist = 1.0 - sim;
            if (sim >= minSimilarityThreshold)
            {
                searchList.Add(new VectorSearchResult(chunk, dist, sim));
            }
        }

        return searchList
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topK)
            .ToList();
    }

    public static double ComputeCosineSimilarity(float[] vecA, float[] vecB)
    {
        if (vecA == null || vecB == null || vecA.Length == 0 || vecB.Length == 0)
            return 0.0;

        int len = Math.Min(vecA.Length, vecB.Length);
        double dot = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < len; i++)
        {
            dot += vecA[i] * vecB[i];
            normA += vecA[i] * vecA[i];
            normB += vecB[i] * vecB[i];
        }

        if (normA <= 0.0 || normB <= 0.0) return 0.0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}

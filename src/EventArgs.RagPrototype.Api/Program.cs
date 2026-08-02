using System;
using System.IO;
using System.Threading.RateLimiting;
using EventArgs.RagPrototype.Application.Models;
using EventArgs.RagPrototype.Application.Services;
using EventArgs.RagPrototype.Domain.Abstractions;
using EventArgs.RagPrototype.Infrastructure.AI;
using EventArgs.RagPrototype.Infrastructure.Extractors;
using EventArgs.RagPrototype.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("InteractivePolicy", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

// Configure Services & Dependency Injection
builder.Services.AddSingleton<TextChunker>();
builder.Services.AddSingleton<IDocumentExtractor, MarkdownExtractor>();
builder.Services.AddSingleton<IDocumentExtractor, TextExtractor>();
builder.Services.AddSingleton<IDocumentExtractor, PdfExtractor>();

builder.Services.AddSingleton<IOllamaApiClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
    return new OllamaApiClient(new Uri(endpoint));
});

builder.Services.AddSingleton<IEmbeddingGeneratorService, OllamaEmbeddingGeneratorService>();
builder.Services.AddSingleton<IChatClientService, OllamaChatClientService>();
builder.Services.AddSingleton<IVectorStore, PgVectorStore>();

builder.Services.AddSingleton<IngestionCoordinator>();
builder.Services.AddSingleton<GroundedChatService>();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

// Correlation ID Middleware
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N");
    }
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Items["CorrelationId"] = correlationId.ToString();
    await next();
});

// Endpoints

// GET /health
app.MapGet("/health", async (IVectorStore vectorStore) =>
{
    await vectorStore.EnsureSchemaCreatedAsync();
    return Results.Ok(new
    {
        status = "Healthy",
        runtime = ".NET 10",
        timestamp = DateTime.UtcNow
    });
});

// POST /api/chat
app.MapPost("/api/chat", async (
    GroundedChatRequest request,
    HttpContext httpContext,
    GroundedChatService chatService) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest(new { error = "Prompt cannot be empty." });
    }

    var correlationId = httpContext.Items["CorrelationId"]?.ToString();
    var response = await chatService.ProcessChatQueryAsync(request, correlationId, httpContext.RequestAborted);

    return Results.Ok(response);
});

// POST /api/admin/ingest
app.MapPost("/api/admin/ingest", async (
    IngestionRequest request,
    IngestionCoordinator coordinator) =>
{
    if (string.IsNullOrWhiteSpace(request.FilePath) || !File.Exists(request.FilePath))
    {
        return Results.BadRequest(new { error = "File path does not exist or is invalid." });
    }

    var result = await coordinator.IngestFileAsync(request.FilePath);
    return Results.Ok(result);
});

// Initialize database schema asynchronously on startup
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<IVectorStore>();
    await store.EnsureSchemaCreatedAsync();
});

app.Run();

public record IngestionRequest(string FilePath);

// Make Program class accessible to WebApplicationFactory for testing
public partial class Program { }

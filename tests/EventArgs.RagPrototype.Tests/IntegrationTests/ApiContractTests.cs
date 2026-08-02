using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EventArgs.RagPrototype.Application.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EventArgs.RagPrototype.Tests.IntegrationTests;

public class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsOkAndHealthyStatus()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body);
    }

    [Fact]
    public async Task PostChat_ReturnsRefusal_OnEmptyStore()
    {
        var client = _factory.CreateClient();
        var request = new GroundedChatRequest(
            Prompt: "What is the architecture baseline?",
            TopK: 5,
            RelevanceThreshold: 0.99,
            MinEvidenceCount: 2
        );

        var response = await client.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GroundedChatResponse>();

        Assert.NotNull(payload);
        Assert.NotNull(payload.CorrelationId);
        Assert.True(payload.RefusalState.IsRefused);
    }

    [Fact]
    public async Task PostChat_ReturnsBadRequest_OnEmptyPrompt()
    {
        var client = _factory.CreateClient();
        var request = new GroundedChatRequest(Prompt: "   ");

        var response = await client.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChat_PropagatesCorrelationIdHeader()
    {
        var client = _factory.CreateClient();
        string customCorrelationId = "custom-corr-id-12345";
        client.DefaultRequestHeaders.Add("X-Correlation-ID", customCorrelationId);

        var request = new GroundedChatRequest(Prompt: "Health check query");
        var response = await client.PostAsJsonAsync("/api/chat", request);

        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        var headerValue = response.Headers.GetValues("X-Correlation-ID");
        Assert.Contains(customCorrelationId, headerValue);
    }

    [Fact]
    public async Task PostAdminIngest_ReturnsBadRequest_OnInvalidFile()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/admin/ingest", new { FilePath = "/non/existent/file.md" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

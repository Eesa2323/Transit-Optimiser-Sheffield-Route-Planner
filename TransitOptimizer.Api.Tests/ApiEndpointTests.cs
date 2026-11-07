using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TransitOptimizer.Api.Tests;

public sealed class ApiEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/routes/plan")]
    [InlineData("/api/routes/plan?from=sheffield-station")]
    [InlineData("/api/routes/analyze?to=meadowhall")]
    [InlineData("/api/routes/compare?from=%20&to=meadowhall")]
    public async Task MissingParameters_Return400(string url)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("/api/routes/plan?from=nowhere&to=meadowhall")]
    [InlineData("/api/routes/analyze?from=sheffield-station&to=nowhere")]
    [InlineData("/api/routes/compare?from=nowhere&to=nowhere")]
    public async Task UnknownStations_Return404(string url)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task UnknownAlgorithm_Returns400()
    {
        var response = await _client.GetAsync("/api/routes/analyze?from=sheffield-station&to=meadowhall&algorithm=nope");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Plan_ReturnsRoute()
    {
        using var doc = await GetJson("/api/routes/plan?from=sheffield-station&to=meadowhall");
        Assert.Equal(26, doc.RootElement.GetProperty("totalTravelTimeMinutes").GetInt32());
    }

    [Theory]
    [InlineData("dijkstra")]
    [InlineData("astar")]
    [InlineData("bfs")]
    [InlineData("bellman-ford")]
    public async Task Analyze_ReturnsRouteAndNonEmptyTraceForEveryAlgorithm(string algorithm)
    {
        using var doc = await GetJson($"/api/routes/analyze?from=sheffield-station&to=meadowhall&algorithm={algorithm}");
        var root = doc.RootElement;
        Assert.Equal(algorithm, root.GetProperty("algorithm").GetProperty("id").GetString());
        Assert.True(root.GetProperty("route").GetProperty("legs").GetArrayLength() > 0);
        var trace = root.GetProperty("trace");
        Assert.True(trace.GetArrayLength() > 3);
        Assert.Equal("init", trace[0].GetProperty("type").GetString());
        Assert.Contains(trace.EnumerateArray(), s => s.GetProperty("type").GetString() == "found");
        Assert.True(root.GetProperty("stats").GetProperty("nodesExpanded").GetInt32() > 0);
    }

    [Fact]
    public async Task Compare_ReturnsAllAlgorithms()
    {
        using var doc = await GetJson("/api/routes/compare?from=sheffield-station&to=meadowhall");
        Assert.Equal(4, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task SpanningTree_GraphAlgorithmsHealthAndSwagger_Respond()
    {
        using var mst = await GetJson("/api/network/spanning-tree");
        Assert.True(mst.RootElement.GetProperty("trace").GetArrayLength() > 0);
        Assert.Equal(1, mst.RootElement.GetProperty("components").GetInt32());
        using var graph = await GetJson("/api/graph");
        Assert.True(graph.RootElement.GetProperty("edges").GetArrayLength() > 0);
        using var algorithms = await GetJson("/api/algorithms");
        Assert.Equal(4, algorithms.RootElement.GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/cron-ping")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    private async Task<JsonDocument> GetJson(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}

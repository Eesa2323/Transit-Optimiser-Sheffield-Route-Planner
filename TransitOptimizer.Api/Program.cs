using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using TransitOptimizer.Api.Data;
using TransitOptimizer.Api.Domain;
using TransitOptimizer.Api.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});
builder.Services.AddSingleton(SheffieldTransitData.CreateGraph());
builder.Services.AddSingleton<RouteFinderService>();
builder.Services.AddSingleton<PathfindingService>();
builder.Services.AddHealthChecks();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSwagger();
app.UseSwaggerUI();

// Containers and PaaS hosts terminate TLS at the proxy, so only redirect when HTTPS is configured locally.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health");

app.MapGet("/api/algorithms", () => Results.Ok(PathfindingService.Catalog))
    .WithTags("Algorithms")
    .WithSummary("List supported algorithms and their complexity");

app.MapGet("/api/graph", (TransitGraph graph) =>
{
    var edges = graph.GetAllEdges()
        .Where(pair => string.CompareOrdinal(pair.FromId, pair.Edge.DestinationStationId) < 0)
        .Select(pair => new GraphEdge(pair.FromId, pair.Edge.DestinationStationId, pair.Edge.LineName, pair.Edge.TravelTimeMinutes));
    return Results.Ok(new
    {
        Stations = graph.Stations.OrderBy(s => s.Name),
        Edges = edges,
        AdjacencyList = graph.Stations.OrderBy(s => s.Name).ToDictionary(
            s => s.Id,
            s => graph.GetNeighbors(s.Id).Select(e => new { e.DestinationStationId, e.TravelTimeMinutes, e.LineName }))
    });
})
.WithTags("Stations")
.WithSummary("Get the full graph (stations, edges and adjacency list)");

app.MapGet("/api/routes/analyze", (string? from, string? to, string? algorithm, TransitGraph graph, PathfindingService finder) =>
{
    var invalid = Validate(from, to, graph);
    if (invalid is not null) return invalid;
    var id = string.IsNullOrWhiteSpace(algorithm) ? "dijkstra" : algorithm;
    if (PathfindingService.FindInfo(id) is null)
    {
        return Results.BadRequest(new ProblemDetails
        {
            Title = "Unknown algorithm",
            Detail = $"Algorithm '{id}' is not supported. Use one of: {string.Join(", ", PathfindingService.Catalog.Select(a => a.Id))}.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    return Results.Ok(finder.Run(id, from!, to!));
})
.WithTags("Algorithms")
.WithSummary("Run one algorithm and return the route with its full step-by-step trace");

app.MapGet("/api/routes/compare", (string? from, string? to, TransitGraph graph, PathfindingService finder) =>
    Validate(from, to, graph) ?? Results.Ok(finder.RunAll(from!, to!)))
.WithTags("Algorithms")
.WithSummary("Run every algorithm on the same query and compare results, stats and traces");

app.MapGet("/api/network/spanning-tree", (PathfindingService finder) => Results.Ok(finder.MinimumSpanningTree()))
    .WithTags("Algorithms")
    .WithSummary("Kruskal's minimum spanning tree (union-find) with trace");

static IResult? Validate(string? from, string? to, TransitGraph graph)
{
    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
    {
        return Results.BadRequest(new ProblemDetails
        {
            Title = "Invalid route request",
            Detail = "Both 'from' and 'to' station IDs are required.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    return graph.ContainsStation(from) && graph.ContainsStation(to)
        ? null
        : Results.NotFound(new ProblemDetails
        {
            Title = "Station not found",
            Detail = "One or both station IDs do not exist in the transit network.",
            Status = StatusCodes.Status404NotFound
        });
}

app.MapGet("/api/stations", (TransitGraph graph) =>
{
    return Results.Ok(graph.Stations.OrderBy(station => station.Name));
})
.WithName("GetStations")
.WithTags("Stations")
.WithSummary("List available stations")
.WithDescription("Returns the stations currently loaded into the in-memory Sheffield transit graph.");

app.MapGet("/api/routes/plan", (string? from, string? to, TransitGraph graph, RouteFinderService routeFinder) =>
{
    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
    {
        return Results.BadRequest(new ProblemDetails
        {
            Title = "Invalid route request",
            Detail = "Both 'from' and 'to' station IDs are required.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    if (!graph.ContainsStation(from) || !graph.ContainsStation(to))
    {
        return Results.NotFound(new ProblemDetails
        {
            Title = "Station not found",
            Detail = "One or both station IDs do not exist in the transit network.",
            Status = StatusCodes.Status404NotFound
        });
    }

    var route = routeFinder.FindRoute(from, to);
    if (route is null)
    {
        return Results.NotFound(new ProblemDetails
        {
            Title = "Route not found",
            Detail = $"No route exists from '{from}' to '{to}'.",
            Status = StatusCodes.Status404NotFound
        });
    }

    return Results.Ok(route);
})
.WithName("PlanRoute")
.WithTags("Routes")
.WithSummary("Find the fastest route between two stations")
.WithDescription("Uses Dijkstra's algorithm to return the shortest travel-time route, its legs, and any transfer instructions.");

app.Run();

/// <summary>Entry point marker so integration tests can host the app.</summary>
public partial class Program;

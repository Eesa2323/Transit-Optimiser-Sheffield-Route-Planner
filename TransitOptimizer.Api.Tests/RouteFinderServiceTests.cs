using TransitOptimizer.Api.Domain;
using TransitOptimizer.Api.Routing;

namespace TransitOptimizer.Api.Tests;

public sealed class RouteFinderServiceTests
{
    [Fact]
    public void FindRoute_SelectsTheFastestPathWhenAlternativesExist()
    {
        var graph = CreateGraph();
        graph.AddConnection("a", "b", 4, "Red");
        graph.AddConnection("b", "c", 3, "Blue");
        graph.AddConnection("a", "c", 12, "Direct");

        var route = new RouteFinderService(graph).FindRoute("a", "c");

        Assert.NotNull(route);
        Assert.Equal(7, route.TotalTravelTimeMinutes);
        Assert.Equal(["a", "b", "c"], route.Stations.Select(station => station.Id));
        Assert.Single(route.TransferInstructions);
        Assert.Contains("transfer from Red to Blue", route.TransferInstructions[0]);
    }

    [Fact]
    public void FindRoute_ReturnsZeroMinuteRouteForSameStation()
    {
        var graph = CreateGraph();

        var route = new RouteFinderService(graph).FindRoute("a", "a");

        Assert.NotNull(route);
        Assert.Equal(0, route.TotalTravelTimeMinutes);
        Assert.Single(route.Stations);
        Assert.Empty(route.Legs);
        Assert.Empty(route.TransferInstructions);
    }

    [Fact]
    public void FindRoute_ReturnsNullWhenStationsAreDisconnected()
    {
        var graph = CreateGraph();
        graph.AddStation(new Station("isolated", "Isolated Stop", 53.48, -2.23));

        var route = new RouteFinderService(graph).FindRoute("a", "isolated");

        Assert.Null(route);
    }

    [Fact]
    public void AddConnection_CreatesBidirectionalEdges()
    {
        var graph = CreateGraph();
        graph.AddConnection("a", "b", 4);

        Assert.Equal("b", Assert.Single(graph.GetNeighbors("a")).DestinationStationId);
        Assert.Equal("a", Assert.Single(graph.GetNeighbors("b")).DestinationStationId);
    }

    private static TransitGraph CreateGraph()
    {
        var graph = new TransitGraph();
        graph.AddStation(new Station("a", "Station A", 53.48, -2.23));
        graph.AddStation(new Station("b", "Station B", 53.48, -2.23));
        graph.AddStation(new Station("c", "Station C", 53.48, -2.23));
        return graph;
    }
}

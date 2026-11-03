using TransitOptimizer.Api.Data;
using TransitOptimizer.Api.Domain;
using TransitOptimizer.Api.Routing;

namespace TransitOptimizer.Api.Tests;

public sealed class PathfindingServiceTests
{
    [Theory]
    [InlineData("dijkstra")]
    [InlineData("astar")]
    [InlineData("bellman-ford")]
    public void TimeOptimalAlgorithmsAgreeWithDijkstraOnEveryStationPair(string algorithm)
    {
        var graph = SheffieldTransitData.CreateGraph();
        var service = new PathfindingService(graph);
        var reference = new RouteFinderService(graph);
        var ids = graph.Stations.Select(s => s.Id).ToArray();

        foreach (var from in ids.Take(12))
        {
            foreach (var to in ids)
            {
                var expected = reference.FindRoute(from, to);
                var actual = service.Run(algorithm, from, to);
                Assert.Equal(expected?.TotalTravelTimeMinutes, actual?.Route?.TotalTravelTimeMinutes);
            }
        }
    }

    [Fact]
    public void Bfs_MinimisesHopsAndTraceIsRecorded()
    {
        var graph = new TransitGraph();
        foreach (var id in new[] { "a", "b", "c" })
        {
            graph.AddStation(new Station(id, id, 53.38, -1.47));
        }

        graph.AddConnection("a", "b", 1, "X");
        graph.AddConnection("b", "c", 1, "X");
        graph.AddConnection("a", "c", 10, "Y");

        var result = new PathfindingService(graph).Run("bfs", "a", "c");

        Assert.NotNull(result?.Route);
        Assert.Single(result.Route.Legs);
        Assert.NotEmpty(result.Trace);
        Assert.Contains(result.Trace, step => step.Type == "found");
    }

    [Fact]
    public void Run_ReturnsNullRouteWhenDisconnectedAndNullForUnknownInput()
    {
        var graph = new TransitGraph();
        graph.AddStation(new Station("a", "A", 53.38, -1.47));
        graph.AddStation(new Station("b", "B", 53.39, -1.47));
        var service = new PathfindingService(graph);

        Assert.Null(service.Run("dijkstra", "a", "b")?.Route);
        Assert.Null(service.Run("nope", "a", "b"));
        Assert.Null(service.Run("dijkstra", "a", "zzz"));
    }

    [Fact]
    public void MinimumSpanningTree_ConnectsAllStationsWithVMinusOneEdges()
    {
        var graph = SheffieldTransitData.CreateGraph();

        var mst = new PathfindingService(graph).MinimumSpanningTree();

        Assert.Equal(graph.Stations.Count - mst.Components, mst.Edges.Count);
        Assert.Equal(1, mst.Components);
    }
}

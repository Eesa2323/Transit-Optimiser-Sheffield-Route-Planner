using TransitOptimizer.Api.Data;
using TransitOptimizer.Api.Routing;

namespace TransitOptimizer.Api.Tests;

public sealed class SheffieldTransitDataTests
{
    [Fact]
    public void Graph_ConnectsEveryStationToTheCityCentre()
    {
        var graph = SheffieldTransitData.CreateGraph();
        var finder = new RouteFinderService(graph);

        Assert.True(graph.Stations.Count > 40);
        foreach (var station in graph.Stations)
        {
            Assert.NotNull(finder.FindRoute("sheffield-station", station.Id));
        }
    }

    [Fact]
    public void FindRoute_PlansStationToMeadowhall()
    {
        var finder = new RouteFinderService(SheffieldTransitData.CreateGraph());

        var route = finder.FindRoute("sheffield-station", "meadowhall");

        Assert.NotNull(route);
        Assert.Equal("meadowhall", route.Stations[^1].Id);
        Assert.True(route.TotalTravelTimeMinutes > 0);
    }
}

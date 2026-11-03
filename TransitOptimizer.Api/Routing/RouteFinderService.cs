using TransitOptimizer.Api.Domain;

namespace TransitOptimizer.Api.Routing;

/// <summary>Finds fastest routes in a transit graph using Dijkstra's algorithm.</summary>
public sealed class RouteFinderService(TransitGraph graph)
{
    /// <summary>Finds the shortest-time route, or null when either station is missing or unreachable.</summary>
    public RoutePlan? FindRoute(string originId, string destinationId)
    {
        if (!graph.TryGetStation(originId, out _) || !graph.TryGetStation(destinationId, out _))
        {
            return null;
        }

        var distances = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            [originId] = 0
        };
        var previous = new Dictionary<string, (string StationId, Edge Edge)>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, long>();
        queue.Enqueue(originId, 0);

        while (queue.TryDequeue(out var currentId, out var currentDistance))
        {
            if (distances[currentId] != currentDistance)
            {
                continue;
            }

            if (string.Equals(currentId, destinationId, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            foreach (var edge in graph.GetNeighbors(currentId))
            {
                var nextDistance = currentDistance + edge.TravelTimeMinutes;
                if (distances.TryGetValue(edge.DestinationStationId, out var knownDistance))
                {
                    // On equal time, prefer staying on the current line to avoid needless transfers.
                    if (nextDistance == knownDistance &&
                        previous.TryGetValue(currentId, out var arrival) &&
                        arrival.Edge.LineName == edge.LineName &&
                        previous.TryGetValue(edge.DestinationStationId, out var existing) &&
                        existing.Edge.LineName != edge.LineName)
                    {
                        previous[edge.DestinationStationId] = (currentId, edge);
                    }

                    if (nextDistance >= knownDistance)
                    {
                        continue;
                    }
                }

                distances[edge.DestinationStationId] = nextDistance;
                previous[edge.DestinationStationId] = (currentId, edge);
                queue.Enqueue(edge.DestinationStationId, nextDistance);
            }
        }

        if (!distances.TryGetValue(destinationId, out var totalTime))
        {
            return null;
        }

        var stationIds = new List<string> { destinationId };
        var legs = new List<RouteLeg>();
        var routeStationId = destinationId;
        while (!string.Equals(routeStationId, originId, StringComparison.OrdinalIgnoreCase))
        {
            var step = previous[routeStationId];
            legs.Add(new RouteLeg(step.StationId, routeStationId, step.Edge.LineName, step.Edge.TravelTimeMinutes));
            routeStationId = step.StationId;
            stationIds.Add(routeStationId);
        }

        stationIds.Reverse();
        legs.Reverse();
        PreferContinuousLines(legs);

        var stations = stationIds
            .Select(stationId => graph.TryGetStation(stationId, out var station)
                ? station!
                : throw new InvalidOperationException($"Station '{stationId}' disappeared from the graph."))
            .ToArray();
        var transfers = new List<string>();
        for (var index = 1; index < legs.Count; index++)
        {
            if (!string.Equals(legs[index - 1].LineName, legs[index].LineName, StringComparison.Ordinal))
            {
                transfers.Add(
                    $"At {stations[index].Name}, transfer from {legs[index - 1].LineName} to {legs[index].LineName}.");
            }
        }

        return new RoutePlan(originId, destinationId, totalTime, stations, legs, transfers);
    }

    // Parallel lines can share a segment with equal time; pick the one that avoids a transfer.
    private void PreferContinuousLines(List<RouteLeg> legs)
    {
        for (var index = 0; index < legs.Count; index++)
        {
            var leg = legs[index];
            var wanted = new List<string>();
            if (index > 0)
            {
                wanted.Add(legs[index - 1].LineName);
            }

            if (index < legs.Count - 1)
            {
                wanted.Add(legs[index + 1].LineName);
            }

            foreach (var line in wanted)
            {
                if (line == leg.LineName)
                {
                    break;
                }

                var alternative = graph.GetNeighbors(leg.FromStationId).FirstOrDefault(edge =>
                    string.Equals(edge.DestinationStationId, leg.ToStationId, StringComparison.OrdinalIgnoreCase) &&
                    edge.LineName == line &&
                    edge.TravelTimeMinutes == leg.TravelTimeMinutes);
                if (alternative is not null)
                {
                    legs[index] = leg with { LineName = line };
                    break;
                }
            }
        }
    }
}

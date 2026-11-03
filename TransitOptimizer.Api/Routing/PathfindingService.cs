using System.Diagnostics;
using TransitOptimizer.Api.Domain;

namespace TransitOptimizer.Api.Routing;

/// <summary>
/// Runs several shortest-path algorithms over the transit graph and records a step-by-step trace
/// of each so the behaviour of the underlying data structures can be visualised.
/// </summary>
public sealed class PathfindingService
{
    private const int MaxTraceSteps = 1500;
    private const int FrontierSnapshotSize = 8;

    private readonly TransitGraph _graph;
    private readonly double _maxSpeedKmPerMinute;

    /// <summary>Creates the service and derives the A* heuristic scale from the graph.</summary>
    public PathfindingService(TransitGraph graph)
    {
        _graph = graph;
        _maxSpeedKmPerMinute = ComputeMaxSpeed(graph);
    }

    /// <summary>Metadata for every supported algorithm.</summary>
    public static IReadOnlyList<AlgorithmInfo> Catalog { get; } =
    [
        new("dijkstra", "Dijkstra's algorithm", "Binary-heap priority queue (PriorityQueue) + hash maps",
            "O((V + E) log V)", "O(V)", "Minimum travel time",
            "Always expands the unsettled station with the smallest known travel time, relaxing its outgoing edges."),
        new("astar", "A* search", "Priority queue ordered by g + h (haversine heuristic)",
            "O(E log V) worst case; usually expands far fewer nodes", "O(V)", "Minimum travel time",
            "Dijkstra guided by an admissible straight-line-distance heuristic so it heads toward the destination."),
        new("bfs", "Breadth-first search", "FIFO queue + hash maps",
            "O(V + E)", "O(V)", "Fewest stops (ignores travel time)",
            "Explores the network in rings of equal stop count; optimal only when every hop costs the same."),
        new("bellman-ford", "Bellman-Ford", "Edge list relaxed in V-1 rounds",
            "O(V * E)", "O(V)", "Minimum travel time",
            "Relaxes every edge repeatedly until nothing improves; handles negative weights and detects negative cycles.")
    ];

    /// <summary>Looks up algorithm metadata by ID.</summary>
    public static AlgorithmInfo? FindInfo(string id) =>
        Catalog.FirstOrDefault(info => string.Equals(info.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Runs one algorithm. Returns null for an unknown algorithm or station.</summary>
    public AlgorithmResult? Run(string algorithmId, string originId, string destinationId)
    {
        var info = FindInfo(algorithmId);
        if (info is null || !_graph.TryGetStation(originId, out var origin) ||
            !_graph.TryGetStation(destinationId, out var destination))
        {
            return null;
        }

        originId = origin!.Id;
        destinationId = destination!.Id;
        var trace = new Tracer();
        var stopwatch = Stopwatch.StartNew();
        var search = info.Id switch
        {
            "dijkstra" => Dijkstra(originId, destinationId, trace, useHeuristic: false),
            "astar" => Dijkstra(originId, destinationId, trace, useHeuristic: true),
            "bfs" => Bfs(originId, destinationId, trace),
            _ => BellmanFord(originId, destinationId, trace)
        };
        stopwatch.Stop();

        var route = BuildRoute(originId, destinationId, search.Distances, search.Previous);
        var stats = new AlgorithmStats(
            search.Expanded.Count,
            search.Examined,
            search.Relaxed,
            search.MaxFrontier,
            Math.Round(stopwatch.Elapsed.TotalMilliseconds * 1000, 1),
            trace.Truncated);
        return new AlgorithmResult(info, route, route?.Legs.Count, search.Expanded, stats, trace.Steps);
    }

    /// <summary>Runs every algorithm for the same query.</summary>
    public IReadOnlyList<AlgorithmResult> RunAll(string originId, string destinationId) =>
        Catalog.Select(info => Run(info.Id, originId, destinationId)).OfType<AlgorithmResult>().ToArray();

    /// <summary>Builds a minimum spanning forest with Kruskal's algorithm and union-find.</summary>
    public SpanningTreeResult MinimumSpanningTree()
    {
        var trace = new Tracer();
        var edges = _graph.GetAllEdges()
            .Where(pair => string.CompareOrdinal(pair.FromId, pair.Edge.DestinationStationId) < 0)
            .OrderBy(pair => pair.Edge.TravelTimeMinutes)
            .ThenBy(pair => pair.FromId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Edge.DestinationStationId, StringComparer.Ordinal)
            .ToList();
        var parent = _graph.Stations.ToDictionary(s => s.Id, s => s.Id, StringComparer.OrdinalIgnoreCase);
        var rank = _graph.Stations.ToDictionary(s => s.Id, _ => 0, StringComparer.OrdinalIgnoreCase);

        string Find(string id)
        {
            while (parent[id] != id)
            {
                parent[id] = parent[parent[id]];
                id = parent[id];
            }

            return id;
        }

        var chosen = new List<GraphEdge>();
        long total = 0;
        var components = parent.Count;
        foreach (var (fromId, edge) in edges)
        {
            var rootA = Find(fromId);
            var rootB = Find(edge.DestinationStationId);
            if (rootA == rootB)
            {
                trace.Add("skip", edge.DestinationStationId, fromId, edge.TravelTimeMinutes,
                    $"Skip {Name(fromId)} - {Name(edge.DestinationStationId)} ({edge.TravelTimeMinutes} min): already connected (would form a cycle).");
                continue;
            }

            if (rank[rootA] < rank[rootB]) (rootA, rootB) = (rootB, rootA);
            parent[rootB] = rootA;
            if (rank[rootA] == rank[rootB]) rank[rootA]++;
            components--;
            total += edge.TravelTimeMinutes;
            chosen.Add(new GraphEdge(fromId, edge.DestinationStationId, edge.LineName, edge.TravelTimeMinutes));
            trace.Add("accept", edge.DestinationStationId, fromId, edge.TravelTimeMinutes,
                $"Accept {Name(fromId)} - {Name(edge.DestinationStationId)} ({edge.TravelTimeMinutes} min): union of two components. Total {total} min.");
        }

        var info = new AlgorithmInfo("kruskal", "Kruskal's minimum spanning tree",
            "Sorted edge list + union-find (path compression, union by rank)",
            "O(E log E)", "O(V)", "Cheapest set of links connecting the network",
            "Adds the cheapest edge that does not create a cycle; union-find answers 'already connected?' in near-constant time.");
        return new SpanningTreeResult(info, chosen, total, components, trace.Steps);
    }

    private SearchState Dijkstra(string originId, string destinationId, Tracer trace, bool useHeuristic)
    {
        var state = new SearchState();
        var destination = _graph.TryGetStation(destinationId, out var d) ? d! : null!;
        double H(string id) => useHeuristic && _graph.TryGetStation(id, out var s)
            ? HeuristicMinutes(s!, destination)
            : 0;

        var settled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, double>();
        state.Distances[originId] = 0;
        queue.Enqueue(originId, H(originId));
        state.MaxFrontier = 1;
        trace.Add("init", originId, null, 0,
            $"Enqueue origin {Name(originId)} with priority {H(originId):0.##}.", Snapshot(queue), state.Distances);

        while (queue.TryDequeue(out var currentId, out var priority))
        {
            if (!settled.Add(currentId))
            {
                trace.Add("stale", currentId, null, priority,
                    $"Dequeued stale entry for {Name(currentId)}; already settled, ignore.", Snapshot(queue), state.Distances);
                continue;
            }

            var currentDistance = state.Distances[currentId];
            state.Expanded.Add(currentId);
            trace.Add("visit", currentId, null, currentDistance,
                $"Dequeue {Name(currentId)} (best known {currentDistance:0.##} min{(useHeuristic ? $", priority {priority:0.##}" : "")}). Settle it.",
                Snapshot(queue), state.Distances);

            if (string.Equals(currentId, destinationId, StringComparison.OrdinalIgnoreCase))
            {
                trace.Add("found", currentId, null, currentDistance,
                    $"Destination reached in {currentDistance:0.##} min. Stop early.", Snapshot(queue), state.Distances);
                break;
            }

            foreach (var edge in _graph.GetNeighbors(currentId))
            {
                state.Examined++;
                var next = edge.DestinationStationId;
                if (settled.Contains(next))
                {
                    continue;
                }

                var candidate = currentDistance + edge.TravelTimeMinutes;
                if (state.Distances.TryGetValue(next, out var known) && candidate >= known)
                {
                    trace.Add("no-improve", next, currentId, candidate,
                        $"Via {edge.LineName} to {Name(next)}: {candidate:0.##} min is not better than {known:0.##}.");
                    continue;
                }

                state.Distances[next] = candidate;
                state.Previous[next] = (currentId, edge);
                state.Relaxed++;
                queue.Enqueue(next, candidate + H(next));
                state.MaxFrontier = Math.Max(state.MaxFrontier, queue.Count);
                trace.Add("relax", next, currentId, candidate,
                    $"Relax via {edge.LineName} to {Name(next)}: new best {candidate:0.##} min"
                    + (useHeuristic ? $" (+ heuristic {H(next):0.##})" : "") + ". Enqueue.",
                    Snapshot(queue), state.Distances);
            }
        }

        return state;
    }

    private SearchState Bfs(string originId, string destinationId, Tracer trace)
    {
        var state = new SearchState();
        var hops = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [originId] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(originId);
        state.Distances[originId] = 0;
        state.MaxFrontier = 1;
        trace.Add("init", originId, null, 0, $"Enqueue origin {Name(originId)} (0 hops).",
            Snapshot(queue, hops), hops.ToDictionary(p => p.Key, p => (double)p.Value));

        while (queue.TryDequeue(out var currentId))
        {
            state.Expanded.Add(currentId);
            trace.Add("visit", currentId, null, hops[currentId],
                $"Dequeue {Name(currentId)} ({hops[currentId]} hops).", Snapshot(queue, hops),
                hops.ToDictionary(p => p.Key, p => (double)p.Value));
            if (string.Equals(currentId, destinationId, StringComparison.OrdinalIgnoreCase))
            {
                trace.Add("found", currentId, null, hops[currentId],
                    $"Destination reached with {hops[currentId]} hops (fewest stops).", Snapshot(queue, hops),
                    hops.ToDictionary(p => p.Key, p => (double)p.Value));
                break;
            }

            foreach (var edge in _graph.GetNeighbors(currentId))
            {
                state.Examined++;
                var next = edge.DestinationStationId;
                if (hops.ContainsKey(next))
                {
                    continue;
                }

                hops[next] = hops[currentId] + 1;
                state.Distances[next] = hops[next];
                state.Previous[next] = (currentId, edge);
                state.Relaxed++;
                queue.Enqueue(next);
                state.MaxFrontier = Math.Max(state.MaxFrontier, queue.Count);
                trace.Add("relax", next, currentId, hops[next],
                    $"Discover {Name(next)} via {edge.LineName} at {hops[next]} hops. Enqueue.",
                    Snapshot(queue, hops), hops.ToDictionary(p => p.Key, p => (double)p.Value));
            }
        }

        return state;
    }

    private SearchState BellmanFord(string originId, string destinationId, Tracer trace)
    {
        var state = new SearchState();
        var edges = _graph.GetAllEdges().ToList();
        var vertexCount = _graph.Stations.Count;
        state.Distances[originId] = 0;
        state.Expanded.Add(originId);
        trace.Add("init", originId, null, 0,
            $"Distance to {Name(originId)} = 0; all others infinite. {edges.Count} directed edges, up to {vertexCount - 1} rounds.",
            [], state.Distances);

        for (var round = 1; round < vertexCount; round++)
        {
            var changed = false;
            foreach (var (fromId, edge) in edges)
            {
                if (!state.Distances.TryGetValue(fromId, out var fromDistance))
                {
                    continue;
                }

                state.Examined++;
                var candidate = fromDistance + edge.TravelTimeMinutes;
                if (state.Distances.TryGetValue(edge.DestinationStationId, out var known) && candidate >= known)
                {
                    continue;
                }

                state.Distances[edge.DestinationStationId] = candidate;
                state.Previous[edge.DestinationStationId] = (fromId, edge);
                state.Relaxed++;
                changed = true;
                if (!state.Expanded.Contains(edge.DestinationStationId))
                {
                    state.Expanded.Add(edge.DestinationStationId);
                }

                trace.Add("relax", edge.DestinationStationId, fromId, candidate,
                    $"Round {round}: relax {Name(fromId)} -> {Name(edge.DestinationStationId)} via {edge.LineName}, new best {candidate:0.##} min.",
                    [], state.Distances);
            }

            trace.Add("round", null, null, round,
                changed ? $"End of round {round}: at least one distance improved." : $"End of round {round}: nothing changed, distances are final. Stop early.",
                [], state.Distances);
            if (!changed)
            {
                break;
            }
        }

        state.MaxFrontier = edges.Count;
        if (state.Distances.ContainsKey(destinationId))
        {
            trace.Add("found", destinationId, null, state.Distances[destinationId],
                $"Shortest time to {Name(destinationId)} is {state.Distances[destinationId]:0.##} min.", [], state.Distances);
        }

        return state;
    }

    private RoutePlan? BuildRoute(
        string originId,
        string destinationId,
        Dictionary<string, double> distances,
        Dictionary<string, (string StationId, Edge Edge)> previous)
    {
        if (!distances.ContainsKey(destinationId))
        {
            return null;
        }

        var stationIds = new List<string> { destinationId };
        var legs = new List<RouteLeg>();
        var current = destinationId;
        while (!string.Equals(current, originId, StringComparison.OrdinalIgnoreCase))
        {
            var step = previous[current];
            legs.Add(new RouteLeg(step.StationId, current, step.Edge.LineName, step.Edge.TravelTimeMinutes));
            current = step.StationId;
            stationIds.Add(current);
        }

        stationIds.Reverse();
        legs.Reverse();
        var stations = stationIds.Select(id => _graph.TryGetStation(id, out var s) ? s! : throw new InvalidOperationException(id)).ToArray();
        var transfers = new List<string>();
        for (var i = 1; i < legs.Count; i++)
        {
            if (legs[i - 1].LineName != legs[i].LineName)
            {
                transfers.Add($"At {stations[i].Name}, transfer from {legs[i - 1].LineName} to {legs[i].LineName}.");
            }
        }

        return new RoutePlan(originId, destinationId, legs.Sum(l => (long)l.TravelTimeMinutes), stations, legs, transfers);
    }

    private double HeuristicMinutes(Station from, Station to) =>
        _maxSpeedKmPerMinute <= 0 ? 0 : HaversineKm(from, to) / _maxSpeedKmPerMinute;

    // The heuristic stays admissible only if no edge is faster than the speed used to scale it.
    private static double ComputeMaxSpeed(TransitGraph graph)
    {
        double max = 0;
        foreach (var (fromId, edge) in graph.GetAllEdges())
        {
            if (!graph.TryGetStation(fromId, out var a) || !graph.TryGetStation(edge.DestinationStationId, out var b))
            {
                continue;
            }

            var km = HaversineKm(a!, b!);
            if (edge.TravelTimeMinutes == 0)
            {
                if (km > 1e-6) return 0;
                continue;
            }

            max = Math.Max(max, km / edge.TravelTimeMinutes);
        }

        return max;
    }

    private static double HaversineKm(Station a, Station b)
    {
        const double R = 6371.0088;
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(b.Latitude - a.Latitude);
        var dLon = Rad(b.Longitude - a.Longitude);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Rad(a.Latitude)) * Math.Cos(Rad(b.Latitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private string Name(string id) => _graph.TryGetStation(id, out var s) ? s!.Name : id;

    private static List<FrontierEntry> Snapshot(PriorityQueue<string, double> queue) =>
        queue.UnorderedItems
            .OrderBy(item => item.Priority)
            .Take(FrontierSnapshotSize)
            .Select(item => new FrontierEntry(item.Element, Math.Round(item.Priority, 2)))
            .ToList();

    private static List<FrontierEntry> Snapshot(Queue<string> queue, Dictionary<string, int> hops) =>
        queue.Take(FrontierSnapshotSize).Select(id => new FrontierEntry(id, hops[id])).ToList();

    private sealed class SearchState
    {
        public Dictionary<string, double> Distances { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, (string StationId, Edge Edge)> Previous { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Expanded { get; } = [];
        public int Examined { get; set; }
        public int Relaxed { get; set; }
        public int MaxFrontier { get; set; }
    }

    private sealed class Tracer
    {
        public List<TraceStep> Steps { get; } = [];
        public bool Truncated { get; private set; }

        public void Add(
            string type,
            string? stationId,
            string? fromId,
            double? value,
            string message,
            IReadOnlyList<FrontierEntry>? frontier = null,
            IReadOnlyDictionary<string, double>? distances = null)
        {
            if (Steps.Count >= MaxTraceSteps)
            {
                Truncated = true;
                return;
            }

            Steps.Add(new TraceStep(
                Steps.Count + 1,
                type,
                stationId,
                fromId,
                value,
                message,
                frontier ?? [],
                distances is null ? new Dictionary<string, double>() : new Dictionary<string, double>(distances)));
        }
    }
}

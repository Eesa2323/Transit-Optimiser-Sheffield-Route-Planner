namespace TransitOptimizer.Api.Routing;

/// <summary>One entry in a snapshot of the algorithm's frontier (priority queue, queue or edge list).</summary>
public sealed record FrontierEntry(string StationId, double Priority);

/// <summary>A single recorded step of an algorithm run.</summary>
public sealed record TraceStep(
    int Step,
    string Type,
    string? StationId,
    string? FromStationId,
    double? Value,
    string Message,
    IReadOnlyList<FrontierEntry> Frontier,
    IReadOnlyDictionary<string, double> Distances);

/// <summary>Performance counters for an algorithm run.</summary>
public sealed record AlgorithmStats(
    int NodesExpanded,
    int EdgesExamined,
    int EdgesRelaxed,
    int MaxFrontierSize,
    double ElapsedMicroseconds,
    bool TraceTruncated);

/// <summary>Static description of an algorithm for display.</summary>
public sealed record AlgorithmInfo(
    string Id,
    string Name,
    string DataStructure,
    string TimeComplexity,
    string SpaceComplexity,
    string Optimises,
    string Description);

/// <summary>The route found by an algorithm plus its full execution trace.</summary>
public sealed record AlgorithmResult(
    AlgorithmInfo Algorithm,
    RoutePlan? Route,
    long? TotalHops,
    IReadOnlyList<string> VisitedOrder,
    AlgorithmStats Stats,
    IReadOnlyList<TraceStep> Trace);

/// <summary>An undirected connection used for drawing the network.</summary>
public sealed record GraphEdge(string FromId, string ToId, string LineName, int TravelTimeMinutes);

/// <summary>Result of a minimum spanning tree run (Kruskal + union-find).</summary>
public sealed record SpanningTreeResult(
    AlgorithmInfo Algorithm,
    IReadOnlyList<GraphEdge> Edges,
    long TotalMinutes,
    int Components,
    IReadOnlyList<TraceStep> Trace);

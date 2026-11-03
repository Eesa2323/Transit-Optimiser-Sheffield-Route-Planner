using TransitOptimizer.Api.Domain;

namespace TransitOptimizer.Api.Routing;

/// <summary>A shortest-time route and the instructions needed to follow it.</summary>
public sealed record RoutePlan(
    string OriginId,
    string DestinationId,
    long TotalTravelTimeMinutes,
    IReadOnlyList<Station> Stations,
    IReadOnlyList<RouteLeg> Legs,
    IReadOnlyList<string> TransferInstructions);

/// <summary>A connection used by a route.</summary>
public sealed record RouteLeg(
    string FromStationId,
    string ToStationId,
    string LineName,
    int TravelTimeMinutes);

namespace TransitOptimizer.Api.Domain;

/// <summary>A weighted connection from one station to another.</summary>
public sealed record Edge(string DestinationStationId, int TravelTimeMinutes, string LineName);

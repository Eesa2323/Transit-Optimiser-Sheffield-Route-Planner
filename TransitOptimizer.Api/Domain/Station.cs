namespace TransitOptimizer.Api.Domain;

/// <summary>A transit station or stop in the network.</summary>
public sealed record Station(string Id, string Name, double Latitude, double Longitude);

namespace TransitOptimizer.Api.Domain;

/// <summary>An in-memory transit network represented by an adjacency list.</summary>
public sealed class TransitGraph
{
    private readonly Dictionary<string, Station> _stations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Edge>> _adjacencyList = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets all stations in the network.</summary>
    public IReadOnlyCollection<Station> Stations => _stations.Values;

    /// <summary>Adds a station to the network.</summary>
    public void AddStation(Station station)
    {
        ArgumentNullException.ThrowIfNull(station);
        ArgumentException.ThrowIfNullOrWhiteSpace(station.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(station.Name);

        if (!double.IsFinite(station.Latitude) || station.Latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(station), "Latitude must be between -90 and 90.");
        }

        if (!double.IsFinite(station.Longitude) || station.Longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(station), "Longitude must be between -180 and 180.");
        }

        if (!_stations.TryAdd(station.Id, station))
        {
            throw new InvalidOperationException($"A station with ID '{station.Id}' already exists.");
        }

        _adjacencyList.Add(station.Id, []);
    }

    /// <summary>Adds a bidirectional connection between two existing stations.</summary>
    public void AddConnection(string fromId, string toId, int time, string lineName = "Transit")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toId);
        ArgumentException.ThrowIfNullOrWhiteSpace(lineName);

        if (time < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(time), "Travel time cannot be negative.");
        }

        if (!_adjacencyList.TryGetValue(fromId, out var fromEdges))
        {
            throw new KeyNotFoundException($"Station '{fromId}' does not exist.");
        }

        if (!_adjacencyList.TryGetValue(toId, out var toEdges))
        {
            throw new KeyNotFoundException($"Station '{toId}' does not exist.");
        }

        fromEdges.Add(new Edge(toId, time, lineName));
        toEdges.Add(new Edge(fromId, time, lineName));
    }

    /// <summary>Gets outgoing connections for a station, or an empty list for an unknown ID.</summary>
    public IReadOnlyList<Edge> GetNeighbors(string stationId)
    {
        return _adjacencyList.TryGetValue(stationId, out var edges)
            ? edges.AsReadOnly()
            : Array.Empty<Edge>();
    }

    /// <summary>Enumerates every directed edge in the adjacency list.</summary>
    public IEnumerable<(string FromId, Edge Edge)> GetAllEdges()
    {
        foreach (var (fromId, edges) in _adjacencyList)
        {
            foreach (var edge in edges)
            {
                yield return (_stations[fromId].Id, edge);
            }
        }
    }

    /// <summary>Checks whether the network contains a station ID.</summary>
    public bool ContainsStation(string stationId) => _stations.ContainsKey(stationId);

    /// <summary>Attempts to retrieve a station by ID.</summary>
    public bool TryGetStation(string stationId, out Station? station) =>
        _stations.TryGetValue(stationId, out station);
}

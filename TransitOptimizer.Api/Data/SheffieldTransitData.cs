using TransitOptimizer.Api.Domain;

namespace TransitOptimizer.Api.Data;

/// <summary>
/// Creates the in-memory sample network for Sheffield: Supertram lines plus walking links to
/// nearby landmarks. Coordinates and travel times are approximate, not an official timetable.
/// </summary>
public static class SheffieldTransitData
{
    private const string Yellow = "Yellow Line";
    private const string Blue = "Blue Line";
    private const string Purple = "Purple Line";
    private const string Walk = "Walking";

    private static readonly Station[] Stations =
    [
        // North-west: Yellow / Blue
        new("middlewood", "Middlewood", 53.4170, -1.5055),
        new("leppings-lane", "Leppings Lane", 53.4115, -1.5020),
        new("hillsborough", "Hillsborough", 53.4075, -1.4985),
        new("hillsborough-park", "Hillsborough Park", 53.4040, -1.4960),
        new("malin-bridge", "Malin Bridge", 53.4075, -1.5155),
        new("langsett-primrose-view", "Langsett / Primrose View", 53.3995, -1.4905),
        new("infirmary-road", "Infirmary Road", 53.3945, -1.4815),
        new("shalesmoor", "Shalesmoor", 53.3875, -1.4795),
        new("netherthorpe-road", "Netherthorpe Road", 53.3845, -1.4815),
        new("university-of-sheffield", "University of Sheffield", 53.3810, -1.4875),
        new("west-street", "West Street", 53.3805, -1.4775),

        // City centre
        new("cathedral", "Cathedral", 53.3818, -1.4707),
        new("city-hall", "City Hall", 53.3800, -1.4690),
        new("fitzalan-square", "Fitzalan Square / Ponds Forge", 53.3790, -1.4655),
        new("castle-square", "Castle Square", 53.3830, -1.4635),
        new("sheffield-station", "Sheffield Station", 53.3785, -1.4620),
        new("hallam-university", "Sheffield Hallam University", 53.3790, -1.4635),

        // East: Yellow line to Meadowhall
        new("attercliffe", "Attercliffe", 53.3925, -1.4380),
        new("arena", "Sheffield Arena / Olympic Legacy Park", 53.4045, -1.4170),
        new("carbrook", "Carbrook", 53.4000, -1.4260),
        new("valley-centertainment", "Valley Centertainment", 53.4090, -1.4150),
        new("meadowhall", "Meadowhall Interchange", 53.4150, -1.4120),

        // South-east: Blue / Purple lines
        new("granville-road", "Granville Road / The Sheffield College", 53.3680, -1.4540),
        new("park-grange", "Park Grange", 53.3640, -1.4400),
        new("arbourthorne", "Arbourthorne", 53.3585, -1.4300),
        new("manor-top", "Manor Top", 53.3530, -1.4290),
        new("hollinsclough", "Hollinsclough", 53.3480, -1.4300),
        new("gleadless-townend", "Gleadless Townend", 53.3400, -1.4350),
        new("herdings-park", "Herdings Park", 53.3330, -1.4310),
        new("westfield", "Westfield", 53.3300, -1.3950),
        new("halfway", "Halfway", 53.3260, -1.3680),

        // Landmarks reachable on foot
        new("peace-gardens", "Peace Gardens", 53.3808, -1.4702),
        new("winter-garden", "Winter Garden", 53.3796, -1.4687),
        new("crucible-theatre", "Crucible Theatre", 53.3793, -1.4668),
        new("kelham-island", "Kelham Island Museum", 53.3880, -1.4770),
        new("devonshire-green", "Devonshire Green", 53.3775, -1.4795),
        new("botanical-gardens", "Sheffield Botanical Gardens", 53.3705, -1.4965),
        new("royal-hallamshire", "Royal Hallamshire Hospital", 53.3711, -1.4930),
        new("endcliffe-park", "Endcliffe Park", 53.3720, -1.5075),
        new("hillsborough-stadium", "Hillsborough Stadium", 53.4118, -1.5005),
        new("sheffield-arena-venue", "Utilita Arena Sheffield", 53.4050, -1.4175),
        new("magna", "Magna Science Adventure Centre", 53.4160, -1.3920),
        new("victoria-quays", "Victoria Quays", 53.3830, -1.4585)
    ];

    /// <summary>Builds the Sheffield network.</summary>
    public static TransitGraph CreateGraph()
    {
        var graph = new TransitGraph();
        foreach (var station in Stations)
        {
            graph.AddStation(station);
        }

        // Yellow Line: Middlewood to Meadowhall
        Link(graph, Yellow, 3, "middlewood", "leppings-lane");
        Link(graph, Yellow, 2, "leppings-lane", "hillsborough");
        Link(graph, Yellow, 2, "hillsborough", "hillsborough-park");
        Link(graph, Yellow, 3, "hillsborough-park", "langsett-primrose-view");
        Link(graph, Yellow, 3, "langsett-primrose-view", "infirmary-road");
        Link(graph, Yellow, 3, "infirmary-road", "shalesmoor");
        Link(graph, Yellow, 3, "shalesmoor", "cathedral");
        Link(graph, Yellow, 2, "cathedral", "city-hall");
        Link(graph, Yellow, 2, "city-hall", "fitzalan-square");
        Link(graph, Yellow, 2, "fitzalan-square", "castle-square");
        Link(graph, Yellow, 7, "castle-square", "attercliffe");
        Link(graph, Yellow, 5, "attercliffe", "arena");
        Link(graph, Yellow, 3, "arena", "carbrook");
        Link(graph, Yellow, 3, "carbrook", "valley-centertainment");
        Link(graph, Yellow, 3, "valley-centertainment", "meadowhall");

        // Blue Line: Malin Bridge to Halfway via the university
        Link(graph, Blue, 4, "malin-bridge", "hillsborough-park");
        Link(graph, Blue, 3, "hillsborough-park", "langsett-primrose-view");
        Link(graph, Blue, 3, "langsett-primrose-view", "netherthorpe-road");
        Link(graph, Blue, 3, "langsett-primrose-view", "infirmary-road");
        Link(graph, Blue, 3, "infirmary-road", "shalesmoor");
        Link(graph, Blue, 3, "shalesmoor", "cathedral");
        Link(graph, Blue, 2, "cathedral", "city-hall");
        Link(graph, Blue, 2, "netherthorpe-road", "university-of-sheffield");
        Link(graph, Blue, 3, "university-of-sheffield", "west-street");
        Link(graph, Blue, 3, "west-street", "city-hall");
        Link(graph, Blue, 2, "city-hall", "fitzalan-square");
        Link(graph, Blue, 4, "fitzalan-square", "granville-road");
        Link(graph, Blue, 4, "granville-road", "park-grange");
        Link(graph, Blue, 3, "park-grange", "arbourthorne");
        Link(graph, Blue, 3, "arbourthorne", "manor-top");
        Link(graph, Blue, 6, "manor-top", "westfield");
        Link(graph, Blue, 5, "westfield", "halfway");

        // Purple Line: Cathedral to Herdings Park
        Link(graph, Purple, 2, "cathedral", "city-hall");
        Link(graph, Purple, 2, "city-hall", "fitzalan-square");
        Link(graph, Purple, 4, "fitzalan-square", "granville-road");
        Link(graph, Purple, 4, "granville-road", "park-grange");
        Link(graph, Purple, 3, "park-grange", "arbourthorne");
        Link(graph, Purple, 3, "arbourthorne", "manor-top");
        Link(graph, Purple, 3, "manor-top", "hollinsclough");
        Link(graph, Purple, 4, "hollinsclough", "gleadless-townend");
        Link(graph, Purple, 3, "gleadless-townend", "herdings-park");

        // Walking links
        Link(graph, Walk, 6, "cathedral", "peace-gardens");
        Link(graph, Walk, 2, "city-hall", "peace-gardens");
        Link(graph, Walk, 1, "peace-gardens", "winter-garden");
        Link(graph, Walk, 4, "winter-garden", "crucible-theatre");
        Link(graph, Walk, 4, "fitzalan-square", "crucible-theatre");
        Link(graph, Walk, 6, "winter-garden", "sheffield-station");
        Link(graph, Walk, 8, "fitzalan-square", "sheffield-station");
        Link(graph, Walk, 3, "sheffield-station", "hallam-university");
        Link(graph, Walk, 5, "castle-square", "sheffield-station");
        Link(graph, Walk, 3, "castle-square", "victoria-quays");
        Link(graph, Walk, 6, "sheffield-station", "victoria-quays");
        Link(graph, Walk, 7, "shalesmoor", "kelham-island");
        Link(graph, Walk, 12, "cathedral", "kelham-island");
        Link(graph, Walk, 8, "west-street", "devonshire-green");
        Link(graph, Walk, 7, "devonshire-green", "winter-garden");
        Link(graph, Walk, 10, "university-of-sheffield", "botanical-gardens");
        Link(graph, Walk, 8, "botanical-gardens", "royal-hallamshire");
        Link(graph, Walk, 6, "university-of-sheffield", "royal-hallamshire");
        Link(graph, Walk, 12, "botanical-gardens", "endcliffe-park");
        Link(graph, Walk, 5, "leppings-lane", "hillsborough-stadium");
        Link(graph, Walk, 2, "arena", "sheffield-arena-venue");
        Link(graph, Walk, 15, "meadowhall", "magna");

        return graph;
    }

    private static void Link(TransitGraph graph, string line, int minutes, string fromId, string toId) =>
        graph.AddConnection(fromId, toId, minutes, line);
}

# Transit Optimiser – Sheffield Route Planner

**Explore routes across Sheffield, then watch the computer work them out step by step.**

Transit Optimiser is a route planner for a model of Sheffield's tram network plus walking links. Pick two stations and an algorithm to see a journey on a map. Dijkstra, A\* and Bellman-Ford find a fastest route; breadth-first search finds a route with the fewest stops, which may take longer. You can replay each search, see the data structures change, and compare four algorithms on the same trip.

> The stations, coordinates and journey times are approximate sample data for demonstration. This is not a live timetable.

**Contents:** [What you can do](#what-you-can-do) · [Algorithms](#the-algorithms) · [Quick start](#quick-start) · [Docker (optional)](#run-with-docker-optional) · [API](#api) · [Project layout](#project-layout) · [Testing](#testing) · [Custom network](#adding-your-own-network)

## What you can do

- **Plan a route** between any two of 43 stations. You get the total time, each leg, and where to change lines.
- **Watch an algorithm think.** Play, pause, step or scrub through every decision. Stations on the map change colour as the search progresses, alongside the live queue and a table of best-known times.
- **Compare algorithms.** Run all four on the same journey and see how much work each one did.
- **Explore the graph.** Browse the adjacency list the network is stored in.
- **See a minimum spanning tree.** Draw the cheapest set of links that connects the whole network.
- **Get help in the app.** A welcome guide appears on first load and can be reopened with _How does this work?_. It is fullscreen on phones.

## The algorithms

| Algorithm                | In plain English                                                                                 | Data structure                                   | Time complexity       | Finds                             |
| ------------------------ | ------------------------------------------------------------------------------------------------ | ------------------------------------------------ | --------------------- | --------------------------------- |
| **Dijkstra**             | Always explores the closest unvisited station next.                                              | Priority queue (min-heap)                        | O((V + E) log V)      | Fastest route                     |
| **A\***                  | Dijkstra plus a straight-line "which way is the destination?" hint, so it checks fewer stations. | Priority queue ordered by cost so far + estimate | O(E log V) worst case | Fastest route                     |
| **Breadth-first search** | Explores outward one stop at a time.                                                             | FIFO queue                                       | O(V + E)              | Fewest stops (not always fastest) |
| **Bellman-Ford**         | Re-checks every connection until nothing improves.                                               | Edge list, repeated rounds                       | O(V · E)              | Fastest route                     |
| **Kruskal's MST**        | Adds the cheapest link that does not form a loop.                                                | Sorted edges + union-find                        | O(E log E)            | Cheapest connected network        |

Also used: hash maps for fast lookups, the haversine formula for the A\* estimate, and backtracking through a "previous station" map to rebuild the final path.

## Quick start

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download). Docker is **not** required. Run these commands from the repository root:

```powershell
dotnet test TransitOptimizer.slnx
dotnet run --project TransitOptimizer.Api --launch-profile http
```

Then open <http://localhost:5094>. Swagger (interactive API docs) is at <http://localhost:5094/swagger>.
Leave the server running while using the app; press `Ctrl+C` in its terminal to stop it. The first run restores NuGet packages. The map also needs internet access to load Leaflet and OpenStreetMap tiles.

### Using the app

1. Choose **From** and **To** stations and an **Algorithm**, then press **Plan route & trace**. Press it again after changing a selection; **Swap stations** also recalculates the route.
2. In the **Step-through trace** tab, press **Play** or use the arrows and slider. Green stations are finished, yellow are waiting in the queue, red is the one being examined. Click any line in the log to jump to that step.
3. Open **Compare algorithms** to see nodes expanded, edges examined and timings side by side. Click a row to trace that algorithm.
4. Open **Graph & data structures** and **Spanning tree (Kruskal)** to explore the rest.

## Run with Docker (optional)

> **You can skip this section.** Docker is only an alternative way to run or deploy the app without installing the .NET SDK. The [Quick start](#quick-start) works without it, and nothing else in the project depends on Docker.

```powershell
docker build -t transit-optimizer .
docker run -p 8080:8080 transit-optimizer
```

Open <http://localhost:8080>. The container listens on port 8080 (change it with `ASPNETCORE_URLS`) and exposes `/health`. Swagger is available in every environment. The app accepts forwarded headers without restricting known proxies, so only expose it behind a trusted proxy that overwrites incoming forwarded headers.

## API

| Endpoint                                                    | Purpose                                                                                             |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `GET /api/stations`                                         | List all stations                                                                                   |
| `GET /api/graph`                                            | Stations, edges and the raw adjacency list                                                          |
| `GET /api/algorithms`                                       | Algorithm names, data structures and complexity                                                     |
| `GET /api/routes/plan?from=sheffield-station&to=meadowhall` | Fastest route (Dijkstra) with legs and transfer instructions                                        |
| `GET /api/routes/analyze?from=..&to=..&algorithm=dijkstra`  | One algorithm's route, stats and trace. Algorithms: `dijkstra`, `astar`, `bfs`, `bellman-ford`      |
| `GET /api/routes/compare?from=..&to=..`                     | All four algorithms on the same journey                                                             |
| `GET /api/network/spanning-tree`                            | Kruskal's minimum spanning tree with trace                                                          |
| `GET /health`                                               | Health check                                                                                        |
| `GET /cron-ping`                                            | Simple `200 OK` ping response                                                                       |

Route endpoints require `from` and `to` station IDs, which can be obtained from `/api/stations`. The `algorithm` parameter on `/api/routes/analyze` defaults to `dijkstra` when omitted.

Errors: route endpoints return `400 Bad Request` for a missing parameter, or for an unknown algorithm on `/api/routes/analyze`, and `404 Not Found` for an unknown station. `/api/routes/plan` also returns `404` when no route exists; `/api/routes/analyze` and `/api/routes/compare` instead return results with a null `route` for disconnected stations.

Example:

```powershell
Invoke-RestMethod "http://localhost:5094/api/routes/analyze?from=sheffield-station&to=meadowhall&algorithm=astar"
```

Each trace step records its type (`init`, `visit`, `relax`, `found`, ...), a readable message, a frontier snapshot and the distance table. Route traces are capped at 1,500 steps; `stats.traceTruncated` indicates whether steps were omitted. Queue snapshots contain at most eight entries.

## Project layout

```
TransitOptimizer.Api/
  Domain/     Station, Edge, TransitGraph (adjacency list)
  Routing/    RouteFinderService (plain Dijkstra), PathfindingService (all algorithms + traces)
  Data/       SheffieldTransitData (sample network)
  wwwroot/    Leaflet map and visualiser (single index.html)
  Program.cs  Minimal API endpoints
TransitOptimizer.Api.Tests/   xUnit unit and API integration tests
Dockerfile
```

The graph and services are registered as singletons and loaded once at startup, so lookups stay in memory.

## Testing

`dotnet test TransitOptimizer.slnx` runs the xUnit unit and API integration tests. They cover same-station and disconnected routes, picking the shortest of several paths, agreement between Dijkstra, A\* and Bellman-Ford for the first 12 origin stations against every destination, BFS minimising stops, the spanning tree, and the API's 400/404 responses and traces. The tests do not automate the browser UI.

Keep the `TransitOptimizer.Api.Tests` project and its source files in Git. Its generated `bin`, `obj`, test results and coverage output are covered by `.gitignore`; ignore rules do not untrack files that Git already tracks.

## Adding your own network

Edit `TransitOptimizer.Api/Data/SheffieldTransitData.cs`: add `Station` entries (id, name, latitude, longitude) and connect them with `Link(graph, line, minutes, fromId, toId)`. Connections are bidirectional and require non-negative travel times. Optionally add a colour for any new line name in the `lineColours` map in `TransitOptimizer.Api/wwwroot/index.html`; unrecognised lines use a default colour.

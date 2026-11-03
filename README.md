# Transit Optimizer – Sheffield Route Planner

**Find the fastest way across Sheffield, then watch the computer work it out step by step.**

Transit Optimizer is a route planner for a model of Sheffield's Supertram network plus walking links. Pick two stations and it shows the quickest journey on a map. The point of the project is to make the algorithms behind a route planner visible: you can replay each search, see the data structures change, and compare four algorithms on the same trip.

> The stations, coordinates and journey times are approximate sample data for demonstration. This is not a live timetable.

**Contents:** [What you can do](#what-you-can-do) · [Algorithms](#the-algorithms) · [Quick start](#quick-start) · [Docker (optional)](#run-with-docker-optional) · [API](#api) · [Project layout](#project-layout) · [Testing](#testing) · [Custom network](#adding-your-own-network)

## What you can do

- **Plan a route** between any two of 47 stations. You get the total time, each leg, and where to change lines.
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

All you need is the [.NET 10 SDK](https://dotnet.microsoft.com/download). Docker is **not** required.

```powershell
dotnet test TransitOptimizer.slnx
dotnet run --project TransitOptimizer.Api --launch-profile http
```

Then open <http://localhost:5094>. Swagger (interactive API docs) is at <http://localhost:5094/swagger>.

### Using the app

1. Choose **From** and **To** stations and an **Algorithm**, then press **Plan route & trace**.
2. In the **Step-through trace** tab, press **Play** or use the arrows and slider. Green stations are finished, yellow are waiting in the queue, red is the one being examined. Click any line in the log to jump to that step.
3. Open **Compare algorithms** to see nodes expanded, edges examined and timings side by side. Click a row to trace that algorithm.
4. Open **Graph & data structures** and **Spanning tree (Kruskal)** to explore the rest.

## Run with Docker (optional)

> **You can skip this section.** Docker is only an alternative way to run or deploy the app without installing the .NET SDK. The [Quick start](#quick-start) works without it, and nothing else in the project depends on Docker.

```powershell
docker build -t transit-optimizer .
docker run -p 8080:8080 transit-optimizer
```

Open <http://localhost:8080>. The container listens on port 8080 (change it with `ASPNETCORE_URLS`), respects `X-Forwarded-*` headers from a reverse proxy, and exposes `/health`, so it suits hosts such as Azure Container Apps, Render or Fly.io. Swagger is available in every environment.

## API

| Endpoint                                                    | Purpose                                                                                             |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `GET /api/stations`                                         | List all stations                                                                                   |
| `GET /api/graph`                                            | Stations, edges and the raw adjacency list                                                          |
| `GET /api/algorithms`                                       | Algorithm names, data structures and complexity                                                     |
| `GET /api/routes/plan?from=sheffield-station&to=meadowhall` | Fastest route (Dijkstra) with legs and transfer instructions                                        |
| `GET /api/routes/analyze?from=..&to=..&algorithm=dijkstra`  | One algorithm's route, stats and full trace. Algorithms: `dijkstra`, `astar`, `bfs`, `bellman-ford` |
| `GET /api/routes/compare?from=..&to=..`                     | All four algorithms on the same journey                                                             |
| `GET /api/network/spanning-tree`                            | Kruskal's minimum spanning tree with trace                                                          |
| `GET /health`                                               | Health check                                                                                        |

Errors: `400 Bad Request` for a missing parameter or unknown algorithm, `404 Not Found` for an unknown station or when no route exists.

Example:

```powershell
curl "http://localhost:5094/api/routes/analyze?from=sheffield-station&to=meadowhall&algorithm=astar"
```

Each trace step records its type (`init`, `visit`, `relax`, `found`, ...), a readable message, the current frontier and the distance table.

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

`dotnet test TransitOptimizer.slnx` runs the unit and integration tests. They cover same-station and disconnected routes, picking the shortest of several paths, agreement between Dijkstra, A\* and Bellman-Ford across the network, BFS minimising stops, the spanning tree, and the API's 400/404 responses and traces.

## Adding your own network

Edit `Data/SheffieldTransitData.cs`: add `Station` entries (id, name, latitude, longitude) and connect them with `Link(graph, line, minutes, fromId, toId)`. Add a colour for any new line name in the `lineColours` map in `wwwroot/index.html`.

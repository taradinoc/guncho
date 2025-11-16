# Guncho AI Coding Instructions

## Project Overview

Guncho is a **multiplayer Interactive Fiction (IF) server** that runs Inform 7 story files in shared persistent worlds. Players connect via browser or telnet, interact using text commands, and can build/edit realms in real-time. This is fundamentally a **multi-user dungeon (MUD) architecture** with collaborative authoring capabilities.

**Critical architecture**: Game output is player-scoped using a custom tag protocol (`<$t N>`, `<$a>`, `<$b>`, etc.) parsed in `FyreVMInstance.cs` to route messages to specific players in shared game instances.

## Repository Structure

- **`legacy/`**: Original .NET Framework 4.5.1 implementation (OWIN/Web API 2/AngularJS)
  - `Guncho.Core/`: Game engine, realm management, FyreVM integration, SignalR v2.2
  - `Guncho.Site/`: Legacy AngularJS client (deprecated)
  - `GunchoConsole/`: Standalone console host
  - `Guncho.Api.Tests/`: Legacy test suite
  
- **`src/`**: Modern .NET 10 rewrite (**primary development focus**)
  - `Guncho.Engine/`: Core game engine (ported from legacy `Guncho.Core`)
  - `Guncho.WebHost/`: ASP.NET Core 10 host with SignalR and REST APIs
  - `Guncho.Client/`: Blazor WebAssembly front-end (Bootstrap 5.3.8)
  - `Guncho.Shared/`: DTOs and contracts shared between client/server
  - `Guncho.Engine.Tests/`: xUnit tests with Moq
  - `Guncho.WebHost.Tests/`: ASP.NET Core integration tests

- **Root data directories** (used by both legacy and modern):
  - `HackedI7/`: Modified Inform 7 compiler builds (5T18, 5Z71) with custom extensions
  - `Skeleton.inform/`: Default shared world template ("The Outer Realm")
  - `RealmData/`: XML persistence for players/realms (`.ni` source files, `playerIndex.xml`)
  - `Cache/`: Compiled `.ulx` game files

## Critical Concepts

### 1. Inform 7 Integration & Tag Protocol

Game stories run on **FyreVM** (Glulx interpreter). Output uses custom tags for multiplayer filtering:

- `<$t N>` / `</$t>`: Target player N (by ID)
- `<$a>` / `</$a>`: Announcement mode (all players)
- `<$b NAME>`: Transfer current player context
- `<$d NAME>`: Disambiguate player reference

### 2. Realm Compilation Workflow

Realms are **Inform 7 source files** (`.ni` extension) compiled to Glulx (`.ulx`):

1. Source stored in `RealmData/<RealmName>.ni`
2. Compilation via `InformRealmFactory.CompileRealmAsync()` using hacked I7 builds in `HackedI7/`
3. Compiler path configured via `Settings.settings` or `appsettings.json`
4. Compiled `.ulx` cached in `Cache/` directory
5. Editing source files through the API triggers a recompile and restart

**Custom Inform 7 Extensions** in `HackedI7/*/Inform7/Extensions/Guncho Cabal/`:
- `Guncho Realms.i7x`: Multiplayer command parsing, player switching
- `Guncho Mockup (Client Version).i7x`: Client-side simulation helpers

### 3. Player Commands & System Commands

- **In-game commands**: Passed to Inform 7 VM (e.g., `look`, `take sword`)
- **System commands** (intercepted in `Commands.cs`):
  - `@teleport <realm>`: Jump between realms
  - `@shutdown`: Admin-only server shutdown
  - `@wall <message>`: Broadcast to all players
  - `connect <user> <password>`: Authentication
  - `who`: List online players
  - `page <player>=<message>`: Private messaging

Commands starting with `@` or digits (e.g., `123:command` for player-directed actions) are special protocol prefixes.

### 4. SignalR Communication & Service Architecture

**Modern stack**: `Microsoft.AspNetCore.SignalR` in `Guncho.WebHost/Hubs/PlayHub.cs`

**Service layer pattern**: `GunchoServerServices` implements multiple interfaces:
- `IPlayerService`: Player management and authentication (BCrypt + legacy SHA1 support)
- `IRealmService`: Realm CRUD operations and compilation
- `IInstanceService`: Game instance lifecycle management
- `IConnectionService`: SignalR/TCP connection tracking
- `IInstanceSite`: Callback interface for VM-to-server communication

All interfaces defined in `Guncho.Engine/Services/`. Single service instance registered in DI as all interfaces.

**SignalR Hub methods**:
- `SendCommandAsync(string command)`: Client sends command
- `ConnectToRealmAsync(string realmName)`: Join realm (guest mode)
- Client-callable: `WriteLine(string line)`, `Goodbye()`

**TCP server**: `TcpServerHostedService` runs as `IHostedService` on port 4108 (configurable), supporting telnet clients.

**Event queue pattern**: All game logic serialized through `AsyncProducerConsumerQueue<Func<Task>>` to prevent race conditions. Never modify player/realm state outside event queue context.

### 5. Data Persistence

**Current**: XML serialization via `Guncho.Core/XML/`:
- `playerIndex.xml`: Player registry (ID, name, password hash)
- Per-realm XML files: Access control lists (ACLs), metadata

**Path configuration**:
- Legacy: `app.config` in `Guncho.Core` (example: `CachePath`, `RealmDataPath`)
- Modern: `appsettings.json` in `Guncho.WebHost`

## Build & Development

### Modern Stack (Primary)
```powershell
# Build entire solution from src/ directory
cd src
dotnet build

# Run development server (hosts both API and Blazor WASM client)
dotnet run --project Guncho.WebHost

# Or use Docker (recommended for production-like environment)
docker compose up -d
# Server runs at http://localhost:5000

# Run tests
dotnet test                                    # All tests
dotnet test Guncho.Engine.Tests                # Engine tests only
dotnet test Guncho.WebHost.Tests               # Integration tests only
```

**Default test credentials**: `admin` / `password123` (seeded in XML files)

### Legacy Stack
```powershell
cd legacy
msbuild Guncho.sln /p:Configuration=Debug     # Or use 'make' for Mono compatibility
```

### Docker Deployment
- `Dockerfile`: Multi-stage build for `src/Guncho.WebHost`
- `docker-compose.yml`: Production setup with persistent volumes
- Volumes: `guncho-cache` (compiled realms), `guncho-logs`
- Environment variables: `Guncho__CachePath`, `Guncho__RealmDataPath`, etc.

## Coding Conventions

### Async/Await Patterns
- **All I/O operations are async** (file, network, VM communication)
- Use `Nito.AsyncEx` library for `AsyncReaderWriterLock` and `AsyncProducerConsumerQueue`
- Never block async code with `.Wait()` or `.Result` (causes deadlocks in OWIN context)

### Concurrency
- `ConcurrentDictionary` for player/realm/instance registries
- Reader-writer locks for player state access
- Event queue pattern for serialized game logic execution (see `Server.eventQueue`)

### Error Handling
- `ILogger` abstraction (custom interface in `Guncho.Core`, built-in in ASP.NET Core)
- Log levels: `Spam`, `Normal`, `Error` (legacy) or standard `Debug`/`Info`/`Warning`/`Error`
- VM errors should never crash the server — catch and log in `FyreVMInstance.TerpThreadFunc()`

### Naming
- Realms use **case-insensitive names** (normalize to lowercase for lookups)
- Player IDs are **positive integers**; guest IDs are **negative** (e.g., `Guest1` = -1)
- File paths use `Path.Combine()` — support Windows and Linux (historical Mono support)

## Common Pitfalls

1. **Tag parsing off-by-one errors**: Always output consumed characters when aborting tag sequences (`await SendCurPlayerAsync(c)` before `tagstate = 0`)

2. **Realm compilation paths**: Skeleton path must match configured `NiSkeletonPath`. Compilers expect specific directory structure (`Source/story.ni`, `Build/auto.inf`).

3. **Player instance tracking**: `_playerInstances` maps players to their current `IInstance`. Always check if player is connected before sending output.

4. **XML serialization**: Generated classes in `Guncho.Engine/XML/` are from XSD schemas. Don't hand-edit — regenerate from `.xsd` files if schema changes.

5. **SignalR connection lifecycle**: Connections can drop and reconnect. Use `Connection.Player` to track authenticated sessions, not just `Context.ConnectionId`.

6. **TextfyreVM.dll dependency**: Engine requires local reference to `TextfyreVM.dll` in repo root. Not on NuGet. Dockerfile copies it explicitly.

## Key Files Reference

- **Game loop**: `Guncho.WebHost/Services/GunchoServerServices.cs` (event queue processing)
- **VM integration**: `Guncho.Engine/FyreVMInstance.cs` (tag parsing, output routing)
- **Realm factory**: `Guncho.Engine/InformRealmFactory.cs` (compilation pipeline)
- **Command dispatch**: `Guncho.Engine/CommandProcessor.cs` (system command handlers)
- **Modern API**: `Guncho.WebHost/Program.cs` (ASP.NET Core startup)
- **Client hub**: `Guncho.WebHost/Hubs/PlayHub.cs` (SignalR communication)
- **TCP server**: `Guncho.WebHost/Services/TcpServerHostedService.cs` (telnet support)

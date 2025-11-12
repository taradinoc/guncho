# Guncho Modernization – Getting Started

This directory contains the modernized Guncho stack built on .NET 10 and Blazor WebAssembly.

## Projects

- **Guncho.Shared** – DTOs and contracts shared between client and server
- **Guncho.Client** – Blazor WASM front-end (Bootstrap 5.3.8)
- **Guncho.Next.WebHost** – ASP.NET Core 10 minimal API host + JWT auth + SignalR

## Quick Start

### Prerequisites

- .NET 10 SDK (verify with `dotnet --version`)

### Build

```bash
cd modernization
dotnet build
```

### Run

**Development mode** (single server - recommended):

```bash
cd Guncho.Next.WebHost
dotnet run
```

Then browse to **http://localhost:5000**.

The server hosts both the Blazor WASM UI and the API endpoints. Hot reload is supported for server-side code changes.

**Production mode**:
```bash
cd Guncho.Next.WebHost
dotnet publish -c Release
cd bin/Release/net10.0/publish
dotnet Guncho.Next.WebHost.dll --urls "http://localhost:5000"
```

Then browse to **http://localhost:5000**.

### Default user

A test user is seeded on startup:

- **Username:** `admin`
- **Password:** `password123`

## Features implemented

✅ JWT bearer authentication  
✅ User registration and login  
✅ Bootstrap 5.3.8 UI  
✅ Blazor WASM client with auth state management  
✅ ASP.NET Core 10 controllers (Account)

## What's next

- Port legacy Player and Realm services from `Guncho.Core`
- Migrate SignalR PlayHub to ASP.NET Core SignalR
- Build Play page and integrate real-time game client
- Wire up XML persistence for players/realms or migrate to EF Core

## Architecture notes

- **Auth:** JWT issued by `/api/account/login`, stored in browser localStorage
- **Static hosting:** Blazor WASM files served by `Guncho.Next.WebHost` via Static Web Assets middleware in Development, UseBlazorFrameworkFiles() in Production
- **CORS:** Not needed - client and API served from same origin (http://localhost:5000)
- **Single server:** Like the original OWIN setup, one server hosts both UI and API

## Comparison to legacy stack

| Component             | Legacy (.NET Framework)        | Modern (.NET 10)              |
|-----------------------|--------------------------------|-------------------------------|
| Server                | OWIN + Web API 2              | ASP.NET Core Minimal APIs     |
| Auth                  | OWIN OAuth2 + Identity        | JWT Bearer + custom UserService |
| Client                | AngularJS 1.2.16              | Blazor WASM                   |
| Bootstrap             | ~3.x                          | 5.3.8                         |
| SignalR               | 2.2                           | ASP.NET Core SignalR (todo)   |
| DI                    | SimpleInjector                | Built-in ASP.NET Core DI      |
| Config                | app.config / Settings.settings | appsettings.json              |

---

For the overall modernization roadmap, see `docs/Modernization.md` at the repo root.

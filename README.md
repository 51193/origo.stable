# Origo
A platform-agnostic pure C# game framework built around the **SND (Strategy-Node-Data)** entity model and the **Strategy Pattern**, decoupled from engines through interface abstractions.
## Project Structure
```
Origo.Core/                Pure C# core framework (Microsoft.NET.Sdk, no engine dependency)
Origo.GodotAdapter/        Godot adapter layer (Godot.NET.Sdk, thin implementation + DI)
Origo.Core.Tests/          Core test project (xUnit v3)
Origo.GodotAdapter.Tests/  Adapter test project (xUnit v3)
```
---
## Architectural Principles
1. **Core contains only platform-agnostic logic**. All engine interactions must go through interfaces in `Abstractions/`.
2. **Adapter has exactly two responsibilities**: provide engine-specific implementations of Core interfaces, and perform dependency injection at startup. No game business logic.
3. The **game layer** only focuses on strategy implementations (inherit `BaseSndStrategy`) and data configuration (JSON).
---
## Internal Architecture (OOD + Business Flows)
### Object-Oriented Layers and Responsibility Boundaries
- **Composition over inheritance**: `SndEntity` is the aggregate root and composes `SndDataManager` (data), `SndNodeManager` (nodes), and `SndStrategyManager` (behaviors).
- **Dependency Inversion (DIP)**: Core depends on abstractions such as `IFileSystem`, `ISndSceneHost`, `INodeFactory`, and `ILogger`; concrete Godot implementations live in the Adapter.
- **Facade orchestration**: `OrigoRuntime` provides unified runtime capabilities, `SndRuntime` provides unified entity operations, and `SndContext` provides save/load/change-level APIs.
- **Explicit lifecycle modeling**: `SystemRun -> ProgressRun -> SessionRun` represent system-level, flow-level, and session-level states to avoid a single overloaded context object.
- **Strategy pool reuse**: `SndStrategyPool` handles strategy creation, sharing, and reference-counted recycling. Strategies hook into entity behavior via `BaseSndStrategy` lifecycle hooks.
### Key Runtime Object Relations
```
OrigoDefaultEntry (Godot startup entry)
  -> OrigoAutoHost (creates Runtime + injects adapter implementations)
    -> OrigoRuntime
      -> SndWorld (StrategyPool / Json / Mappings)
      -> SndRuntime (ISndSceneHost facade)
      -> SystemBlackboard
  -> SndContext (save/load/change-level orchestration)
    -> RunFactory
      -> SystemRun -> ProgressRun -> SessionRun
```
### Business Flow 1: Startup to Main Menu
1. `OrigoDefaultEntry._Ready()` calls `OrigoAutoHost._Ready()` and creates `OrigoRuntime`.
2. The entry initializes `SndContext` and binds it to `GodotSndManager`.
3. Scene aliases and template mappings are loaded (`SndMappings.LoadSceneAliases/LoadTemplates`).
4. `SndContext.RequestLoadMainMenuEntrySave()` enqueues main-menu loading into `SystemDeferred`; at end-of-frame it clears old runs, spawns main-menu entities from entry JSON, and creates corresponding `ProgressRun + SessionRun`.
5. `GodotSndManager._Process` drives entity `Process` every frame and strategies start running.
### Business Flow 2: Save/Load
1. Game logic calls `SndContext.RequestSaveGame(...)` or `RequestSaveGameAuto(...)`.
2. `SndContext` validates runtime instances and merges `meta.map` (contributors + custom override).
3. `SaveContext` converts `Progress/Session/SND Scene` into `SaveGamePayload`.
4. `SaveStorageFacade` writes to `current/` first, then snapshots to `save_xxx/` via `SnapshotCurrentToSave`.
5. During load, `RequestLoadGame(saveId)` is enqueued into `SystemDeferred`; at frame end it restores snapshot to `current/`, rebuilds `ProgressRun/SessionRun`, and restores blackboards/scenes.
### Business Flow 3: Change Level
1. `SndContext.RequestChangeLevel(newLevelId)` -> frame-end `ProgressRun.SwitchLevel(newLevelId)`.
2. Current level state is persisted by `SessionRun.PersistLevelState()` to `current/level_xxx/`.
3. `ActiveLevelId` is updated and `progress.json` is persisted.
4. Old `SessionRun` is disposed (session blackboard + scene cleanup).
5. New level is restored from `current/level_new/`; if no complete save exists, an empty session is created and scene is cleared.
---
## Core Module Design
### Abstractions
All capabilities that require engine-specific implementation are defined here.
| Interface | Responsibility |
|------|------|
| `IFileSystem` | File I/O, path composition, directory operations. Virtual path logic (`res://`, `user://`) is implemented by adapters |
| `INodeFactory` | Creates nodes by logical name and resource ID, returns `INodeHandle` |
| `INodeHandle` | Minimal node abstraction: `Name`, `Native`, `Free()`, `SetVisible()` |
| `INodeHost` | Manages restored nodes: lookup, metadata export, release |
| `ISndSceneHost` | Scene-level entity management: `Spawn`, `GetEntities`, `FindByName`; extends `ISndSceneAccess` |
| `ISndEntity` | Minimal entity abstraction: data access, node access, strategy add/remove, data subscriptions |
| `IBlackboard` | Type-safe key-value storage with `ExportAll`/`ImportAll` |
| `ILogger` | Logging API: `Log(LogLevel level, string tag, string message)` |
| `IOrigoRuntimeProvider` | Runtime provider entry |
| `IScheduler` | Deferred action queue |
| `IStateMachine` | String stack state machine API |
| `IRandom` | Deterministic random API |
| `IClock` | Time source |
| `IConsoleInputSource` | Console input source (`TryDequeueCommand`) |
| `IConsoleOutputChannel` | Console output publish channel (`Publish`) |
### Snd Subsystem
SND is the framework core. Game objects are modeled as aggregates of **Data + Nodes + Strategies**.
Core types:
- `SndMetaData`: serializable entity description
- `SndEntity`: aggregate root with data/node/strategy managers
- `SndWorld`: subsystem entry (strategy pool, mappings, JSON config)
- `SndRuntime`: facade over `SndWorld + ISndSceneHost`
- `SndContext`: runtime orchestration for blackboards, save/load, level switch
Entity lifecycle:
```
Spawn/Load -> [AfterSpawn/AfterLoad] -> Process (per-frame) -> [BeforeSave] -> [BeforeQuit/BeforeDead] -> Dispose
```
Strategies (`BaseSndStrategy`) are shared and reused across entities by `SndStrategyPool`, so they **must be stateless**. All mutable state must be stored in entity Data.
### Save Subsystem
Uses a **save/level** two-layer snapshot structure plus a `current` workspace.
- Main flow is strict fail-fast in early stage.
- Missing required save files/fields are treated as corruption.
- No silent fallback for missing strategy indexes, invalid state values, or missing templates.
### Blackboard
Three semantic layers:
- `SystemBlackboard`: global state
- `ProgressBlackboard`: save-slot progress
- `SessionBlackboard`: current-level session state
Three run instances aligned to blackboard semantics:
- `SystemRun`
- `ProgressRun`
- `SessionRun`
### Serialization
- `TypeStringMapping`: bidirectional type-string mapping
- `OrigoJson`: default `JsonSerializerOptions` and converters
### Runtime
- `OrigoRuntime`: unified runtime entry (`SndRuntime + Logger + SystemBlackboard`)
- End-of-frame deferred channels: `BusinessDeferred` then `SystemDeferred`
- `OrigoConsole`: string command routing (built-in: `spawn`, `snd_count`)
- `Runtime/Lifecycle/*`: `SystemRun / ProgressRun / SessionRun / RunFactory`
---
## GodotAdapter Module Design
The adapter provides Godot implementations for Core interfaces and wires dependencies at startup.
Key modules:
- `Bootstrap/OrigoAutoHost`: creates `OrigoRuntime`, injects dependencies
- `Bootstrap/OrigoConsolePump`: per-frame `Console.ProcessPending()`
- `Bootstrap/OrigoDefaultEntry`: startup orchestration and facade forwarding
- `FileSystem/GodotFileSystem`: thin `IFileSystem` implementation
- `Logging/GodotLogger`: `ILogger` implementation
- `Snd/GodotSndManager`: `ISndSceneHost` implementation and per-frame driving
- `Serialization/GodotJsonConverterRegistry`: registers Godot type mappings/converters
---
## Startup Flow
1. `OrigoDefaultEntry._Ready()` -> `OrigoAutoHost._Ready()` creates runtime
2. `GodotFileSystem` + `PersistentBlackboard(saveRoot/system.json)` are created and injected
3. Godot type mappings are registered into `SndWorld.TypeMapping`
4. Reflection discovers and registers all `BaseSndStrategy` subclasses
5. `SndContext` is created and bound to `GodotSndManager`
6. Scene aliases and template mappings are loaded
7. `RequestLoadMainMenuEntrySave()` is deferred and flushed at end of `_Ready()`
8. `GodotSndManager._Process` drives strategy `Process` each frame
---
## JSON Conventions
### Naming Conventions for External Strings
- Strategy index (`StrategyIndexAttribute` / JSON `strategy.indices[]`): lowercase dot namespace, each segment `lower_snake_case`
- Blackboard/TypedData keys: same lowercase dot namespace style
- `.map` keys (scene/template/meta): `lower_snake_case`, unique per file
- `SndMetaData.name`: `PascalCase`
### `SndMetaData` Example
```json
{
  "name": "EntityName",
  "node": { "pairs": { "logicalName": "resourceAlias" } },
  "strategy": { "indices": ["game.some_strategy"] },
  "data": { "pairs": { "key": { "type": "Int32", "data": 100 } } }
}
```
### `.map` Format
`key: value` per line, with `#` comments supported.
### Save metadata (`meta.map`) format
Same as `.map`:
```
# Save display metadata
title: Chapter 2 - Forest
play_time: 03:12:55
player_level: 18
```
---
## Save Directory Conventions
- `res://origo/initial/` - initial save (read-only, distributed with project)
- `user://origo_saves/` - runtime saves (read/write)
- `res://origo/entry/` - entry configuration
- `res://origo/maps/` - alias/template mapping files
---
## Runtime Constraints and Test Matrix
- Strategy auto-discovery validates statelessness; strategies with instance fields are rejected.
- Strategies must declare non-empty `StrategyIndexAttribute`; otherwise auto-discovery fails.
- Save facade APIs in `OrigoDefaultEntry` fail-fast before `_Ready()` (`EnsureContextOrThrow()`).
Current test projects:
- `Origo.Core.Tests`: Core unit tests (SND, Save, Lifecycle, Console, Serialization)
- `Origo.GodotAdapter.Tests`: minimal adapter behavior tests
Run tests from repository root:
```bash
dotnet test addons/Origo/Origo.sln
```
Note: `W3.csproj` does not reference xUnit. To avoid IDE analyzing Origo test sources as part of W3, test folders under `addons/Origo` are excluded from W3.

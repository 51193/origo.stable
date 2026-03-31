# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.0.3] - 2026-03-31

### Added
- **DataSource abstraction system** — a new tree-based data representation layer for structured data access and conversion:
  - `DataSourceNode`, `DataSourceNodeKind`, `DataSourceFactory`
  - `DataSourceConverter`, `DataSourceConverterRegistry`, `IDataSourceCodec`
  - Codecs: `JsonDataSourceCodec`, `MapDataSourceCodec`
  - Converters: `DomainConverters`, `PrimitiveConverters`, `TypedDataConverter`
- **Expanded console command system** — 13 new command handlers covering all major runtime operations:
  - `AutoSaveCommandHandler` (`auto_save`)
  - `BlackboardGetCommandHandler` (`bb_get`)
  - `BlackboardKeysCommandHandler` (`bb_keys`)
  - `BlackboardSetCommandHandler` (`bb_set`)
  - `ChangeLevelCommandHandler` (`change_level`)
  - `ClearEntitiesCommandHandler` (`clear_entities`)
  - `ContinueGameCommandHandler` (`continue_game`)
  - `FindEntityCommandHandler` (`find_entity`)
  - `HelpCommandHandler` (`help`)
  - `ListSavesCommandHandler` (`list_saves`)
  - `LoadGameCommandHandler` (`load_game`)
  - `SaveGameCommandHandler` (`save_game`)
  - `ConsoleCommandHandlerBase` — abstract base class for all command handlers
- New abstractions: `IOrigoRuntimeProvider`, `ISndSceneHost`, `IStateMachine`, `INodeHandle`
- `OrigoJson` — unified JSON serialization utilities
- `BlackboardSerializer` — refactored blackboard serialization
- `WellKnownKeys` — well-known save key constants
- `SaveMetaDataEntry`, `ISaveMetaContributor` — structured save metadata
- `NodeMetaData`, `DataMetaData`, `SndMetaData` — explicit metadata structures for entity components
- `TypedData` — type-aware data value wrapper
- `StrategyIndexAttribute` — attribute for strategy indexing
- `StateMachinePersistenceModels` — persistence model for state machines
- `GodotDataSourceConverters`, `GodotLogger`, `GodotNodeHandle` — Godot adapter additions
- Architecture guardrail tests (`AdapterArchitectureGuardrailTests`, `CoreArchitectureGuardrailTests`)
- New tests: `DataSourceTests`, `ConsoleCommandExtendedTests`, `TypeStringMappingTests`

### Changed
- Refactored `ConsoleCommandParser` and `ConsoleCommandRouter` with improved command dispatching
- `ConsoleCommandInvocation` renamed to `CommandInvocation`
- Random module relocated to `Origo.Core/Random/RandomNumberGenerator.cs`
- State machine refactored with new `StateMachinePersistenceModels`; `StateMachineDataKeys` replaced by `WellKnownKeys`
- `SndSceneJsonSerializer` renamed to `SndSceneSerializer`
- Updated README with expanded documentation

### Removed
- Legacy Godot serialization files: `GodotJsonPropertyNames`, `GodotJsonReaderStrict`, `GodotMiscConverters`, `GodotTransformConverters`, `GodotVectorConverters`
- Old Snd JSON converters: `DataMetaDataJsonConverter`, `SndMetaDataJsonConverter`, `StrategyMetaDataJsonConverter`, `TypedDataJsonConverter` (replaced by unified converters)
- `StateMachineStrategyEntityAdapter` (functionality integrated elsewhere)
- `SaveStorageAndPayloadTests` (replaced with more targeted tests)

---

## [0.0.2] - 2026-03-30

### Added
- New abstractions: `ISndDataAccess`, `ISndNodeAccess` for typed component access on entities
- `NullLogger` — no-op logger implementation
- `RunDependencies` — lifecycle dependency injection container
- `SndDefaults` — default configuration values
- `EntryPointWorkflow`, `SaveGameWorkflow` — orchestration helpers for common game flows
- `BclTypeNames` — BCL type name mapping for serialization
- `KeyValueFileParser` — configuration file parser
- `SndTemplateResolver` — entity template resolution
- Godot adapter serialization infrastructure: `GodotEngineTypeNames`, `GodotJsonPropertyNames`, `GodotJsonReaderStrict`
- New test coverage: `DataObserverManagerTests`, `ExtendedCoverageTests`, `StrategyPoolTypeSafetyAndExtensionTests`, `TechnicalDebtFixTests`, `SaveMetaMapCodecTests`, `SaveStorageAndPayloadTests`
- Chinese documentation (`README.zh-CN.md`)
- MIT License (`LICENSE`)
- CI workflow (`.github/workflows/ci.yml`)
- `.editorconfig` for consistent code style

### Changed
- Blackboard API: `ExportAll()` renamed to `SerializeAll()`, `ImportAll()` renamed to `DeserializeAll()`
- `IFileSystem` extended with `Rename()` and `DeleteDirectory()` methods
- `INodeFactory.Create()` now returns non-nullable `INodeHandle` (previously `INodeHandle?`)
- `ISndEntity` and related interfaces updated with improved component access patterns
- `ConsoleCommandParser` improved for robust command tokenisation
- `TypedDataJsonConverter` and `StrategyMetaDataJsonConverter` enhanced for better type mapping
- `Directory.Build.props` centralised project-wide build settings (nullable, analysers)
- Project file cleaned up — duplicate `TargetFramework`/`Nullable` properties removed from `Origo.Core.csproj`

### Removed
- `IClock` abstraction — time handling delegated to the scheduler
- `SaveFormat` — format handling simplified
- `SaveSnapshotService` — snapshot logic integrated into `SaveStorageFacade`
- `GodotScheduler` — scheduling moved to adapter configuration
- Partial `SndContext` files (`ActiveSaveState`, `Entry`, `SaveFlow`) — consolidated into `SndContext.cs`

### Fixed
- Strategy pool now enforces fail-fast type checking for type safety

---

## [0.0.1] - 2026-03-26

### Added
- Initial release of **Origo** — a game architecture framework targeting .NET 8 with a first-party Godot 4 adapter
- **SND entity model** — Data, Node, and Strategy component architecture for game entities
- **Three-layer lifecycle system**: `SystemRun`, `ProgressRun`, `SessionRun`
- **Typed blackboards** (`IBlackboard`) with `TypedData` serialization support
- **Slot-based save system** with `SaveMetaMapCodec`, `SaveMetaMerger`, `SaveStorageFacade`, `SaveSnapshotService`
- **Persistent blackboard** (`PersistentBlackboard`) for cross-session data persistence
- **Stack state machine** (`StackStateMachine`, `StateMachineStrategyBase`, `StateMachineStrategyEntityAdapter`)
- **Strategy pool** (`SndStrategyPool`) for stateless strategy management
- **Built-in console system** with `SndCountCommandHandler` and `SpawnTemplateCommandHandler`
- **Deterministic RNG** (`RandomNumberGenerator`) using XorShift128+ algorithm
- **Data observer manager** (`DataObserverManager`) for reactive data-change notifications
- **Godot 4 adapter** with file system, scheduling, serialization, and entity factory implementations
- Core abstractions: `IBlackboard`, `IClock`, `IFileSystem`, `INodeFactory`, `IScheduler`, `ISndEntity`, `ISndSceneAccess`, `ISndStrategyAccess`, `ISndDataAccess`, `ISndNodeAccess`
- Comprehensive test suite covering lifecycle, save/load, strategies, state machines, and serialization

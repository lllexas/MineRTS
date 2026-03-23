# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MineRTS** is a 2D real-time strategy (RTS) game built with Unity 2022.3.57f1c2, combining factory-building logistics (like Factorio) with traditional RTS combat. The project uses a custom Entity-Component-System (ECS) architecture and the **NekoGraph visual scripting system** for high-performance game logic and flexible mission/story editing.

## Development Environment

### Unity Setup
- **Unity Version**: 2022.3.57f1c2
- **Render Pipeline**: Universal Render Pipeline (URP) 14.0.11
- **Target Platforms**: Windows
- **Key Dependencies**:
  - 2D Animation, SpriteShape, Tilemap Extras
  - TextMeshPro for text rendering
  - Newtonsoft JSON (com.unity.nuget.newtonsoft-json: 3.2.1)

### Opening the Project
1. Install Unity 2022.3.57f1c2 (or compatible version)
2. Open Unity Hub and add this folder as a project
3. The main scene is at `Assets/Scenes/SampleScene.unity`

### Building and Running
- **In Editor**: Press Play in Unity Editor to run `SampleScene`
- **Build Settings**: Configure via `File > Build Settings`
- **Platform-specific settings**: In `ProjectSettings/PlayerSettings`

### Package Management
Dependencies are managed via Unity Package Manager (`Packages/manifest.json`). Add new packages through the Package Manager window in Unity.

## Code Architecture

### Core ECS Pattern
The game follows a custom ECS implementation (not Unity's DOTS):

- **Entities**: Managed by `EntitySystem` (SingletonMono) with ID-based lookup
- **Components**: Structs stored in arrays in `WholeComponent` (see `EntitySystem.cs:58`)
- **Systems**: Singleton MonoBehaviours that process component data each frame

Key systems include:
- `MoveSystem`: Unit movement with tick-based timing (10 ticks/sec)
- `AttackSystem`: Combat and projectile physics
- `IndustrialSystem`: Factory production and resource processing
- `PowerSystem`: Electricity grid management (Union-Find topology)
- `PathfindingSystem`: NavMesh navigation with portal reservation (1082 lines)
- `ArbitrationSystem`: Path conflict resolution
- `TransportSystem`: Conveyor belt network construction and item transport (901 lines)
- `BoidSystem`: Flocking behavior
- `SpawnSystem` / `DeathSystem`: Entity lifecycle
- `DirectorSystem` / `AutoAISystem`: AI scanning and behavior decisions

### Component Structure
Components are defined in `Assets/Scripts/InStage/Component/`:
- `CoreComponent`: Basic entity properties (position, type, team)
- `MoveComponent`: Movement state, waypoints, timing
- `AttackComponent`: Combat attributes
- `ResourceComponent`, `InventoryComponent`: Industrial system data
- `PowerComponent`, `ConveyorComponent`: Logistics network data
- `ProjectileComponent`, `GoComponent`: Combat projectiles and orders

### Singleton Pattern
Two singleton implementations in `Assets/Scripts/InStage/Singleton.cs`:
- `SingletonMono<T>`: For MonoBehaviour systems (scene-persistent)
- `SingletonData<T>`: For pure data classes

### Time System
- **Tick-based logic**: `TimeTicker` class provides 10 ticks per second
- **Visual smoothing**: `SubTickOffset` for interpolation between logic ticks
- **Global timing**: `GlobalTick` increments each logic tick

### Event Bus (PostSystem)
`PostSystem` is the central observer-pattern event bus:
- `PostSystem.Send(eventName, data)`: Broadcast an event to all listeners
- `PostSystem.On(eventName, callback)`: Subscribe to an event
- `PostSystem.Off(eventName, callback)`: Unsubscribe from an event

---

## NekoGraph Visual Scripting System

> **Core highlight** - Located at `Assets/Scripts/NekoGraph/`

NekoGraph 3.0 uses a **full operating system-level architecture** with permission isolation, multiple execution contexts, and a Unix-style VFS interface.

### Core Architecture (OS Analogy)

| OS Concept | NekoGraph Equivalent | Description |
|------------|------------------------|-------------|
| Process Scheduler | `GraphHub` | Global hub managing all execution contexts |
| Process (PCB) | `EntityGraphContext` | Execution context for a single subject (Player/AI/System) |
| CPU | `GraphRunner` | Drives signal execution with time-slicing |
| MMU | `GraphAnalyser` | Memory management unit with permission checking |
| UID | `subjectLevel` | Subject identifier (0=Player, 100+=AI, 1000+=System) |
| Virtual Address Space | `PackDataDict` | Isolated data space per subject |
| Code Segment | `NodeStrategy` | Stateless node execution strategies |
| rwx Permissions | `PackAccessLevel` | Hidden/ReadOnly/ReadWrite access levels |

### Permission System
- **Isolation**: Each subject (Player/AI/System) has its own isolated `PackDataDict`
- **Three Levels**:
  - `Hidden`: subjectLevel < ReadableFrom (no access)`
  - `ReadOnly`: ReadableFrom ≤ subjectLevel < WritableFrom
  - `ReadWrite`: subjectLevel ≥ WritableFrom
- **Check Point**: All IO operations go through `GraphAnalyser.Resolve()`

### Key Files (`Assets/Scripts/NekoGraph/Runtime/`)
- **Runner_Analyser/`GraphHub.cs`: Global scheduler, manages all execution contexts
- **Runner_Analyser/`GraphRunner.cs`: CPU - drives signal flow, carries subjectLevel
- **Runner_Analyser/`GraphAnalyser.cs`: MMU - manages static graphs, VFS, permission checking
- **Runner_Analyser/`NodeStrategy.cs`: Abstract base class for node strategies
- **Runner_Analyser/`SignalContext.cs`: Signal data carrier with traveled path tracking
- **Common/`: TriggerNode, ComparerNode, CommandNode, DestroyNode strategies
- **Social/`: Social-specific node strategies

### Architecture Diagram
```
                    ┌─────────────────────────────────────────────────┐
                    │         GraphHub (Process Scheduler)              │
                    │  ┌───────────────────────────────────────┐  │
                    │  │ EntityGraphContext[Player] (UID=0)  │  │
                    │  │   ├─ GraphRunner (CPU)                 │  │
                    │  │   └─ GraphAnalyser (MMU)        │  │
                    │  └───────────────────────────────────────┘  │
                    │  ┌───────────────────────────────────────┐  │
                    │  │ EntityGraphContext[AI_1] (UID=100)    │  │
                    │  │   ├─ GraphRunner (CPU)                 │  │
                    │  │   └─ GraphAnalyser (MMU)        │  │
                    │  └───────────────────────────────────────┘  │
                    └─────────────────────────────────────────────────┘
                                         │
                                         ▼
                    ┌─────────────────────────────────────────────────┐
                    │         Signal Flow (Per Runner.Tick())                    │
                    │  ┌───────────────────────────────────────┐  │
                    │  │ 1. Dequeue signal from ActiveSignals  │  │
                    │  │ 2. Get NodeStrategy for current node    │  │
                    │  │ 3. Execute OnSignalEnter() with subjectLevel│  │
                    │  │ 4. Enqueue new signals to downstream │  │
                    │  └───────────────────────────────────────┘  │
                    └─────────────────────────────────────────────────┘
```

### VFS (Virtual File System)
NekoGraph provides a Unix-style file system interface for graph manipulation:
- `WriteFile(packID, path, content, subjectLevel)` - Write or create a file
- `CreateDirectory(packID, path, subjectLevel)` - Create a directory
- `Delete(packID, path, subjectLevel)` - Delete a node
- `GetNode(packID, path, subjectLevel)` - Get node by path
- `GetChildren(packID, path, subjectLevel)` - Get child nodes

### Node Types Summary

| Node | Role |
|------|------|
| RootNode | Entry point |
| SpineNode | Main flow; waits for LeafNode_B completion |
| LeafNode_A | Activates task, pushes to UI |
| LeafNode_B | Terminal node, signals completion |
| TriggerNode | Listens to PostSystem events |
| ComparerNode | Logic gate with fail backtrace |
| CommandNode | Executes registered commands |
| DestroyNode | Destroys entities/nodes |
| Social*Node | Social system-specific nodes |

### Adding a New Node Type
1. Create a `*NodeData` class in `Runtime/` or appropriate subfolder
2. Implement `NodeStrategy` subclass in corresponding location
3. Register in `NodeStrategyFactory` static constructor
4. No changes to core `GraphRunner`/`GraphAnalyser` required

---

## Command Pipeline System

> Located at `Assets/Scripts/InStage/UI/` (CommandRegistry) and integrated into NekoGraph via `CommandNode`

A centralised command execution framework with pipeline support for chaining commands.

### Core API
```csharp
// Execute a command
CommandOutput result = CommandRegistry.Execute(
    commandName,        // case-insensitive
    args,               // string[] parameters
    payload,            // upstream command output (pipeline data)
    console);           // optional DeveloperConsole reference

// Define a command
[CommandInfo("spawn", "🏗️ 召唤单位", "Entity",
    Parameters = new[] { "type", "count" },
    Tooltip = "在指定位置生成单位")]
public static CommandOutput Spawn(DeveloperConsole console, string[] args, object payload) { ... }
```

### CommandOutput Structure
```csharp
public class CommandOutput {
    public CommandResult Result { get; set; }   // Success / Failure / etc.
    public string Message { get; set; }         // Log message
    public object Payload { get; set; }         // Data passed to next command
}
```

Commands are auto-registered via reflection — any static method with `[CommandInfo]` attribute is discovered automatically.

---

## Key Directories

### Scripts (`Assets/Scripts/`)
- `Common/`: Shared systems
- `NekoGraph/`: NekoGraph visual scripting system
  - `Runtime/`: Core runtime (GraphRunner, GraphAnalyser, GraphHub)
  - `Editor/`: Visual editor tools
- `InStage/`: Core gameplay systems
  - `Component/`: Data structs for ECS
  - `Controller/`: Player input and camera control
  - `System/`: 20+ ECS logic processors
  - `UI/`: In-game UI (contains CommandRegistry)
  - `Singleton.cs`: Singleton pattern implementations
- `OutStage/`: Menus, saving, level selection
  - `BigMap/`: World map with GPU instancing (`BigMapGPUBufferManager.cs`)
  - `Mission/`: Mission manager (NekoGraph integration)
  - `GameFlowManager.cs`: Scene transitions
  - `SaveManager.cs`: Game state persistence (352 lines)
  - `View/`: Menu UI controllers

### Editor Tools (`Assets/Editor/`)
- `MissionGraphWindow.cs`: NekoGraph visual editor (Tools > 猫娘助手)

### Resources
- `Resources/Levels/`: Level JSON files
- `Resources/Missions/`: Mission pack JSON files (NekoGraph PackData)
- `UIPrefab/`: UI element templates
- `Settings/`: Configuration files
- `Shaders/`: Custom shaders for visual effects

---

## Development Notes

### Code Style
- **Comments**: Extensive Chinese comments explain logic (阅读友好)
- **Naming**: Mixed English method names with Chinese variable comments
- **Performance**: Array-based component storage avoids GC pressure
- **Debugging**: Rich Gizmos visualization for systems (enable in Scene view)

### Design Patterns in Use
| Pattern | Where |
|---------|-------|
| Singleton | All system managers (`SingletonMono<T>`, `SingletonData<T>`) |
| Strategy | NekoGraph node processors (`INodeStrategy`), industrial building work logic (`IWorkStrategy`) |
| Observer | Event bus (`PostSystem`) |
| Factory | Level data (`WorldFactory`), node strategies (`NodeStrategyFactory`) |
| State | Game flow (`GameFlowController`) |
| Object Pool | Building preview ghosts |

### Game Systems Design
1. **Navigation**: Rectangular NavMesh with portal-based pathfinding + time-slot reservation (64-bit mask)
2. **Logistics**: Conveyor belt networks — same-direction belts merge into `TransportLine`
3. **Power Grid**: Union-Find topology; BFS connectivity check; proportional distribution when supply is insufficient
4. **Combat**: Projectile physics with unit collision
5. **Building**: Grid-based placement with adjacency bonuses
6. **NekoGraph**: Signal-driven visual scripting for missions, stories, events

### Testing and Debugging
- Use Unity's Scene view with Gizmos enabled to visualize:
  - NavMesh portals and reservations
  - Power grid connections
  - Conveyor belt item flow
  - Unit pathfinding waypoints
- The `TestManager.cs` provides debugging utilities
- Enable verbose NekoGraph logging: `GraphRunner.Instance.EnableDebugLog = true`
- Inspect graph state: `GraphRunner.Instance.GetDebugInfo()`

### Save System
Game state is serializable via `SaveManager.cs`. Save files include:
- Entity positions and components
- Building configurations
- Resource inventories
- Mission progress

---

## Common Development Tasks

### Adding a New ECS Component
1. Define struct in `Component/` folder
2. Add array to `WholeComponent` in `EntitySystem.cs`
3. Create or extend a System to process the component
4. Update `EntitySystem.Initialize()` to allocate array

### Creating a New Building Type
1. Define blueprint in relevant configuration
2. Implement `IWorkStrategy` for production behavior
3. Add to `BuildSystem` placement logic
4. Create UI elements in `UIPrefab/`

### Modifying Movement Logic
1. Edit `MoveSystem.UpdateMovement()`
2. Adjust `PathfindingSystem` for navigation changes
3. Update `ArbitrationSystem` for collision handling
4. Test with various unit sizes and congestion scenarios

### Adding a New Game Command
1. Create a static method with `[CommandInfo]` attribute in a commands class
2. It is auto-discovered by `CommandRegistry` via reflection — no registration step needed
3. Return `CommandOutput` with optional `Payload` for pipeline chaining

### Creating a New Mission Pack (NekoGraph)
1. Design the node graph in `MissionGraphWindow` (Tools > 猫娘助手)
2. Export JSON to `Resources/Missions/`
3. Load at runtime: `MissionManager.Instance.LoadMissionPack("Missions/YourPack")`
4. Multiple packs can run in parallel (each gets its own `RuntimeGraphInstance`)

---

## Troubleshooting

### Common Issues
- **Entities not moving**: Check `MoveComponent.IsBlocked` and pathfinding status
- **Power not flowing**: Verify `PowerSystem` connections and generator output
- **Items stuck on conveyors**: Inspect `ConveyorComponent` neighbor links
- **AI not attacking**: Review `AIBrainSystem` / `AutoAISystem` decision weights
- **NekoGraph signal stuck**: Check `GraphRunner.Instance.GetDebugInfo()` for powered Triggers; verify `PostSystem` event names match exactly
- **Command not found**: Ensure the method has `[CommandInfo]` attribute and the class is loaded

### Performance Considerations
- Entity count is limited to 1024 by default (`EntitySystem.maxEntityCount`)
- Component arrays are pre-allocated for cache efficiency
- Pathfinding uses spatial partitioning to reduce search space
- Industrial systems batch process similar entities
- Unpowered NekoGraph Trigger nodes consume zero performance (only powered nodes subscribe to PostSystem)

## Extension Points

### Custom AI Behaviors
Extend `AIBrainSystem` / `AutoAISystem` with new decision nodes

### New Resource Types
Add to `ResourceComponent` enum and update `IndustrialSystem` processing

### New NekoGraph Node Types
Implement `INodeStrategy`, register in `NodeStrategyFactory` — no core changes needed

### Additional Game Modes
Create new `OutStage` scenes and connect via `GameFlowManager`

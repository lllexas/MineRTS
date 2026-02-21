# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MineRTS** is a 2D real-time strategy (RTS) game built with Unity 2022.3.57f1c2, combining factory-building logistics (like Factorio) with traditional RTS combat. The project uses an Entity-Component-System (ECS) architecture for performance and modularity.

## Development Environment

### Unity Setup
- **Unity Version**: 2022.3.57f1c2
- **Render Pipeline**: Universal Render Pipeline (URP) 14.0.11
- **Target Platforms**: Windows, Android, iOS, Switch, PS4/5
- **Key Dependencies**:
  - 2D Animation, SpriteShape, Tilemap Extras
  - TextMeshPro for text rendering
  - Visual Scripting for visual programming

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
- `PowerSystem`: Electricity grid management
- `PathfindingSystem`: NavMesh navigation with portal reservation
- `ArbitrationSystem`: Path conflict resolution

### Component Structure
Components are defined in `Assets/Scripts/InStage/Component/`:
- `CoreComponent`: Basic entity properties (position, type, team)
- `MoveComponent`: Movement state, waypoints, timing
- `AttackComponent`: Combat attributes
- `ResourceComponent`, `InventoryComponent`: Industrial system data
- `PowerComponent`, `ConveyorComponent`: Logistics network data

### Singleton Pattern
Two singleton implementations in `Assets/Scripts/InStage/Singleton.cs`:
- `SingletonMono<T>`: For MonoBehaviour systems (scene-persistent)
- `SingletonData<T>`: For pure data classes

### Time System
- **Tick-based logic**: `TimeTicker` class provides 10 ticks per second
- **Visual smoothing**: `SubTickOffset` for interpolation between logic ticks
- **Global timing**: `GlobalTick` increments each logic tick

## Key Directories

### Scripts (`Assets/Scripts/`)
- `InStage/`: Core gameplay systems
  - `Component/`: Data structs for ECS
  - `Controller/`: Player input and camera control
  - `System/`: Game logic processors (ECS systems)
  - `UI/`: In-game user interface
  - `Singleton.cs`: Singleton pattern implementations
- `OutStage/`: Menus, saving, level selection
  - `GameFlowManager.cs`: Scene transitions
  - `SaveManager.cs`: Game state persistence
  - `View/`: Menu UI controllers

### Game Systems
- `AIBrainSystem/`: Enemy AI decision making
- `IWorkStrategy/`: Strategy pattern for factory building behaviors
- `GridSystem.cs`: Spatial partitioning and collision detection
- `MapRegistry.cs`: Terrain and static object data

### Editor Tools (`Assets/Editor/`)
- `MissionGraphWindow.cs`: Visual mission editor (Tools > 猫娘助手)

### Resources
- `UIPrefab/`: UI element templates
- `Settings/`: Configuration files
- `Shaders/`: Custom shaders for visual effects

## Development Notes

### Code Style
- **Comments**: Extensive Chinese comments explain logic (阅读友好)
- **Naming**: Mixed English method names with Chinese variable comments
- **Performance**: Array-based component storage avoids GC pressure
- **Debugging**: Rich Gizmos visualization for systems (enable in Scene view)

### Game Systems Design
1. **Navigation**: Rectangular NavMesh with portal-based pathfinding
2. **Logistics**: Conveyor belt networks for item transport
3. **Power Grid**: Electricity generation, transmission, and consumption
4. **Combat**: Projectile physics with unit collision
5. **Building**: Grid-based placement with adjacency bonuses

### Testing and Debugging
- Use Unity's Scene view with Gizmos enabled to visualize:
  - NavMesh portals and reservations
  - Power grid connections
  - Conveyor belt item flow
  - Unit pathfinding waypoints
- The `TestManager.cs` provides debugging utilities

### Save System
Game state is serializable via `SaveManager.cs`. Save files include:
- Entity positions and components
- Building configurations
- Resource inventories
- Mission progress

## Common Development Tasks

### Adding a New Component
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

## Troubleshooting

### Common Issues
- **Entities not moving**: Check `MoveComponent.IsBlocked` and pathfinding status
- **Power not flowing**: Verify `PowerSystem` connections and generator output
- **Items stuck on conveyors**: Inspect `ConveyorComponent` neighbor links
- **AI not attacking**: Review `AIBrainSystem` decision weights

### Performance Considerations
- Entity count is limited to 1024 by default (`EntitySystem.maxEntityCount`)
- Component arrays are pre-allocated for cache efficiency
- Pathfinding uses spatial partitioning to reduce search space
- Industrial systems batch process similar entities

## Extension Points

### Custom AI Behaviors
Extend `AIBrainSystem` with new decision nodes or modify `AttackWaveBrain`

### New Resource Types
Add to `ResourceComponent` enum and update `IndustrialSystem` processing

### Additional Game Modes
Create new `OutStage` scenes and connect via `GameFlowManager`

This architecture supports mixing RTS combat with complex factory logistics while maintaining performance through ECS and tick-based simulation.
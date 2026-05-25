# Technical Overview: The Gauntlet

## 1. Project Description
**The Gauntlet** is a high-fidelity third-person Action RPG (ARPG) built in Unity 6, focusing on rhythmic, souls-lite combat and a modular character progression system. Players navigate dangerous dungeon environments, utilizing "Gauntlets" that can be customized with various Skill and Stat Gems to define their playstyle. The experience targets players who enjoy technical melee combat, build-crafting, and atmospheric dungeon crawling.

## 2. Gameplay Flow / User Loop
1.  **Preparation**: From the `MainMenu`, players enter the game world (starting in `Tutorial` or a hub area).
2.  **Exploration**: Navigate through non-linear environments like `Dungeon Level` and `Cave Level`.
3.  **Engagement**: Encounter enemies (Grunts, Elites, Bosses) using a lock-on combat system.
4.  **Progression**: Defeated enemies drop experience (tracked by `ProgressBarCircle`) and potentially gems.
5.  **Modification**: Use the `InventoryManager` and `GauntletManager` (via UI) to socket new Skill and Stat gems into primary/secondary gauntlets, dynamically altering the player's stats and available abilities.
6.  **Transition**: Clear chambers to trigger `ChamberRewardTrigger` and progress via `LevelTransition` to more difficult areas.

## 3. Architecture
The project follows a **Component-Based Architecture** with a strong emphasis on **State Machines** for actor behavior and **ScriptableObjects** for data definition.

*   **Central State Management**: `PlayerManager` acts as the "Source of Truth" for the player's current state (`Idle`, `Attacking`, `Dodging`, etc.), coordinating between movement, combat, and animation.
*   **Combat Logic**: Driven by `PlayerCombat`, which utilizes a `ScriptableObject`-based combo tree (`AttackNode`).
*   **Stats & Modifiers**: `PlayerStatsManager` calculates global attributes by aggregating base stats with modifiers from equipped gems using a flat/percentage calculation pattern.
*   **Event-Driven Systems**: Enemy deaths and stat changes utilize C# Actions (e.g., `BaseEnemy.OnAnyEnemyDied`) to notify decoupled systems like reward managers or UI.

`Location: Assets/Scripts/Player`

## 4. Game Systems & Domain Concepts

### Combat System
The combat system uses a "Combo Tree" approach. Each attack is defined as an `AttackNode` containing animation triggers, stamina costs, and references to the next possible light or heavy attacks.
*   `PlayerCombat`: Handles input buffering, combo execution, and stamina verification.
*   `HitboxController`: Manages the activation of physical colliders on the player's hands during specific animation frames.
*   `AttackNode`: A `ScriptableObject` defining individual moves in a combo string.
*   **Patterns**: Command-like pattern for attack nodes and a Buffer Pattern for input handling.

`Location: Assets/Scripts/Player`

### Enemy AI System
Built on Unity's `NavMesh` system, enemies use an inheritance-based AI model.
*   `BaseEnemy`: An abstract class providing core functionality for awareness, line-of-sight, chasing, and hit-stun logic.
*   `BasicEnemy`, `EliteEnemy`, `DemonBoss`: Concrete implementations with specific attack patterns.
*   `EnemyHealthBar`: World-space UI component that tracks individual enemy health.

`Location: Assets/Scripts/Enemy`

### Gem & Equipment System
A modular system allowing for deep customization of player capabilities.
*   `RunTimeGauntlet`: A backend class (non-MonoBehaviour) representing an equipped weapon and its current sockets.
*   `StatGemData` / `SkillGemData`: `ScriptableObjects` that define modifiers (e.g., +10% Physical Damage) or active spells (e.g., Fireball).
*   `GemEffectsManager`: Listens for specific gameplay events to trigger dynamic gem effects like "Berserker" (increased damage after kills).

`Location: Assets/Gem Upgrades`

## 5. Scene Overview
*   **MainMenu**: Entry point for the application.
*   **UI**: A persistent scene loaded additively containing the global HUD, inventory screens, and menus.
*   **Tutorial**: A controlled environment teaching basic movement and combat.
*   **Dungeon Level / Cave Level**: Primary gameplay environments featuring "Chamber" logic where clearing enemies unlocks rewards.
*   **EnemiesSandbox**: A developer scene for testing enemy AI and combat balance.

`Location: Assets/Scenes`

## 6. UI System
The project uses a hybrid of **UGUI (Unity GUI)** for world-space elements and **UI Toolkit** for complex menu structures.
*   **Inventory UI**: Managed by `InventoryManager` and `GauntletManager`, supporting drag-and-drop gem socketing.
*   **HUD**: Displays real-time health, stamina, and skill cooldowns (queried from `PlayerCombat`).
*   **Persistence**: `TempUIPersistence` ensures UI state remains consistent across scene transitions.
*   **Color Picking**: Integration of `FlexibleColorPicker` for potential character or equipment customization.

`Location: Assets/UI`

## 7. Asset & Data Model
*   **ScriptableObjects**: Extensively used for `AttackNodes`, `GauntletData`, and `GemData`. This allows designers to create new items and moves without modifying code.
*   **Addressables**: The project structure includes `AddressableAssetsData`, indicating that assets are managed for efficient loading/memory usage, likely for late-game content or DLC.
*   **Prefabs**: All actors (Player, Enemies) and environment modules (Cave Level Design) are prefab-based to ensure consistency across scenes.
*   **Naming Convention**: Follows a standard `Category_Name` or `Name_Data` convention (e.g., `Light1.asset`, `Demon-Boss.fbx`).

`Location: Assets/ScriptableObjects`

## 8. Notes, Caveats & Gotchas
*   **Animation Events**: Combat relies heavily on `AnimationEvents` (e.g., `ArmTargetHitbox`, `EndAttack`). If an animation is changed, ensure these events are re-added or the player may get stuck in an `Attacking` state.
*   **Combat Timeout**: The `PlayerManager` has a `CombatTimeout` that drops the player out of the "InCombat" state after 5 seconds of inactivity, which affects stamina regeneration.
*   **Input System**: Uses the **New Input System** package. Key bindings are found in `InputSystem_Actions.inputactions`.
*   **Fracture System**: Uses the `OpenFracture` plugin for environmental destruction (see `Fracture.cs`).

`Location: Assets/Scripts`
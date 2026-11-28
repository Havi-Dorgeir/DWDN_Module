# Tooling and Reverse-Engineering Methodology

> Part of the DWDN agent guide. See [AGENTS.md](../../AGENTS.md) for the entry point, build commands and the topic index.

### 2.1. GitHub repositories and tools
* **Main repository for Bannerlord modding:**  
  [https://github.com/rehan-remade/universal-modder](https://github.com/rehan-remade/universal-modder)  
  *Use it for SubModule templates, CLI builds, asset parsing, and for understanding non-obvious engine structures.*
* **Reference mods from the Steam Workshop (study their architecture before reinventing the wheel):**
  * `<Steam>\steamapps\workshop\content\261550\3774022783` (**LOTR / OMEE**) — **the primary model**: the cleanest Gauntlet UI architecture, full isolation of the 3D preview via `AgentVisuals.Create`, network packets for custom spawning.
  * `<Steam>\steamapps\workshop\content\261550\3772431545` (**Warlords**) — the reference for custom combat classes and perks.
  * `<Steam>\steamapps\workshop\content\261550\3716004111` — an example of network item balance configuration.

### 2.2. Decompilation and research of the TaleWorlds libraries
All core assemblies are located in the game folder:  
`<Bannerlord>\bin\Win64_Shipping_Client\`  
Key assemblies for analysis:
* `TaleWorlds.MountAndBlade.dll` — missions, agent spawning, network messages (`GameNetwork`), `MissionPeer`, `MissionMultiplayerGameModeBase`.
* `TaleWorlds.Core.dll` — `ItemObject`, `BasicCharacterObject`, `Equipment`, `EquipmentIndex`, `WeaponDesign`.
* `TaleWorlds.Engine.dll` — `AgentVisuals`, `Camera`, `Scene`, `SoundEvent`, `MatrixFrame` matrices.
* `TaleWorlds.MountAndBlade.Multiplayer.dll` — native components of the network lobby and spawning.

> [!TIP]
> **Headless research without a GUI:**  
> To quickly find the signature of any private TaleWorlds method or property, create a short C# or PowerShell script using `[System.Reflection.Assembly]::LoadFrom(...)` directly in `scratch\` (outside the repository). This saves tens of minutes compared to opening heavy decompilers.

---

### Full Reverse Engineering of the Game Core and Decompilation Tools

To gain a deep understanding of the original game (*Native*, *SandBoxCore*, *SandBox*, *Multiplayer* v1.4.8) and to build your own modding utilities, use the following resources from the author's scratch hub `d:\banAImod` (it is not part of this repository):

1. **[docs/ENGINE.md](../ENGINE.md)** — a full encyclopedia of the Bannerlord architecture ("taken apart brick by brick and rebuilt"):
   * The C++/C# bridge (`DotNetObject`, `[MBCallback]` methods in `TaleWorlds.MountAndBlade.Module`).
   * The dual XML pipeline: the native `ModuleData/project.mbproj` (all 39 `soln_*` identifiers) vs the managed `MBObjectManager` (the XSD-validation algorithm, dynamic `_replaceWhileMerging` injection, XSLT transformations and `GetPresumedObject`).
   * The binary structure of `.tpac` assets (LZ4 + xxHash64), of the `.rdc` cache (145-byte `RDC0` records) and the strict 64-skeleton-bone contract (the `0x1050`-byte `AnimResult` buffer).
   * The full lifecycle of `MBSubModuleBase`, `MBGameManager`, `Game`, `GameType` (`Campaign` and `MultiplayerGame`), `GameStateScreenManager` and `ViewCreatorManager` (`[OverrideView]`, `[ViewMethod]`).
   * Blueprints for building your own modding tools (headless XML/XSD/XSLT validator, `.tpac`/`.rdc` packer, static analyzer).
2. **Static CLI decompiler and dumps (`scratch\`, outside the repository)**:
   * `scratch\inspector\Program.cs` — a tool based on `ICSharpCode.Decompiler` that lets you instantly decompile any class from `Win64_Shipping_Client`, `DedicatedCustomServer` or the modules, without `AssemblyResolve` crashes.
   * `scratch\module_types_dump.txt` — the complete registry of all 1923 classes and interfaces across the 11 DLL assemblies of `Native`, `Multiplayer` and `SandBox`.
   * `scratch\submodules_decompiled.txt` — the decompiled source of all `SubModule` classes, `Module` and `MBSubModuleBase`.
   * `scratch\core_managers_decompiled.txt` — the decompiled code of `MBObjectManager`, `SandBoxManager`, `ViewCreatorManager`, `GameStateScreenManager`, `MultiplayerMissions` and `SandBoxMissions`.
   * `scratch\game_and_mp_decompiled.txt` — the decompiled code of `TaleWorlds.Core.Game`, `MultiplayerGame` and `MultiplayerGameManager`.

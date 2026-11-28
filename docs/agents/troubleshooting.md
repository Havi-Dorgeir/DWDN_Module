# Server and Client Troubleshooting (Symptom / Cause / Fix)

> Part of the DWDN agent guide. See [AGENTS.md](../../AGENTS.md) for the entry point, build commands and the topic index.

### Diagnosing an Endless Loading Screen When Joining the Dedicated Server (TaleWorlds Patches)
* **Symptom**: The client enters the multiplayer lobby, presses "Join server", and hangs in the `WaitingToJoinCustomGame` state on an endless loading screen while `AliveMessage` packets are sent in a loop to the TaleWorlds lobby.
* **Cause**: The `DedicatedCustomServer.Starter.exe` process crashes at the moment of the player handshake (`KERNELBASE.dll`, .NET Runtime `Event ID 1026`).
* **Case 11.11.2026**: In an engine update TaleWorlds removed the method `TaleWorlds.MountAndBlade.NetworkCommunicator.UpdateForJoiningCustomGame(bool)`. The third-party module `DoFAdminTools` (compiled against the older build) called this method from `DoFGameHandler.OnPlayerConnect(VirtualPlayer peer)` whenever any player joined, which produced an unhandled `System.MissingMethodException` and brought the server down. The automatic restart loop in `ZAPUSKSERVER.bat` immediately brought the server back up, creating the illusion of a "live" server that crashes exactly at the moment a player connects.
* **Fix**:
  1. Check the Windows Event Log: `Get-WinEvent -LogName Application -MaxEvents 10 | Where-Object { $_.ProviderName -match 'Application Error|.NET Runtime' }`.
  2. Remove outdated third-party DLLs from the `_MODULES_` launch arguments in `ZAPUSKSERVER.bat`. The `DWDN_TRY` module is fully autonomous and does not depend on `DoFAdminTools`.

### Server Crash on Spawn (NullReferenceException in Equipment.GetInitialWeaponIndicesToEquip)
* **Symptom**: The player on the server presses `[F]` to spawn — the agent does not appear and the server crashes immediately. The Windows event log shows a `NullReferenceException` crash in `Equipment.GetInitialWeaponIndicesToEquip` $\rightarrow$ `Agent.WieldInitialWeapons` $\rightarrow$ `SpawningBehaviorBase.OnTick`.
* **Cause**: In multiplayer TaleWorlds loads exclusively `mp_crafting_pieces.xml` and ignores the single-player `crafting_pieces.xml`. If a weapon in the mod is declared as `<CraftedItem>` referencing single-player pieces (for example `cleaver_blade_1` instead of the multiplayer `mp_battania_cleaver_blade`), the engine cannot initialize the `WeaponComponent`. As a result `item.PrimaryWeapon == null`. When the engine tries to equip the weapon into the hands, the call `item.PrimaryWeapon.WeaponFlags` throws a fatal `NullReferenceException`.
* **Consequence in the spawn logic**: In `DWDN_ServerLoadoutBehavior.cs` the ternary expression `int targetIdx = string.IsNullOrEmpty(selection.HorseId) ? 0 : idx;` incorrectly reset any infantry class (including the Artillerist) to index 0 (`Ratay`), whose first default perk was precisely the broken `dwdn_cleaver`.
* **Fix**:
  1. In `mpitems/dwdn_weapon.xml` use only legitimate multiplayer pieces with the `mp_...` prefix from `Native/ModuleData/mp_crafting_pieces.xml`.
  2. In `DWDN_ServerLoadoutBehavior.cs` remove the forced reset to 0: `int targetIdx = idx;`.
  3. In `ApplySelectionToEquipment` add a hard check: if an item in a weapon slot has `item.PrimaryWeapon == null` (and is not a shield/banner), clear the slot to protect the process from crashing.

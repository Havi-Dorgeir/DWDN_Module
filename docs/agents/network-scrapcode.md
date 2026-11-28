# The Anti-Scrapcode Codex and Network Protocol Pitfalls

> Part of the DWDN agent guide. See [AGENTS.md](../../AGENTS.md) for the entry point, build commands and the topic index.

1. **The rule of minimal intervention:**  
   Never rewrite working systems when the task is only to add a new item or class. One clumsy change to a network message signature breaks client-server backward compatibility.
2. **Always check the engine logs:**  
   Game logs are written in real time:  
   `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_*.txt`  
   If the client closed silently, open the latest `rgl_log` and scroll to the very bottom — it will name the specific XML tag or the missing mesh.
3. **UI thread safety:**  
   No heavy search loops in `Tick` or `OnMissionScreenTick`. All item lists in `DWDN_DuchyManager` must be cached at initialization.
4. **Removing items (Empty slot `""`):**  
   For helmets, cloaks, gauntlets and shields, always provide the string ID `""` (empty string). This lets the player take off a helmet or a shield by clearing the corresponding slot.
5. **Synchronize all nodes:**  
   After any XML edit or DLL compilation, **always** run `sync_all.ps1`. If the client and the dedicated server differ by even one attribute in `mpclassdivisions.xml`, the server will kick the player with a version-mismatch error.

---

## 46. CraftedItem Network Scrapcode and TaleWorlds Network Compression (WeaponUsageIndexChangeMessage)

* **The Campaign vs Multiplayer crafting-pieces trap**:
  * The Bannerlord engine uses **different XML manifests of crafting pieces** for the single-player campaign (`Campaign`) and for the network game (`MultiplayerGame`).
  * In `Native/SubModule.xml`: `MultiplayerGame` loads strictly `mp_crafting_pieces.xml`, while `crafting_pieces.xml` is available only in `Campaign` and `CustomGame`.
  * If you declare `<CraftedItem multiplayer_item="true">` with single-player pieces (for example `dagger_blade_1`, `empire_dagger_guard_3`, `vlandian_grip_1`, `vlandian_pommel_1`), the item loads in single player, but **on the multiplayer server the engine will not find these pieces and will return null**:
	```
	Null object reference found with ID: <item_id>
	```
  * For network items it is allowed to use ONLY pieces with the `mp_` prefix from `mp_crafting_pieces.xml` (for example `mp_dagger_blade_1`, `mp_battania_dagger_guard`, `mp_battania_dagger_grip`, `mp_battania_dagger_pommel`).

* **Cascading network-protocol crash when a player joins (`Integer_compression_info::decode_safe`)**:
  * If a `Null object reference` ends up in a class's equipment (`mpclassdivisions.xml` or `mpcharacters.xml`), the engine loses the validity of that slot.
  * When a player connects (`Join game successful`) the server synchronizes weapon and equipment state. A broken slot integrity leads either to a desynchronization of the packet bit stream or to sending a slot outside the allowed range:
	```
	Reading message: WeaponUsageIndexChangeMessage
	Invalid encode/decode in Integer_compression_info::decode_safe. min: 0. max: 4. input: 5. Message: internal_decode
	```
  * In TaleWorlds, `ItemSlotCompressionInfo` is compressed strictly into the range `min: 0, max: 4` (weapons 0..3 + ExtraSlot 4). The value 5 corresponds to `EquipmentIndex.Head` / `ArmorItemBeginSlot` and is categorically invalid in `WeaponUsageIndexChangeMessage`. The decoder immediately triggers `internal_decode` and throws the player out of the session.

* **Hard rule for deploying builds onto the Dedicated Server**:
  * The Bannerlord dedicated server may reference the module either by the folder name `Modules\DaewdenM` or by `Modules\DWDN_TRY`.
  * To prevent Mod Network Messages desynchronization, the `sync_all.ps1` script must mirror `DWDN_Script.dll` and `DWDN_Script.Server.dll` into both module folders and also directly into `bin\Win64_Shipping_Server`.

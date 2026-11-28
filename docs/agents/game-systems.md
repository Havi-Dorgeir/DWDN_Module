# Standalone Modding Tools and DWDN Game Systems

> Part of the DWDN agent guide. See [AGENTS.md](../../AGENTS.md) for the entry point, build commands and the topic index.

The repository ships three specialized GUI studios (WinForms .NET 8.0) under `Tools/` for automating modding and verification:

1. **Automatic 3D Weapon Calibrator (`Tools/WeaponCalibrator/`, shortcut [`ЗАПУСК_КАЛИБРАТОРА.bat`](../../Tools/ЗАПУСК_КАЛИБРАТОРА.bat))**:
   * Parses the module's binary `.tpac` archives (`LZ4` + `METM`) and `AssetSources/*.fbx` directly, computing exact 3D dimensions (`BoundingBox`, Z joints, neck radius) and automatically generating calibrated XML for `crafting_pieces.xml` and `mpitems.xml`, with an interactive 3D viewport.
2. **Visual Duchy and Loadout Constructor (`Tools/DuchyLoadoutStudio/`, shortcut [`ЗАПУСК_КОНСТРУКТОРА_КЛАССОВ.bat`](../../Tools/ЗАПУСК_КОНСТРУКТОРА_КЛАССОВ.bat))**:
   * Parses `DWDN_DuchyManager.cs`, `mpclassdivisions.xml`, `mpcharacters.xml` and `mpitems.xml`, showing a 3D mannequin of the selected class in real time.
   * The **"⚡ AUTO-APPLY INTO THE DWDN_TRY MOD"** button automatically patches `DWDN_DuchyManager.cs`, synchronizes `mpclassdivisions.xml` and `mpcharacters.xml`, compiles `DWDN_Script.dll` + `DWDN_Script.Server.dll` and mirrors the build onto the dedicated server `DaewdenM`.
3. **Instant Pre-Launch Mod Validator (`Tools/ModValidator/`, shortcut [`ЗАПУСК_ВАЛИДАТОРА_МОДА.bat`](../../Tools/ЗАПУСК_ВАЛИДАТОРА_МОДА.bat))**:
   * In about 0.25 s it checks 8 critical nodes of the mod: XML syntax, `SubModule.xml`, the integrity of the chain `DWDN_DuchyManager.cs` $\leftrightarrow$ `mpclassdivisions.xml` $\leftrightarrow$ `mpcharacters.xml` $\leftrightarrow$ `mpitems.xml` $\leftrightarrow$ `crafting_pieces.xml`, the presence of 3D meshes in `.tpac`, the gap balance of crafted weapons, and byte-for-byte `SHA-256` hash synchronization between client and server (with a one-click auto-fix button).

### Bannerman Tactical Aura System (`DWDN_BannerAuraBehavior.cs`)
* File: [`DWDN_BannerAuraBehavior.cs`](../../ModuleData/scripts/DWDN_Script/DWDN_BannerAuraBehavior.cs), registered in `SubModule.AddMissionBehaviors`.
* **Important technical pattern: `AgentDrivenProperties` in multiplayer**:
  * In Bannerlord, when the wielded weapon changes (`OnWieldedItemIndexChange`) the engine calls `agent.UpdateAgentProperties()`, which overwrites the dynamic stats (`MaxSpeedMultiplier`, `SwingSpeedMultiplier`, `ReloadSpeed`) but does **not** recompute the static spawn stats (`ArmorHead/Torso/Arms/Legs`, `CombatMaxSpeedMultiplier`).
  * Therefore, when applying or removing buffs in `DWDN_BannerAuraBehavior`, the base armor values (`BaseArmorHead` and so on) are cached first on the agent's first spawn, then `agent.UpdateAgentProperties()` is called, after which `Armor* = BaseArmor* + bonus` is assigned on top of the clean stats, the speed coefficients are multiplied and `agent.UpdateCustomDrivenProperties()` is called. This completely eliminates cumulative stat stacking and buff loss on weapon change, and running on client and server at the same time prevents a network speed desync (rubberbanding).

### Field Artillery and Crew System (`DWDN_FieldArtilleryBehavior.cs`)
* File: [`DWDN_FieldArtilleryBehavior.cs`](../../ModuleData/scripts/DWDN_Script/DWDN_FieldArtilleryBehavior.cs), class `mp_Artillerist` (`mp_Artillerist_hero` / `mp_Artillerist_troop`).
* **Crew items (`dwdn_weapon.xml`)**:
  * `dwdn_field_bombard` — Field Bombard (limit: 2 active barrels per team in `DWDN_ServerLoadoutBehavior.cs`).
  * `dwdn_cannonball_ammo` — Cast-Iron Cannonballs (3.4 m AoE high-explosive blast in `OnMissileHit` with `BlowFlags.KnockDown | BlowFlags.CanDismount`).
  * `dwdn_canister_ammo` — Canister Charges (conical volley of $13.5^\circ$ out to 32 m in `OnAgentShootMissile`).
  * `dwdn_artillery_ramrod` — Artillery Rammer-Swab (tool of the second crew member, based on `TwoHandedMace`).
  * `dwdn_powder_chest` — Bombardier's Charge Pouch (10 charges for automatic feeding of the gunner every 6 s through `SetWeaponAmountInSlot`).
* **Inviolability of `native_parameters.xml`**:
  * Never override `ModuleData/native_parameters.xml` in the mod — the TaleWorlds developers change the structure and values of `Native/ModuleData/native_parameters.xml` in game patches. Implement all dynamic speed, reload and armor modifiers strictly through `AgentDrivenProperties` in a C# `MissionLogic`.

---

### Built-in DWDN Admin Tools Administration System (`Server\Admin\`)
* Files:
  * [`DWDN_AdminService.cs`](../../ModuleData/scripts/DWDN_Script/Server/Admin/DWDN_AdminService.cs) — the permission-management core, the command handler, and the formation and action calculations.
  * [`DWDN_AdminGameHandler.cs`](../../ModuleData/scripts/DWDN_Script/Server/Admin/DWDN_AdminGameHandler.cs) — interception of the chat network messages `PlayerMessageAll` and `PlayerMessageTeam`.
  * [`DWDN_AdminBehavior.cs`](../../ModuleData/scripts/DWDN_Script/Server/Admin/DWDN_AdminBehavior.cs) — maintaining the invulnerability state across agent changes and damage protection.
  * [`DWDN_InputBehavior.cs`](../../ModuleData/scripts/DWDN_Script/DWDN_InputBehavior.cs) — the `[F10]` hotkey for the quick administrator help.
* **Intercepting chat commands without spamming the public chat**:
  * In `GameHandler.OnGameNetworkBegin()` the delegates `registerer.Register<PlayerMessageAll>` and `Register<PlayerMessageTeam>` are registered.
  * If a message starts with `!`, `DWDN_AdminService.HandleCommand(peer, message.Message)` is called.
  * So that the command text does not appear in the public chat, the private receiver list is emptied through reflection:
	```csharp
	typeof(PlayerMessageAll).GetProperty("ReceiverList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
		?.SetValue(message, new List<VirtualPlayer>());
	typeof(PlayerMessageAll).GetProperty("HasReceiverList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
		?.SetValue(message, true);
	```
	In the native method `ChatBox.ServerPrepareAndSendMessage` the check `receiverList.Contains(item.VirtualPlayer)` then returns `false` for every player, which blocks delivery to clients.
* **Native invulnerability (God Mode)**:
  * No third-party workarounds are needed: Bannerlord has a built-in agent state `agent.SetMortalityState(Agent.MortalityState.Invulnerable);` (and `agent.MountAgent.SetMortalityState(...)`), in which the native C++ engine zeroes all incoming damage (`InflictedDamage = 0`).
  * For reliability, `DWDN_AdminBehavior` also intercepts `OnAgentHit` and ticks in a loop, guaranteeing invulnerability even after a class change or respawn.
* **Group teleportation in formation**:
  * The command `!tp all me` or `!tp team1 me` / `!tp team2 me` lines players up in even ranks in front of the administrator:
	* The administrator's view vector $f$ and the perpendicular $r = (-f_y, f_x)$.
	* Base point $P_0 = P_{\text{admin}} + f \cdot 4.0\text{ m}$.
	* Grid spacing: $dx = 1.8\text{ m}$ along the front, $dy = 2.0\text{ m}$ in depth.
	* Terrain snapping: `Mission.Current.Scene.GetHeightAtPoint(targetPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref targetPos.z)`.
	* This prevents players and horses from getting stuck in each other or under the map textures.
* **Official mechanism for kicking and banning a player on the server**:
  ```csharp
  DisconnectInfo disconnectInfo = peer.PlayerConnectionInfo?.GetParameter<DisconnectInfo>("DisconnectInfo") ?? new DisconnectInfo();
  disconnectInfo.Type = DisconnectType.KickedByHost;
  peer.PlayerConnectionInfo?.AddParameter("DisconnectInfo", disconnectInfo);
  peer.QuitFromMission = true;
  GameNetwork.AddNetworkPeerToDisconnectAsServer(peer);
  ```
  When banning, the entry is added to `Modules/Native/banlist.txt` and `Modules/DaewdenM/banlist.txt`.
* **Command reference**:
  * Authentication: `!login <password from DWDN_config.txt>`, `!logout`
  * Teleportation: `!goto <nickname>`, `!bring <nickname|all|team1|team2>`, `!tp <target1> <target2>`, `!tp <target> <x> <y> [z]`, `!mark`, `!recall [target]`
  * Supply: `!heal [target] [hp]`, `!ammo [target]`, `!repair [target]`, `!god [target]`
  * Moderation: `!kick <nickname> [reason]`, `!ban <nickname> [reason]`, `!mute <nickname>`, `!unmute <nickname>`, `!slay [target]`, `!freeze [target]`, `!unfreeze [target]`, `!disarm [target]`, `!dismount [target]`
  * Server: `!warmup end|extend`, `!endround`, `!map <map>`, `!say <message>`, `!broadcast <message>`, `!speed [target] <multiplier>`, `!info [nickname]`, `!specs`, `!help`


### Mobile Siege Engines on Carts (Onagers and Ballistae, DWDN_FieldArtilleryBehavior)
* **Fundamental TaleWorlds engine bug when dynamically spawning deeply nested prefabs in multiplayer**:
  * In the native TaleWorlds method `Mission.CreateMissionObjectFromPrefab`, and on the client in `MissionNetworkComponent.HandleServerEventCreateMissionObject`, the loop over child entities is hard-limited to direct first-level children (`foreach (GameEntity child in entity.GetChildren())`).
  * For complex prefabs such as `mangonel_a` and `ballista_a`, all key standing points (`StandingPoint`: aiming `use_reload_fire_l`, rotation `use_rotate_r/l`, loading `use_load`, taking ammunition `ammo_pos_a..h`) are located at the 3rd and 4th levels of the hierarchy (inside `clean` and `projectile_pile`).
  * As a result the client **is not assigned network `MissionObjectId`s for the working standing points** (`Id` stays 0 or Invalid). When `[F]` is pressed the client sends `RequestUseObject(0)`, the server cannot find the object in `Mission.Current.MissionObjects` and drops the packet. The player cannot interact with the artillery piece.
  * **The DWDN architectural solution**:
	1. Recursively collect every `MissionObject` in the prefab's entity tree (`CollectMissionObjectsRecursive`).
	2. Assign each nested `MissionObject` a server runtime ID (`mission.GetFreeRuntimeMissionObjectId()`) and register it with `mission.AddActiveMissionObject(mo)`.
	3. Broadcast the complete ID map to clients through the mod's dedicated network packet `SyncArtilleryDescendantsMessage`.
	4. On the client, exactly the same recursive traversal is replayed and identical `MissionObjectId`s are assigned, after which they are registered with `Mission.Current.AddActiveMissionObject`. Now `RequestUseObject` from the client finds a 100% exact match on the server.

* **Highly dangerous double OnInit() trap (`An item with the same key has already been added`)**:
  * In the TaleWorlds engine the method `GameEntity.Instantiate(scene, prefab, frame, bool callScriptCallbacks)` takes the callback-launch flag as its 4th argument.
  * If you pass `true`, the engine immediately calls `OnInit()` on every script of the siege engine, including `RangedSiegeWeapon.OnInit()`, which fills the internal dictionary `PilotReservePriorityValues.Add(standingPoint, distance)`.
  * If you then call `root.CallScriptCallbacks(true)` manually, `OnInit()` runs a **second time** on the same instance. Attempting to add the same key to `PilotReservePriorityValues` again throws a fatal `ArgumentException: An item with the same key has already been added`, which brings the game client down during the spawn.
  * **Hard rule**: when instantiating manually, ALWAYS pass `callScriptCallbacks: false`, assign the synchronized network IDs first, and only then call `root.CallScriptCallbacks(true)` exactly once. The network packet handler must also be wrapped in `try / catch` to prevent a client crash.

* **Neutralizing the team side (`StandingPointSide = BattleSideEnum.None`)**:
  * The decompiled IL of `StandingPoint.IsDisabledForAgent` shows: if `StandingPointSide != BattleSideEnum.None`, the engine forbids using the artillery piece for any player whose team (`team.Side`) does not match `StandingPointSide`.
  * In the siege-engine prefabs `BattleSide` is written as `Attacker` by default. If a player spawns on the Defender side, the engine blocks the artillery piece.
  * **Solution**: through reflection the `StandingPointSide` field of every standing point is reset to `BattleSideEnum.None`, and the `IsDisabledForPlayers` flag is set to `false`. This gives any team a 100% ability to operate and reload the artillery piece.

* **Client-Server Carriage Movement (Pushing / Towing)**:
  * On the Dedicated Server `Mission.MainAgent` is `null` and there is no keyboard. Key polling and push-distance checking must happen **on the client**.
  * When the client is behind the carriage it shows the hint `[F] — Push the artillery piece`, and on key press it sends `RequestPushArtilleryMessage(engineId, isPushing)`.
  * The server validates the distance, assigns `PushingAgent` and hides the ammunition pile (`projectile_pile.SetVisibilityExcludeParents(false)`).
  * In the update loop the server reads the agent's movement direction (`pusher.GetMovementDirection()`), snaps the artillery piece to the terrain (`GetHeightAtPoint`) and broadcasts the coordinates to all clients through `SynchedMissionObject.SetGlobalFrameSynched(ref frame, false)`.
  * On stopping, the server deploys the projectile pile on the ground next to the artillery piece (`SetVisibilityExcludeParents(true)`) and broadcasts `SetArtilleryPushStateMessage` to clients.

* **Artillerist Equipment and Protection Against the CreateAgentVisuals Crash (0xc0000005)**:
  * **Fatal error**: trying to give out a static prop mesh (for example `lib_scroll_d` from `building_details.xml`) as equippable weapon with a holster (`item_holsters="mace_right_hip"`). Inside `CreateAgentVisuals` the C++ TaleWorlds engine tries to bind the weapon's skeleton bones to the soldier's body; a static mesh has no bones -> null pointer -> immediate game exit with `0xc0000005: Access Violation`.
  * **Ideal architectural solution**: the request for an artillery piece (`Mobile Onager` / `Mobile Ballista`) is expressed **solely as a perk in the division's 2nd perk list** (`mpclassdivisions.xml`), without passing any prop items into the inventory (`OnSpawnEffect` is not required).
  * In the character files (`mpcharacters.xml`) the `Item1` slot was removed from the artillerist. In his hands and in his slots the artillerist carries **exactly one chosen melee weapon** in slot `Item0` (falchion, cleaver, maul or sword).
  * In the `CheckAndSpawnArtilleryForAgent` code: the server queries the player's selected perk (`agent.MissionPeer.SelectedPerks` or `GetSelectedPerkIndexWithPerkListIndex(selectedTroopIndex, 1)`), determines the artillery type (`ballista` or `mangonel`) and spawns it at the base, automatically bringing the melee weapon in slot 0 into a ready state (`agent.TryToWieldWeaponInSlot(WeaponItemBeginSlot, Instant, false)`). The soldier holds no extra items and the crashes are eliminated entirely.

* **Built-in Ammunition Pile (projectile_pile) and Stowing While Pushing**:
  * In the native prefabs `mangonel_a` and `ballista_a` the ammunition pile `projectile_pile` is **already built into the hierarchy** as a child entity. Spawning a foreign `stone_pile_a_usable` is strictly forbidden — it breaks ID synchronization.
  * The `CollectMissionObjectsRecursive` method must traverse all components of an entity through `entity.GetScriptComponents<MissionObject>()`, because nodes such as `mangonel_a_body` contain several `MissionObject` instances at once (`Mangonel` + `DestructableComponent`).
  * For the artillery pieces, `siegeWeapon.SetAmmo(25)` and `siegeWeapon.SetStartAmmo(25)` are called on both server and client.
  * While the artillery piece is being pushed, the ammunition pile is hidden and blocked (`SetAmmoPileActive(false)`); when the cart stops, it is deployed at the new position next to the carriage (`SetAmmoPileActive(true)`).
  * The interaction zone behind the carriage is computed with `currentFrame.TransformToLocal(player.Position)` (`localPos.y` from 1.0 m to 5.0 m, width up to 2.5 m).
  * While moving, the pushing agent is continuously given the native siege-machine pushing animation on both server and client: `act_usage_siege_machine_push` (`pusher.SetActionChannel(0, ActSiegePush)`).

* **Anti-Dupe and Admin Control**:
  * 1 live artillery piece per engineer (checked by the network `VirtualPlayer.Id`).
  * Team limit `MaxCannonsPerTeam` (2 by default, changed on the fly with `!artylimit <N>`).
  * Cooldown after destruction: 25 seconds before the slot is released.
  * Admin chat commands: `!artylimit <N>`, `!cannons`, `!clearcannons`, `!spawncannon [mangonel|ballista]`.

---

### Custom Admin Tools Architecture (DWDN_AdminMissionView, DWDN_AdminPanelVM, DWDN_AdminService)
* **MissionView lifecycle trap when registering through AddMissionBehavior**:
  * In TaleWorlds `MissionView` derives from `MissionBehavior`. However, if the class is registered via `mission.AddMissionBehavior(new DWDN_AdminMissionView())`, TaleWorlds **does not call** `OnMissionScreenInitialize()` and `OnMissionScreenTick(dt)`! The engine calls these methods only when the view is added through `MissionScreen.AddMissionView(view)` or via `[ViewCreatorModule]` factories.
  * The base-class `MissionScreen` property is `null` in that case.
  * **Solution**:
	1. Put the hotkey checks (`[F10]`, `[Insert]`) and the tick logic updates into `OnMissionTick(float dt)`.
	2. Obtain the mission screen through a fallback: `MissionScreen screen = MissionScreen ?? (ScreenManager.TopScreen as MissionScreen)`.
	3. When the interface is invoked, create `GauntletLayer("DWDN_AdminPanel", ViewOrderPriority)` and load the prefab with `_gauntletLayer.LoadMovie("DWDN_AdminPanel", _dataSource)`.
	4. If `screen == null` or the Gauntlet prefab failed to load, fall back to the native TaleWorlds interface `MBInformationManager.ShowMultiSelectionInquiry`. This guarantees 100% availability of the admin panel even in non-standard scenes.
* **Binding lists in Gauntlet XML (DataSource)**:
  * To display dynamic lists (for example the player list `PlayerRows`), the `<ListPanel Id="PlayerList">` widget with a nested `<ItemTemplate>` **must** have the `DataSource="{PlayerRows}"` attribute. Without this attribute the engine ignores `ItemTemplate` and the list stays empty.
* **Universal Administrator Authentication**:
  * The custom admin tool must be granted to the player regardless of how they authenticated:
	1. **Through the native console**: `adminpassword <server_password>`. In `DWDN_AdminService.TickMission` the server checks `peer.IsAdmin && !_authenticatedPeers.Contains(peer)`, automatically promotes the player into `_authenticatedPeers` and sends the client `SyncAdminStateMessage(true)`.
	2. **Through chat**: `!login <server_password>`.
	3. **Through the GUI password-entry dialog**: when `[F10]` or `[Insert]` is pressed by an unauthenticated player, the client opens the native dialog `InformationManager.ShowTextInquiry` ("Enter the administrator password:"). Input validator: `(text) => new Tuple<bool, string>(!string.IsNullOrWhiteSpace(text), "")`. On confirmation, `RequestAdminActionMessage("login", "", password)` is sent.
	4. The server handles `Action == "login"` in `HandleClientEventRequestAdminAction` **before the admin-rights check**, verifies the password strictly against `_adminPassword` loaded from `MultiplayerOptions.OptionType.AdminPassword` or `DWDN_config.txt`, calls `SetPeerAdmin(peer, true)` and sends `SyncAdminStateMessage(true)`.
	5. On receiving `SyncAdminStateMessage(true)` the client automatically opens the graphical admin panel.

---

### Horse-Drawn Field Artillery (DWDN_FieldArtilleryBehavior)
* **3D model specifics: Cart vs Own Wheels**:
  * **Onager (`mangonel_a`)**: the prefab already has massive wooden wheels on its own model. Therefore `UsesCart = false` — it stands and is towed **directly on the ground** (`position.z = groundZ`), without creating a `bd_cart_a` platform underneath it.
  * **Ballista (`ballista_a`)**: the prefab has no wheels (a stationary tripod). Therefore `UsesCart = true` — a vanilla cart `bd_cart_a` is spawned underneath it, and the artillery piece itself is raised into the cart body by `+0.40f` on the Z axis.
* **Spawning the Draft Horse**:
  * TaleWorlds signature: `mission.SpawnMonster(new ItemRosterElement(horseItem, 1), new ItemRosterElement(harnessItem, 1), horsePos, horseHeading, (int)teamSide)`. (Position and heading are passed by value, without `ref`).
  * A hard speed limit is applied to the horse: `horseAgent.SetMaximumSpeedLimit(2.2f, false)` and `horseAgent.UpdateAgentProperties()`. The horse moves **strictly at a walk**; trotting and galloping are blocked.
* **Towing While Hitched in `OnMissionTick`**:
  * The hitching distance of the artillery piece behind the horse is `3.6f`. The ground height under the artillery piece is continuously corrected with `mission.Scene.GetHeightAtPoint(targetPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref gZ)`.
  * As the horse moves, the coordinates of the artillery piece and the cart are updated smoothly. In the travel configuration the ammunition (`projectile_pile`) is hidden and the aiming and firing standing points are blocked (`sp.SetDisabled(true)`).
* **Firing Position via `[F] Unhitch the horse`**:
  * When the player comes within `InteractRadius` (3.5 m), the hint is shown: `[F] Unhitch the horse (Move into firing position)`.
  * On `[F]` the client sends `RequestDeployArtilleryMessage(engineId)`.
  * The server puts the artillery piece into the firing state: the horse is released (`SetMaximumSpeedLimit(-1f, false)`), the projectiles (`projectile_pile`) are deployed on the ground (`SetVisibilityExcludeParents(true)`), and the aiming and firing station standing points are activated (`sp.SetDisabled(false)`). The artillery piece is ready for full siege combat.
* **Call on Spawn**:
  * You must override `OnAgentBuild(Agent agent, Banner banner)` in `DWDN_FieldArtilleryBehavior` and call `CheckAndSpawnArtilleryForAgent(agent)`. The spawn happens on the server, followed by broadcasting `SyncArtilleryDescendantsMessage` to clients.

---

*May logic prevail in the sacred silicon. Glory to the Omnissiah!*

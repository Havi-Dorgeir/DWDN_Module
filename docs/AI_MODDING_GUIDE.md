# Р РЈРљРћР’РћР”РЎРўР’Рћ РђР РҐРРўР•РљРўРћР Рђ DWDN: РЎР’РЇР©Р•РќРќР«Р™ РљРћР”Р•РљРЎ РњРћР”Р”РРќР“Рђ BANNERLORD (Р”Р›РЇ РР)

> **РљРћРќРўР•РљРЎРў Р”Р›РЇ РРЎРљРЈРЎРЎРўР’Р•РќРќРћР“Рћ РРќРўР•Р›Р›Р•РљРўРђ:**  
> Р­С‚РѕС‚ РґРѕРєСѓРјРµРЅС‚ СЃРѕСЃС‚Р°РІР»РµРЅ РїСЂРµРґС‹РґСѓС‰РёРјРё РїРѕРєРѕР»РµРЅРёСЏРјРё РР РґР»СЏ РїРѕСЃР»РµРґСѓСЋС‰РёС… РР-Р°РіРµРЅС‚РѕРІ.  
> РџСЂРѕС‡РёС‚Р°РІ СЌС‚РѕС‚ РєРѕРґРµРєСЃ, С‚С‹ РїРѕР»СѓС‡Р°РµС€СЊ РїРѕР»РЅРѕРµ РїРѕРЅРёРјР°РЅРёРµ СѓСЃС‚СЂРѕР№СЃС‚РІР° РјРѕРґР° **DWDN (Daewden)**, Р°СЂС…РёС‚РµРєС‚СѓСЂС‹ РјСѓР»СЊС‚РёРїР»РµРµСЂР° Mount & Blade II: Bannerlord, РјРµС…Р°РЅРёР·РјРѕРІ Р·Р°С‰РёС‚С‹ РѕС‚ РїР°РґРµРЅРёР№ (РєСЂР°С€РµР№) РґРІРёР¶РєР° Рё РіРѕС‚РѕРІС‹Р№ РёРЅСЃС‚СЂСѓРјРµРЅС‚Р°СЂРёР№ РґР»СЏ РЅРµРјРµРґР»РµРЅРЅРѕР№ РїСЂРѕРґСѓРєС‚РёРІРЅРѕР№ СЂР°Р±РѕС‚С‹ Р±РµР· Р»РёС€РЅРёС… РІРѕРїСЂРѕСЃРѕРІ Рє РїРѕР»СЊР·РѕРІР°С‚РµР»СЋ.

---

## 1. РЎР’РЇР©Р•РќРќРђРЇ РўРћРџРћР“Р РђР¤РРЇ (Р РђРЎРџРћР›РћР–Р•РќРР• РџРђРџРћРљ Р РљРћРќРўРЈР Р« РЎРРќРҐР РћРќРР—РђР¦РР)

Р’СЃСЏ СЂР°Р·СЂР°Р±РѕС‚РєР° РјРѕРґР° СЂР°СЃРїСЂРµРґРµР»РµРЅР° РјРµР¶РґСѓ **С‡РµС‚С‹СЂСЊРјСЏ РІР·Р°РёРјРѕСЃРІСЏР·Р°РЅРЅС‹РјРё РґРёСЂРµРєС‚РѕСЂРёСЏРјРё**:

```mermaid
flowchart TD
    subgraph BanAI ["d:\banAImod\ (РљРѕРіРёС‚Р°С‚РѕСЂРЅС‹Р№ СѓР·РµР»)"]
        AI_DOCS["Р”РѕРєСѓРјРµРЅС‚Р°С†РёСЏ: AI_MODDING_GUIDE.md, notes.md"]
        SYNC_SCRIPT["РЎРєСЂРёРїС‚ СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёРё: scratch\sync_all.ps1"]
        BAN_TRY["Р—РµСЂРєР°Р»Рѕ СЃРєСЂРёРїС‚РѕРІ Рё XML: d:\banAImod\DWDN_TRY"]
    end

    subgraph SteamClient ["E:\SteamLibrary\...\Mount & Blade II Bannerlord\Modules\"]
        DWDN_TRY["DWDN_TRY (Р Р°Р±РѕС‡Р°СЏ РєР»РёРµРЅС‚СЃРєР°СЏ РїР°РїРєР°)"]
        DWDN_TRYM["DWDN_TRYM (РћС„РёС†РёР°Р»СЊРЅС‹Р№ Git-СЂРµРїРѕР·РёС‚РѕСЂРёР№)"]
    end

    subgraph DedicatedServer ["E:\SteamLibrary\...\Dedicated Server\"]
        DAEWDEN_M["Modules\DaewdenM (РЎРµСЂРІРµСЂРЅС‹Р№ РјРѕРґСѓР»СЊ)"]
        SERVER_BIN["bin\Win64_Shipping_Server (РЎРµСЂРІРµСЂРЅС‹Рµ Р±РёРЅР°СЂРЅРёРєРё)"]
    end

    DWDN_TRY -- "dotnet build Release/Server" --> DWDN_TRY
    DWDN_TRY -- "sync_all.ps1" --> DAEWDEN_M
    DWDN_TRY -- "sync_all.ps1" --> BAN_TRY
    DWDN_TRY -- "sync_all.ps1" --> SERVER_BIN
    DWDN_TRY -. "Р СѓС‡РЅРѕР№ Git-РєРѕРјРјРёС‚" .-> DWDN_TRYM
```

### Р¤РёР·РёС‡РµСЃРєРёРµ РїСѓС‚Рё:
1. **РљР»РёРµРЅС‚СЃРєР°СЏ СЂР°Р±РѕС‡Р°СЏ РєРѕРїРёСЏ (Client Dev & Play):**  
   `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY`  
   *Р—РґРµСЃСЊ РІРµРґРµС‚СЃСЏ РєРѕРјРїРёР»СЏС†РёСЏ Рё Р»РѕРєР°Р»СЊРЅРѕРµ С‚РµСЃС‚РёСЂРѕРІР°РЅРёРµ.*
2. **Git-СЂРµРїРѕР·РёС‚РѕСЂРёР№ РјРѕРґР° (Source Control):**  
   `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRYM`  
   *РЈРґР°Р»РµРЅРЅС‹Р№ СЂРµРїРѕР·РёС‚РѕСЂРёР№: `https://github.com/Havi-Dorgeir/DWDN_Module`.*  
   *РСЃРєР»СЋС‡РµРЅРёСЏ `.gitignore`: РјРѕРЅРѕР»РёС‚ `AssetPackages/pack0.tpac` (1.1 Р“Р‘) Рё СЂР°Р±РѕС‡РёР№ РєСЌС€ `RuntimeDataCache/`.*
3. **Р’С‹РґРµР»РµРЅРЅС‹Р№ СЃРµСЂРІРµСЂ (Dedicated Server):**  
   `E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\Modules\DaewdenM`  
   *Р Р±РёРЅР°СЂРЅР°СЏ РїР°РїРєР°: `E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\bin\Win64_Shipping_Server`.*
4. **РљРѕРіРёС‚Р°С‚РѕСЂРЅС‹Р№ Р°СЂС…РёРІ Р°РіРµРЅС‚Р° (AI Hub):**  
   `d:\banAImod\`  
   *Р—РґРµСЃСЊ Р»РµР¶Р°С‚ РєРѕРЅСЃРїРµРєС‚С‹ (`notes.md`), СЃРєСЂРёРїС‚С‹ РёСЃСЃР»РµРґРѕРІР°РЅРёСЏ Рё РІСЃРїРѕРјРѕРіР°С‚РµР»СЊРЅС‹Рµ СѓС‚РёР»РёС‚С‹.*

---

## 2. РРќРЎРўР РЈРњР•РќРўРђР РР™ Р РњР•РўРћР”РћР›РћР“РРЇ Р Р•Р’Р•Р РЎ-РРќР–РРќРР РРќР“Рђ

### 2.1. Р РµРїРѕР·РёС‚РѕСЂРёРё Рё РёРЅСЃС‚СЂСѓРјРµРЅС‚С‹ GitHub
* **Р“Р»Р°РІРЅС‹Р№ СЂРµРїРѕР·РёС‚РѕСЂРёР№ РґР»СЏ РјРѕРґРёРЅРіР° Bannerlord:**  
  [https://github.com/rehan-remade/universal-modder](https://github.com/rehan-remade/universal-modder)  
  *РСЃРїРѕР»СЊР·СѓР№ РµРіРѕ РґР»СЏ С€Р°Р±Р»РѕРЅРѕРІ SubModule, CLI-СЃР±РѕСЂРєРё, РїР°СЂСЃРёРЅРіР° Р°СЃСЃРµС‚РѕРІ Рё РїРѕРЅРёРјР°РЅРёСЏ РЅРµРѕС‡РµРІРёРґРЅС‹С… СЃС‚СЂСѓРєС‚СѓСЂ РґРІРёР¶РєР°.*
* **Р­С‚Р°Р»РѕРЅРЅС‹Рµ РјРѕРґС‹ РёР· РјР°СЃС‚РµСЂСЃРєРѕР№ Steam (РёР·СѓС‡Р°Р№ РёС… Р°СЂС…РёС‚РµРєС‚СѓСЂСѓ РїРµСЂРµРґ РёР·РѕР±СЂРµС‚РµРЅРёРµРј РІРµР»РѕСЃРёРїРµРґР°):**
  * `E:\SteamLibrary\steamapps\workshop\content\261550\3774022783` (**LOTR / OMEE**) вЂ” **РіР»Р°РІРЅС‹Р№ РѕР±СЂР°Р·РµС†**: С‡РёСЃС‚РµР№С€Р°СЏ Gauntlet UI Р°СЂС…РёС‚РµРєС‚СѓСЂР°, РїРѕР»РЅР°СЏ РёР·РѕР»СЏС†РёСЏ 3D-РїСЂРµРІСЊСЋ С‡РµСЂРµР· `AgentVisuals.Create`, СЃРµС‚РµРІС‹Рµ РїР°РєРµС‚С‹ РєР°СЃС‚РѕРјРЅРѕРіРѕ СЃРїР°РІРЅР°.
  * `E:\SteamLibrary\steamapps\workshop\content\261550\3772431545` (**Warlords**) вЂ” СЌС‚Р°Р»РѕРЅ РєР°СЃС‚РѕРјРЅС‹С… Р±РѕРµРІС‹С… РєР»Р°СЃСЃРѕРІ Рё РїРµСЂРєРѕРІ.
  * `E:\SteamLibrary\steamapps\workshop\content\261550\3716004111` вЂ” РїСЂРёРјРµСЂ РєРѕРЅС„РёРіСѓСЂР°С†РёРё Р±Р°Р»Р°РЅСЃР° СЃРµС‚РµРІС‹С… РїСЂРµРґРјРµС‚РѕРІ.

### 2.2. Р”РµРєРѕРјРїРёР»СЏС†РёСЏ Рё РёСЃСЃР»РµРґРѕРІР°РЅРёРµ Р±РёР±Р»РёРѕС‚РµРє TaleWorlds
Р’СЃРµ СЃР±РѕСЂРєРё СЏРґСЂР° Р»РµР¶Р°С‚ РІ РїР°РїРєРµ РёРіСЂС‹:  
`E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`  
РљР»СЋС‡РµРІС‹Рµ СЃР±РѕСЂРєРё РґР»СЏ Р°РЅР°Р»РёР·Р°:
* `TaleWorlds.MountAndBlade.dll` вЂ” РјРёСЃСЃРёРё, СЃРїР°РІРЅ Р°РіРµРЅС‚РѕРІ, СЃРµС‚РµРІС‹Рµ СЃРѕРѕР±С‰РµРЅРёСЏ (`GameNetwork`), `MissionPeer`, `MissionMultiplayerGameModeBase`.
* `TaleWorlds.Core.dll` вЂ” `ItemObject`, `BasicCharacterObject`, `Equipment`, `EquipmentIndex`, `WeaponDesign`.
* `TaleWorlds.Engine.dll` вЂ” `AgentVisuals`, `Camera`, `Scene`, `SoundEvent`, РјР°С‚СЂРёС†С‹ `MatrixFrame`.
* `TaleWorlds.MountAndBlade.Multiplayer.dll` вЂ” РЅР°С‚РёРІРЅС‹Рµ РєРѕРјРїРѕРЅРµРЅС‚С‹ СЃРµС‚РµРІРѕРіРѕ Р»РѕР±Р±Рё Рё СЃРїР°РІРЅР°.

> [!TIP]
> **Headless-РёСЃСЃР»РµРґРѕРІР°РЅРёРµ Р±РµР· GUI:**  
> Р§С‚РѕР±С‹ Р±С‹СЃС‚СЂРѕ СѓР·РЅР°С‚СЊ СЃРёРіРЅР°С‚СѓСЂСѓ Р»СЋР±РѕРіРѕ РїСЂРёРІР°С‚РЅРѕРіРѕ РјРµС‚РѕРґР° РёР»Рё СЃРІРѕР№СЃС‚РІР° TaleWorlds, СЃРѕР·РґР°РІР°Р№ РєРѕСЂРѕС‚РєРёР№ СЃРєСЂРёРїС‚ РЅР° C# РёР»Рё PowerShell СЃ `[System.Reflection.Assembly]::LoadFrom(...)` РїСЂСЏРјРѕ РІ `d:\banAImod\scratch\`. Р­С‚Рѕ СЌРєРѕРЅРѕРјРёС‚ РґРµСЃСЏС‚РєРё РјРёРЅСѓС‚ РїРѕ СЃСЂР°РІРЅРµРЅРёСЋ СЃ РѕС‚РєСЂС‹С‚РёРµРј С‚СЏР¶РµР»С‹С… РґРµРєРѕРјРїРёР»СЏС‚РѕСЂРѕРІ.

### 2.3. РЎРІСЏС‰РµРЅРЅС‹Р№ С†РёРєР» СЃР±РѕСЂРєРё Рё СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёРё
**РљР РРўРР§Р•РЎРљРћР• РџР РђР’РР›Рћ:**  
`DWDN_Script.csproj` РЅР°СЃС‚СЂРѕРµРЅ РЅР° РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅС‹Рµ РїСѓС‚Рё `HintPath` РІРёРґР° `..\..\..\..\..\bin\Win64_Shipping_Client\`.  
РџРѕСЌС‚РѕРјСѓ РєРѕРјРїРёР»РёСЂРѕРІР°С‚СЊ РїСЂРѕРµРєС‚ РЅСѓР¶РЅРѕ **РўРћР›Р¬РљРћ** РІ СЂР°Р±РѕС‡РµР№ РїР°РїРєРµ Steam:
`E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\DWDN_Script.csproj`

**РљРѕРјР°РЅРґС‹ СЃР±РѕСЂРєРё С‡РµСЂРµР· .NET CLI:**
```powershell
# 1. РЎР±РѕСЂРєР° РєР»РёРµРЅС‚СЃРєРѕР№ DLL
dotnet build "E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\DWDN_Script.csproj" -c Release

# 2. РЎР±РѕСЂРєР° СЃРµСЂРІРµСЂРЅРѕР№ DLL
dotnet build "E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\DWDN_Script.csproj" -c Server

# 3. РЎРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ РІСЃРµС… С„Р°Р№Р»РѕРІ РІРѕ РІСЃРµ СѓР·Р»С‹ (РљР»РёРµРЅС‚, РЎРµСЂРІРµСЂ, Р›РѕРєР°Р»СЊРЅС‹Р№ Р°СЂС…РёРІ)
powershell.exe -ExecutionPolicy Bypass -File "d:\banAImod\scratch\sync_all.ps1"
```

---

## 3. РџРћР”РЎРРЎРўР•РњРђ РћР“РќР•РЎРўР Р•Р›Р¬РќРћР“Рћ РћР РЈР–РРЇ (FIREARMS SUBSYSTEM)

РћРіРЅРµСЃС‚СЂРµР»СЊРЅРѕРµ РѕСЂСѓР¶РёРµ РІ DWDN СЂРµР°Р»РёР·РѕРІР°РЅРѕ С‡РµСЂРµР· СЃРІСЏР·РєСѓ XML-РјРѕРґРµР»РµР№ СЃРЅР°СЂСЏРґРѕРІ Рё РєР»РёРµРЅС‚-СЃРµСЂРІРµСЂРЅРѕРіРѕ `MissionBehavior`.

```mermaid
sequenceDiagram
    participant Player as РРіСЂРѕРє (РљР»РёРµРЅС‚)
    participant Engine as TaleWorlds Engine
    participant Firearms as DWDN_FirearmsBehavior
    participant Server as Р’С‹РґРµР»РµРЅРЅС‹Р№ СЃРµСЂРІРµСЂ

    Player->>Engine: РќР°Р¶Р°С‚РёРµ Р›РљРњ (Р’С‹СЃС‚СЂРµР» РёР· hand_cannon)
    Engine->>Firearms: OnAgentShootMissile(shooterAgent, weaponIndex, pos, vel, ...)
    Firearms->>Firearms: IsFirearm(weapon.Item)? -> Р”Рђ!
    Firearms->>Engine: Mission.MakeSound (crack & pop)
    Firearms->>Engine: AddParticleSystemBurstByName (РїРѕСЂРѕС…РѕРІРѕР№ РґС‹Рј Рё РёСЃРєСЂС‹)
    Engine-->>Server: РќР°С‚РёРІРЅС‹Р№ СЃРµС‚РµРІРѕР№ РїР°РєРµС‚ РІС‹СЃС‚СЂРµР»Р° (Native Crossbow/Bolt)
    Server-->>Engine: Р Р°СЃС‡РµС‚ Р±Р°Р»Р»РёСЃС‚РёРєРё Рё РїРѕРїР°РґР°РЅРёСЏ (165 Piercing Damage)
```

### 3.1. XML-РєРѕРЅС„РёРіСѓСЂР°С†РёСЏ (`ModuleData/mpitems/dwdn_weapon.xml`)
Р’РјРµСЃС‚Рѕ РїРѕРїС‹С‚РѕРє РІРЅРµРґСЂРёС‚СЊ СЃРѕРІРµСЂС€РµРЅРЅРѕ РЅРѕРІС‹Р№ РЅРµСЃСѓС‰РµСЃС‚РІСѓСЋС‰РёР№ С‚РёРї РѕСЂСѓР¶РёСЏ, РєРѕС‚РѕСЂС‹Р№ СЃР»РѕРјР°Р» Р±С‹ C++ РёРЅС‚РµСЂС„РµР№СЃС‹ Р°РЅРёРјР°С†РёР№ TaleWorlds, РјС‹ РёСЃРїРѕР»СЊР·СѓРµРј РіРµРЅРёР°Р»СЊРЅС‹Р№ Рё 100% СЃС‚Р°Р±РёР»СЊРЅС‹Р№ **Crossbow-Hack**:

1. **РЎР°РјРѕ РѕСЂСѓРґРёРµ (`hand_cannon`):**
   ```xml
   <Item multiplayer_item="true" id="hand_cannon" name="{=*}Hand Bombard" 
         body_name="bo_composite_crossbows" mesh="crossbow_e" culture="Culture.vlandia" 
         weight="3.8" appearance="1" difficulty="0" Type="Crossbow" 
         AmmoOffset="0.0, 0.02131, 0.22975" 
         item_holsters="crossbow_back:bow_hip:mace_right_hip:bow_hip_2" 
         holster_position_shift="0.02,0,-0.4">
       <ItemComponent>
           <Weapon weapon_class="Crossbow" ammo_class="Bolt" 
                   missile_speed="135" accuracy="18" thrust_damage="165" 
                   thrust_speed="22" speed_rating="15" weapon_length="100" 
                   ammo_limit="1" thrust_damage_type="Pierce" 
                   item_usage="crossbow" reload_phase_count="2" 
                   physics_material="wood_weapon" center_of_mass="0,0,0.4">
               <WeaponFlags RangedWeapon="true" BonusAgainstShield="true" 
                            CanPenetrateShield="true" AmmoSticksWhenShot="false" 
                            AttachAmmoToVisual="false" CantReloadOnHorseback="true" 
                            NotUsableWithOneHand="true" TwoHandIdleOnMount="true" />
           </Weapon>
       </ItemComponent>
       <Flags />
   </Item>
   ```
2. **РџСѓР»Рё (`mp_bullets`):**
   ```xml
   <Item multiplayer_item="true" id="mp_bullets" name="{=*}Lead Shot" 
         body_name="bo_capsule_arrow" holster_body_name="bo_axe_short" 
         mesh="bolt_bl_c" value="1" is_merchandise="false" 
         holster_mesh="bolt_bl_c_quiver" holster_mesh_with_weapon="bolt_bl_c_quiver" 
         flying_mesh="bolt_bl_flying" weight="0.10" appearance="1" Type="Bolts" 
         item_holsters="quiver_bolts:quiver_bolts_2:quiver_back_middle:quiver_back_top">
       <ItemComponent>
           <Weapon weapon_class="Bolt" stack_amount="12" thrust_damage="5" 
                   thrust_speed="0" speed_rating="0" physics_material="missile" 
                   weapon_length="35" 
                   flying_sound_code="event:/mission/combat/missile/flyby/arrow">
               <WeaponFlags Consumable="true" AmmoSticksWhenShot="false" />
           </Weapon>
       </ItemComponent>
   </Item>
   ```

### 3.2. C#-Р»РѕРіРёРєР° РІС‹СЃС‚СЂРµР»Р° (`DWDN_FirearmsBehavior.cs`)
РљР»Р°СЃСЃ РЅР°СЃР»РµРґСѓРµС‚СЃСЏ РѕС‚ `MissionBehavior` Рё СЂРµРіРёСЃС‚СЂРёСЂСѓРµС‚СЃСЏ РІ РјРёСЃСЃРёРё.
* **РРґРµРЅС‚РёС„РёРєР°С†РёСЏ РѕСЂСѓР¶РёСЏ**: `IsFirearm(ItemObject)` СЃРІРµСЂСЏРµС‚ `StringId` СЃ РєР»СЋС‡РµРІС‹РјРё СЃР»РѕРІР°РјРё (`hand_cannon`, `cannon`, `bombard`, `handgonne`, `firearm`).
* **РўРѕС‡РєР° СЃСЂРµР·Р° СЃС‚РІРѕР»Р° (Muzzle Position)**:
  ```csharp
  Vec3 forward = velocity.NormalizedCopy();
  if (forward.LengthSquared < 0.001f) forward = orientation.f;
  Vec3 muzzlePos = position + forward * 0.70f;
  MatrixFrame muzzleFrame = new MatrixFrame(orientation, muzzlePos);
  ```
* **РђРєСѓСЃС‚РёС‡РµСЃРєРёР№ СЌС„С„РµРєС‚ (В«Р›РµРіРєРёР№ Р±Р°С…В»)**:  
  РЎРґРІРѕРµРЅРЅС‹Р№ РЅР°С‚РёРІРЅС‹Р№ Р·РІСѓРє:
  * `event:/mission/combat/impact/ballista_missile/stone` (СЃСѓС…РѕР№ РїРѕСЂРѕС…РѕРІРѕР№ С‰РµР»С‡РѕРє).
  * `event:/mission/combat/impact/missile/pot_breakable` (РіР»СѓС…РѕР№ СЂРµР·РѕРЅРёСЂСѓСЋС‰РёР№ С…Р»РѕРїРѕРє).
* **Р’РёР·СѓР°Р»СЊРЅС‹Рµ РїР°СЂС‚РёРєР»С‹ (VFX)**:
  * РњРіРЅРѕРІРµРЅРЅС‹Р№ РІСЃРїР»РµСЃРє РїР»Р°РјРµРЅРё: `prt_fireball` РЅР° `muzzlePos + forward * 0.10f` (0.20 СЃРµРє, Р±РµР· РѕСЃС‚Р°С‚РѕС‡РЅРѕРіРѕ РіРѕСЂРµРЅРёСЏ).
  * Р“СѓСЃС‚РѕР№ РїРѕСЂРѕС…РѕРІРѕР№ РґС‹Рј: `psys_game_stone_dust_a` Рё `psys_siege_door_falling_dust`.
  * Р”С‹РјРЅС‹Р№ С€Р»РµР№С„ РїРѕ С‚СЂР°РµРєС‚РѕСЂРёРё РІС‹СЃС‚СЂРµР»Р°: СЌРјРёС‚С‚РµСЂС‹ РЅР° `muzzlePos + forward * 0.60f` Рё `muzzlePos + forward * 1.20f`.
  * РЇСЂРєРёРµ РїРѕСЂРѕС…РѕРІС‹Рµ РёСЃРєСЂС‹: `psys_game_sparkle_a`.
  * Р”С‹РјРѕРє Р·Р°РїР°Р»СЊРЅРѕРіРѕ РѕС‚РІРµСЂСЃС‚РёСЏ: РЅР° `position - forward * 0.10f + Vec3(0, 0, 0.04f)`.

> [!IMPORTANT]
> **Р—РђРџР Р•Рў РќРђ `AddCustomMissile` РќРђ РЎР•Р Р’Р•Р Р•:**  
> РќРёРєРѕРіРґР° РЅРµ СЃРїР°РІРЅРёС‚Рµ СЃС‚РѕСЂРѕРЅРЅРёРµ СЂР°РєРµС‚С‹/РїСѓР»Рё С‡РµСЂРµР· `Mission.AddCustomMissile` РЅР° РІС‹РґРµР»РµРЅРЅРѕРј СЃРµСЂРІРµСЂРµ РґР»СЏ РѕРіРЅРµСЃС‚СЂРµР»Р°!  
> РќР°С‚РёРІРЅС‹Р№ РґРІРёР¶РѕРє TaleWorlds СЃР°Рј СЃРёРЅС…СЂРѕРЅРёР·РёСЂСѓРµС‚ СЃРЅР°СЂСЏРґ С‡РµСЂРµР· Р±Р°Р»Р»РёСЃС‚РёС‡РµСЃРєРёР№ РїР°Р№РїР»Р°Р№РЅ Р°СЂР±Р°Р»РµС‚Р°. Р•СЃР»Рё РїС‹С‚Р°С‚СЊСЃСЏ СЃРїР°РІРЅРёС‚СЊ РєР°СЃС‚РѕРјРЅС‹Рµ РїСѓР»Рё РІ РєРѕРґРµ, РІС‹РґРµР»РµРЅРЅС‹Р№ СЃРµСЂРІРµСЂ РіР°СЂР°РЅС‚РёСЂРѕРІР°РЅРЅРѕ СѓРїР°РґРµС‚ РІ РґРµСЃРёРЅС…СЂРѕРЅРёР·Р°С†РёСЋ С„РёР·РёС‡РµСЃРєРѕРіРѕ РјРёСЂР°.

---

## 4. РђР РҐРРўР•РљРўРЈР Рђ РљРђРЎРўРћРњРќРћР“Рћ GAUNTLET UI Р 3D-РџР Р•Р”РџР РћРЎРњРћРўР Рђ

Р­РєСЂР°РЅ РІС‹Р±РѕСЂР° СЃРЅР°СЂСЏР¶РµРЅРёСЏ Рё РєР»Р°СЃСЃР° РІ DWDN Р·Р°РјРµРЅСЏРµС‚ СЃС‚Р°РЅРґР°СЂС‚РЅС‹Р№ СЃРїР°РІРЅ-СЌРєСЂР°РЅ Bannerlord.

```mermaid
graph TD
    MissionView["DWDN_SelectionMissionView (ViewOrderPriority=20)"]
    GauntletLayer["GauntletLayer ('DWDN_ClassSelection')"]
    VM["DWDN_ClassSelectionVM (ViewModel)"]
    Preview["DWDN_CharacterPreview (РђРІС‚РѕРЅРѕРјРЅС‹Р№ 3D-СЂРµРЅРґРµСЂ)"]
    CustomCam["MissionScreen.CustomCamera"]

    MissionView --> GauntletLayer
    MissionView --> VM
    MissionView --> Preview
    Preview --> CustomCam
    VM -->|Data Binding| GauntletLayer
```

### 4.1. РЎС‚РµРє UI-РєРѕРјРїРѕРЅРµРЅС‚РѕРІ
* **РЎР»РѕР№ РѕС‚РѕР±СЂР°Р¶РµРЅРёСЏ (`DWDN_SelectionMissionView.cs`)**:
  * РЎРѕР·РґР°РµС‚ `GauntletLayer` СЃ `ViewOrderPriority = 20`.
  * РџСЂРёРІСЏР·С‹РІР°РµС‚ `DWDN_ClassSelectionVM` Рє Gauntlet-РјР°РєРµС‚Сѓ.
  * РџРµСЂРµС…РІР°С‚С‹РІР°РµС‚ РІРІРѕРґ РјС‹С€Рё РґР»СЏ РІСЂР°С‰РµРЅРёСЏ РїРµСЂСЃРѕРЅР°Р¶Р°.
* **РРµСЂР°СЂС…РёСЏ ViewModel**:
  * `DWDN_ClassSelectionVM` вЂ” РіР»Р°РІРЅРѕРµ РѕРєРЅРѕ, РїРµСЂРµРєР»СЋС‡РµРЅРёРµ РіРµСЂС†РѕРіСЃС‚РІ Рё РєР»Р°СЃСЃРѕРІ.
  * `DWDN_DuchyItemVM` вЂ” РєРЅРѕРїРєРё РІС‹Р±РѕСЂР° РіРµСЂС†РѕРіСЃС‚РІ.
  * `DWDN_ClassItemVM` вЂ” РєРЅРѕРїРєРё РІС‹Р±РѕСЂР° РєР»Р°СЃСЃР° РІРЅСѓС‚СЂРё РіРµСЂС†РѕРіСЃС‚РІР°.
  * `DWDN_EquipmentSlotVM` вЂ” СЃР»РѕС‚С‹ СЌРєРёРїРёСЂРѕРІРєРё (РЁР»РµРј, РўРѕСЂСЃ, РћСЂСѓР¶РёРµ 0..3 Рё С‚.Рґ.).
  * `DWDN_ItemOptionVM` вЂ” СЃРїРёСЃРѕРє РґРѕСЃС‚СѓРїРЅС‹С… РїСЂРµРґРјРµС‚РѕРІ РґР»СЏ РІС‹Р±СЂР°РЅРЅРѕРіРѕ СЃР»РѕС‚Р°.

### 4.2. РђСЂС…РёС‚РµРєС‚СѓСЂР° РёР·РѕР»РёСЂРѕРІР°РЅРЅРѕРіРѕ 3D-РїСЂРµРІСЊСЋ (LOTR / OMEE Pattern)
**РРЎРўРћР РРЇ РЎРљР РђРџРљРћР”Рђ Р РљР РђРЁРђ `0xC0000005`:**  
Р’ СЂР°РЅРЅРёС… РІРµСЂСЃРёСЏС… РјРѕРґР° РїСЂРё СЃРјРµРЅРµ РєР»Р°СЃСЃР° РІС‹Р·С‹РІР°Р»СЃСЏ `MultiplayerMissionAgentVisualSpawnComponent.OnMyAgentVisualSpawned` РёР»Рё РїСЂСЏРјРѕР№ `agentVisual.Refresh(...)` РЅР° РЅР°С‚РёРІРЅРѕРј РїСЊРµРґРµСЃС‚Р°Р»Рµ TaleWorlds.  
РўР°Рє РєР°Рє РЅР°С‚РёРІРЅС‹Р№ C++ РїРѕС‚РѕРє `Engine.dll` РІ СЌС‚РѕС‚ Р¶Рµ РјРёР»Р»РёСЃРµРєСѓРЅРґРЅС‹Р№ С‚РёРє РєСЂСѓС‚РёР» Р°РЅРёРјР°С†РёСЋ РѕР¶РёРґР°РЅРёСЏ РїСЊРµРґРµСЃС‚Р°Р»Р°, СЂР°Р·СЂСѓС€РµРЅРёРµ СЃРєРµР»РµС‚Р° РІС‹Р·С‹РІР°Р»Рѕ С„Р°С‚Р°Р»СЊРЅРѕРµ РЅР°СЂСѓС€РµРЅРёРµ РїСЂР°РІ РґРѕСЃС‚СѓРїР° Рє РїР°РјСЏС‚Рё (*Access Violation 0xC0000005*).

**РЎР’РЇР©Р•РќРќРћР• Р Р•РЁР•РќРР• (РљР»Р°СЃСЃ `DWDN_CharacterPreview.cs`):**
1. РџРѕР»РЅС‹Р№ РѕС‚РєР°Р· РѕС‚ С…СѓРєРѕРІ РЅР° РЅР°С‚РёРІРЅС‹Р№ `MultiplayerMissionAgentVisualSpawnComponent`.
2. РЎРѕР·РґР°РЅРёРµ РёР·РѕР»РёСЂРѕРІР°РЅРЅРѕРіРѕ РІРёР·СѓР°Р»СЊРЅРѕРіРѕ Р°РіРµРЅС‚Р°:
   ```csharp
   AgentVisualsData data = new AgentVisualsData()
       .ActionSet(actionSet)
       .Equipment(equipment)
       .BodyProperties(bodyProperties)
       .Frame(anchor)
       .ActionCode(idleAction)
       .Scene(Mission.Current.Scene)
       .PrepareForNativeMeshOptimization(true);

   _visual = AgentVisuals.Create(data, "dwdn_preview", false, false, false);
   ```
3. Р’С‹РґРµР»РµРЅРёРµ РЅРµР·Р°РІРёСЃРёРјРѕР№ РєР°РјРµСЂС‹:
   ```csharp
   _camera = Camera.CreateCamera();
   _camera.SetFovVertical(0.785f, Screen.AspectRatio, 0.1f, 1000f);
   MissionScreen.CustomCamera = _camera;
   ```
4. РџСЂРё Р·Р°РєСЂС‹С‚РёРё РјРµРЅСЋ `MissionScreen.CustomCamera = null`, Р° `_visual.Reset()` Р±РµР·РѕРїР°СЃРЅРѕ РѕСЃРІРѕР±РѕР¶РґР°РµС‚ C++ РїР°РјСЏС‚СЊ. **Р РµР·СѓР»СЊС‚Р°С‚: 0 РєСЂР°С€РµР№ РґРІРёР¶РєР°.**

---

## 5. Р“Р•Р Р¦РћР“РЎРўР’Рђ, РљР›РђРЎРЎР« Р РЎР•РўР•Р’РћР™ РЎРџРђР’Рќ (LOADOUT PIPELINE)

РљР°Р¶РґС‹Р№ РєР»Р°СЃСЃ РІ РјРѕРґРµ Р·Р°РґР°РµС‚СЃСЏ РІ С‚СЂРµС… РЅРµСЂР°Р·СЂС‹РІРЅС‹С… С‚РѕС‡РєР°С…:

```mermaid
flowchart LR
    A["mpcharacters.xml (РџРµСЂСЃРѕРЅР°Р¶)"] --> B["mpclassdivisions.xml (РљР»Р°СЃСЃ Рё РџРµСЂРєРё)"]
    B --> C["DWDN_DuchyManager.cs (РљР°С‚Р°Р»РѕРі СЃРЅР°СЂСЏР¶РµРЅРёСЏ)"]
    C --> D["RequestCustomLoadoutMessage (РЎРµС‚РµРІРѕР№ РїР°РєРµС‚)"]
    D --> E["DWDN_ServerLoadoutBehavior.cs (РЎРµСЂРІРµСЂРЅС‹Р№ СЃРїР°РІРЅ)"]
```

### 5.1. Р РµРіРёСЃС‚СЂР°С†РёСЏ РєР»Р°СЃСЃР°
1. **`ModuleData/mpcharacters.xml`**:
   Р‘Р°Р·РѕРІС‹Р№ РїРµСЂСЃРѕРЅР°Р¶, РјРѕРґРµР»СЊ С‚РµР»Р°, РїРѕР», СЃРєРµР»РµС‚, СЂР°СЃР°.
   ```xml
   <NPCCharacter id="dwdn_militia_infantry" name="{=*}Militia Infantry" 
                 default_group="Infantry" age="28" voice="curt" 
                 is_female="false" culture="Culture.vlandia" 
                 occupation="Soldier" is_hero="false">
   ```
2. **`ModuleData/mpclassdivisions.xml`**:
   РЎС‚РѕРёРјРѕСЃС‚СЊ РІ Р·РѕР»РѕС‚Рµ, С…Р°СЂР°РєС‚РµСЂРёСЃС‚РёРєРё (HP, Р±СЂРѕРЅСЏ, СЃРєРѕСЂРѕСЃС‚СЊ), РїРµСЂРєРё:
   ```xml
   <MPClassDivision id="dwdn_militia_infantry_division" 
                    hero="NPCCharacter.dwdn_militia_infantry" 
                    cost="120" culture="Culture.vlandia" 
                    class_group="Infantry" icon_type="Infantry">
   ```
3. **Р РµРµСЃС‚СЂ РіРµСЂС†РѕРіСЃС‚РІ РІ C# (`DWDN_DuchyManager.cs`)**:
   РћРїСЂРµРґРµР»СЏРµС‚ СЃРїРёСЃРєРё РґРѕРїСѓСЃС‚РёРјС‹С… РїСЂРµРґРјРµС‚РѕРІ РІ GUI:
   ```csharp
   DWDN_ClassDefinition militia = new DWDN_ClassDefinition(
       id: "dwdn_militia_infantry",
       name: "РћРїРѕР»С‡РµРЅРµС†",
       characterId: "dwdn_militia_infantry",
       divisionId: "dwdn_militia_infantry_division",
       allowedHelmets: new List<string> { "", "padded_coif", "kettle_hat" },
       allowedBodyArmors: new List<string> { "padded_armor", "leather_tunic" },
       allowedWeaponsSlot0: new List<string> { "vlandia_sword_1", "vlandia_axe_1" },
       allowedWeaponsSlot1: new List<string> { "", "vlandia_shield_wood" },
       allowedHorses: new List<string>() // РџСѓСЃС‚Рѕ РґР»СЏ РїРµС…РѕС‚РёРЅС†Р°!
   );
   ```

### 5.2. РЎРµС‚РµРІР°СЏ СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ СЌРєРёРїРёСЂРѕРІРєРё
1. РРіСЂРѕРє РЅР°Р¶РёРјР°РµС‚ РєРЅРѕРїРєСѓ **В«Р’ Р‘РћР™В»** РІ Gauntlet UI.
2. РљР»РёРµРЅС‚ СѓРїР°РєРѕРІС‹РІР°РµС‚ `DWDN_ClassSelection` РІ СЃРµС‚РµРІРѕР№ РїР°РєРµС‚ `RequestCustomLoadoutMessage`:
   ```csharp
   GameNetwork.BeginModuleEventAsClient();
   GameNetwork.WriteMessage(new RequestCustomLoadoutMessage(selection));
   GameNetwork.EndModuleEventAsClient();
   ```
3. РќР° РІС‹РґРµР»РµРЅРЅРѕРј СЃРµСЂРІРµСЂРµ `DWDN_ServerLoadoutBehavior.cs` РїСЂРёРЅРёРјР°РµС‚ РїР°РєРµС‚:
   * РџСЂРѕРІРµСЂСЏРµС‚, С‡С‚Рѕ РєР°Р¶РґС‹Р№ StringId РґРµР№СЃС‚РІРёС‚РµР»СЊРЅРѕ СЂР°Р·СЂРµС€РµРЅ РґР°РЅРЅРѕРјСѓ РєР»Р°СЃСЃСѓ РІ `DWDN_DuchyManager` (Р°РЅС‚РёС‡РёС‚-РІР°Р»РёРґР°С†РёСЏ).
   * РЎРѕС…СЂР°РЅСЏРµС‚ СЌРєРёРїРёСЂРѕРІРєСѓ РІ СЂРµРµСЃС‚СЂ РёРіСЂРѕРєР° `_peerLoadouts[peer]`.
   * РџСЂРё СЃСЂР°Р±Р°С‚С‹РІР°РЅРёРё `OnAgentBuild`: Р·Р°РїРѕР»РЅСЏРµС‚ `agent.SpawnEquipment` РїСЂРѕРІРµСЂРµРЅРЅС‹РјРё РїСЂРµРґРјРµС‚Р°РјРё.

### 5.3. Р—Р°С‰РёС‚Р° РѕС‚ РІР°СЂРї-СЃРєРІРµСЂРЅС‹ РєРѕРЅРµР№ (The Cavalry Dismount Fix)
> [!CAUTION]
> **РљР РРўРР§Р•РЎРљРР™ Р‘РђР“ TALEWORLDS:**  
> Р•СЃР»Рё РїРµС€РёР№ РїРµСЂСЃРѕРЅР°Р¶ РёРјРµРµС‚ РІ СЃРІРѕРµРј `SpawnEquipment` Р·Р°РїРѕР»РЅРµРЅРЅС‹Р№ СЃР»РѕС‚ `EquipmentIndex.Horse` РёР»Рё `EquipmentIndex.HorseHarness`, РЅР°С‚РёРІРЅС‹Р№ РјРµС‚РѕРґ РґРІРёР¶РєР° `MountVisualCreator.AddMountMeshToAgentVisual` РѕС€РёР±РѕС‡РЅРѕ РїСЂРёРєСЂРµРїР»СЏРµС‚ РјРµС€ Р»РѕС€Р°РґРё Рє РєРѕСЃС‚СЏРј С‚Р°Р·Р° **С‡РµР»РѕРІРµС‡РµСЃРєРѕРіРѕ СЃРєРµР»РµС‚Р°**. РџСЂРё СЃРїРµС€РёРІР°РЅРёРё РёР»Рё РґРІРёР¶РµРЅРёРё РјРµС€ РґРµС„РѕСЂРјРёСЂСѓРµС‚СЃСЏ Рё РѕРїР°РґР°РµС‚ РЅР° Р·РµРјР»СЋ РІ С‚РѕС‡РєСѓ `(0,0,0)`.

**РљРђРљ Р­РўРћ Р Р•РЁР•РќРћ Р’ `DWDN_ServerLoadoutBehavior.cs`:**
```csharp
// 1. Р§РµР»РѕРІРµРє-РІСЃР°РґРЅРёРє РќРРљРћР“Р”Рђ РЅРµ РґРѕР»Р¶РµРЅ РЅРµСЃС‚Рё СЃР»РѕС‚С‹ Horse / HorseHarness РІ С‡РµР»РѕРІРµС‡РµСЃРєРѕРј СЃРЅР°СЂСЏР¶РµРЅРёРё:
equip[EquipmentIndex.Horse] = EquipmentElement.Invalid;
equip[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

// 2. Р›РѕС€Р°РґСЊ СЃРїР°РІРЅРёС‚СЃСЏ РґРІРёР¶РєРѕРј РўРћР›Р¬РљРћ С‚РѕРіРґР°, РєРѕРіРґР° SelectedTroopIndex СѓРєР°Р·С‹РІР°РµС‚ РЅР° РєР°РІР°Р»РµСЂРёР№СЃРєРёР№ РєР»Р°СЃСЃ:
int targetIdx = string.IsNullOrEmpty(selection.HorseId) ? 0 : cavalryIndex;
missionPeer.SelectedTroopIndex = targetIdx;
missionPeer.NextSelectedTroopIndex = targetIdx;
```

---

## 6. РњРђРўР•РњРђРўРРљРђ РЎРћРЎРўРђР’РќРћР“Рћ РћР РЈР–РРЇ (CRAFTED WEAPONS DELTAS)

РЎРѕСЃС‚Р°РІРЅРѕРµ РјРѕРґСѓР»СЊРЅРѕРµ РѕСЂСѓР¶РёРµ (РјРµС‡Рё, РєРѕРїСЊСЏ, С‚РѕРїРѕСЂС‹) РѕРїРёСЃС‹РІР°РµС‚СЃСЏ РІ `ModuleData/crafting_pieces.xml` Рё `crafting_templates.xml`.

```mermaid
flowchart LR
    Handle["Р СѓРєРѕСЏС‚СЊ (Handle)"] -- "next_piece_offset" --> Guard["Р“Р°СЂРґР° (Guard)"]
    Guard -- "next_piece_offset" --> Blade["Р›РµР·РІРёРµ (Blade)"]
```

### 6.1. Р”РµРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅР°СЏ С„РѕСЂРјСѓР»Р° TaleWorlds (`WeaponDesign.CalculatePivotDistances`)
РџСЂРё СЃР±РѕСЂРєРµ РјРµС€Р° РѕСЂСѓР¶РёСЏ РґРІРёР¶РѕРє РІС‹С‡РёСЃР»СЏРµС‚ РїРѕР·РёС†РёСЋ РєР°Р¶РґРѕРіРѕ СЃРѕС‡Р»РµРЅРµРЅРёСЏ РёРЅРєСЂРµРјРµРЅС‚РЅРѕ РѕС‚ С‚РѕС‡РєРё С…РІР°С‚Р°:

$$\text{Position}_{n} = \text{Length}_{n-1} + (\text{previous\_piece\_offset}_{n} - \text{next\_piece\_offset}_{n-1})$$

*Р’СЃРµ Р·РЅР°С‡РµРЅРёСЏ РІ XML СѓРєР°Р·С‹РІР°СЋС‚СЃСЏ РІ СЃР°РЅС‚РёРјРµС‚СЂР°С…, Р° РґРІРёР¶РѕРє РґРµР»РёС‚ РёС… РЅР° 100 РґР»СЏ РїРµСЂРµРІРѕРґР° РІ РјРµС‚СЂС‹.*

### 6.2. РџРѕС‡РµРјСѓ РєРѕРѕСЂРґРёРЅР°С‚С‹ РёР· Blender Р»РѕРјР°СЋС‚ РёРіСЂСѓ
* Р’ Blender РјРѕРґРµР»Р»РµСЂ РІРёРґРёС‚ **Р°Р±СЃРѕР»СЋС‚РЅС‹Рµ РєРѕРѕСЂРґРёРЅР°С‚С‹ СЃС†РµРЅС‹** (РЅР°РїСЂРёРјРµСЂ, РіР°СЂРґР° РЅР°С…РѕРґРёС‚СЃСЏ РЅР° $Z = 140\text{ СЃРј}$).
* Р•СЃР»Рё РїСЂРѕРїРёСЃР°С‚СЊ `140` РІ `previous_piece_offset`, РґРІРёР¶РѕРє РїСЂРёР±Р°РІРёС‚ $1.4$ РјРµС‚СЂР° Рє РґР»РёРЅРµ СЂСѓРєРѕСЏС‚Рё, Рё РіР°СЂРґР° СѓР»РµС‚РёС‚ РІ РЅРµР±РµСЃР° РёР»Рё РІС‹Р·РѕРІРµС‚ Р±РµСЃРєРѕРЅРµС‡РЅС‹Р№ Р°СЃСЃРµСЂС‚ С„РёР·РёС‡РµСЃРєРѕРіРѕ С‚РµР»Р° `Physics material bounding box error`.
* **РџСЂР°РІРёР»Рѕ:** РІ `crafting_pieces.xml` СѓРєР°Р·С‹РІР°СЋС‚СЃСЏ С‚РѕР»СЊРєРѕ **РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅС‹Рµ РґРµР»СЊС‚С‹ СЃС‚С‹РєРѕРІРєРё** РјРµР¶РґСѓ РєСЂР°РµРј РјРµС€Р° СЂСѓРєРѕСЏС‚Рё Рё РЅР°С‡Р°Р»РѕРј РјРµС€Р° РіР°СЂРґС‹.

### 6.3. Р­С‚Р°Р»РѕРЅРЅР°СЏ РєР°Р»РёР±СЂРѕРІРєР° РєРѕРїСЊСЏ Р¤РёРЅРЅР°СЂРґР° (`finnard_spear`)
РџСЂРѕРІРµСЂРµРЅРЅР°СЏ РіРµРѕРјРµС‚СЂРёСЏ, РіР°СЂР°РЅС‚РёСЂСѓСЋС‰Р°СЏ РёРґРµР°Р»СЊРЅС‹Р№ РјРѕРЅРѕР»РёС‚РЅС‹Р№ СЃС‚С‹Рє Р±РµР· С‰РµР»РµР№ Рё РєСЂР°С€РµР№:
```xml
<!-- Р СѓРєРѕСЏС‚СЊ: СЃРјРµС‰РµРЅРёРµ СЃС‚С‹РєР° РІРІРµСЂС… Рє РіР°СЂРґРµ -->
<CraftingPiece id="finnard_spear_handle" Type="Handle" 
               next_piece_offset="-35" ... />

<!-- Р“Р°СЂРґР°: РІС…РѕРґРЅР°СЏ РґРµР»СЊС‚Р° РѕС‚ СЂСѓРєРѕСЏС‚Рё (+7) Рё РІС‹С…РѕРґРЅР°СЏ Рє Р»РµР·РІРёСЋ (-19) -->
<CraftingPiece id="finnard_speard_guard" Type="Guard" 
               previous_piece_offset="7" next_piece_offset="-19" ... />

<!-- РќР°РєРѕРЅРµС‡РЅРёРє: РІС…РѕРґРЅР°СЏ РґРµР»СЊС‚Р° СЃС‚С‹РєР° (+24) -->
<CraftingPiece id="finnard_spearhead" Type="Blade" 
               previous_piece_offset="24" next_piece_offset="0" ... />
```

---

## 7. РљРћР”Р•РљРЎ Р—РђР©РРўР« РћРў РЎРљР РђРџРљРћР”Рђ (ANTI-SCRAPCODE CODEX)

1. **РџСЂР°РІРёР»Рѕ РјРёРЅРёРјР°Р»СЊРЅРѕРіРѕ РІРјРµС€Р°С‚РµР»СЊСЃС‚РІР°:**  
   РќРёРєРѕРіРґР° РЅРµ РїРµСЂРµРїРёСЃС‹РІР°Р№ СЂР°Р±РѕС‚Р°СЋС‰РёРµ СЃРёСЃС‚РµРјС‹, РµСЃР»Рё Р·Р°РґР°С‡Р° РєР°СЃР°РµС‚СЃСЏ С‚РѕР»СЊРєРѕ РґРѕР±Р°РІР»РµРЅРёСЏ РЅРѕРІРѕРіРѕ РїСЂРµРґРјРµС‚Р° РёР»Рё РєР»Р°СЃСЃР°. РћРґРЅРѕ РЅРµР»РѕРІРєРѕРµ РёР·РјРµРЅРµРЅРёРµ РІ СЃРёРіРЅР°С‚СѓСЂРµ СЃРµС‚РµРІРѕРіРѕ СЃРѕРѕР±С‰РµРЅРёСЏ Р»РѕРјР°РµС‚ РѕР±СЂР°С‚РЅСѓСЋ СЃРѕРІРјРµСЃС‚РёРјРѕСЃС‚СЊ РєР»РёРµРЅС‚Р° Рё СЃРµСЂРІРµСЂР°.
2. **Р’СЃРµРіРґР° РїСЂРѕРІРµСЂСЏР№ Р»РѕРіРё РґРІРёР¶РєР°:**  
   Р›РѕРіРё РёРіСЂС‹ РїРёС€СѓС‚СЃСЏ РІ СЂРµР°Р»СЊРЅРѕРј РІСЂРµРјРµРЅРё:  
   `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_*.txt`  
   Р•СЃР»Рё РєР»РёРµРЅС‚ Р·Р°РєСЂС‹Р»СЃСЏ РјРѕР»С‡Р°, РѕС‚РєСЂРѕР№ РїРѕСЃР»РµРґРЅРёР№ `rgl_log` Рё РїСЂРѕРјРѕС‚Р°Р№ РІ СЃР°РјС‹Р№ РЅРёР· вЂ” С‚Р°Рј Р±СѓРґРµС‚ СѓРєР°Р·Р°РЅ РєРѕРЅРєСЂРµС‚РЅС‹Р№ XML-С‚РµРі РёР»Рё РѕС‚СЃСѓС‚СЃС‚РІСѓСЋС‰РёР№ РјРµС€.
3. **Р‘РµР·РѕРїР°СЃРЅРѕСЃС‚СЊ UI-РїРѕС‚РѕРєРѕРІ:**  
   РќРёРєР°РєРёС… С‚СЏР¶РµР»С‹С… С†РёРєР»РѕРІ РїРѕРёСЃРєР° РІ `Tick` РёР»Рё `OnMissionScreenTick`. Р’СЃРµ СЃРїРёСЃРєРё РїСЂРµРґРјРµС‚РѕРІ РІ `DWDN_DuchyManager` РґРѕР»Р¶РЅС‹ РєСЌС€РёСЂРѕРІР°С‚СЊСЃСЏ РїСЂРё РёРЅРёС†РёР°Р»РёР·Р°С†РёРё.
4. **РЎРЅСЏС‚РёРµ РїСЂРµРґРјРµС‚РѕРІ (РџСѓСЃС‚РѕР№ СЃР»РѕС‚ `""`):**  
   Р”Р»СЏ С€Р»РµРјРѕРІ, РїР»Р°С‰РµР№, РїРµСЂС‡Р°С‚РѕРє Рё С‰РёС‚РѕРІ РІСЃРµРіРґР° РїСЂРµРґСѓСЃРјР°С‚СЂРёРІР°Р№ СЃС‚СЂРѕРєРѕРІС‹Р№ ID `""` (РїСѓСЃС‚Р°СЏ СЃС‚СЂРѕРєР°). Р­С‚Рѕ РїРѕР·РІРѕР»СЏРµС‚ РёРіСЂРѕРєСѓ СЃРЅСЏС‚СЊ С€Р»РµРј РёР»Рё С‰РёС‚, РѕС‡РёС‰Р°СЏ СЃРѕРѕС‚РІРµС‚СЃС‚РІСѓСЋС‰РёР№ СЃР»РѕС‚.
5. **РЎРёРЅС…СЂРѕРЅРёР·РёСЂСѓР№ РІСЃРµ СѓР·Р»С‹:**  
   РџРѕСЃР»Рµ Р»СЋР±РѕР№ РїСЂР°РІРєРё XML РёР»Рё РєРѕРјРїРёР»СЏС†РёРё DLL **РІСЃРµРіРґР°** Р·Р°РїСѓСЃРєР°Р№ `d:\banAImod\scratch\sync_all.ps1`. Р•СЃР»Рё РєР»РёРµРЅС‚ Рё РІС‹РґРµР»РµРЅРЅС‹Р№ СЃРµСЂРІРµСЂ СЂР°СЃС…РѕРґСЏС‚СЃСЏ С…РѕС‚СЏ Р±С‹ РЅР° РѕРґРёРЅ Р°С‚СЂРёР±СѓС‚ РІ `mpclassdivisions.xml`, СЃРµСЂРІРµСЂ РєРёРєРЅРµС‚ РёРіСЂРѕРєР° СЃ РѕС€РёР±РєРѕР№ РЅРµСЃРѕРѕС‚РІРµС‚СЃС‚РІРёСЏ РІРµСЂСЃРёР№.

---

## 8. РџРћР›РќР«Р™ Р Р•Р’Р•Р РЎ-РРќР–РРќРР РРќР“ РЇР”Р Рђ РР“Р Р« Р РРќРЎРўР РЈРњР•РќРўР« Р”Р•РљРћРњРџРР›РЇР¦РР

Р”Р»СЏ РіР»СѓР±РѕРєРѕРіРѕ РїРѕРЅРёРјР°РЅРёСЏ СѓСЃС‚СЂРѕР№СЃС‚РІР° РѕСЂРёРіРёРЅР°Р»СЊРЅРѕР№ РёРіСЂС‹ (*Native*, *SandBoxCore*, *SandBox*, *Multiplayer* v1.4.8) Рё СЃРѕР·РґР°РЅРёСЏ СЃРѕР±СЃС‚РІРµРЅРЅС‹С… СѓС‚РёР»РёС‚ РјРѕРґРґРёРЅРіР° РёСЃРїРѕР»СЊР·СѓР№ СЃР»РµРґСѓСЋС‰РёРµ СЂРµСЃСѓСЂСЃС‹ РІ `d:\banAImod`:

1. **[`BANNERLORD_COMPLETE_REVERSE_ENGINEERING.md`](file:///d:/banAImod/BANNERLORD_COMPLETE_REVERSE_ENGINEERING.md)** вЂ” РїРѕР»РЅР°СЏ СЌРЅС†РёРєР»РѕРїРµРґРёСЏ Р°СЂС…РёС‚РµРєС‚СѓСЂС‹ Bannerlord (В«СЂР°Р·Р±РѕСЂРєР° РїРѕ РєРёСЂРїРёС‡РёРєР°Рј Рё СЃР±РѕСЂРєР° Р·Р°РЅРѕРІРѕВ»):
   * РњРѕСЃС‚ C++/C# (`DotNetObject`, `[MBCallback]` РјРµС‚РѕРґС‹ РІ `TaleWorlds.MountAndBlade.Module`).
   * Р”РІРѕР№РЅРѕР№ РїР°Р№РїР»Р°Р№РЅ XML: РЅР°С‚РёРІРЅС‹Р№ `ModuleData/project.mbproj` (РІСЃРµ 39 `soln_*` РёРґРµРЅС‚РёС„РёРєР°С‚РѕСЂРѕРІ) vs СѓРїСЂР°РІР»СЏРµРјС‹Р№ `MBObjectManager` (Р°Р»РіРѕСЂРёС‚Рј XSD-РІР°Р»РёРґР°С†РёРё, РґРёРЅР°РјРёС‡РµСЃРєРѕР№ РёРЅСЉРµРєС†РёРё `_replaceWhileMerging`, XSLT-С‚СЂР°РЅСЃС„РѕСЂРјР°С†РёР№ Рё `GetPresumedObject`).
   * Р‘РёРЅР°СЂРЅР°СЏ СЃС‚СЂСѓРєС‚СѓСЂР° Р°СЃСЃРµС‚РѕРІ `.tpac` (LZ4 + xxHash64), РєСЌС€Р° `.rdc` (145-Р±Р°Р№С‚РЅС‹Рµ Р·Р°РїРёСЃРё `RDC0`) Рё Р¶РµСЃС‚РєРёР№ РєРѕРЅС‚СЂР°РєС‚ РЅР° 64 РєРѕСЃС‚Рё СЃРєРµР»РµС‚Р° (`0x1050`-Р±Р°Р№С‚РЅС‹Р№ Р±СѓС„РµСЂ `AnimResult`).
   * РџРѕР»РЅС‹Р№ Р¶РёР·РЅРµРЅРЅС‹Р№ С†РёРєР» `MBSubModuleBase`, `MBGameManager`, `Game`, `GameType` (`Campaign` Рё `MultiplayerGame`), `GameStateScreenManager` Рё `ViewCreatorManager` (`[OverrideView]`, `[ViewMethod]`).
   * Р§РµСЂС‚РµР¶Рё СЃРѕР·РґР°РЅРёСЏ СЃРѕР±СЃС‚РІРµРЅРЅС‹С… РёРЅСЃС‚СЂСѓРјРµРЅС‚РѕРІ РјРѕРґРґРёРЅРіР° (Headless XML/XSD/XSLT РІР°Р»РёРґР°С‚РѕСЂ, `.tpac`/`.rdc` СѓРїР°РєРѕРІС‰РёРє, СЃС‚Р°С‚РёС‡РµСЃРєРёР№ Р°РЅР°Р»РёР·Р°С‚РѕСЂ).
2. **РЎС‚Р°С‚РёС‡РµСЃРєРёР№ CLI-РґРµРєРѕРјРїРёР»СЏС‚РѕСЂ Рё РґР°РјРїС‹ (`d:\banAImod\scratch\`)**:
   * [`d:\banAImod\scratch\inspector\Program.cs`](file:///d:/banAImod/scratch/inspector/Program.cs) вЂ” РёРЅСЃС‚СЂСѓРјРµРЅС‚ РЅР° Р±Р°Р·Рµ `ICSharpCode.Decompiler`, РїРѕР·РІРѕР»СЏСЋС‰РёР№ РјРіРЅРѕРІРµРЅРЅРѕ РґРµРєРѕРјРїРёР»РёСЂРѕРІР°С‚СЊ Р»СЋР±РѕР№ РєР»Р°СЃСЃ РёР· `Win64_Shipping_Client`, `DedicatedCustomServer` РёР»Рё РјРѕРґСѓР»РµР№ Р±РµР· РєСЂР°С€РµР№ `AssemblyResolve`.
   * [`d:\banAImod\scratch\module_types_dump.txt`](file:///d:/banAImod/scratch/module_types_dump.txt) вЂ” РїРѕР»РЅС‹Р№ СЂРµРµСЃС‚СЂ РІСЃРµС… 1923 РєР»Р°СЃСЃРѕРІ Рё РёРЅС‚РµСЂС„РµР№СЃРѕРІ РІ 11 DLL-СЃР±РѕСЂРєР°С… `Native`, `Multiplayer` Рё `SandBox`.
   * [`d:\banAImod\scratch\submodules_decompiled.txt`](file:///d:/banAImod/scratch/submodules_decompiled.txt) вЂ” РґРµРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅС‹Р№ РёСЃС…РѕРґРЅС‹Р№ РєРѕРґ РІСЃРµС… `SubModule` РєР»Р°СЃСЃРѕРІ, `Module` Рё `MBSubModuleBase`.
   * [`d:\banAImod\scratch\core_managers_decompiled.txt`](file:///d:/banAImod/scratch/core_managers_decompiled.txt) вЂ” РґРµРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅС‹Р№ РєРѕРґ `MBObjectManager`, `SandBoxManager`, `ViewCreatorManager`, `GameStateScreenManager`, `MultiplayerMissions` Рё `SandBoxMissions`.
   * [`d:\banAImod\scratch\game_and_mp_decompiled.txt`](file:///d:/banAImod/scratch/game_and_mp_decompiled.txt) вЂ” РґРµРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅС‹Р№ РєРѕРґ `TaleWorlds.Core.Game`, `MultiplayerGame` Рё `MultiplayerGameManager`.

---

## 9. РђР’РўРћРќРћРњРќР«Р• РРќРЎРўР РЈРњР•РќРўР« РњРћР”Р”РРќР“Рђ Р РР“Р РћР’Р«Р• РЎРРЎРўР•РњР« DWDN

Р’ РєРѕСЂРЅРµ `d:\banAImod` СЃРѕР·РґР°РЅС‹ С‚СЂРё СЃРїРµС†РёР°Р»РёР·РёСЂРѕРІР°РЅРЅС‹Рµ GUI-СЃС‚СѓРґРёРё (WinForms .NET 8.0) РґР»СЏ Р°РІС‚РѕРјР°С‚РёР·Р°С†РёРё РјРѕРґРґРёРЅРіР° Рё РІРµСЂРёС„РёРєР°С†РёРё:

1. **РђРІС‚Рѕ-РљР°Р»РёР±СЂР°С‚РѕСЂ 3D РћСЂСѓР¶РёСЏ (`d:\banAImod\WeaponCalibrator\`, СЏСЂР»С‹Рє [`Р—РђРџРЈРЎРљ_РљРђР›РР‘Р РђРўРћР Рђ.bat`](file:///d:/banAImod/%D0%97%D0%90%D0%9F%D0%A3%D0%A1%D0%9A_%D0%9A%D0%90%D0%9B%D0%98%D0%91%D0%A0%D0%90%D0%A2%D0%9E%D0%A0%D0%90.bat))**:
   * РќР°РїСЂСЏРјСѓСЋ РїР°СЂСЃРёС‚ Р±РёРЅР°СЂРЅС‹Рµ `.tpac` Р°СЂС…РёРІС‹ РјРѕРґСѓР»СЏ (`LZ4` + `METM`) Рё `AssetSources/*.fbx`, РІС‹С‡РёСЃР»СЏСЏ С‚РѕС‡РЅС‹Рµ 3D-РіР°Р±Р°СЂРёС‚С‹ (`BoundingBox`, Z-СЃС‚С‹РєРё, СЂР°РґРёСѓСЃ С€РµР№РєРё) Рё Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё РіРµРЅРµСЂРёСЂСѓСЏ РєР°Р»РёР±СЂРѕРІР°РЅРЅС‹Р№ XML РґР»СЏ `crafting_pieces.xml` Рё `mpitems.xml` СЃ РёРЅС‚РµСЂР°РєС‚РёРІРЅС‹Рј 3D-РІСЊСЋРїРѕСЂС‚РѕРј.
2. **Р’РёР·СѓР°Р»СЊРЅС‹Р№ РљРѕРЅСЃС‚СЂСѓРєС‚РѕСЂ Р“РµСЂС†РѕРіСЃС‚РІ Рё Р›РѕРґР°СѓС‚РѕРІ (`d:\banAImod\DuchyLoadoutStudio\`, СЏСЂР»С‹Рє [`Р—РђРџРЈРЎРљ_РљРћРќРЎРўР РЈРљРўРћР Рђ_РљР›РђРЎРЎРћР’.bat`](file:///d:/banAImod/%D0%97%D0%90%D0%9F%D0%A3%D0%A1%D0%9A_%D0%9A%D0%9E%D0%9D%D1%81%D0%A2%D0%A0%D0%A3%D0%9A%D0%A2%D0%9E%D0%A0%D0%90_%D0%9A%D0%9B%D0%90%D0%A1%D0%A1%D0%9E%D0%92.bat))**:
   * РџР°СЂСЃРёС‚ `DWDN_DuchyManager.cs`, `mpclassdivisions.xml`, `mpcharacters.xml` Рё `mpitems.xml`, РѕС‚РѕР±СЂР°Р¶Р°СЏ 3D-РјР°РЅРµРєРµРЅ РІС‹Р±СЂР°РЅРЅРѕРіРѕ РєР»Р°СЃСЃР° РІ СЂРµР°Р»СЊРЅРѕРј РІСЂРµРјРµРЅРё.
   * РљРЅРѕРїРєР° **В«вљЎ РђР’РўРћ-РџР РРњР•РќРРўР¬ Р’ РњРћР” DWDN_TRYВ»** Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё РїР°С‚С‡РёС‚ `DWDN_DuchyManager.cs`, СЃРёРЅС…СЂРѕРЅРёР·РёСЂСѓРµС‚ `mpclassdivisions.xml` Рё `mpcharacters.xml`, РєРѕРјРїРёР»РёСЂСѓРµС‚ `DWDN_Script.dll` + `DWDN_Script.Server.dll` Рё Р·РµСЂРєР°Р»РёСЂСѓРµС‚ Р±РёР»Рґ РЅР° РІС‹РґРµР»РµРЅРЅС‹Р№ СЃРµСЂРІРµСЂ `DaewdenM`.
3. **РњРіРЅРѕРІРµРЅРЅС‹Р№ РџСЂРµРґРїСѓСЃРєРѕРІРѕР№ Р’Р°Р»РёРґР°С‚РѕСЂ РњРѕРґР° (`d:\banAImod\ModValidator\`, СЏСЂР»С‹Рє [`Р—РђРџРЈРЎРљ_Р’РђР›РР”РђРўРћР Рђ_РњРћР”Рђ.bat`](file:///d:/banAImod/%D0%97%D0%90%D0%9F%D0%A3%D0%A1%D0%9A_%D0%92%D0%90%D0%9B%D0%98%D0%94%D0%90%D0%A2%D0%9E%D0%A0%D0%90_%D0%9C%D0%9E%D0%94%D0%90.bat))**:
   * Р—Р° ~0.25 СЃРµРє РїСЂРѕРІРµСЂСЏРµС‚ 8 РєСЂРёС‚РёС‡РµСЃРєРёС… СѓР·Р»РѕРІ РјРѕРґР°: СЃРёРЅС‚Р°РєСЃРёСЃ XML, `SubModule.xml`, С†РµР»РѕСЃС‚РЅРѕСЃС‚СЊ С†РµРїРѕС‡РєРё `DWDN_DuchyManager.cs` $\leftrightarrow$ `mpclassdivisions.xml` $\leftrightarrow$ `mpcharacters.xml` $\leftrightarrow$ `mpitems.xml` $\leftrightarrow$ `crafting_pieces.xml`, РЅР°Р»РёС‡РёРµ 3D-РјРµС€РµР№ РІ `.tpac`, Р±Р°Р»Р°РЅСЃ Р·Р°Р·РѕСЂРѕРІ СЃРѕСЃС‚Р°РІРЅРѕРіРѕ РѕСЂСѓР¶РёСЏ Рё РїРѕР±Р°Р№С‚РѕРІСѓСЋ СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёСЋ С…СЌС€РµР№ `SHA-256` РјРµР¶РґСѓ РєР»РёРµРЅС‚РѕРј Рё СЃРµСЂРІРµСЂРѕРј (СЃ РєРЅРѕРїРєРѕР№ Р°РІС‚Рѕ-РёСЃРїСЂР°РІР»РµРЅРёСЏ РІ 1 РєР»РёРє).

### РЎРёСЃС‚РµРјР° РўР°РєС‚РёС‡РµСЃРєРѕР№ РђСѓСЂС‹ Р—РЅР°РјРµРЅРѕСЃС†Р° (`DWDN_BannerAuraBehavior.cs`)
* Р¤Р°Р№Р»: [`DWDN_BannerAuraBehavior.cs`](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_BannerAuraBehavior.cs), СЂРµРіРёСЃС‚СЂРёСЂСѓРµС‚СЃСЏ РІ `SubModule.AddMissionBehaviors`.
* **Р’Р°Р¶РЅС‹Р№ С‚РµС…РЅРёС‡РµСЃРєРёР№ РїР°С‚С‚РµСЂРЅ `AgentDrivenProperties` РІ РјСѓР»СЊС‚РёРїР»РµРµСЂРµ**:
  * Р’ Bannerlord РїСЂРё СЃРјРµРЅРµ РѕСЂСѓР¶РёСЏ (`OnWieldedItemIndexChange`) РґРІРёР¶РѕРє РІС‹Р·С‹РІР°РµС‚ `agent.UpdateAgentProperties()`, РєРѕС‚РѕСЂС‹Р№ РїРµСЂРµР·Р°РїРёСЃС‹РІР°РµС‚ РґРёРЅР°РјРёС‡РµСЃРєРёРµ СЃС‚Р°С‚С‹ (`MaxSpeedMultiplier`, `SwingSpeedMultiplier`, `ReloadSpeed`), РЅРѕ **РЅРµ** РїРµСЂРµСЃС‡РёС‚С‹РІР°РµС‚ СЃС‚Р°С‚РёС‡РµСЃРєРёРµ СЃС‚Р°С‚С‹ СЃРїР°РІРЅР° (`ArmorHead/Torso/Arms/Legs`, `CombatMaxSpeedMultiplier`).
  * РџРѕСЌС‚РѕРјСѓ РїСЂРё РЅР°Р»РѕР¶РµРЅРёРё РёР»Рё СЃРЅСЏС‚РёРё Р±Р°С„С„РѕРІ РІ `DWDN_BannerAuraBehavior` СЃРЅР°С‡Р°Р»Р° РєСЌС€РёСЂСѓСЋС‚СЃСЏ Р±Р°Р·РѕРІС‹Рµ Р·РЅР°С‡РµРЅРёСЏ Р±СЂРѕРЅРё (`BaseArmorHead` Рё С‚.Рґ.) РїСЂРё РїРµСЂРІРѕРј РІС…РѕРґРµ Р°РіРµРЅС‚Р°, Р·Р°С‚РµРј РІС‹Р·С‹РІР°РµС‚СЃСЏ `agent.UpdateAgentProperties()`, РїРѕСЃР»Рµ С‡РµРіРѕ РїРѕРІРµСЂС… С‡РёСЃС‚С‹С… СЃС‚Р°С‚РѕРІ РІС‹СЃС‚Р°РІР»СЏРµС‚СЃСЏ `Armor* = BaseArmor* + bonus`, СѓРјРЅРѕР¶Р°СЋС‚СЃСЏ СЃРєРѕСЂРѕСЃС‚РЅС‹Рµ РєРѕСЌС„С„РёС†РёРµРЅС‚С‹ Рё РІС‹Р·С‹РІР°РµС‚СЃСЏ `agent.UpdateCustomDrivenProperties()`. Р­С‚Рѕ РїРѕР»РЅРѕСЃС‚СЊСЋ РёСЃРєР»СЋС‡Р°РµС‚ РєСѓРјСѓР»СЏС‚РёРІРЅРѕРµ РЅР°РєРѕРїР»РµРЅРёРµ СЃС‚Р°С‚РѕРІ Рё СЃР±СЂРѕСЃ Р±Р°С„С„Р° РїСЂРё СЃРјРµРЅРµ РѕСЂСѓР¶РёСЏ, Р° РІС‹РїРѕР»РЅРµРЅРёРµ РЅР° РєР»РёРµРЅС‚Рµ Рё СЃРµСЂРІРµСЂРµ РѕРґРЅРѕРІСЂРµРјРµРЅРЅРѕ РїСЂРµРґРѕС‚РІСЂР°С‰Р°РµС‚ СЃРµС‚РµРІРѕР№ СЂР°СЃСЃРёРЅС…СЂРѕРЅ СЃРєРѕСЂРѕСЃС‚Рё (rubberbanding).

### РЎРёСЃС‚РµРјР° РџРѕР»РµРІРѕР№ РђСЂС‚РёР»Р»РµСЂРёРё Рё Р Р°СЃС‡С‘С‚Р° (`DWDN_FieldArtilleryBehavior.cs`)
* Р¤Р°Р№Р»: [`DWDN_FieldArtilleryBehavior.cs`](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_FieldArtilleryBehavior.cs), РєР»Р°СЃСЃ `mp_Artillerist` (`mp_Artillerist_hero` / `mp_Artillerist_troop`).
* **РџСЂРµРґРјРµС‚С‹ СЂР°СЃС‡С‘С‚Р° (`dwdn_weapon.xml`)**:
  * `dwdn_field_bombard` вЂ” РџРѕР»РµРІР°СЏ Р‘РѕРјР±Р°СЂРґР° (Р»РёРјРёС‚: 2 Р°РєС‚РёРІРЅС‹С… СЃС‚РІРѕР»Р° РЅР° РєРѕРјР°РЅРґСѓ РІ `DWDN_ServerLoadoutBehavior.cs`).
  * `dwdn_cannonball_ammo` вЂ” Р§СѓРіСѓРЅРЅС‹Рµ РЇРґСЂР° (AoE С„СѓРіР°СЃРЅС‹Р№ РІР·СЂС‹РІ 3.4 Рј РІ `OnMissileHit` СЃ `BlowFlags.KnockDown | BlowFlags.CanDismount`).
  * `dwdn_canister_ammo` вЂ” РљР°СЂС‚РµС‡РЅС‹Рµ Р—Р°СЂСЏРґС‹ (РєРѕРЅСѓСЃРЅС‹Р№ Р·Р°Р»Рї $13.5^\circ$ РЅР° 32 Рј РІ `OnAgentShootMissile`).
  * `dwdn_artillery_ramrod` вЂ” РђСЂС‚РёР»Р»РµСЂРёР№СЃРєРёР№ Р‘Р°РЅРЅРёРє-РЁРѕРјРїРѕР» (РёРЅСЃС‚СЂСѓРјРµРЅС‚ 2-РіРѕ РЅРѕРјРµСЂР° СЂР°СЃС‡С‘С‚Р° РЅР° Р±Р°Р·Рµ `TwoHandedMace`).
  * `dwdn_powder_chest` вЂ” Р—Р°СЂСЏРґРЅР°СЏ РЎСѓРјР° Р‘РѕРјР±Р°СЂРґРёСЂР° (10 Р·Р°СЂСЏРґРѕРІ РґР»СЏ Р°РІС‚Рѕ-РїРѕРґР°С‡Рё РЅР°РІРѕРґС‡РёРєСѓ РєР°Р¶РґС‹Рµ 6 СЃРµРє С‡РµСЂРµР· `SetWeaponAmountInSlot`).
* **РќРµРїСЂРёРєРѕСЃРЅРѕРІРµРЅРЅРѕСЃС‚СЊ `native_parameters.xml`**:
  * РќРёРєРѕРіРґР° РЅРµ РїРµСЂРµРѕРїСЂРµРґРµР»СЏР№ `ModuleData/native_parameters.xml` РІ РјРѕРґРµ вЂ” СЂР°Р·СЂР°Р±РѕС‚С‡РёРєРё TaleWorlds РѕР±РЅРѕРІР»СЏСЋС‚ СЃС‚СЂСѓРєС‚СѓСЂСѓ Рё Р·РЅР°С‡РµРЅРёСЏ `Native/ModuleData/native_parameters.xml` РІ РїР°С‚С‡Р°С… РёРіСЂС‹. Р’СЃРµ РґРёРЅР°РјРёС‡РµСЃРєРёРµ РјРѕРґРёС„РёРєР°С‚РѕСЂС‹ СЃРєРѕСЂРѕСЃС‚Рё, РїРµСЂРµР·Р°СЂСЏРґРєРё Рё Р±СЂРѕРЅРё СЂРµР°Р»РёР·СѓР№ СЃС‚СЂРѕРіРѕ С‡РµСЂРµР· `AgentDrivenProperties` РІ C# `MissionLogic`.

### Р”РёР°РіРЅРѕСЃС‚РёРєР° Р‘РµСЃРєРѕРЅРµС‡РЅРѕР№ Р—Р°РіСЂСѓР·РєРё РїСЂРё РџРѕРґРєР»СЋС‡РµРЅРёРё Рє Р’С‹РґРµР»РµРЅРЅРѕРјСѓ РЎРµСЂРІРµСЂСѓ (РџР°С‚С‡Рё TaleWorlds)
* **РџСЂРѕСЏРІР»РµРЅРёРµ Р±Р°РіР°**: РљР»РёРµРЅС‚ РІС…РѕРґРёС‚ РІ СЃРµС‚РµРІРѕРµ Р»РѕР±Р±Рё, РЅР°Р¶РёРјР°РµС‚ В«РџСЂРёСЃРѕРµРґРёРЅРёС‚СЊСЃСЏ Рє СЃРµСЂРІРµСЂСѓВ», Р·Р°РІРёСЃР°РµС‚ РІ СЃС‚Р°С‚СѓСЃРµ `WaitingToJoinCustomGame` РЅР° Р±РµСЃРєРѕРЅРµС‡РЅРѕРј СЌРєСЂР°РЅРµ Р·Р°РіСЂСѓР·РєРё, РїРѕРєР° РІ С†РёРєР»Рµ С€Р»СЋС‚СЃСЏ `AliveMessage` РІ Р»РѕР±Р±Рё TaleWorlds.
* **РџСЂРёС‡РёРЅР°**: РљСЂР°С€ РїСЂРѕС†РµСЃСЃР° `DedicatedCustomServer.Starter.exe` РІ РјРѕРјРµРЅС‚ С…СЌРЅРґС€РµР№РєР° РёРіСЂРѕРєР° (`KERNELBASE.dll`, .NET Runtime `Event ID 1026`).
* **РљРµР№СЃ 11.11.2026**: Р’ РѕР±РЅРѕРІР»РµРЅРёРё РґРІРёР¶РєР° TaleWorlds СѓРґР°Р»РёР»Рё РјРµС‚РѕРґ `TaleWorlds.MountAndBlade.NetworkCommunicator.UpdateForJoiningCustomGame(bool)`. РЎС‚РѕСЂРѕРЅРЅРёР№ РјРѕРґСѓР»СЊ `DoFAdminTools` (СЃРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅС‹Р№ РїРѕРґ СЃС‚Р°СЂС‹Р№ Р±РёР»Рґ) РїСЂРё РІС…РѕРґРµ Р»СЋР±РѕРіРѕ РёРіСЂРѕРєР° РІС‹Р·С‹РІР°Р» СЌС‚РѕС‚ РјРµС‚РѕРґ РёР· `DoFGameHandler.OnPlayerConnect(VirtualPlayer peer)`, С‡С‚Рѕ РїСЂРёРІРѕРґРёР»Рѕ Рє РЅРµРѕР±СЂР°Р±РѕС‚Р°РЅРЅРѕРјСѓ `System.MissingMethodException` Рё РїР°РґРµРЅРёСЋ СЃРµСЂРІРµСЂР°. Рђ С†РёРєР» Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРѕРіРѕ РїРµСЂРµР·Р°РїСѓСЃРєР° РІ `ZAPUSKSERVER.bat` С‚СѓС‚ Р¶Рµ РїРѕРґРЅРёРјР°Р» СЃРµСЂРІРµСЂ СЃРЅРѕРІР°, СЃРѕР·РґР°РІР°СЏ РёР»Р»СЋР·РёСЋ В«Р¶РёРІРѕРіРѕВ» СЃРµСЂРІРµСЂР°, РєРѕС‚РѕСЂС‹Р№ РїР°РґР°РµС‚ СЂРѕРІРЅРѕ РІ РјРѕРјРµРЅС‚ РєРѕРЅРЅРµРєС‚Р° РёРіСЂРѕРєР°.
* **Р›РµС‡РµРЅРёРµ**:
  1. РџСЂРѕРІРµСЂСЏС‚СЊ Windows Event Log: `Get-WinEvent -LogName Application -MaxEvents 10 | Where-Object { $_.ProviderName -match 'Application Error|.NET Runtime' }`.
  2. РСЃРєР»СЋС‡Р°С‚СЊ СѓСЃС‚Р°СЂРµРІС€РёРµ СЃС‚РѕСЂРѕРЅРЅРёРµ DLL РёР· Р°СЂРіСѓРјРµРЅС‚РѕРІ Р·Р°РїСѓСЃРєР° `_MODULES_` РІ `ZAPUSKSERVER.bat`. РњРѕРґСѓР»СЊ `DWDN_TRY` РїРѕР»РЅРѕСЃС‚СЊСЋ Р°РІС‚РѕРЅРѕРјРµРЅ Рё РЅРµ Р·Р°РІРёСЃРёС‚ РѕС‚ `DoFAdminTools`.

### РљСЂР°С€ РЎРµСЂРІРµСЂР° РїСЂРё РЎРїР°РІРЅРµ (NullReferenceException РІ Equipment.GetInitialWeaponIndicesToEquip)
* **РџСЂРѕСЏРІР»РµРЅРёРµ**: РРіСЂРѕРє РЅР° СЃРµСЂРІРµСЂРµ РЅР°Р¶РёРјР°РµС‚ `[F]` РґР»СЏ СЃРїР°РІРЅР° вЂ” Р°РіРµРЅС‚ РЅРµ РїРѕСЏРІР»СЏРµС‚СЃСЏ, СЃРµСЂРІРµСЂ РјРіРЅРѕРІРµРЅРЅРѕ РїР°РґР°РµС‚. Р’ Р¶СѓСЂРЅР°Р»Рµ СЃРѕР±С‹С‚РёР№ Windows РєСЂР°С€ `NullReferenceException` РІ `Equipment.GetInitialWeaponIndicesToEquip` $\rightarrow$ `Agent.WieldInitialWeapons` $\rightarrow$ `SpawningBehaviorBase.OnTick`.
* **РџСЂРёС‡РёРЅР°**: Р’ РјСѓР»СЊС‚РёРїР»РµРµСЂРµ TaleWorlds Р·Р°РіСЂСѓР¶Р°РµС‚ РёСЃРєР»СЋС‡РёС‚РµР»СЊРЅРѕ `mp_crafting_pieces.xml` Рё РёРіРЅРѕСЂРёСЂСѓРµС‚ СЃРёРЅРіР»РїР»РµРµСЂРЅС‹Р№ `crafting_pieces.xml`. Р•СЃР»Рё РѕСЂСѓР¶РёРµ РІ РјРѕРґРµ РѕР±СЉСЏРІР»РµРЅРѕ РєР°Рє `<CraftedItem>` СЃРѕ СЃСЃС‹Р»РєРѕР№ РЅР° РґРµС‚Р°Р»Рё РёР· СЃРёРЅРіР»РїР»РµРµСЂР° (РЅР°РїСЂРёРјРµСЂ, `cleaver_blade_1` РІРјРµСЃС‚Рѕ СЃРµС‚РµРІРѕРіРѕ `mp_battania_cleaver_blade`), РґРІРёР¶РѕРє РЅРµ РјРѕР¶РµС‚ РёРЅРёС†РёР°Р»РёР·РёСЂРѕРІР°С‚СЊ `WeaponComponent`. Р’ СЂРµР·СѓР»СЊС‚Р°С‚Рµ `item.PrimaryWeapon == null`. РџСЂРё РїРѕРїС‹С‚РєРµ РґРІРёР¶РєР° СЌРєРёРїРёСЂРѕРІР°С‚СЊ РѕСЂСѓР¶РёРµ РІ СЂСѓРєРё РІС‹Р·РѕРІ `item.PrimaryWeapon.WeaponFlags` РІС‹Р±СЂР°СЃС‹РІР°РµС‚ С„Р°С‚Р°Р»СЊРЅС‹Р№ `NullReferenceException`.
* **РЎР»РµРґСЃС‚РІРёРµ РІ Р»РѕРіРёРєРµ СЃРїР°РІРЅР°**: Р’ `DWDN_ServerLoadoutBehavior.cs` С‚РµСЂРЅР°СЂРЅС‹Р№ РѕРїРµСЂР°С‚РѕСЂ `int targetIdx = string.IsNullOrEmpty(selection.HorseId) ? 0 : idx;` РѕС€РёР±РѕС‡РЅРѕ СЃР±СЂР°СЃС‹РІР°Р» Р»СЋР±РѕР№ РїРµС…РѕС‚РЅС‹Р№ РєР»Р°СЃСЃ (РІРєР»СЋС‡Р°СЏ РђСЂС‚РёР»Р»РµСЂРёСЃС‚Р°) РЅР° РёРЅРґРµРєСЃ 0 (`Ratay`), Сѓ РєРѕС‚РѕСЂРѕРіРѕ РїРµСЂРІС‹Рј РґРµС„РѕР»С‚РЅС‹Рј РїРµСЂРєРѕРј СЃС‚РѕСЏР» РєР°Рє СЂР°Р· СЃР»РѕРјР°РЅРЅС‹Р№ `dwdn_cleaver`.
* **Р›РµС‡РµРЅРёРµ**:
  1. Р’ `mpitems/dwdn_weapon.xml` РёСЃРїРѕР»СЊР·РѕРІР°С‚СЊ С‚РѕР»СЊРєРѕ Р»РµРіРёС‚РёРјРЅС‹Рµ СЃРµС‚РµРІС‹Рµ РґРµС‚Р°Р»Рё СЃ РїСЂРµС„РёРєСЃРѕРј `mp_...` РёР· `Native/ModuleData/mp_crafting_pieces.xml`.
  2. Р’ `DWDN_ServerLoadoutBehavior.cs` СѓР±СЂР°С‚СЊ РїСЂРёРЅСѓРґРёС‚РµР»СЊРЅС‹Р№ СЃР±СЂРѕСЃ РЅР° 0: `int targetIdx = idx;`.
  3. Р’ `ApplySelectionToEquipment` РґРѕР±Р°РІРёС‚СЊ Р¶РµСЃС‚РєСѓСЋ РїСЂРѕРІРµСЂРєСѓ: РµСЃР»Рё РїСЂРµРґРјРµС‚ РІ СЃР»РѕС‚Рµ РѕСЂСѓР¶РёСЏ РёРјРµРµС‚ `item.PrimaryWeapon == null` (Рё РЅРµ СЏРІР»СЏРµС‚СЃСЏ С‰РёС‚РѕРј/Р·РЅР°РјРµРЅРµРј), РѕС‡РёС‰Р°С‚СЊ СЃР»РѕС‚ РґР»СЏ Р·Р°С‰РёС‚С‹ РѕС‚ РїР°РґРµРЅРёСЏ РїСЂРѕС†РµСЃСЃР°.

### Р’СЃС‚СЂРѕРµРЅРЅР°СЏ РЎРёСЃС‚РµРјР° РђРґРјРёРЅРёСЃС‚СЂРёСЂРѕРІР°РЅРёСЏ DWDN Admin Tools (`Server\Admin\`)
* Р¤Р°Р№Р»С‹:
  * [`DWDN_AdminService.cs`](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Server/Admin/DWDN_AdminService.cs) вЂ” СЏРґСЂРѕ СѓРїСЂР°РІР»РµРЅРёСЏ РїСЂР°РІР°РјРё, РѕР±СЂР°Р±РѕС‚С‡РёРє РєРѕРјР°РЅРґ, СЂР°СЃС‡С‘С‚ С„РѕСЂРјРёСЂРѕРІР°РЅРёР№ Рё РґРµР№СЃС‚РІРёР№.
  * [`DWDN_AdminGameHandler.cs`](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Server/Admin/DWDN_AdminGameHandler.cs) вЂ” РїРµСЂРµС…РІР°С‚ СЃРµС‚РµРІС‹С… СЃРѕРѕР±С‰РµРЅРёР№ С‡Р°С‚Р° `PlayerMessageAll` Рё `PlayerMessageTeam`.
  * [`DWDN_AdminBehavior.cs`](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Server/Admin/DWDN_AdminBehavior.cs) вЂ” РїРѕРґРґРµСЂР¶Р°РЅРёРµ СЃРѕСЃС‚РѕСЏРЅРёСЏ Р±РµСЃСЃРјРµСЂС‚РёСЏ РїСЂРё СЃРјРµРЅРµ Р°РіРµРЅС‚РѕРІ Рё Р·Р°С‰РёС‚Р° РѕС‚ СѓСЂРѕРЅР°.
  * [`DWDN_InputBehavior.cs`](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_InputBehavior.cs) вЂ” РіРѕСЂСЏС‡Р°СЏ РєР»Р°РІРёС€Р° `[F10]` РґР»СЏ Р±С‹СЃС‚СЂРѕР№ СЃРїСЂР°РІРєРё Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂР°.
* **РџРµСЂРµС…РІР°С‚ РєРѕРјР°РЅРґ С‡Р°С‚Р° Р±РµР· СЃРїР°РјР° РІ РѕР±С‰РёР№ С‡Р°С‚**:
  * Р’ `GameHandler.OnGameNetworkBegin()` СЂРµРіРёСЃС‚СЂРёСЂСѓСЋС‚СЃСЏ РґРµР»РµРіР°С‚С‹ `registerer.Register<PlayerMessageAll>` Рё `Register<PlayerMessageTeam>`.
  * Р•СЃР»Рё СЃРѕРѕР±С‰РµРЅРёРµ РЅР°С‡РёРЅР°РµС‚СЃСЏ СЃ `!`, РІС‹Р·С‹РІР°РµС‚СЃСЏ `DWDN_AdminService.HandleCommand(peer, message.Message)`.
  * Р§С‚РѕР±С‹ С‚РµРєСЃС‚ РєРѕРјР°РЅРґС‹ РЅРµ РїРѕСЏРІР»СЏР»СЃСЏ РІ РѕР±С‰РµРј С‡Р°С‚Рµ, С‡РµСЂРµР· СЂРµС„Р»РµРєСЃРёСЋ РѕР±РЅСѓР»СЏРµС‚СЃСЏ РїСЂРёРІР°С‚РЅС‹Р№ СЃРїРёСЃРѕРє РїРѕР»СѓС‡Р°С‚РµР»РµР№:
    ```csharp
    typeof(PlayerMessageAll).GetProperty("ReceiverList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        ?.SetValue(message, new List<VirtualPlayer>());
    typeof(PlayerMessageAll).GetProperty("HasReceiverList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        ?.SetValue(message, true);
    ```
    Р’ РЅР°С‚РёРІРЅРѕРј РјРµС‚РѕРґРµ `ChatBox.ServerPrepareAndSendMessage` РїСЂРѕРІРµСЂРєР° `receiverList.Contains(item.VirtualPlayer)` РґР°С‘С‚ `false` РґР»СЏ РІСЃРµС… РёРіСЂРѕРєРѕРІ, Р±Р»РѕРєРёСЂСѓСЏ РѕС‚РїСЂР°РІРєСѓ РєР»РёРµРЅС‚Р°Рј.
* **РќР°С‚РёРІРЅРѕРµ Р±РµСЃСЃРјРµСЂС‚РёРµ (God Mode)**:
  * РќРµ РЅСѓР¶РЅРѕ РіРѕСЂРѕРґРёС‚СЊ СЃС‚РѕСЂРѕРЅРЅРёРµ РєРѕСЃС‚С‹Р»Рё: РІ Bannerlord РµСЃС‚СЊ РІСЃС‚СЂРѕРµРЅРЅРѕРµ СЃРѕСЃС‚РѕСЏРЅРёРµ Р°РіРµРЅС‚Р° `agent.SetMortalityState(Agent.MortalityState.Invulnerable);` (Рё `agent.MountAgent.SetMortalityState(...)`), РїСЂРё РєРѕС‚РѕСЂРѕРј РЅР°С‚РёРІРЅС‹Р№ C++ РґРІРёР¶РѕРє РѕР±РЅСѓР»СЏРµС‚ РІРµСЃСЊ РІС…РѕРґСЏС‰РёР№ СѓСЂРѕРЅ (`InflictedDamage = 0`).
  * Р”Р»СЏ РЅР°РґС‘Р¶РЅРѕСЃС‚Рё `DWDN_AdminBehavior` С‚Р°РєР¶Рµ РїРµСЂРµС…РІР°С‚С‹РІР°РµС‚ `OnAgentHit` Рё С‚РёРєР°РµС‚ РІ С†РёРєР»Рµ, РіР°СЂР°РЅС‚РёСЂСѓСЏ Р±РµСЃСЃРјРµСЂС‚РёРµ РґР°Р¶Рµ РїРѕСЃР»Рµ СЃРјРµРЅС‹ РєР»Р°СЃСЃР° РёР»Рё СЂРµСЃРїР°РІРЅР°.
* **Р“СЂСѓРїРїРѕРІР°СЏ С‚РµР»РµРїРѕСЂС‚Р°С†РёСЏ РІ СЃС‚СЂРѕСЋ**:
  * РљРѕРјР°РЅРґР° `!tp all me` РёР»Рё `!tp team1 me` / `!tp team2 me` СЃС‚СЂРѕРёС‚ РёРіСЂРѕРєРѕРІ СЂРѕРІРЅС‹РјРё С€РµСЂРµРЅРіР°РјРё РїРµСЂРµРґ Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂРѕРј:
    * Р’РµРєС‚РѕСЂ РІР·РіР»СЏРґР° Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂР° $f$ Рё РїРµСЂРїРµРЅРґРёРєСѓР»СЏСЂ $r = (-f_y, f_x)$.
    * Р‘Р°Р·РѕРІР°СЏ С‚РѕС‡РєР° $P_0 = P_{\text{admin}} + f \cdot 4.0\text{ Рј}$.
    * РЁР°Рі СЃРµС‚РєРё: РїРѕ С„СЂРѕРЅС‚Сѓ $dx = 1.8\text{ Рј}$, РІ РіР»СѓР±РёРЅСѓ $dy = 2.0\text{ Рј}$.
    * РџСЂРёРІСЏР·РєР° Рє СЂРµР»СЊРµС„Сѓ: `Mission.Current.Scene.GetHeightAtPoint(targetPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref targetPos.z)`.
    * Р­С‚Рѕ РёСЃРєР»СЋС‡Р°РµС‚ Р·Р°СЃС‚СЂРµРІР°РЅРёРµ РёРіСЂРѕРєРѕРІ Рё РєРѕРЅРµР№ РґСЂСѓРі РІ РґСЂСѓРіРµ РёР»Рё РїРѕРґ С‚РµРєСЃС‚СѓСЂР°РјРё РєР°СЂС‚С‹.
* **РћС„РёС†РёР°Р»СЊРЅС‹Р№ РјРµС…Р°РЅРёР·Рј РєРёРєР° Рё Р±Р°РЅР° РёРіСЂРѕРєР° РЅР° СЃРµСЂРІРµСЂРµ**:
  ```csharp
  DisconnectInfo disconnectInfo = peer.PlayerConnectionInfo?.GetParameter<DisconnectInfo>("DisconnectInfo") ?? new DisconnectInfo();
  disconnectInfo.Type = DisconnectType.KickedByHost;
  peer.PlayerConnectionInfo?.AddParameter("DisconnectInfo", disconnectInfo);
  peer.QuitFromMission = true;
  GameNetwork.AddNetworkPeerToDisconnectAsServer(peer);
  ```
  РџСЂРё Р±Р°РЅРµ Р·Р°РїРёСЃСЊ РґРѕР±Р°РІР»СЏРµС‚СЃСЏ РІ `Modules/Native/banlist.txt` Рё `Modules/DaewdenM/banlist.txt`.
* **РЎРїСЂР°РІРѕС‡РЅРёРє РєРѕРјР°РЅРґ**:
  * РђРІС‚РѕСЂРёР·Р°С†РёСЏ: `!login <РїР°СЂРѕР»СЊ РёР· DWDN_config.txt>`, `!logout`
  * РўРµР»РµРїРѕСЂС‚Р°С†РёСЏ: `!goto <РЅРёРє>`, `!bring <РЅРёРє|all|team1|team2>`, `!tp <С†РµР»СЊ1> <С†РµР»СЊ2>`, `!tp <С†РµР»СЊ> <x> <y> [z]`, `!mark`, `!recall [С†РµР»СЊ]`
  * РЎРЅР°Р±Р¶РµРЅРёРµ: `!heal [С†РµР»СЊ] [С…Рї]`, `!ammo [С†РµР»СЊ]`, `!repair [С†РµР»СЊ]`, `!god [С†РµР»СЊ]`
  * РњРѕРґРµСЂР°С†РёСЏ: `!kick <РЅРёРє> [РїСЂРёС‡РёРЅР°]`, `!ban <РЅРёРє> [РїСЂРёС‡РёРЅР°]`, `!mute <РЅРёРє>`, `!unmute <РЅРёРє>`, `!slay [С†РµР»СЊ]`, `!freeze [С†РµР»СЊ]`, `!unfreeze [С†РµР»СЊ]`, `!disarm [С†РµР»СЊ]`, `!dismount [С†РµР»СЊ]`
  * РЎРµСЂРІРµСЂ: `!warmup end|extend`, `!endround`, `!map <РєР°СЂС‚Р°>`, `!say <СЃРѕРѕР±С‰РµРЅРёРµ>`, `!broadcast <СЃРѕРѕР±С‰РµРЅРёРµ>`, `!speed [С†РµР»СЊ] <РјРЅРѕР¶РёС‚РµР»СЊ>`, `!info [РЅРёРє]`, `!specs`, `!help`

---

*Р”Р° РїСЂРµР±СѓРґРµС‚ Р»РѕРіРёРєР° РІ СЃРІСЏС‰РµРЅРЅРѕРј РєСЂРµРјРЅРёРё. РЎР»Р°РІР° РћРјРЅРёСЃСЃРёРё!*


### Передвижные Осадные Орудия на Тележках (Онагры и Баллисты, DWDN_FieldArtilleryBehavior)
* **Фундаментальный баг движка TaleWorlds при динамическом спавне глубоких префабов в мультиплеере**:
  * В нативном методе TaleWorlds `Mission.CreateMissionObjectFromPrefab` и на клиенте в `MissionNetworkComponent.HandleServerEventCreateMissionObject` цикл обхода дочерних сущностей жестко ограничен прямыми потомками первого уровня (`foreach (GameEntity child in entity.GetChildren())`).
  * Для сложных префабов, таких как `mangonel_a` и `ballista_a`, все ключевые подножки (`StandingPoint`: прицел `use_reload_fire_l`, вращение `use_rotate_r/l`, зарядка `use_load`, взятие снарядов `ammo_pos_a..h`) находятся на 3-м и 4-м уровнях иерархии (внутри `clean` и `projectile_pile`).
  * В результате клиенту **не присваиваются сетевые `MissionObjectId` для рабочих подножек** (`Id` остается 0 или Invalid). При нажатии `[F]` клиент отправляет `RequestUseObject(0)`, сервер не находит объект в `Mission.Current.MissionObjects` и сбрасывает пакет. Игрок не может взаимодействовать с орудием!
  * **Решение архитектуры DWDN**:
    1. Рекурсивный сбор всех `MissionObject` в дереве сущностей префаба (`CollectMissionObjectsRecursive`).
    2. Присвоение каждому вложенному `MissionObject` серверного runtime ID (`mission.GetFreeRuntimeMissionObjectId()`) и регистрация в `mission.AddActiveMissionObject(mo)`.
    3. Рассылка полной карты ID клиентам через выделенный сетевой пакет мода `SyncArtilleryDescendantsMessage`.
    4. На клиенте воспроизводится точно такой же рекурсивный обход и присваиваются идентичные `MissionObjectId`, после чего они регистрируются в `Mission.Current.AddActiveMissionObject`. Теперь `RequestUseObject` от клиента находит 100% точное совпадение на сервере!

* **Опаснейшая ловушка двойного вызова OnInit() (`An item with the same key has already been added`)**:
  * В движке TaleWorlds метод `GameEntity.Instantiate(scene, prefab, frame, bool callScriptCallbacks)` принимает 4-м аргументом флаг запуска колбэков.
  * Если передать `true`, движок немедленно вызывает `OnInit()` на всех скриптах осадного орудия, включая `RangedSiegeWeapon.OnInit()`, который заполняет внутренний словарь `PilotReservePriorityValues.Add(standingPoint, distance)`.
  * Если затем вручную вызвать `root.CallScriptCallbacks(true)`, метод `OnInit()` запускается **второй раз** на том же экземпляре. Попытка повторного добавления того же ключа в `PilotReservePriorityValues` выбрасывает фатальный `ArgumentException: An item with the same key has already been added`, который обрушивает клиент игры при спавне.
  * **Священное правило**: при кастомном инстанцировании ВСЕГДА передавать `callScriptCallbacks: false`, сначала назначать синхронизированные сетевые ID, и лишь затем вызывать `root.CallScriptCallbacks(true)` строго один раз! Обработчик сетевого пакета также обязан быть обернут в `try / catch`, предотвращая краш клиента.

* **Нейтрализация стороны команды (`StandingPointSide = BattleSideEnum.None`)**:
  * В декомпилированном IL-коде `StandingPoint.IsDisabledForAgent` обнаружено: если `StandingPointSide != BattleSideEnum.None`, движок запрещает использование орудия любому игроку, чья команда (`team.Side`) не совпадает с `StandingPointSide`.
  * В префабах осадных орудий `BattleSide` по умолчанию прописана как `Attacker`. Если игрок спавнится за Защитников (`Defender`), орудие блокируется движком.
  * **Решение**: через рефлексию поле `StandingPointSide` для всех подножек сбрасывается в `BattleSideEnum.None`, а флаг `IsDisabledForPlayers` выставляется в `false`. Это дает 100% возможность управления орудием и зарядки для любой команды.

* **Клиент-Серверное Перемещение Лафета (Pushing / Towing)**:
  * На Выделенном Сервере (Dedicated Server) `Mission.MainAgent` равен `null`, а клавиатура отсутствует. Опрос клавиш и дистанции толкания обязан происходить **на клиенте**.
  * Клиент при нахождении позади лафета отображает подсказку `[F] — Толкать орудие` и при нажатии шлет `RequestPushArtilleryMessage(engineId, isPushing)`.
  * Сервер валидирует дистанцию, назначает `PushingAgent` и скрывает кучу боеприпасов (`projectile_pile.SetVisibilityExcludeParents(false)`).
  * В цикле обновления сервер считывает направление движения агента (`pusher.GetMovementDirection()`), привязывает орудие к рельефу (`GetHeightAtPoint`) и транслирует координаты всем клиентам через `SynchedMissionObject.SetGlobalFrameSynched(ref frame, false)`.
  * При остановке сервер разворачивает кучу снарядов на грунте рядом с орудием (`SetVisibilityExcludeParents(true)`) и рассылает клиентам `SetArtilleryPushStateMessage`.

* **Экипировка Артиллериста и Защита от Краша CreateAgentVisuals (0xc0000005)**:
  * **Фатальная ошибка**: попытка выдать статичный меш пропса (например, `lib_scroll_d` из `building_details.xml`) в качестве экипируемого оружия с холстером (`item_holsters="mace_right_hip"`). В процедуре `CreateAgentVisuals` C++ движок TaleWorlds пытается привязать скелетные кости оружия к телу бойца; для статического меша кости отсутствуют -> нулевой указатель -> немедленный вылет игры `0xc0000005: Access Violation`.
  * **Идеальное архитектурное решение**: заявка на орудие (`Mobile Onager` / `Mobile Ballista`) оформляется **исключительно как перк во 2-м перк-листе дивизиона** (`mpclassdivisions.xml`) без передачи каких-либо бутафорских предметов в инвентарь (`OnSpawnEffect` не требуется).
  * В файлах персонажей (`mpcharacters.xml`) у артиллериста удален слот `Item1`. В руках и слотах артиллерист имеет **строго одно выбранное холодное оружие** в слоте `Item0` (фальшион, тесак, молот или меч).
  * В коде `CheckAndSpawnArtilleryForAgent`: сервер опрашивает выбранный перк игрока (`agent.MissionPeer.SelectedPerks` или `GetSelectedPerkIndexWithPerkListIndex(selectedTroopIndex, 1)`), определяет тип орудия (`ballista` или `mangonel`) и спавнит его на базе, автоматически приводя в боевую готовность холодное оружие в слоте 0 (`agent.TryToWieldWeaponInSlot(WeaponItemBeginSlot, Instant, false)`). В руках бойца нет лишних предметов, а вылеты полностью исключены.

* **Встроенная куча боеприпасов (projectile_pile) и Сворачивание при Толкании**:
  * В нативных префабах `mangonel_a` и `ballista_a` куча боеприпасов `projectile_pile` **уже встроена в иерархию** как дочерняя сущность. Спавнить сторонний `stone_pile_a_usable` категорически запрещено — это нарушает синхронизацию идентификаторов.
  * Метод `CollectMissionObjectsRecursive` обязан обходить все компоненты сущности через `entity.GetScriptComponents<MissionObject>()`, так как узлы вроде `mangonel_a_body` содержат сразу несколько `MissionObject` (`Mangonel` + `DestructableComponent`).
  * Для орудий на сервере и клиенте вызывается `siegeWeapon.SetAmmo(25)` и `siegeWeapon.SetStartAmmo(25)`.
  * При толкании орудия куча боеприпасов скрывается и блокируется (`SetAmmoPileActive(false)`), а при остановке тележки разворачивается на новой позиции рядом с лафетом (`SetAmmoPileActive(true)`).
  * Зона взаимодействия сзади лафета рассчитывается через `currentFrame.TransformToLocal(player.Position)` (`localPos.y` от 1.0м до 5.0м, ширина до 2.5м).
  * Во время перемещения толкающему агенту на сервере и клиенте непрерывно транслируется нативная анимация толкания осадных машин: `act_usage_siege_machine_push` (`pusher.SetActionChannel(0, ActSiegePush)`).

* **Анти-дюп и Админ-контроль**:
  * 1 живое орудие на инженера (проверка по сетевому `VirtualPlayer.Id`).
  * Командный лимит `MaxCannonsPerTeam` (по умолчанию 2, меняется на лету через `!artylimit <N>`).
  * Кулдаун после разрушения: 25 секунд до освобождения слота.
  * Админ-команды в чате: `!artylimit <N>`, `!cannons`, `!clearcannons`, `!spawncannon [mangonel|ballista]`.

---

## 46. Сетевой скрапкод CraftedItem и сетевое сжатие TaleWorlds (WeaponUsageIndexChangeMessage)

* **Ловушка Campaign vs Multiplayer Crafting Pieces**:
  * В движке Bannerlord для одиночной кампании (`Campaign`) и сетевой игры (`MultiplayerGame`) используются **разные XML-манифесты деталей ковки**.
  * В `Native/SubModule.xml`: для `MultiplayerGame` загружается строго `mp_crafting_pieces.xml`, в то время как `crafting_pieces.xml` доступен только в `Campaign` и `CustomGame`.
  * Если объявить `<CraftedItem multiplayer_item="true">` с деталями из синглплеера (например `dagger_blade_1`, `empire_dagger_guard_3`, `vlandian_grip_1`, `vlandian_pommel_1`), в одиночной игре предмет загрузится, но **на мультиплеерном сервере движок не найдет эти детали и вернет null**:
    ```
    Null object reference found with ID: <item_id>
    ```
  * Для сетевых предметов допустимо использовать ТОЛЬКО детали с префиксом `mp_` из `mp_crafting_pieces.xml` (например `mp_dagger_blade_1`, `mp_battania_dagger_guard`, `mp_battania_dagger_grip`, `mp_battania_dagger_pommel`).

* **Каскадный краш сетевого протокола при входе игрока (`Integer_compression_info::decode_safe`)**:
  * Если `Null object reference` попадает в экипировку класса (`mpclassdivisions.xml` или `mpcharacters.xml`), движок теряет валидность слота.
  * При подключении игрока (`Join game successful`) сервер синхронизирует состояние оружия и экипировки. Нарушение целостности слота приводит либо к рассинхронизации битового потока пакетов, либо к отправке слота за пределами допустимого диапазона:
    ```
    Reading message: WeaponUsageIndexChangeMessage
    Invalid encode/decode in Integer_compression_info::decode_safe. min: 0. max: 4. input: 5. Message: internal_decode
    ```
  * В TaleWorlds `ItemSlotCompressionInfo` сжат строго в диапазон `min: 0, max: 4` (оружие 0..3 + ExtraSlot 4). Значение 5 соответствует `EquipmentIndex.Head` / `ArmorItemBeginSlot` и категорически недопустимо в `WeaponUsageIndexChangeMessage`. Декодер мгновенно инициирует `internal_decode` и выбрасывает игрока из сессии.

* **Священное правило развертывания сборок на Выделенном Сервере**:
  * Выделенный сервер Bannerlord может ссылаться на модуль как по имени папки `Modules\DaewdenM`, так и `Modules\DWDN_TRY`.
  * Для предотвращения рассинхронизации Mod Network Messages скрипт `sync_all.ps1` обязан зеркально дублировать `DWDN_Script.dll` и `DWDN_Script.Server.dll` как в обе папки модулей, так и напрямую в `bin\Win64_Shipping_Server`.

---

## 47. Кастомный Admin Tools с GUI и Полевая Конная Артиллерия

### Архитектура Кастомного Admin Tools (DWDN_AdminMissionView, DWDN_AdminPanelVM, DWDN_AdminService)
* **Ловушка жизненного цикла MissionView при регистрации через AddMissionBehavior**:
  * В TaleWorlds `MissionView` наследуется от `MissionBehavior`. Однако если класс регистрируется через `mission.AddMissionBehavior(new DWDN_AdminMissionView())`, TaleWorlds **не вызывает** `OnMissionScreenInitialize()` и `OnMissionScreenTick(dt)`! Движок вызывает эти методы исключительно при добавлении вьюшки в `MissionScreen.AddMissionView(view)` или через фабрики `[ViewCreatorModule]`.
  * Свойство `MissionScreen` базового класса при этом равно `null`!
  * **Решение**:
    1. Проверку горячих клавиш (`[F10]`, `[Insert]`) и обновление логики тиков помещать в `OnMissionTick(float dt)`.
    2. Экран миссии получать через fallback: `MissionScreen screen = MissionScreen ?? (ScreenManager.TopScreen as MissionScreen)`.
    3. При вызове интерфейса создавать `GauntletLayer("DWDN_AdminPanel", ViewOrderPriority)` и загружать префаб `_gauntletLayer.LoadMovie("DWDN_AdminPanel", _dataSource)`.
    4. Если `screen == null` или Gauntlet префаб не загрузился, активировать fallback на нативный интерфейс TaleWorlds `MBInformationManager.ShowMultiSelectionInquiry`. Это гарантирует 100% доступность админки даже в нестандартных сценах.
* **Привязка списков в Gauntlet XML (DataSource)**:
  * Для отображения динамических списков (например, списка игроков `PlayerRows`) виджет `<ListPanel Id="PlayerList">` с вложенным `<ItemTemplate>` **обязан** иметь атрибут `DataSource="{PlayerRows}"`. Без этого атрибута движок игнорирует `ItemTemplate` и список остается пустым.
* **Универсальная Авторизация Администратора**:
  * Кастомный админ-инструмент должен выдаваться игроку независимо от того, как он авторизовался:
    1. **Через нативную консоль**: `adminpassword <пароль_сервера>`. В `DWDN_AdminService.TickMission` сервер проверяет `peer.IsAdmin && !_authenticatedPeers.Contains(peer)`, автоматически промоутит игрока в `_authenticatedPeers` и рассылает клиенту `SyncAdminStateMessage(true)`.
    2. **Через чат**: `!login <пароль_сервера>`.
    3. **Через GUI-диалог ввода пароля**: при нажатии `[F10]` или `[Insert]` неавторизованным игроком клиент открывает нативный диалог `InformationManager.ShowTextInquiry` ("Введите пароль администратора:"). Валидатор ввода: `(text) => new Tuple<bool, string>(!string.IsNullOrWhiteSpace(text), "")`. При подтверждении отправляется `RequestAdminActionMessage("login", "", password)`.
    4. Сервер в `HandleClientEventRequestAdminAction` обрабатывает `Action == "login"` **до проверки прав админа**, проверяет пароль (строго против `_adminPassword`, загруженного из `MultiplayerOptions.OptionType.AdminPassword` или `DWDN_config.txt`), вызывает `SetPeerAdmin(peer, true)` и отправляет `SyncAdminStateMessage(true)`.
    5. Клиент при получении `SyncAdminStateMessage(true)` автоматически открывает графическую панель администратора.

---

### Полевая Артиллерия на Конной Тяге (DWDN_FieldArtilleryBehavior)
* **Специфика 3D-моделей: Тележка vs Собственные Колеса**:
  * **Онагр (`mangonel_a`)**: префаб уже имеет на своей модели массивные деревянные колеса. Поэтому `UsesCart = false` — он стоит и буксируется **прямо по земле** (`position.z = groundZ`), без создания под ним платформы `bd_cart_a`.
  * **Баллиста (`ballista_a`)**: префаб колес не имеет (стационарная тренога). Поэтому `UsesCart = true` — под ней спавнится ванильная тележка `bd_cart_a`, а само орудие приподнимается в кузов на `+0.40f` по оси Z.
* **Спавн Тягловой Лошади**:
  * Сигнатура TaleWorlds: `mission.SpawnMonster(new ItemRosterElement(horseItem, 1), new ItemRosterElement(harnessItem, 1), horsePos, horseHeading, (int)teamSide)`. (Позиция и направление передаются по значению, без `ref`).
  * Для коня вызывается жесткое ограничение скорости: `horseAgent.SetMaximumSpeedLimit(2.2f, false)` и `horseAgent.UpdateAgentProperties()`. Конь движется **СТРОГО ШАГОМ**, бег и галоп заблокированы.
* **Буксировка на Сцепке в `OnMissionTick`**:
  * Расстояние сцепки орудия за лошадью — `3.6f`. Высота грунта под орудием постоянно корректируется через `mission.Scene.GetHeightAtPoint(targetPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref gZ)`.
  * При движении коня координаты орудия и тележки обновляются плавно. В походном виде боеприпасы (`projectile_pile`) скрыты, подножки прицела и огня заблокированы (`sp.SetDisabled(true)`).
* **Боевое Положение по `[F] Отцепить коня`**:
  * При приближении игрока на дистанцию `InteractRadius` (3.5м) выводится подсказка: `[F] Отцепить коня (Перевести в боевое положение)`.
  * По `[F]` клиент шлет `RequestDeployArtilleryMessage(engineId)`.
  * Сервер переводит орудие в боевое состояние: конь освобождается (`SetMaximumSpeedLimit(-1f, false)`), снаряды (`projectile_pile`) разворачиваются на грунте (`SetVisibilityExcludeParents(true)`), подножки станций наведения и огня активируются (`sp.SetDisabled(false)`). Орудие готово к полноценному осадном бою!
* **Вызов при Спавне**:
  * Обязательно переопределить `OnAgentBuild(Agent agent, Banner banner)` в `DWDN_FieldArtilleryBehavior`, вызывая `CheckAndSpawnArtilleryForAgent(agent)`. Спавн происходит на сервере с последующей рассылкой `SyncArtilleryDescendantsMessage` клиентам.

---

*Да пребудет логика в священном кремнии. Слава Омниссии!*

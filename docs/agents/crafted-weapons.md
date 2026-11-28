# The Mathematics of Crafted Weapons (Crafted Weapons Deltas)

> Part of the DWDN agent guide. See [AGENTS.md](../../AGENTS.md) for the entry point, build commands and the topic index.

Crafted modular weapons (swords, spears, axes) are described in `ModuleData/crafting_pieces.xml` and `crafting_templates.xml`.

```mermaid
flowchart LR
	Handle["Рукоять (Handle)"] -- "next_piece_offset" --> Guard["Гарда (Guard)"]
	Guard -- "next_piece_offset" --> Blade["Лезвие (Blade)"]
```

### 6.1. The Decompiled TaleWorlds Formula (`WeaponDesign.CalculatePivotDistances`)
When assembling the weapon mesh, the engine computes the position of each joint incrementally from the grip point:

$$\text{Position}_{n} = \text{Length}_{n-1} + (\text{previous\_piece\_offset}_{n} - \text{next\_piece\_offset}_{n-1})$$

*All values in the XML are given in centimeters, and the engine divides them by 100 to convert them to meters.*

### 6.2. Why Blender Coordinates Break the Game
* In Blender the modeler sees **absolute scene coordinates** (for example, the guard is located at $Z = 140\text{cm}$).
* If you write `140` into `previous_piece_offset`, the engine will add $1.4$ meters to the handle length, and the guard will fly off into the sky or trigger an endless physics-body assertion `Physics material bounding box error`.
* **Rule:** `crafting_pieces.xml` contains only the **relative docking deltas** between the edge of the handle mesh and the start of the guard mesh.

### 6.3. Reference Calibration of Finnard's Spear (`finnard_spear`)
Verified geometry that guarantees a perfect monolithic joint with no gaps and no crashes:
```xml
<!-- Рукоять: смещение стыка вверх к гарде -->
<CraftingPiece id="finnard_spear_handle" Type="Handle" 
			   next_piece_offset="-35" ... />

<!-- Гарда: входная дельта от рукояти (+7) и выходная к лезвию (-19) -->
<CraftingPiece id="finnard_speard_guard" Type="Guard" 
			   previous_piece_offset="7" next_piece_offset="-19" ... />

<!-- Наконечник: входная дельта стыка (+24) -->
<CraftingPiece id="finnard_spearhead" Type="Blade" 
			   previous_piece_offset="24" next_piece_offset="0" ... />
```

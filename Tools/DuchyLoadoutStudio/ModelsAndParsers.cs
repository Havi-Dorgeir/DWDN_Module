using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DuchyLoadoutStudio;

public enum SlotCategory
{
    HeadArmor = 0,
    Cape = 1,
    BodyArmor = 2,
    Gloves = 3,
    Legs = 4,
    Weapon0 = 5,
    Weapon1 = 6,
    Weapon2 = 7,
    Weapon3 = 8,
    Mount = 9
}

public class GameItemEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ItemType { get; set; } = ""; // HeadArmor, Cape, BodyArmor, HandArmor, LegArmor, Shield, OneHandedWeapon, TwoHandedWeapon, Polearm, Bow, Crossbow, Arrows, Bolts, Horse, Banner
    public string MeshName { get; set; } = "";
    public bool IsCustomModItem { get; set; }
    public int ArmorValue { get; set; }
    public float Weight { get; set; }
    public List<(string PieceId, float Scale)> CraftedPieces { get; set; } = new();

    public string DisplayLabel =>
        string.IsNullOrEmpty(Id)
            ? "∅ (Пустой слот — разрешить снять предмет)"
            : $"{(IsCustomModItem ? "★ [DWDN] " : "")}{Id} — {CleanLocName(Name)} [{ItemType}]";

    public static string CleanLocName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        int idx = raw.IndexOf('}');
        return idx >= 0 && idx + 1 < raw.Length ? raw.Substring(idx + 1).Trim() : raw;
    }
}

public class DuchyModel
{
    public string Id { get; set; } = "new_duchy";
    public string Name { get; set; } = "Новое Герцогство";
    public string Description { get; set; } = "Описание герцогства...";
    public string BannerCode { get; set; } = "11.14.14.1536.1536.768.768.1.0.0.160.0.0.512.512.768.768.1.0.0";
    public bool IsAvailable { get; set; } = true;
    public List<ClassModel> Classes { get; set; } = new();
}

public class SlotModel
{
    public SlotCategory Target { get; set; } = SlotCategory.Weapon0;
    public string Category { get; set; } = "Вооружение";
    public string Label { get; set; } = "Оружие";
    public bool Visible { get; set; } = true;
    public bool AllowEmpty { get; set; } = false;
    public List<string> Items { get; set; } = new();

    public SlotModel Clone() => new()
    {
        Target = Target,
        Category = Category,
        Label = Label,
        Visible = Visible,
        AllowEmpty = AllowEmpty,
        Items = new List<string>(Items)
    };

    public static string TargetToXmlName(SlotCategory cat) => cat switch
    {
        SlotCategory.HeadArmor => "Head",
        SlotCategory.Cape      => "Cape",
        SlotCategory.BodyArmor => "Body",
        SlotCategory.Gloves    => "Gloves",
        SlotCategory.Legs      => "Legs",
        SlotCategory.Weapon0   => "Weapon0",
        SlotCategory.Weapon1   => "Weapon1",
        SlotCategory.Weapon2   => "Weapon2",
        SlotCategory.Weapon3   => "Weapon3",
        SlotCategory.Mount     => "Horse",
        _ => "Weapon0"
    };

    public static bool TryParseXmlTarget(string raw, out SlotCategory cat)
    {
        cat = SlotCategory.Weapon0;
        switch ((raw ?? "").Trim().ToLowerInvariant())
        {
            case "head": case "headarmor": cat = SlotCategory.HeadArmor; return true;
            case "cape": cat = SlotCategory.Cape; return true;
            case "body": case "bodyarmor": cat = SlotCategory.BodyArmor; return true;
            case "gloves": case "handarmor": cat = SlotCategory.Gloves; return true;
            case "legs": case "leg": case "legarmor": cat = SlotCategory.Legs; return true;
            case "weapon0": case "item0": cat = SlotCategory.Weapon0; return true;
            case "weapon1": case "item1": cat = SlotCategory.Weapon1; return true;
            case "weapon2": case "item2": cat = SlotCategory.Weapon2; return true;
            case "weapon3": case "item3": cat = SlotCategory.Weapon3; return true;
            case "horse": case "mount": cat = SlotCategory.Mount; return true;
            default: return false;
        }
    }

    public static string DefaultCategoryFor(SlotCategory cat) => cat switch
    {
        SlotCategory.HeadArmor or SlotCategory.Cape or SlotCategory.BodyArmor or SlotCategory.Gloves or SlotCategory.Legs => "Броня",
        SlotCategory.Mount => "Верховая езда",
        _ => "Вооружение"
    };

    public static string DefaultLabelFor(SlotCategory cat) => cat switch
    {
        SlotCategory.HeadArmor => "Шлем",
        SlotCategory.Cape      => "Наплечники",
        SlotCategory.BodyArmor => "Доспех",
        SlotCategory.Gloves    => "Перчатки",
        SlotCategory.Legs      => "Обувь",
        SlotCategory.Weapon0   => "Оружие I",
        SlotCategory.Weapon1   => "Оружие II",
        SlotCategory.Weapon2   => "Оружие III",
        SlotCategory.Weapon3   => "Оружие IV",
        SlotCategory.Mount     => "Скакун",
        _ => cat.ToString()
    };
}

public class ClassModel
{
    public string Id { get; set; } = "mp_NewClass";
    public string Name { get; set; } = "Новый Класс";
    public string Description { get; set; } = "Описание боевой роли класса...";
    public string TroopId { get; set; } = "mp_NewClass_hero";
    public string IconSpriteName { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public string Group { get; set; } = "Infantry"; // Infantry, Ranged, Cavalry
    public string IconType { get; set; } = "Infantry_Heavy";
    public int Cost { get; set; } = 120;
    public int Armor { get; set; } = 25;
    public int Hitpoints { get; set; } = 100;
    public float MovementSpeed { get; set; } = 0.82f;
    public float CombatMovementSpeed { get; set; } = 0.85f;
    public float Acceleration { get; set; } = 2.1f;
    public float Multiplier { get; set; } = 0.70f;

    // Skills
    public int SkillOneHanded { get; set; } = 130;
    public int SkillTwoHanded { get; set; } = 120;
    public int SkillPolearm { get; set; } = 130;
    public int SkillBow { get; set; } = 60;
    public int SkillCrossbow { get; set; } = 60;
    public int SkillRiding { get; set; } = 100;

    // Dynamic ordered slots (primary model for ModuleData/dwdn_loadouts.xml)
    public List<SlotModel> Slots { get; set; } = new();

    // 10 Equipment Pools (kept synced with Slots for 3D Mannequin & legacy helpers)
    public Dictionary<SlotCategory, List<string>> Pools { get; set; } = new()
    {
        { SlotCategory.HeadArmor, new List<string>() },
        { SlotCategory.Cape,      new List<string>() },
        { SlotCategory.BodyArmor, new List<string>() },
        { SlotCategory.Gloves,    new List<string>() },
        { SlotCategory.Legs,      new List<string>() },
        { SlotCategory.Weapon0,   new List<string>() },
        { SlotCategory.Weapon1,   new List<string>() },
        { SlotCategory.Weapon2,   new List<string>() },
        { SlotCategory.Weapon3,   new List<string>() },
        { SlotCategory.Mount,     new List<string>() }
    };

    // Currently previewed item index per slot in the 3D Mannequin
    public Dictionary<SlotCategory, string> PreviewSelectedItem { get; set; } = new();

    public void SyncPoolsFromSlots()
    {
        foreach (var kv in Pools) kv.Value.Clear();
        foreach (var s in Slots)
        {
            Pools[s.Target].Clear();
            Pools[s.Target].AddRange(s.Items);
            if (s.AllowEmpty && !Pools[s.Target].Contains(""))
                Pools[s.Target].Add("");
        }
    }

    public void EnsureSlotsFromPools()
    {
        if (Slots.Count > 0) return;
        foreach (SlotCategory cat in Enum.GetValues(typeof(SlotCategory)))
        {
            if (Pools[cat].Count == 0) continue;
            Slots.Add(new SlotModel
            {
                Target = cat,
                Category = SlotModel.DefaultCategoryFor(cat),
                Label = SlotModel.DefaultLabelFor(cat),
                Visible = true,
                AllowEmpty = Pools[cat].Contains(""),
                Items = new List<string>(Pools[cat])
            });
        }
    }

    public string GetPreviewOrDefault(SlotCategory slot)
    {
        if (PreviewSelectedItem.TryGetValue(slot, out var sel) && Pools[slot].Contains(sel))
            return sel;
        return Pools[slot].FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";
    }

    public ClassModel Clone()
    {
        var c = new ClassModel
        {
            Id = Id + "_Copy",
            Name = Name + " (Копия)",
            Description = Description,
            TroopId = TroopId + "_Copy",
            IconSpriteName = IconSpriteName,
            IsEnabled = IsEnabled,
            Group = Group,
            IconType = IconType,
            Cost = Cost,
            Armor = Armor,
            Hitpoints = Hitpoints,
            MovementSpeed = MovementSpeed,
            CombatMovementSpeed = CombatMovementSpeed,
            Acceleration = Acceleration,
            Multiplier = Multiplier,
            SkillOneHanded = SkillOneHanded,
            SkillTwoHanded = SkillTwoHanded,
            SkillPolearm = SkillPolearm,
            SkillBow = SkillBow,
            SkillCrossbow = SkillCrossbow,
            SkillRiding = SkillRiding
        };
        foreach (var s in Slots)
            c.Slots.Add(s.Clone());
        c.SyncPoolsFromSlots();
        return c;
    }
}

public class Mesh3DData
{
    public string Name { get; set; } = "";
    public (float X, float Y, float Z)[] VerticesCm { get; set; } = Array.Empty<(float, float, float)>();
    public (int A, int B, int C)[] Triangles { get; set; } = Array.Empty<(int, int, int)>();
    public float MinZCm { get; set; }
    public float MaxZCm { get; set; }
}

public static class StudioDataLoader
{
    public static Dictionary<string, GameItemEntry> LoadAllItems(string modPath, string nativePath)
    {
        var dict = new Dictionary<string, GameItemEntry>(StringComparer.OrdinalIgnoreCase);

        // Add special empty slot entry
        dict[""] = new GameItemEntry
        {
            Id = "",
            Name = "Пустой слот (Снять предмет)",
            ItemType = "Any",
            IsCustomModItem = true
        };

        // 1. Load vanilla Native mpitems first (so custom DWDN items can cleanly override if needed)
        string nativeMpItemsFile = Path.Combine(nativePath, @"ModuleData\mpitems.xml");
        if (File.Exists(nativeMpItemsFile))
            ParseItemsXml(nativeMpItemsFile, isCustomMod: false, dict);

        string nativeItemsDir = Path.Combine(nativePath, @"ModuleData\mpitems");
        if (Directory.Exists(nativeItemsDir))
        {
            foreach (var xml in Directory.GetFiles(nativeItemsDir, "*.xml"))
                ParseItemsXml(xml, isCustomMod: false, dict);
        }

        // Also check Modules/Multiplayer/ModuleData/mpitems.xml if present
        string modulesDir = Directory.GetParent(nativePath)?.FullName ?? "";
        if (!string.IsNullOrEmpty(modulesDir))
        {
            string mpModuleFile = Path.Combine(modulesDir, @"Multiplayer\ModuleData\mpitems.xml");
            if (File.Exists(mpModuleFile))
                ParseItemsXml(mpModuleFile, isCustomMod: false, dict);
        }

        // 2. Load custom DWDN items second (marked with IsCustomModItem = true)
        string modItemsDir = Path.Combine(modPath, @"ModuleData\mpitems");
        if (Directory.Exists(modItemsDir))
        {
            foreach (var xml in Directory.GetFiles(modItemsDir, "*.xml"))
                ParseItemsXml(xml, isCustomMod: true, dict);
        }
        string rootItemsXml = Path.Combine(modPath, @"ModuleData\items.xml");
        if (File.Exists(rootItemsXml))
            ParseItemsXml(rootItemsXml, isCustomMod: true, dict);

        return dict;
    }

    private static void ParseItemsXml(string xmlPath, bool isCustomMod, Dictionary<string, GameItemEntry> dict)
    {
        try
        {
            var doc = XDocument.Load(xmlPath);
            foreach (var el in doc.Descendants("Item"))
            {
                string id = el.Attribute("id")?.Value ?? "";
                if (string.IsNullOrEmpty(id)) continue;

                string type = el.Attribute("Type")?.Value ?? "Unknown";
                string name = el.Attribute("name")?.Value ?? id;
                string mesh = el.Attribute("mesh")?.Value ?? "";
                float.TryParse(el.Attribute("weight")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float w);

                int armor = 0;
                var armEl = el.Descendants("Armor").FirstOrDefault();
                if (armEl != null)
                {
                    foreach (var attr in new[] { "head_armor", "body_armor", "leg_armor", "arm_armor" })
                    {
                        if (int.TryParse(armEl.Attribute(attr)?.Value, out int a) && a > armor)
                            armor = a;
                    }
                }

                dict[id] = new GameItemEntry
                {
                    Id = id,
                    Name = name,
                    ItemType = type,
                    MeshName = mesh,
                    IsCustomModItem = isCustomMod,
                    ArmorValue = armor,
                    Weight = w
                };
            }

            foreach (var el in doc.Descendants("CraftedItem"))
            {
                string id = el.Attribute("id")?.Value ?? "";
                if (string.IsNullOrEmpty(id)) continue;

                string name = el.Attribute("name")?.Value ?? id;
                string tmpl = el.Attribute("crafting_template")?.Value ?? "Weapon";
                var entry = new GameItemEntry
                {
                    Id = id,
                    Name = name,
                    ItemType = $"Crafted:{tmpl}",
                    IsCustomModItem = isCustomMod
                };

                foreach (var p in el.Descendants("Piece"))
                {
                    string pid = p.Attribute("id")?.Value ?? "";
                    float scale = 100f;
                    float.TryParse(p.Attribute("scale_factor")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out scale);
                    if (!string.IsNullOrEmpty(pid))
                        entry.CraftedPieces.Add((pid, scale * 0.01f));
                }

                dict[id] = entry;
            }
        }
        catch
        {
            // Ignore malformed XML
        }
    }

    /// <summary>
    /// Loads Duchies & Classes from ModuleData/dwdn_loadouts.xml (primary), falling back to DWDN_DuchyManager.cs if XML is absent.
    /// Enriches stats & skills from mpclassdivisions.xml and mpcharacters.xml.
    /// </summary>
    public static List<DuchyModel> LoadDuchiesAndClasses(string modPath)
    {
        string loadoutsXmlPath = Path.Combine(modPath, @"ModuleData\dwdn_loadouts.xml");
        string csPath = Path.Combine(modPath, @"ModuleData\scripts\DWDN_Script\Common\Duchies\DWDN_DuchyManager.cs");
        string divXmlPath = Path.Combine(modPath, @"ModuleData\mpclassdivisions.xml");
        string charXmlPath = Path.Combine(modPath, @"ModuleData\mpcharacters.xml");

        var duchies = new List<DuchyModel>();

        if (File.Exists(loadoutsXmlPath))
        {
            var doc = XDocument.Load(loadoutsXmlPath);
            var duchiesEl = doc.Root?.Element("Duchies") ?? doc.Root;
            if (duchiesEl != null)
            {
                foreach (var dEl in duchiesEl.Elements("Duchy"))
                {
                    var d = new DuchyModel
                    {
                        Id = dEl.Attribute("id")?.Value ?? "",
                        Name = dEl.Attribute("name")?.Value ?? "",
                        Description = dEl.Attribute("description")?.Value ?? "",
                        BannerCode = dEl.Attribute("banner_code")?.Value ?? "",
                        IsAvailable = !string.Equals(dEl.Attribute("enabled")?.Value, "false", StringComparison.OrdinalIgnoreCase)
                    };

                    foreach (var cEl in dEl.Elements("Class"))
                    {
                        var c = new ClassModel
                        {
                            Id = cEl.Attribute("id")?.Value ?? "",
                            Name = cEl.Attribute("name")?.Value ?? "",
                            Description = cEl.Attribute("description")?.Value ?? "",
                            TroopId = cEl.Attribute("troop_id")?.Value ?? "",
                            IconSpriteName = cEl.Attribute("icon")?.Value ?? "",
                            IsEnabled = !string.Equals(cEl.Attribute("enabled")?.Value, "false", StringComparison.OrdinalIgnoreCase)
                        };

                        foreach (var sEl in cEl.Elements("Slot"))
                        {
                            if (!SlotModel.TryParseXmlTarget(sEl.Attribute("target")?.Value ?? "", out var target))
                                continue;

                            var sm = new SlotModel
                            {
                                Target = target,
                                Category = sEl.Attribute("category")?.Value ?? SlotModel.DefaultCategoryFor(target),
                                Label = sEl.Attribute("label")?.Value ?? SlotModel.DefaultLabelFor(target),
                                Visible = !string.Equals(sEl.Attribute("visible")?.Value, "false", StringComparison.OrdinalIgnoreCase),
                                AllowEmpty = string.Equals(sEl.Attribute("allow_empty")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                            };

                            foreach (var iEl in sEl.Elements("Item"))
                            {
                                string itemId = iEl.Attribute("id")?.Value ?? "";
                                if (itemId.StartsWith("Item.", StringComparison.OrdinalIgnoreCase))
                                    itemId = itemId.Substring(5);
                                sm.Items.Add(itemId);
                                if (string.IsNullOrEmpty(itemId))
                                    sm.AllowEmpty = true;
                            }

                            c.Slots.Add(sm);
                        }

                        c.SyncPoolsFromSlots();
                        d.Classes.Add(c);
                    }

                    duchies.Add(d);
                }
            }
        }
        else if (File.Exists(csPath))
        {
            var varToDuchy = new Dictionary<string, DuchyModel>();
            var varToClass = new Dictionary<string, ClassModel>();
            string code = File.ReadAllText(csPath);

            var duchyRegex = new Regex(
                @"var\s+(\w+)\s*=\s*new\s+DWDN_Duchy\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*(true|false)\s*\)",
                RegexOptions.Multiline);
            foreach (Match m in duchyRegex.Matches(code))
            {
                var d = new DuchyModel
                {
                    Id = m.Groups[2].Value,
                    Name = m.Groups[3].Value,
                    Description = m.Groups[4].Value,
                    BannerCode = m.Groups[5].Value,
                    IsAvailable = m.Groups[6].Value == "true"
                };
                varToDuchy[m.Groups[1].Value] = d;
                duchies.Add(d);
            }

            var classRegex = new Regex(
                @"var\s+(\w+)\s*=\s*new\s+DWDN_ClassDefinition\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*\)",
                RegexOptions.Multiline);
            foreach (Match m in classRegex.Matches(code))
            {
                var c = new ClassModel
                {
                    Id = m.Groups[2].Value,
                    Name = m.Groups[3].Value,
                    Description = m.Groups[4].Value,
                    TroopId = m.Groups[5].Value
                };
                varToClass[m.Groups[1].Value] = c;
            }

            var poolMap = new Dictionary<string, SlotCategory>
            {
                { "AllowedHelmets",      SlotCategory.HeadArmor },
                { "AllowedCapes",        SlotCategory.Cape },
                { "AllowedBodyArmors",   SlotCategory.BodyArmor },
                { "AllowedGloves",       SlotCategory.Gloves },
                { "AllowedLegs",         SlotCategory.Legs },
                { "AllowedWeaponsSlot0", SlotCategory.Weapon0 },
                { "AllowedWeaponsSlot1", SlotCategory.Weapon1 },
                { "AllowedWeaponsSlot2", SlotCategory.Weapon2 },
                { "AllowedWeaponsSlot3", SlotCategory.Weapon3 },
                { "AllowedMounts",       SlotCategory.Mount }
            };

            var addRangeRegex = new Regex(@"(\w+)\.(Allowed\w+)\.AddRange\(\s*new\s*\[\]\s*\{([^\}]*)\}\s*\)", RegexOptions.Multiline);
            foreach (Match m in addRangeRegex.Matches(code))
            {
                string cVar = m.Groups[1].Value;
                string prop = m.Groups[2].Value;
                string itemsRaw = m.Groups[3].Value;

                if (varToClass.TryGetValue(cVar, out var cls) && poolMap.TryGetValue(prop, out var slot))
                {
                    foreach (Match im in Regex.Matches(itemsRaw, @"""([^""]*)"""))
                        cls.Pools[slot].Add(im.Groups[1].Value);
                }
            }

            var addClassRegex = new Regex(@"(\w+)\.Classes\.Add\((\w+)\)");
            foreach (Match m in addClassRegex.Matches(code))
            {
                if (varToDuchy.TryGetValue(m.Groups[1].Value, out var d) &&
                    varToClass.TryGetValue(m.Groups[2].Value, out var c))
                {
                    c.EnsureSlotsFromPools();
                    d.Classes.Add(c);
                }
            }
        }

        // Enrich with stats from mpclassdivisions.xml
        if (File.Exists(divXmlPath))
        {
            try
            {
                var doc = XDocument.Load(divXmlPath);
                var allClasses = duchies.SelectMany(d => d.Classes).ToList();
                foreach (var el in doc.Descendants("MPClassDivision"))
                {
                    string id = el.Attribute("id")?.Value ?? "";
                    string hero = el.Attribute("hero")?.Value ?? "";
                    var cls = allClasses.FirstOrDefault(c =>
                        string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.TroopId, hero, StringComparison.OrdinalIgnoreCase));
                    if (cls == null) continue;

                    if (int.TryParse(el.Attribute("cost")?.Value, out int cost)) cls.Cost = cost;
                    if (int.TryParse(el.Attribute("armor")?.Value, out int arm)) cls.Armor = arm;
                    if (int.TryParse(el.Attribute("hitpoints")?.Value, out int hp)) cls.Hitpoints = hp;
                    if (float.TryParse(el.Attribute("movement_speed")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float ms)) cls.MovementSpeed = ms;
                    if (float.TryParse(el.Attribute("combat_movement_speed")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float cms)) cls.CombatMovementSpeed = cms;
                    if (float.TryParse(el.Attribute("acceleration")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float acc)) cls.Acceleration = acc;
                    if (float.TryParse(el.Attribute("multiplier")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float mult)) cls.Multiplier = mult;
                    cls.IconType = el.Attribute("icon")?.Value ?? cls.IconType;
                }
            }
            catch { }
        }

        // Enrich with skills & default_group from mpcharacters.xml
        if (File.Exists(charXmlPath))
        {
            try
            {
                var doc = XDocument.Load(charXmlPath);
                var allClasses = duchies.SelectMany(d => d.Classes).ToList();
                foreach (var el in doc.Descendants("NPCCharacter"))
                {
                    string id = el.Attribute("id")?.Value ?? "";
                    var cls = allClasses.FirstOrDefault(c => string.Equals(c.TroopId, id, StringComparison.OrdinalIgnoreCase));
                    if (cls == null) continue;

                    cls.Group = el.Attribute("default_group")?.Value ?? cls.Group;
                    foreach (var sk in el.Descendants("skill"))
                    {
                        string sid = sk.Attribute("id")?.Value ?? "";
                        int.TryParse(sk.Attribute("value")?.Value, out int val);
                        switch (sid)
                        {
                            case "OneHanded": cls.SkillOneHanded = val; break;
                            case "TwoHanded": cls.SkillTwoHanded = val; break;
                            case "Polearm":   cls.SkillPolearm = val; break;
                            case "Bow":       cls.SkillBow = val; break;
                            case "Crossbow":  cls.SkillCrossbow = val; break;
                            case "Riding":    cls.SkillRiding = val; break;
                        }
                    }
                }
            }
            catch { }
        }

        return duchies;
    }

    /// <summary>
    /// Scans all _geo.tpac and .fbx files in DWDN_TRY so the 3D Warrior Mannequin can render all armor & weapon meshes.
    /// </summary>
    public static Dictionary<string, Mesh3DData> LoadAll3DMeshes(string modPath)
    {
        var dict = new Dictionary<string, Mesh3DData>(StringComparer.OrdinalIgnoreCase);
        string assetsDir = Path.Combine(modPath, "Assets");
        string sourcesDir = Path.Combine(modPath, "AssetSources");

        // Index all .fbx files in AssetSources by filename (without extension) for instant fallback lookup
        var fbxByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(sourcesDir))
        {
            foreach (var fbx in Directory.GetFiles(sourcesDir, "*.fbx", SearchOption.AllDirectories))
            {
                string key = Path.GetFileNameWithoutExtension(fbx);
                if (!fbxByName.ContainsKey(key))
                    fbxByName[key] = fbx;
            }
        }

        if (Directory.Exists(assetsDir))
        {
            foreach (var tpacFile in Directory.GetFiles(assetsDir, "*_geo.tpac", SearchOption.AllDirectories))
            {
                try
                {
                    byte[] b = File.ReadAllBytes(tpacFile);
                    if (b.Length < 280 || b[0] != 'T' || b[1] != 'P' || b[2] != 'A' || b[3] != 'C')
                        continue;

                    string fileKey = Path.GetFileNameWithoutExtension(tpacFile);
                    if (fileKey.EndsWith("_geo", StringComparison.OrdinalIgnoreCase))
                        fileKey = fileKey.Substring(0, fileKey.Length - 4);

                    string fbxRel = "";
                    string asciiHeader = Encoding.ASCII.GetString(b, 0, Math.Min(350, b.Length));
                    int baseIdx = asciiHeader.IndexOf("$BASE/", StringComparison.Ordinal);
                    if (baseIdx >= 4)
                    {
                        int strLen = BitConverter.ToInt32(b, baseIdx - 4);
                        if (strLen > 0 && baseIdx + strLen <= b.Length)
                            fbxRel = Encoding.ASCII.GetString(b, baseIdx, strLen);
                    }

                    for (int i = 120; i < b.Length - 170; i++)
                    {
                        bool allOne = true;
                        for (int k = 0; k < 8; k++)
                        {
                            if (BitConverter.ToSingle(b, i + k * 4) != 1.0f) { allOne = false; break; }
                        }
                        if (!allOne) continue;

                        int afterName = i - 24;
                        string subMeshName = fileKey;
                        for (int len = 1; len <= 64; len++)
                        {
                            int lenPos = afterName - len - 4;
                            if (lenPos > 0 && BitConverter.ToInt32(b, lenPos) == len)
                            {
                                subMeshName = Encoding.ASCII.GetString(b, lenPos + 4, len);
                                break;
                            }
                        }

                        float minX = BitConverter.ToSingle(b, afterName + 112) * 100f;
                        float minY = BitConverter.ToSingle(b, afterName + 116) * 100f;
                        float minZ = BitConverter.ToSingle(b, afterName + 120) * 100f;
                        float maxZ = BitConverter.ToSingle(b, afterName + 136) * 100f;

                        string resolvedFbx = "";
                        if (!string.IsNullOrEmpty(fbxRel))
                        {
                            string gameRoot = Path.GetFullPath(Path.Combine(modPath, @"..\.."));
                            resolvedFbx = Path.Combine(gameRoot, fbxRel.Replace("$BASE/", "").Replace('/', Path.DirectorySeparatorChar));
                        }
                        if (!File.Exists(resolvedFbx))
                        {
                            // Try direct mirror path in AssetSources
                            string relFromAssets = Path.GetRelativePath(assetsDir, tpacFile);
                            if (relFromAssets.EndsWith("_geo.tpac", StringComparison.OrdinalIgnoreCase))
                            {
                                string candidate = Path.Combine(sourcesDir, relFromAssets.Substring(0, relFromAssets.Length - 9) + ".fbx");
                                if (File.Exists(candidate)) resolvedFbx = candidate;
                            }
                        }
                        if (!File.Exists(resolvedFbx))
                        {
                            if (fbxByName.TryGetValue(fileKey, out var byFileKey)) resolvedFbx = byFileKey;
                            else if (fbxByName.TryGetValue(subMeshName, out var bySubName)) resolvedFbx = bySubName;
                        }

                        if (File.Exists(resolvedFbx))
                        {
                            var meshData = ReadFbxMesh(resolvedFbx, subMeshName, minX, minY, minZ, maxZ, hasTpacBounds: true);
                            if (meshData != null)
                            {
                                dict[fileKey] = meshData;
                                dict[subMeshName] = meshData;
                            }
                        }
                        break;
                    }
                }
                catch { }
            }
        }

        // Also load any remaining .fbx in AssetSources that didn't have a _geo.tpac match
        foreach (var kv in fbxByName)
        {
            if (dict.ContainsKey(kv.Key)) continue;
            try
            {
                var meshData = ReadFbxMesh(kv.Value, kv.Key, 0, 0, 0, 0, hasTpacBounds: false);
                if (meshData != null)
                    dict[kv.Key] = meshData;
            }
            catch { }
        }

        return dict;
    }

    private static Mesh3DData ReadFbxMesh(string fbxPath, string name, float tpacMinX, float tpacMinY, float tpacMinZ, float tpacMaxZ, bool hasTpacBounds)
    {
        using var fs = File.OpenRead(fbxPath);
        using var br = new BinaryReader(fs);
        byte[] header = br.ReadBytes(23);
        if (!Encoding.ASCII.GetString(header, 0, 20).StartsWith("Kaydara FBX Binary")) return null;

        uint version = br.ReadUInt32();
        var allGeoms = new List<(List<(double x, double y, double z)> verts, int[] polys)>();
        CollectAllGeometries(br, fs, version, allGeoms);
        if (allGeoms.Count == 0) return null;

        // Combine all geometry nodes in the FBX into a single combined mesh
        var combinedVerts = new List<(double x, double y, double z)>();
        var combinedTris = new List<(int, int, int)>();

        foreach (var (verts, polys) in allGeoms)
        {
            int baseIdx = combinedVerts.Count;
            combinedVerts.AddRange(verts);

            var poly = new List<int>(8);
            foreach (int rawIdx in polys)
            {
                bool isLast = rawIdx < 0;
                int vIdx = isLast ? (-rawIdx - 1) : rawIdx;
                if (vIdx >= 0 && vIdx < verts.Count)
                    poly.Add(baseIdx + vIdx);

                if (isLast)
                {
                    for (int k = 1; k + 1 < poly.Count; k++)
                        combinedTris.Add((poly[0], poly[k], poly[k + 1]));
                    poly.Clear();
                }
            }
        }

        if (combinedVerts.Count < 3 || combinedTris.Count == 0) return null;

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxZ = double.MinValue;
        double maxAbs = 0;
        foreach (var v in combinedVerts)
        {
            if (v.x < minX) minX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
            maxAbs = Math.Max(maxAbs, Math.Max(Math.Abs(v.x), Math.Max(Math.Abs(v.y), Math.Abs(v.z))));
        }

        // Determine unit scale (if raw FBX coordinates are in meters (~0.1..2.5), multiply by 100 for cm; if already in cm (~10..200), keep 1.0)
        float unitScale = maxAbs <= 6.0 ? 100.0f : 1.0f;

        float xShift = 0f, yShift = 0f, zShift = 0f;
        if (hasTpacBounds && Math.Abs(tpacMaxZ - tpacMinZ) > 0.5f)
        {
            float rawSpanZ = (float)((maxZ - minZ) * unitScale);
            float tpacSpanZ = tpacMaxZ - tpacMinZ;
            // If raw span was in cm/meters mismatch, adjust unitScale to match TPAC bounding box span
            if (rawSpanZ > 0.01f && (tpacSpanZ / rawSpanZ > 50f || tpacSpanZ / rawSpanZ < 0.02f))
            {
                unitScale = tpacSpanZ / (float)(maxZ - minZ);
            }
            xShift = tpacMinX - (float)(minX * unitScale);
            yShift = tpacMinY - (float)(minY * unitScale);
            zShift = tpacMinZ - (float)(minZ * unitScale);
        }

        var vertsCm = new (float X, float Y, float Z)[combinedVerts.Count];
        float finalMinZ = float.MaxValue, finalMaxZ = float.MinValue;
        for (int i = 0; i < combinedVerts.Count; i++)
        {
            float vx = (float)(combinedVerts[i].x * unitScale) + xShift;
            float vy = (float)(combinedVerts[i].y * unitScale) + yShift;
            float vz = (float)(combinedVerts[i].z * unitScale) + zShift;
            vertsCm[i] = (vx, vy, vz);
            if (vz < finalMinZ) finalMinZ = vz;
            if (vz > finalMaxZ) finalMaxZ = vz;
        }

        return new Mesh3DData
        {
            Name = name,
            VerticesCm = vertsCm,
            Triangles = combinedTris.ToArray(),
            MinZCm = finalMinZ,
            MaxZCm = finalMaxZ
        };
    }

    private static void CollectAllGeometries(BinaryReader br, Stream fs, uint version, List<(List<(double x, double y, double z)> verts, int[] polys)> results)
    {
        List<(double x, double y, double z)> localVerts = null;
        int[] localPolys = null;

        while (fs.Position < fs.Length)
        {
            long endOffset = version >= 7500 ? (long)br.ReadUInt64() : br.ReadUInt32();
            long numProps = version >= 7500 ? (long)br.ReadUInt64() : br.ReadUInt32();
            long propListLen = version >= 7500 ? (long)br.ReadUInt64() : br.ReadUInt32();
            byte nameLen = br.ReadByte();
            if (endOffset == 0 || endOffset > fs.Length) break;
            string name = Encoding.ASCII.GetString(br.ReadBytes(nameLen));
            long propStart = fs.Position;

            if (name == "Vertices")
            {
                char type = (char)br.ReadByte();
                int arrLen = br.ReadInt32();
                int enc = br.ReadInt32();
                int compLen = br.ReadInt32();
                byte[] dec = ReadFbxBytes(br, compLen, enc);
                localVerts = new List<(double x, double y, double z)>(arrLen / 3);
                using var vBr = new BinaryReader(new MemoryStream(dec));
                int count = arrLen / 3;
                for (int i = 0; i < count; i++)
                {
                    double x = type == 'd' ? vBr.ReadDouble() : vBr.ReadSingle();
                    double y = type == 'd' ? vBr.ReadDouble() : vBr.ReadSingle();
                    double z = type == 'd' ? vBr.ReadDouble() : vBr.ReadSingle();
                    localVerts.Add((x, y, z));
                }
            }
            else if (name == "PolygonVertexIndex" && localVerts != null)
            {
                char type = (char)br.ReadByte();
                int arrLen = br.ReadInt32();
                int enc = br.ReadInt32();
                int compLen = br.ReadInt32();
                byte[] dec = ReadFbxBytes(br, compLen, enc);
                localPolys = new int[arrLen];
                using var pBr = new BinaryReader(new MemoryStream(dec));
                for (int i = 0; i < arrLen; i++)
                    localPolys[i] = type == 'i' ? pBr.ReadInt32() : (int)pBr.ReadInt64();
                results.Add((localVerts, localPolys));
                localVerts = null;
                localPolys = null;
            }

            fs.Position = propStart + propListLen;
            if (fs.Position < endOffset)
            {
                CollectAllGeometries(br, fs, version, results);
            }
            fs.Position = endOffset;
        }
    }

    private static byte[] ReadFbxBytes(BinaryReader br, int compLen, int enc)
    {
        byte[] raw = br.ReadBytes(compLen);
        if (enc != 1) return raw;
        using var ms = new MemoryStream(raw);
        ms.ReadByte(); ms.ReadByte();
        using var ds = new DeflateStream(ms, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        ds.CopyTo(outMs);
        return outMs.ToArray();
    }

    /// <summary>
    /// Generates the complete, ready-to-compile DWDN_DuchyManager.cs source file from the current Duchies & Classes.
    /// </summary>
    public static string GenerateDuchyManagerCSharp(List<DuchyModel> duchies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace DWDN_Script.Common.Duchies");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Центральный менеджер герцогств и классов мода Daewden.");
        sb.AppendLine("    /// Автоматически сгенерировано в OMNISSIAH Duchy & Loadout Studio.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static class DWDN_DuchyManager");
        sb.AppendLine("    {");
        sb.AppendLine("        public static List<DWDN_Duchy> Duchies { get; } = new List<DWDN_Duchy>();");
        sb.AppendLine("        private static bool _initialized = false;");
        sb.AppendLine();
        sb.AppendLine("        public static void Initialize()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_initialized) return;");
        sb.AppendLine("            Duchies.Clear();");
        sb.AppendLine();

        int dIdx = 0;
        foreach (var d in duchies)
        {
            dIdx++;
            string dVar = SanitizeVarName(d.Id, $"duchy_{dIdx}");
            sb.AppendLine($"            // =========================================================================");
            sb.AppendLine($"            // ГЕРЦОГСТВО: {d.Name.ToUpperInvariant()} ({d.Id})");
            sb.AppendLine($"            // =========================================================================");
            sb.AppendLine($"            var {dVar} = new DWDN_Duchy(");
            sb.AppendLine($"                \"{EscapeCs(d.Id)}\",");
            sb.AppendLine($"                \"{EscapeCs(d.Name)}\",");
            sb.AppendLine($"                \"{EscapeCs(d.Description)}\",");
            sb.AppendLine($"                \"{EscapeCs(d.BannerCode)}\",");
            sb.AppendLine($"                {(d.IsAvailable ? "true" : "false")}");
            sb.AppendLine($"            );");
            sb.AppendLine();

            int cIdx = 0;
            foreach (var c in d.Classes)
            {
                cIdx++;
                string cVar = SanitizeVarName($"{dVar}_{c.Id}", $"cls_{dIdx}_{cIdx}");
                sb.AppendLine($"            // {cIdx}. {c.Name} ({c.Id})");
                sb.AppendLine($"            var {cVar} = new DWDN_ClassDefinition(");
                sb.AppendLine($"                \"{EscapeCs(c.Id)}\",");
                sb.AppendLine($"                \"{EscapeCs(c.Name)}\",");
                sb.AppendLine($"                \"{EscapeCs(c.Description)}\",");
                sb.AppendLine($"                \"{EscapeCs(c.TroopId)}\"");
                sb.AppendLine($"            );");

                AppendPoolLine(sb, cVar, "AllowedHelmets",      c.Pools[SlotCategory.HeadArmor]);
                AppendPoolLine(sb, cVar, "AllowedCapes",        c.Pools[SlotCategory.Cape]);
                AppendPoolLine(sb, cVar, "AllowedBodyArmors",   c.Pools[SlotCategory.BodyArmor]);
                AppendPoolLine(sb, cVar, "AllowedLegs",         c.Pools[SlotCategory.Legs]);
                AppendPoolLine(sb, cVar, "AllowedGloves",       c.Pools[SlotCategory.Gloves]);
                AppendPoolLine(sb, cVar, "AllowedWeaponsSlot0", c.Pools[SlotCategory.Weapon0]);
                AppendPoolLine(sb, cVar, "AllowedWeaponsSlot1", c.Pools[SlotCategory.Weapon1]);
                AppendPoolLine(sb, cVar, "AllowedWeaponsSlot2", c.Pools[SlotCategory.Weapon2]);
                AppendPoolLine(sb, cVar, "AllowedWeaponsSlot3", c.Pools[SlotCategory.Weapon3]);
                AppendPoolLine(sb, cVar, "AllowedMounts",       c.Pools[SlotCategory.Mount]);

                sb.AppendLine($"            {cVar}.DefaultLoadout = {cVar}.CreateDefaultLoadout({dVar}.Id);");
                sb.AppendLine($"            {dVar}.Classes.Add({cVar});");
                sb.AppendLine();
            }

            sb.AppendLine($"            Duchies.Add({dVar});");
            sb.AppendLine();
        }

        sb.AppendLine("            _initialized = true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static DWDN_Duchy GetDuchy(string duchyId)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (string.IsNullOrEmpty(duchyId)) return Duchies.Count > 0 ? Duchies[0] : null;");
        sb.AppendLine("            return Duchies.Find(d => d.Id.Equals(duchyId, StringComparison.OrdinalIgnoreCase))");
        sb.AppendLine("                   ?? (Duchies.Count > 0 ? Duchies[0] : null);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendPoolLine(StringBuilder sb, string cVar, string propName, List<string> items)
    {
        if (items == null || items.Count == 0) return;
        string joined = string.Join(", ", items.Select(x => $"\"{EscapeCs(x)}\""));
        sb.AppendLine($"            {cVar}.{propName}.AddRange(new[] {{ {joined} }});");
    }

    /// <summary>
    /// Generates synchronized XML snippets for mpclassdivisions.xml and mpcharacters.xml for the selected class.
    /// Automatically enforces the Anti-Scrapcode Cavalry Rule (never puts Horse in footman equipment).
    /// </summary>
    public static string GenerateClassXmlSnippets(ClassModel c)
    {
        var sb = new StringBuilder();
        string troopId = c.TroopId.EndsWith("_hero", StringComparison.OrdinalIgnoreCase)
            ? c.TroopId.Substring(0, c.TroopId.Length - 5) + "_troop"
            : c.TroopId + "_troop";

        sb.AppendLine("<!-- ===================================================================== -->");
        sb.AppendLine($"<!-- 1. ВСТАВИТЬ В ModuleData/mpclassdivisions.xml (Класс: {c.Name}) -->");
        sb.AppendLine("<!-- ===================================================================== -->");
        sb.AppendLine($"<MPClassDivision id=\"{c.Id}\" hero=\"{c.TroopId}\" troop=\"{troopId}\" hero_idle_anim=\"act_sturgia_mp_huskarl_idle\" troop_idle_anim=\"act_idle_unarmed_1\" multiplier=\"{c.Multiplier.ToString("0.00", CultureInfo.InvariantCulture)}\" cost=\"{c.Cost}\" icon=\"{c.IconType}\" melee_ai=\"50\" ranged_ai=\"40\" armor=\"{c.Armor}\" movement_speed=\"{c.MovementSpeed.ToString("0.00", CultureInfo.InvariantCulture)}\" combat_movement_speed=\"{c.CombatMovementSpeed.ToString("0.00", CultureInfo.InvariantCulture)}\" acceleration=\"{c.Acceleration.ToString("0.00", CultureInfo.InvariantCulture)}\" hitpoints=\"{c.Hitpoints}\">");
        sb.AppendLine("    <Perks>");
        sb.AppendLine("        <Perk game_mode=\"All\" name=\"Conditioning\" description=\"+5% Movement Speed\" icon=\"PerkFleetFooted\" perk_list=\"1\">");
        sb.AppendLine("            <Effect type=\"DrivenProperty\" driven_property=\"MaxSpeedMultiplier\" value=\"0.05\" />");
        sb.AppendLine("        </Perk>");
        sb.AppendLine("        <Perk game_mode=\"All\" name=\"Iron Hide\" description=\"+10 Armor\" icon=\"PerkToughness\" perk_list=\"1\">");
        sb.AppendLine("            <OnSpawnEffect type=\"ArmorOnSpawn\" value=\"10\" />");
        sb.AppendLine("        </Perk>");
        sb.AppendLine("    </Perks>");
        sb.AppendLine("</MPClassDivision>");
        sb.AppendLine();

        sb.AppendLine("<!-- ===================================================================== -->");
        sb.AppendLine($"<!-- 2. ВСТАВИТЬ В ModuleData/mpcharacters.xml (Персонаж: {c.TroopId}) -->");
        sb.AppendLine("<!-- ===================================================================== -->");
        sb.AppendLine($"<NPCCharacter id=\"{c.TroopId}\" default_group=\"{c.Group}\" level=\"25\" name=\"{{=*}}{c.Name}\" occupation=\"Soldier\" culture=\"Culture.dwdn\">");
        sb.AppendLine("    <face>");
        sb.AppendLine("        <BodyProperties version=\"4\" age=\"30\" weight=\"0.5\" build=\"0.5\" key=\"00000000010003000000000000067000000000000000000000000000000000000006660606000000000000000000000000000000000000000000000001180000\" />");
        sb.AppendLine("        <BodyPropertiesMax version=\"4\" age=\"50\" weight=\"0.7\" build=\"0.8\" key=\"0005FC059A0034D0FFFFFFFF887AFFFFFFFEFFFB6FB7FFFFFFFFFFFFFFFFFFFF000FAFFA0FFFFFFF000000000000000000000000000000000000000001680141\" />");
        sb.AppendLine("    </face>");
        sb.AppendLine("    <skills>");
        sb.AppendLine($"        <skill id=\"Riding\" value=\"{c.SkillRiding}\" />");
        sb.AppendLine($"        <skill id=\"OneHanded\" value=\"{c.SkillOneHanded}\" />");
        sb.AppendLine($"        <skill id=\"TwoHanded\" value=\"{c.SkillTwoHanded}\" />");
        sb.AppendLine($"        <skill id=\"Polearm\" value=\"{c.SkillPolearm}\" />");
        sb.AppendLine($"        <skill id=\"Crossbow\" value=\"{c.SkillCrossbow}\" />");
        sb.AppendLine($"        <skill id=\"Bow\" value=\"{c.SkillBow}\" />");
        sb.AppendLine("        <skill id=\"Throwing\" value=\"80\" />");
        sb.AppendLine("    </skills>");
        sb.AppendLine("    <Equipments>");
        sb.AppendLine("        <EquipmentRoster>");

        void AppendDefaultSlot(string xmlSlot, SlotCategory cat)
        {
            string firstItem = c.Pools[cat].FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";
            if (!string.IsNullOrEmpty(firstItem))
                sb.AppendLine($"            <equipment slot=\"{xmlSlot}\" id=\"Item.{firstItem}\" />");
        }

        AppendDefaultSlot("Head",   SlotCategory.HeadArmor);
        AppendDefaultSlot("Cape",   SlotCategory.Cape);
        AppendDefaultSlot("Body",   SlotCategory.BodyArmor);
        AppendDefaultSlot("Gloves", SlotCategory.Gloves);
        AppendDefaultSlot("Leg",    SlotCategory.Legs);
        AppendDefaultSlot("Item0",  SlotCategory.Weapon0);
        AppendDefaultSlot("Item1",  SlotCategory.Weapon1);
        AppendDefaultSlot("Item2",  SlotCategory.Weapon2);
        AppendDefaultSlot("Item3",  SlotCategory.Weapon3);

        if (string.Equals(c.Group, "Cavalry", StringComparison.OrdinalIgnoreCase))
        {
            AppendDefaultSlot("Horse", SlotCategory.Mount);
        }

        sb.AppendLine("        </EquipmentRoster>");
        sb.AppendLine("    </Equipments>");
        sb.AppendLine("</NPCCharacter>");
        return sb.ToString();
    }

    /// <summary>
    /// Updates existing MPClassDivision entries in ModuleData/mpclassdivisions.xml and appends any newly created classes.
    /// </summary>
    public static void PatchAndSaveMpClassDivisionsXml(string divXmlPath, List<DuchyModel> duchies)
    {
        XDocument doc = File.Exists(divXmlPath)
            ? XDocument.Load(divXmlPath)
            : new XDocument(new XElement("MPClassDivisions"));

        var root = doc.Root ?? new XElement("MPClassDivisions");
        if (doc.Root == null) doc.Add(root);

        var allClasses = duchies.SelectMany(d => d.Classes).ToList();
        foreach (var c in allClasses)
        {
            string troopId = c.TroopId.EndsWith("_hero", StringComparison.OrdinalIgnoreCase)
                ? c.TroopId.Substring(0, c.TroopId.Length - 5) + "_troop"
                : c.TroopId + "_troop";

            var existing = root.Elements("MPClassDivision").FirstOrDefault(el =>
                string.Equals(el.Attribute("id")?.Value, c.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(el.Attribute("hero")?.Value, c.TroopId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.SetAttributeValue("cost", c.Cost);
                existing.SetAttributeValue("armor", c.Armor);
                existing.SetAttributeValue("hitpoints", c.Hitpoints);
                existing.SetAttributeValue("movement_speed", c.MovementSpeed.ToString("0.00", CultureInfo.InvariantCulture));
                existing.SetAttributeValue("combat_movement_speed", c.CombatMovementSpeed.ToString("0.00", CultureInfo.InvariantCulture));
                existing.SetAttributeValue("acceleration", c.Acceleration.ToString("0.00", CultureInfo.InvariantCulture));
                existing.SetAttributeValue("icon", c.IconType);
            }
            else
            {
                var newDiv = new XElement("MPClassDivision",
                    new XAttribute("id", c.Id),
                    new XAttribute("hero", c.TroopId),
                    new XAttribute("troop", troopId),
                    new XAttribute("hero_idle_anim", "act_sturgia_mp_huskarl_idle"),
                    new XAttribute("troop_idle_anim", "act_idle_unarmed_1"),
                    new XAttribute("multiplier", c.Multiplier.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("cost", c.Cost),
                    new XAttribute("icon", c.IconType),
                    new XAttribute("melee_ai", 50),
                    new XAttribute("ranged_ai", 40),
                    new XAttribute("armor", c.Armor),
                    new XAttribute("movement_speed", c.MovementSpeed.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("combat_movement_speed", c.CombatMovementSpeed.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("acceleration", c.Acceleration.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("hitpoints", c.Hitpoints),
                    new XElement("Perks",
                        new XElement("Perk",
                            new XAttribute("game_mode", "All"),
                            new XAttribute("name", "Conditioning"),
                            new XAttribute("description", "+5% Movement Speed"),
                            new XAttribute("icon", "PerkFleetFooted"),
                            new XAttribute("perk_list", "1"),
                            new XElement("Effect",
                                new XAttribute("type", "DrivenProperty"),
                                new XAttribute("driven_property", "MaxSpeedMultiplier"),
                                new XAttribute("value", "0.05"))
                        ),
                        new XElement("Perk",
                            new XAttribute("game_mode", "All"),
                            new XAttribute("name", "Iron Hide"),
                            new XAttribute("description", "+10 Armor"),
                            new XAttribute("icon", "PerkToughness"),
                            new XAttribute("perk_list", "1"),
                            new XElement("OnSpawnEffect",
                                new XAttribute("type", "ArmorOnSpawn"),
                                new XAttribute("value", "10"))
                        )
                    )
                );
                root.Add(newDiv);
            }
        }

        doc.Save(divXmlPath);
    }

    /// <summary>
    /// Updates existing NPCCharacter entries in ModuleData/mpcharacters.xml and appends any newly created classes.
    /// Strictly enforces the Anti-Scrapcode Cavalry Rule (never puts Horse in footman EquipmentRoster).
    /// </summary>
    public static void PatchAndSaveMpCharactersXml(string charXmlPath, List<DuchyModel> duchies)
    {
        XDocument doc = File.Exists(charXmlPath)
            ? XDocument.Load(charXmlPath)
            : new XDocument(new XElement("MPCharacters"));

        var root = doc.Root ?? new XElement("MPCharacters");
        if (doc.Root == null) doc.Add(root);

        var allClasses = duchies.SelectMany(d => d.Classes).ToList();
        foreach (var c in allClasses)
        {
            string troopId = c.TroopId.EndsWith("_hero", StringComparison.OrdinalIgnoreCase)
                ? c.TroopId.Substring(0, c.TroopId.Length - 5) + "_troop"
                : c.TroopId + "_troop";

            UpsertCharacterNode(root, c, c.TroopId);
            UpsertCharacterNode(root, c, troopId);
        }

        doc.Save(charXmlPath);
    }

    private static void UpsertCharacterNode(XElement root, ClassModel c, string npcId)
    {
        var existing = root.Elements("NPCCharacter").FirstOrDefault(el =>
            string.Equals(el.Attribute("id")?.Value, npcId, StringComparison.OrdinalIgnoreCase));

        bool isNew = existing == null;
        if (existing == null)
        {
            existing = new XElement("NPCCharacter",
                new XAttribute("id", npcId),
                new XAttribute("default_group", c.Group),
                new XAttribute("level", "25"),
                new XAttribute("name", $"{{=*}}{c.Name}"),
                new XAttribute("occupation", "Soldier"),
                new XAttribute("culture", "Culture.dwdn"),
                new XElement("face",
                    new XElement("BodyProperties",
                        new XAttribute("version", "4"),
                        new XAttribute("age", "30"),
                        new XAttribute("weight", "0.5"),
                        new XAttribute("build", "0.5"),
                        new XAttribute("key", "00000000010003000000000000067000000000000000000000000000000000000006660606000000000000000000000000000000000000000000000001180000")
                    ),
                    new XElement("BodyPropertiesMax",
                        new XAttribute("version", "4"),
                        new XAttribute("age", "50"),
                        new XAttribute("weight", "0.7"),
                        new XAttribute("build", "0.8"),
                        new XAttribute("key", "0005FC059A0034D0FFFFFFFF887AFFFFFFFEFFFB6FB7FFFFFFFFFFFFFFFFFFFF000FAFFA0FFFFFFF000000000000000000000000000000000000000001680141")
                    )
                ),
                new XElement("skills"),
                new XElement("Equipments", new XElement("EquipmentRoster"))
            );
            root.Add(existing);
        }
        else
        {
            existing.SetAttributeValue("default_group", c.Group);
            existing.SetAttributeValue("name", $"{{=*}}{c.Name}");
        }

        // Update skills
        var skillsEl = existing.Element("skills");
        if (skillsEl == null)
        {
            skillsEl = new XElement("skills");
            existing.Add(skillsEl);
        }

        void SetSkill(string skillId, int val)
        {
            var sEl = skillsEl.Elements("skill").FirstOrDefault(x => string.Equals(x.Attribute("id")?.Value, skillId, StringComparison.OrdinalIgnoreCase));
            if (sEl != null) sEl.SetAttributeValue("value", val);
            else skillsEl.Add(new XElement("skill", new XAttribute("id", skillId), new XAttribute("value", val)));
        }

        SetSkill("Riding", c.SkillRiding);
        SetSkill("OneHanded", c.SkillOneHanded);
        SetSkill("TwoHanded", c.SkillTwoHanded);
        SetSkill("Polearm", c.SkillPolearm);
        SetSkill("Crossbow", c.SkillCrossbow);
        SetSkill("Bow", c.SkillBow);

        // Update default EquipmentRoster
        var eqsEl = existing.Element("Equipments") ?? new XElement("Equipments");
        if (existing.Element("Equipments") == null) existing.Add(eqsEl);
        var rosterEl = eqsEl.Element("EquipmentRoster") ?? new XElement("EquipmentRoster");
        if (eqsEl.Element("EquipmentRoster") == null) eqsEl.Add(rosterEl);

        // Preserve any existing HorseHarness if Cavalry
        string existingHarness = rosterEl.Elements("equipment")
            .FirstOrDefault(x => string.Equals(x.Attribute("slot")?.Value, "HorseHarness", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("id")?.Value ?? "";

        rosterEl.RemoveAll();

        void AddSlotIfPresent(string xmlSlot, SlotCategory cat)
        {
            string first = c.Pools[cat].FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";
            if (!string.IsNullOrEmpty(first))
            {
                string itemRef = first.StartsWith("Item.", StringComparison.OrdinalIgnoreCase) ? first : $"Item.{first}";
                rosterEl.Add(new XElement("equipment", new XAttribute("slot", xmlSlot), new XAttribute("id", itemRef)));
            }
        }

        AddSlotIfPresent("Head",   SlotCategory.HeadArmor);
        AddSlotIfPresent("Cape",   SlotCategory.Cape);
        AddSlotIfPresent("Body",   SlotCategory.BodyArmor);
        AddSlotIfPresent("Gloves", SlotCategory.Gloves);
        AddSlotIfPresent("Leg",    SlotCategory.Legs);
        AddSlotIfPresent("Item0",  SlotCategory.Weapon0);
        AddSlotIfPresent("Item1",  SlotCategory.Weapon1);
        AddSlotIfPresent("Item2",  SlotCategory.Weapon2);
        AddSlotIfPresent("Item3",  SlotCategory.Weapon3);

        if (string.Equals(c.Group, "Cavalry", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Group, "HorseArcher", StringComparison.OrdinalIgnoreCase))
        {
            AddSlotIfPresent("Horse", SlotCategory.Mount);
            if (!string.IsNullOrEmpty(existingHarness))
            {
                rosterEl.Add(new XElement("equipment", new XAttribute("slot", "HorseHarness"), new XAttribute("id", existingHarness)));
            }
        }
    }

    public static string ResolveModRoot()
    {
        // Walk up from executable directory looking for SubModule.xml
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "SubModule.xml")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        string cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "SubModule.xml")))
            return cwd;

        return @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY";
    }

    public static string ResolveNativeRoot(string modRoot)
    {
        try
        {
            string modulesDir = Directory.GetParent(modRoot)?.FullName;
            if (!string.IsNullOrEmpty(modulesDir))
            {
                string candidate = Path.Combine(modulesDir, "Native");
                if (Directory.Exists(candidate)) return candidate;
            }
        }
        catch { }
        return @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native";
    }

    public static string ResolveDedicatedServerModRoot(string modRoot)
    {
        try
        {
            // <steamapps/common>/Mount & Blade II Bannerlord/Modules/DWDN_TRY
            string commonDir = Directory.GetParent(modRoot)?.Parent?.Parent?.FullName;
            if (!string.IsNullOrEmpty(commonDir))
            {
                string candidate = Path.Combine(commonDir, "Mount & Blade II Dedicated Server", "Modules", "DaewdenM");
                if (Directory.Exists(candidate)) return candidate;
            }
        }
        catch { }
        return null;
    }

    public static XDocument BuildLoadoutsXDocument(List<DuchyModel> duchies)
    {
        var duchiesEl = new XElement("Duchies");
        foreach (var d in duchies)
        {
            var dEl = new XElement("Duchy",
                new XAttribute("id", d.Id ?? ""),
                new XAttribute("name", d.Name ?? ""),
                new XAttribute("description", d.Description ?? ""),
                new XAttribute("banner_code", d.BannerCode ?? ""),
                new XAttribute("enabled", d.IsAvailable ? "true" : "false")
            );

            foreach (var c in d.Classes)
            {
                c.SyncPoolsFromSlots();
                var cEl = new XElement("Class",
                    new XAttribute("id", c.Id ?? ""),
                    new XAttribute("name", c.Name ?? ""),
                    new XAttribute("description", c.Description ?? ""),
                    new XAttribute("troop_id", c.TroopId ?? ""),
                    new XAttribute("icon", c.IconSpriteName ?? ""),
                    new XAttribute("enabled", c.IsEnabled ? "true" : "false")
                );

                foreach (var s in c.Slots)
                {
                    bool allowEmpty = s.AllowEmpty || s.Items.Contains("");
                    var sEl = new XElement("Slot",
                        new XAttribute("target", SlotModel.TargetToXmlName(s.Target)),
                        new XAttribute("category", string.IsNullOrWhiteSpace(s.Category) ? SlotModel.DefaultCategoryFor(s.Target) : s.Category.Trim()),
                        new XAttribute("label", string.IsNullOrWhiteSpace(s.Label) ? SlotModel.DefaultLabelFor(s.Target) : s.Label.Trim()),
                        new XAttribute("visible", s.Visible ? "true" : "false"),
                        new XAttribute("allow_empty", allowEmpty ? "true" : "false")
                    );

                    foreach (var itemId in s.Items)
                    {
                        string clean = itemId ?? "";
                        if (clean.StartsWith("Item.", StringComparison.OrdinalIgnoreCase))
                            clean = clean.Substring(5);
                        sEl.Add(new XElement("Item", new XAttribute("id", clean)));
                    }

                    if (allowEmpty && !s.Items.Contains(""))
                    {
                        sEl.Add(new XElement("Item", new XAttribute("id", "")));
                    }

                    cEl.Add(sEl);
                }

                dEl.Add(cEl);
            }

            duchiesEl.Add(dEl);
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("DWDNLoadouts", new XAttribute("version", "1"), duchiesEl)
        );
    }

    public static string GenerateLoadoutsXml(List<DuchyModel> duchies)
    {
        var doc = BuildLoadoutsXDocument(duchies);
        using var sw = new StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    public static string SaveLoadoutsXmlAtomic(string xmlPath, string backupDir, List<DuchyModel> duchies)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
        Directory.CreateDirectory(backupDir);

        string backupFile = "";
        if (File.Exists(xmlPath))
        {
            backupFile = Path.Combine(backupDir, $"dwdn_loadouts_{DateTime.Now:yyyyMMdd_HHmmss}.xml.bak");
            File.Copy(xmlPath, backupFile, overwrite: true);
        }

        string tmpPath = xmlPath + ".tmp";
        var doc = BuildLoadoutsXDocument(duchies);
        doc.Save(tmpPath);
        File.Move(tmpPath, xmlPath, overwrite: true);
        return backupFile;
    }

    public static string GetLatestBackupPath(string backupDir)
    {
        if (!Directory.Exists(backupDir)) return null;
        return Directory.GetFiles(backupDir, "dwdn_loadouts_*.xml.bak")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static List<(bool IsError, string Message)> ValidateLoadouts(List<DuchyModel> duchies, Dictionary<string, GameItemEntry> catalog)
    {
        var issues = new List<(bool IsError, string Message)>();
        var seenClassIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in duchies)
        {
            if (string.IsNullOrWhiteSpace(d.Id))
                issues.Add((true, $"Герцогство '{d.Name}' имеет пустой ID."));

            foreach (var c in d.Classes)
            {
                if (string.IsNullOrWhiteSpace(c.Id))
                    issues.Add((true, $"Класс '{c.Name}' в герцогстве '{d.Name}' имеет пустой ID."));
                else if (!seenClassIds.Add(c.Id))
                    issues.Add((true, $"Дублирующийся ID класса: '{c.Id}'."));

                var usedTargets = new HashSet<SlotCategory>();
                foreach (var s in c.Slots)
                {
                    if (!usedTargets.Add(s.Target))
                        issues.Add((true, $"Класс '{c.Name}': физический слот '{SlotModel.TargetToXmlName(s.Target)}' добавлен дважды! Удалите дубликат."));

                    if (s.Items.Count == 0)
                        issues.Add((false, $"Класс '{c.Name}', слот '{s.Label}': пустой список предметов."));

                    foreach (var id in s.Items)
                    {
                        if (string.IsNullOrEmpty(id)) continue;
                        if (id.StartsWith("Item.", StringComparison.OrdinalIgnoreCase))
                            issues.Add((true, $"Класс '{c.Name}', слот '{s.Label}': запрещённый префикс 'Item.' в '{id}'."));
                        else if (catalog.Count > 1 && !catalog.ContainsKey(id))
                            issues.Add((false, $"Класс '{c.Name}', слот '{s.Label}': предмет '{id}' не найден в локальном каталоге DWDN/Native (проверьте ID)."));
                    }
                }

                if (c.IsEnabled)
                {
                    var bodySlot = c.Slots.FirstOrDefault(s => s.Target == SlotCategory.BodyArmor);
                    if (bodySlot == null || bodySlot.Items.Count == 0 || string.IsNullOrEmpty(bodySlot.Items[0]))
                        issues.Add((false, $"Класс '{c.Name}': нет базового доспеха тела (персонаж может заспавниться голым)."));
                }
            }
        }

        return issues;
    }

    private static string SanitizeVarName(string raw, string fallback)
    {
        string clean = Regex.Replace(raw ?? "", @"[^a-zA-Z0-9_]", "_").ToLowerInvariant();
        if (string.IsNullOrEmpty(clean) || char.IsDigit(clean[0]))
            clean = fallback;
        return clean;
    }

    private static string EscapeCs(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}

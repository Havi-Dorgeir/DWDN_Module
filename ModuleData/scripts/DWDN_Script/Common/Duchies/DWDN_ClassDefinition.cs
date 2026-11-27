using System;
using System.Collections.Generic;

namespace DWDN_Script.Common.Duchies
{
    /// <summary>
    /// Физический слот экипировки движка Bannerlord, к которому привязана строка в UI.
    /// </summary>
    public enum DWDN_SlotTarget
    {
        Head,
        Cape,
        Body,
        Gloves,
        Legs,
        Weapon0,
        Weapon1,
        Weapon2,
        Weapon3,
        Horse
    }

    /// <summary>
    /// Описание одного настраиваемого слота экипировки класса (из ModuleData/dwdn_loadouts.xml).
    /// Позволяет произвольно добавлять/удалять слоты, задавать им категорию, название, порядок и пул предметов.
    /// </summary>
    public class DWDN_SlotDefinition
    {
        public DWDN_SlotTarget Target { get; set; }
        public string Category { get; set; } = "Снаряжение";
        public string Label { get; set; } = "";
        public bool Visible { get; set; } = true;
        public bool AllowEmpty { get; set; } = false;
        public List<string> AllowedItems { get; } = new List<string>();

        public DWDN_SlotDefinition() { }

        public DWDN_SlotDefinition(DWDN_SlotTarget target, string category, string label, bool visible = true, bool allowEmpty = false)
        {
            Target = target;
            Category = string.IsNullOrEmpty(category) ? GetDefaultCategory(target) : category;
            Label = string.IsNullOrEmpty(label) ? GetDefaultLabel(target) : label;
            Visible = visible;
            AllowEmpty = allowEmpty;
        }

        public static string GetDefaultCategory(DWDN_SlotTarget target)
        {
            switch (target)
            {
                case DWDN_SlotTarget.Head:
                case DWDN_SlotTarget.Cape:
                case DWDN_SlotTarget.Body:
                case DWDN_SlotTarget.Gloves:
                case DWDN_SlotTarget.Legs:
                    return "Броня";
                case DWDN_SlotTarget.Horse:
                    return "Верховая езда";
                default:
                    return "Вооружение";
            }
        }

        public static string GetDefaultLabel(DWDN_SlotTarget target)
        {
            switch (target)
            {
                case DWDN_SlotTarget.Head: return "Шлем";
                case DWDN_SlotTarget.Cape: return "Наплечники";
                case DWDN_SlotTarget.Body: return "Доспех";
                case DWDN_SlotTarget.Gloves: return "Перчатки";
                case DWDN_SlotTarget.Legs: return "Обувь";
                case DWDN_SlotTarget.Weapon0: return "Оружие I";
                case DWDN_SlotTarget.Weapon1: return "Оружие II";
                case DWDN_SlotTarget.Weapon2: return "Оружие III";
                case DWDN_SlotTarget.Weapon3: return "Оружие IV";
                case DWDN_SlotTarget.Horse: return "Скакун";
                default: return target.ToString();
            }
        }

        public static bool TryParseTarget(string raw, out DWDN_SlotTarget target)
        {
            target = DWDN_SlotTarget.Head;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "head":
                case "headarmor":
                    target = DWDN_SlotTarget.Head; return true;
                case "cape":
                    target = DWDN_SlotTarget.Cape; return true;
                case "body":
                case "bodyarmor":
                    target = DWDN_SlotTarget.Body; return true;
                case "gloves":
                case "handarmor":
                    target = DWDN_SlotTarget.Gloves; return true;
                case "legs":
                case "leg":
                case "legarmor":
                    target = DWDN_SlotTarget.Legs; return true;
                case "weapon0":
                case "item0":
                    target = DWDN_SlotTarget.Weapon0; return true;
                case "weapon1":
                case "item1":
                    target = DWDN_SlotTarget.Weapon1; return true;
                case "weapon2":
                case "item2":
                    target = DWDN_SlotTarget.Weapon2; return true;
                case "weapon3":
                case "item3":
                    target = DWDN_SlotTarget.Weapon3; return true;
                case "horse":
                case "mount":
                    target = DWDN_SlotTarget.Horse; return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Описание класса внутри герцогства (пехотинец, стрелок, рыцарь и т.д.)
    /// Содержит упорядоченный список настраиваемых слотов (Slots) и обратно-совместимые пулы (Allowed*).
    /// </summary>
    public class DWDN_ClassDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TroopId { get; set; }
        public string IconSpriteName { get; set; }
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Динамический упорядоченный список слотов и категорий класса (источник правды для UI и сервера).
        /// </summary>
        public List<DWDN_SlotDefinition> Slots { get; } = new List<DWDN_SlotDefinition>();

        // Обратно-совместимые пулы доступных предметов по слотам
        public List<string> AllowedHelmets { get; } = new List<string>();
        public List<string> AllowedCapes { get; } = new List<string>();
        public List<string> AllowedBodyArmors { get; } = new List<string>();
        public List<string> AllowedGloves { get; } = new List<string>();
        public List<string> AllowedLegs { get; } = new List<string>();

        // Оружие
        public List<string> AllowedWeaponsSlot0 { get; } = new List<string>();
        public List<string> AllowedWeaponsSlot1 { get; } = new List<string>();
        public List<string> AllowedWeaponsSlot2 { get; } = new List<string>();
        public List<string> AllowedWeaponsSlot3 { get; } = new List<string>();

        // Скакуны (для кавалерии: "mp_vlandia_horse", "" - без коня)
        public List<string> AllowedMounts { get; } = new List<string>();

        public DWDN_LoadoutSelection DefaultLoadout { get; set; }

        public DWDN_ClassDefinition(string id, string name, string description, string troopId, string icon = "")
        {
            Id = id;
            Name = name;
            Description = description;
            TroopId = troopId;
            IconSpriteName = icon;
        }

        public List<string> GetLegacyPool(DWDN_SlotTarget target)
        {
            switch (target)
            {
                case DWDN_SlotTarget.Head: return AllowedHelmets;
                case DWDN_SlotTarget.Cape: return AllowedCapes;
                case DWDN_SlotTarget.Body: return AllowedBodyArmors;
                case DWDN_SlotTarget.Gloves: return AllowedGloves;
                case DWDN_SlotTarget.Legs: return AllowedLegs;
                case DWDN_SlotTarget.Weapon0: return AllowedWeaponsSlot0;
                case DWDN_SlotTarget.Weapon1: return AllowedWeaponsSlot1;
                case DWDN_SlotTarget.Weapon2: return AllowedWeaponsSlot2;
                case DWDN_SlotTarget.Weapon3: return AllowedWeaponsSlot3;
                case DWDN_SlotTarget.Horse: return AllowedMounts;
                default: return new List<string>();
            }
        }

        public DWDN_SlotDefinition GetSlot(DWDN_SlotTarget target)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Target == target)
                    return Slots[i];
            }
            return null;
        }

        /// <summary>
        /// Синхронизирует устаревшие списки Allowed* из нового списка Slots (после загрузки из XML).
        /// </summary>
        public void SyncLegacyPoolsFromSlots()
        {
            AllowedHelmets.Clear();
            AllowedCapes.Clear();
            AllowedBodyArmors.Clear();
            AllowedGloves.Clear();
            AllowedLegs.Clear();
            AllowedWeaponsSlot0.Clear();
            AllowedWeaponsSlot1.Clear();
            AllowedWeaponsSlot2.Clear();
            AllowedWeaponsSlot3.Clear();
            AllowedMounts.Clear();

            foreach (var slot in Slots)
            {
                var pool = GetLegacyPool(slot.Target);
                pool.Clear();
                foreach (var item in slot.AllowedItems)
                {
                    pool.Add(item ?? "");
                }
                if (slot.AllowEmpty && !pool.Contains(""))
                {
                    pool.Add("");
                }
            }
        }

        /// <summary>
        /// Заполняет Slots из legacy-пулов Allowed*, если класс был создан в C# (fallback-режим).
        /// </summary>
        public void EnsureSlotsFromLegacyPools()
        {
            if (Slots.Count > 0) return;

            var orderedTargets = new[]
            {
                DWDN_SlotTarget.Head,
                DWDN_SlotTarget.Cape,
                DWDN_SlotTarget.Body,
                DWDN_SlotTarget.Gloves,
                DWDN_SlotTarget.Legs,
                DWDN_SlotTarget.Weapon0,
                DWDN_SlotTarget.Weapon1,
                DWDN_SlotTarget.Weapon2,
                DWDN_SlotTarget.Weapon3,
                DWDN_SlotTarget.Horse
            };

            foreach (var target in orderedTargets)
            {
                var pool = GetLegacyPool(target);
                if (pool.Count == 0) continue;

                bool allowEmpty = pool.Contains("");
                var slotDef = new DWDN_SlotDefinition(
                    target,
                    DWDN_SlotDefinition.GetDefaultCategory(target),
                    DWDN_SlotDefinition.GetDefaultLabel(target),
                    visible: true,
                    allowEmpty: allowEmpty
                );
                slotDef.AllowedItems.AddRange(pool);
                Slots.Add(slotDef);
            }
        }

        /// <summary>
        /// Проверяет, разрешён ли указанный предмет в данном слоте для этого класса (серверный whitelist).
        /// </summary>
        public bool IsItemAllowed(DWDN_SlotTarget target, string itemId)
        {
            string normalized = itemId ?? "";
            var slot = GetSlot(target);
            if (slot != null)
            {
                if (string.IsNullOrEmpty(normalized))
                {
                    return slot.AllowEmpty || slot.AllowedItems.Contains("") || slot.AllowedItems.Count == 0;
                }
                for (int i = 0; i < slot.AllowedItems.Count; i++)
                {
                    if (string.Equals(slot.AllowedItems[i], normalized, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            // Если слот отсутствует у класса — разрешён только пустой слот
            var legacy = GetLegacyPool(target);
            if (legacy.Count == 0)
            {
                return string.IsNullOrEmpty(normalized);
            }
            for (int i = 0; i < legacy.Count; i++)
            {
                if (string.Equals(legacy[i] ?? "", normalized, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Возвращает безопасный предмет для слота: если присланный не разрешён, подставляет дефолтный.
        /// </summary>
        public string SanitizeSlotItem(DWDN_SlotTarget target, string requestedItemId, out bool wasRejected)
        {
            string normalized = requestedItemId ?? "";
            if (IsItemAllowed(target, normalized))
            {
                wasRejected = false;
                return normalized;
            }

            wasRejected = true;
            var slot = GetSlot(target);
            if (slot != null && slot.AllowedItems.Count > 0)
            {
                return slot.AllowedItems[0] ?? "";
            }
            var legacy = GetLegacyPool(target);
            if (legacy.Count > 0)
            {
                return legacy[0] ?? "";
            }
            return "";
        }

        public DWDN_LoadoutSelection CreateDefaultLoadout(string duchyId)
        {
            EnsureSlotsFromLegacyPools();

            string FirstOrEmpty(DWDN_SlotTarget target)
            {
                var slot = GetSlot(target);
                if (slot != null && slot.AllowedItems.Count > 0)
                    return slot.AllowedItems[0] ?? "";
                var pool = GetLegacyPool(target);
                return pool.Count > 0 ? (pool[0] ?? "") : "";
            }

            var loadout = new DWDN_LoadoutSelection
            {
                DuchyId = duchyId,
                ClassId = Id,
                HeadId = FirstOrEmpty(DWDN_SlotTarget.Head),
                CapeId = FirstOrEmpty(DWDN_SlotTarget.Cape),
                BodyId = FirstOrEmpty(DWDN_SlotTarget.Body),
                GlovesId = FirstOrEmpty(DWDN_SlotTarget.Gloves),
                LegsId = FirstOrEmpty(DWDN_SlotTarget.Legs),
                Weapon0Id = FirstOrEmpty(DWDN_SlotTarget.Weapon0),
                Weapon1Id = FirstOrEmpty(DWDN_SlotTarget.Weapon1),
                Weapon2Id = FirstOrEmpty(DWDN_SlotTarget.Weapon2),
                Weapon3Id = FirstOrEmpty(DWDN_SlotTarget.Weapon3),
                HorseId = FirstOrEmpty(DWDN_SlotTarget.Horse)
            };

            return loadout;
        }
    }
}

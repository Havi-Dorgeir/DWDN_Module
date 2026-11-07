using System;
using System.Collections.Generic;

namespace DWDN_Script.Common.Duchies
{
    /// <summary>
    /// Описание класса внутри герцогства (пехотинец, стрелок, рыцарь и т.д.)
    /// Содержит пулы разрешённых предметов для каждого слота.
    /// </summary>
    public class DWDN_ClassDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TroopId { get; set; }
        public string IconSpriteName { get; set; }

        // Пулы доступных предметов по слотам
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

        public DWDN_LoadoutSelection CreateDefaultLoadout(string duchyId)
        {
            var loadout = new DWDN_LoadoutSelection
            {
                DuchyId = duchyId,
                ClassId = Id,
                HeadId = AllowedHelmets.Count > 0 ? AllowedHelmets[0] : "",
                CapeId = AllowedCapes.Count > 0 ? AllowedCapes[0] : "",
                BodyId = AllowedBodyArmors.Count > 0 ? AllowedBodyArmors[0] : "",
                GlovesId = AllowedGloves.Count > 0 ? AllowedGloves[0] : "",
                LegsId = AllowedLegs.Count > 0 ? AllowedLegs[0] : "",
                Weapon0Id = AllowedWeaponsSlot0.Count > 0 ? AllowedWeaponsSlot0[0] : "",
                Weapon1Id = AllowedWeaponsSlot1.Count > 0 ? AllowedWeaponsSlot1[0] : "",
                Weapon2Id = AllowedWeaponsSlot2.Count > 0 ? AllowedWeaponsSlot2[0] : "",
                Weapon3Id = AllowedWeaponsSlot3.Count > 0 ? AllowedWeaponsSlot3[0] : "",
                HorseId = AllowedMounts.Count > 0 ? AllowedMounts[0] : ""
            };

            return loadout;
        }
    }
}

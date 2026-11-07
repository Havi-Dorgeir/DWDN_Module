using System;

namespace DWDN_Script.Common.Duchies
{
    /// <summary>
    /// Представляет набор снаряжения, выбранный игроком для конкретного класса.
    /// </summary>
    public class DWDN_LoadoutSelection
    {
        public string DuchyId { get; set; } = "banreich";
        public string ClassId { get; set; } = "banreich_infantry";

        // Слоты брони
        public string HeadId { get; set; } = "";
        public string CapeId { get; set; } = "";
        public string BodyId { get; set; } = "";
        public string GlovesId { get; set; } = "";
        public string LegsId { get; set; } = "";

        // Слоты оружия (0..3)
        public string Weapon0Id { get; set; } = "";
        public string Weapon1Id { get; set; } = "";
        public string Weapon2Id { get; set; } = "";
        public string Weapon3Id { get; set; } = "";

        // Слот скакуна ("" = пеший бой без коня)
        public string HorseId { get; set; } = "";

        public DWDN_LoadoutSelection()
        {
        }

        public DWDN_LoadoutSelection Clone()
        {
            return new DWDN_LoadoutSelection
            {
                DuchyId = this.DuchyId,
                ClassId = this.ClassId,
                HeadId = this.HeadId,
                CapeId = this.CapeId,
                BodyId = this.BodyId,
                GlovesId = this.GlovesId,
                LegsId = this.LegsId,
                Weapon0Id = this.Weapon0Id,
                Weapon1Id = this.Weapon1Id,
                Weapon2Id = this.Weapon2Id,
                Weapon3Id = this.Weapon3Id,
                HorseId = this.HorseId
            };
        }
    }
}

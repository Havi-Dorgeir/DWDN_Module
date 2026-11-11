using System;
using System.Collections.Generic;

namespace DWDN_Script.Common.Duchies
{
    /// <summary>
    /// Центральный менеджер герцогств и классов мода Daewden.
    /// Содержит полный каноничный список классов DWDN с правильным распределением снаряжения.
    /// </summary>
    public static class DWDN_DuchyManager
    {
        public static List<DWDN_Duchy> Duchies { get; } = new List<DWDN_Duchy>();
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            Duchies.Clear();

            // =========================================================================
            // ГЕРЦОГСТВО БАННРАЙХ (Banreich) - Основная фракция DWDN
            // =========================================================================
            var banreich = new DWDN_Duchy(
                "banreich",
                "Баннрайх",
                "Северное герцогство с сильными рыцарскими традициями, тяжелой пехотой и передовым вооружением.",
                "11.14.14.1536.1536.768.768.1.0.0.160.0.0.512.512.768.768.1.0.0",
                true
            );

            // 1. Ратай (Ополченец / Легкая пехота)
            var ratay = new DWDN_ClassDefinition(
                "mp_Ratay",
                "Ратай",
                "Легкая пехота ополчения, вооруженная копьями, топорами и щитами.",
                "mp_Ratay_hero"
            );
            ratay.AllowedHelmets.AddRange(new[] { "ratay_kettle_bare", "" });
            ratay.AllowedBodyArmors.AddRange(new[] { "ratay_overcoat" });
            ratay.AllowedLegs.AddRange(new[] { "ratay_hose" });
            ratay.AllowedGloves.AddRange(new[] { "arch_banreich_leatherglove", "" });
            ratay.AllowedWeaponsSlot0.AddRange(new[] { "dwdn_cleaver", "mp_sturgia_axe", "mp_vlandian_hammer" });
            ratay.AllowedWeaponsSlot1.AddRange(new[] { "ratay_1_shield", "ratay_2_shield", "" });
            ratay.AllowedWeaponsSlot2.AddRange(new[] { "", "mp_western_short_spear" });
            ratay.DefaultLoadout = ratay.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(ratay);

            // 2. Банобранный (Средняя пехота)
            var banobranny = new DWDN_ClassDefinition(
                "mp_Banobranny",
                "Банобранный",
                "Регулярная пехота герцогства в сюрко и шапелях с надежными щитами.",
                "mp_Banobranny_hero"
            );
            banobranny.AllowedHelmets.AddRange(new[] { "Banobranny_helmet", "" });
            banobranny.AllowedBodyArmors.AddRange(new[] { "Banobranny_surcoat" });
            banobranny.AllowedLegs.AddRange(new[] { "banobranyi_leg" });
            banobranny.AllowedGloves.AddRange(new[] { "crossbowman_mailgoves", "" });
            banobranny.AllowedWeaponsSlot0.AddRange(new[] { "dwdn_falchion", "mp_sturgia_axe", "mp_vlandian_mace" });
            banobranny.AllowedWeaponsSlot1.AddRange(new[] { "bannpbran_shield1", "bannpbran_shield2", "bannpbran_shield3", "bannpbran_shield4", "" });
            banobranny.AllowedWeaponsSlot2.AddRange(new[] { "", "mp_western_spear" });
            banobranny.DefaultLoadout = banobranny.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(banobranny);

            // 3. Пикинёр (Тяжелая пехота против конницы)
            var pikeman = new DWDN_ClassDefinition(
                "mp_Pikeman",
                "Пикинёр",
                "Элитные пехотинцы с длинными пиками, способные остановить любой натиск кавалерии.",
                "mp_Pikeman_hero"
            );
            pikeman.AllowedHelmets.AddRange(new[] { "dwdn_pikeman_kettlehad", "" });
            pikeman.AllowedBodyArmors.AddRange(new[] { "dwdn_pikeman_cuirass_v1" });
            pikeman.AllowedLegs.AddRange(new[] { "pikemen_dwdn_plate_leg" });
            pikeman.AllowedGloves.AddRange(new[] { "plate_gauntlet", "" });
            // Кастомная пика ТОЛЬКО у пикинёра!
            pikeman.AllowedWeaponsSlot0.AddRange(new[] { "pike1" });
            pikeman.AllowedWeaponsSlot1.AddRange(new[] { "mp_vlandian_arming_sword", "mp_sturgia_axe", "mp_vlandian_mace" });
            pikeman.DefaultLoadout = pikeman.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(pikeman);

            // 4. Збройный (Тяжелая латная пехота)
            var zbroyniy = new DWDN_ClassDefinition(
                "mp_Zbroyniy",
                "Збройный",
                "Тяжелобронированные латники передового строя с бацинетами и тяжелыми клинками.",
                "mp_Zbroyniy_hero"
            );
            zbroyniy.AllowedHelmets.AddRange(new[] { "armeds_bacinet_with_facemask", "" });
            zbroyniy.AllowedBodyArmors.AddRange(new[] { "armeds_striped_cuirass" });
            zbroyniy.AllowedLegs.AddRange(new[] { "plate_legs_dwdn" });
            zbroyniy.AllowedGloves.AddRange(new[] { "plate_gauntlet", "plate_round_gauntlet", "" });
            zbroyniy.AllowedWeaponsSlot0.AddRange(new[] { "mp_battania_noble_sword", "mp_sturgia_heavy_axe", "mp_battnia_heavy_mace" });
            zbroyniy.AllowedWeaponsSlot1.AddRange(new[] { "zbroyniy_shield1", "zbroyniy_shield_empty1", "zbroyniy_shield_silver1", "" });
            zbroyniy.AllowedWeaponsSlot2.AddRange(new[] { "", "mp_western_long_spear" });
            zbroyniy.DefaultLoadout = zbroyniy.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(zbroyniy);

            // 5. Десятник (Младший командир пехоты)
            var officer = new DWDN_ClassDefinition(
                "mp_Officer",
                "Десятник",
                "Младший командир пехоты в бригантине с офицерским бацинетом.",
                "mp_Officer_hero"
            );
            officer.AllowedHelmets.AddRange(new[] { "officer_dwdn_bacinet", "" });
            officer.AllowedBodyArmors.AddRange(new[] { "officer_brigantine" });
            officer.AllowedLegs.AddRange(new[] { "officer_legs" });
            officer.AllowedGloves.AddRange(new[] { "plate_gauntlet", "plate_round_gauntlet", "" });
            officer.AllowedWeaponsSlot0.AddRange(new[] { "mp_battania_noble_sword", "mp_sturgia_heavy_axe", "mp_battnia_heavy_mace" });
            officer.AllowedWeaponsSlot1.AddRange(new[] { "zbroyniy_shield1", "zbroyniy_shield_silver1", "zbroyniy_shield_empty1", "" });
            officer.AllowedWeaponsSlot2.AddRange(new[] { "", "mp_western_long_spear" });
            officer.DefaultLoadout = officer.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(officer);

            // 6. Сотник (Старший командир пехоты / богатый латник)
            var sotnik = new DWDN_ClassDefinition(
                "mp_Sotnik",
                "Сотник",
                "Старший офицер в тяжелой латной кирасе, командующий сотней воинов.",
                "mp_Sotnik_hero"
            );
            sotnik.AllowedHelmets.AddRange(new[] { "armeds_bacinet_with_facemask", "officer_dwdn_bacinet", "" });
            sotnik.AllowedBodyArmors.AddRange(new[] { "dwdn_pikeman_cuirass_v1", "officer_brigantine" });
            sotnik.AllowedCapes.AddRange(new[] { "Baron_rundal_cape", "" });
            sotnik.AllowedLegs.AddRange(new[] { "plate_legs_dwdn", "officer_legs" });
            sotnik.AllowedGloves.AddRange(new[] { "gothic_gloves", "plate_gauntlet", "" });
            sotnik.AllowedWeaponsSlot0.AddRange(new[] { "mp_battania_noble_sword", "mp_sturgia_heavy_axe" });
            sotnik.AllowedWeaponsSlot1.AddRange(new[] { "zbroyniy_shield_silver1", "zbroyniy_shield1", "" });
            sotnik.AllowedWeaponsSlot2.AddRange(new[] { "mp_western_long_spear", "" });
            sotnik.DefaultLoadout = sotnik.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(sotnik);

            // 7. Знаменосец (Штандарт герцогства)
            var bannerman = new DWDN_ClassDefinition(
                "mp_Bannerman",
                "Знаменосец",
                "Носитель боевого знамени (Аура 12м со знаменем в руках: +4 брони, +5% скорости, +4% атаки, реген до 75% HP вне боя).",
                "mp_Bannerman_hero"
            );
            bannerman.AllowedHelmets.AddRange(new[] { "banerman_bascinet", "" });
            bannerman.AllowedBodyArmors.AddRange(new[] { "bannerman_cuirass" });
            bannerman.AllowedLegs.AddRange(new[] { "bannerman_legs" });
            bannerman.AllowedGloves.AddRange(new[] { "plate_round_gauntlet", "" });
            bannerman.AllowedWeaponsSlot0.AddRange(new[] { "mp_battania_noble_sword", "mp_sturgia_heavy_axe", "mp_battnia_heavy_mace" });
            bannerman.AllowedWeaponsSlot1.AddRange(new[] { "dwdn_banner1" });
            bannerman.DefaultLoadout = bannerman.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(bannerman);

            // 7. Стрелок (Объединенный класс лучников и арбалетчиков)
            var marksman = new DWDN_ClassDefinition(
                "mp_DWDN_Archer",
                "Стрелок",
                "Опытные стрелки герцогства, использующие как тяжелые арбалеты, так и дальнобойные луки.",
                "mp_DWDN_Archer_hero"
            );
            marksman.AllowedHelmets.AddRange(new[] { "crossbowman_kettle_1", "archer_kettle_1_bare", "Banobranny_helmet", "" });
            marksman.AllowedBodyArmors.AddRange(new[] { "crossbowman_surcoat_gambeson", "arch_bannreich_gambeson", "Banobranny_surcoat" });
            marksman.AllowedLegs.AddRange(new[] { "crossbowman_leggings", "archer_leatherboots", "banobranyi_leg" });
            marksman.AllowedGloves.AddRange(new[] { "crossbowman_mailgoves", "arch_banreich_leatherglove", "" });
            marksman.AllowedCapes.AddRange(new[] { "archer_scarf", "" });
            // Дистанционное оружие (луки и арбалеты)
            marksman.AllowedWeaponsSlot0.AddRange(new[] { "mp_heavier_crossbow", "mp_heavier_crossbow_faster_draw", "mp_woodland_long_bow", "mp_steppe_superior_bow" });
            // Боеприпасы (болты и стрелы)
            marksman.AllowedWeaponsSlot1.AddRange(new[] { "mp_bolts_strong", "mp_bolts_western_extra", "mp_arrows_steppe_extra" });
            // Оружие ближнего боя
            marksman.AllowedWeaponsSlot2.AddRange(new[] { "mp_vlandian_short_sword", "mp_sturgia_axe" });
            // Дополнительный щит
            marksman.AllowedWeaponsSlot3.AddRange(new[] { "", "ratay_1_shield" });
            marksman.DefaultLoadout = marksman.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(marksman);

            // 8. Стрелок с ручницей (Отдельный боец с огнестрелом)
            var handgonner = new DWDN_ClassDefinition(
                "mp_DWDN_Crossbowman",
                "Стрелок с ручницей",
                "Стрелок, вооруженный ручной бомбардой со свинцовыми пулями для пробития тяжелой брони.",
                "mp_DWDN_Crossbowman_hero"
            );
            handgonner.AllowedHelmets.AddRange(new[] { "crossbowman_kettle_1", "archer_kettle_1_bare", "Banobranny_helmet", "" });
            handgonner.AllowedBodyArmors.AddRange(new[] { "crossbowman_surcoat_gambeson", "arch_bannreich_gambeson", "Banobranny_surcoat" });
            handgonner.AllowedLegs.AddRange(new[] { "crossbowman_leggings", "archer_leatherboots", "banobranyi_leg" });
            handgonner.AllowedGloves.AddRange(new[] { "crossbowman_mailgoves", "arch_banreich_leatherglove", "" });
            handgonner.AllowedCapes.AddRange(new[] { "archer_scarf", "" });
            // Огнестрельное оружие (hand_cannon)
            handgonner.AllowedWeaponsSlot0.AddRange(new[] { "hand_cannon" });
            // Пули (mp_bullets)
            handgonner.AllowedWeaponsSlot1.AddRange(new[] { "mp_bullets" });
            // Вторичное оружие ближнего боя
            handgonner.AllowedWeaponsSlot2.AddRange(new[] { "mp_vlandian_short_sword", "mp_sturgia_axe", "baldur_warpick" });
            handgonner.DefaultLoadout = handgonner.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(handgonner);

            // 9. Кавалерия (Всадник Даевдена)
            var cav = new DWDN_ClassDefinition(
                "mp_DWDN_Cav",
                "Кавалерия",
                "Быстрая ударная конница с тяжелыми кавалерийскими копьями и надежными щитами.",
                "mp_DWDN_Cav_hero"
            );
            cav.AllowedHelmets.AddRange(new[] { "bannreich_cav_helmet_visor", "" });
            cav.AllowedBodyArmors.AddRange(new[] { "plate_cav_cuirass" });
            cav.AllowedLegs.AddRange(new[] { "cav_bannreich_plate_legs" });
            cav.AllowedGloves.AddRange(new[] { "plate_gauntlet", "" });
            cav.AllowedWeaponsSlot0.AddRange(new[] { "mp_vlandian_heavy_lance", "mp_battania_cavalry_spear" });
            cav.AllowedWeaponsSlot1.AddRange(new[] { "bann_reich_cav_shield1", "" });
            cav.AllowedWeaponsSlot2.AddRange(new[] { "mp_vlandia_cavalry_sword", "mp_sturgia_cavalry_axe", "mp_vlandian_cavalry_mace" });
            cav.AllowedMounts.AddRange(new[] { "mp_vlandia_horse", "" });
            cav.DefaultLoadout = cav.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(cav);

            // 10. Рыцарь (Тяжелая рыцарская кавалерия)
            var knight = new DWDN_ClassDefinition(
                "mp_Knight",
                "Рыцарь",
                "Благородный рыцарь в полных латах с клапвизором, сокрушающий врагов мощным натиском.",
                "mp_Knight_hero"
            );
            knight.AllowedHelmets.AddRange(new[] { "knight_klappvisor", "" });
            knight.AllowedBodyArmors.AddRange(new[] { "knight_cuirass" });
            knight.AllowedLegs.AddRange(new[] { "knight_legs" });
            knight.AllowedGloves.AddRange(new[] { "plate_round_gauntlet", "" });
            knight.AllowedWeaponsSlot0.AddRange(new[] { "mp_vlandian_heavy_lance", "mp_battania_cavalry_spear" });
            knight.AllowedWeaponsSlot1.AddRange(new[] { "zbroyniy_shield1", "zbroyniy_shield_empty1", "zbroyniy_shield_silver1", "" });
            knight.AllowedWeaponsSlot2.AddRange(new[] { "mp_vlandia_cavalry_sword", "mp_sturgia_cavalry_axe", "mp_vlandian_cavalry_mace" });
            knight.AllowedMounts.AddRange(new[] { "mp_vlandia_horse", "" });
            knight.DefaultLoadout = knight.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(knight);

            // 11. Барон Клаус Берген (Именной герой - строго фиксированное кастомное снаряжение)
            var bergen = new DWDN_ClassDefinition(
                "mp_Bergen_Klaus",
                "Барон Берген",
                "Барон Клаус Берген в хундсгугеле, вооруженный своим именным полуторным мечом.",
                "mp_Bergen_Klaus_hero"
            );
            bergen.AllowedHelmets.AddRange(new[] { "bergen_bascinet_hounskull" });
            bergen.AllowedCapes.AddRange(new[] { "bergen_hood", "" });
            bergen.AllowedBodyArmors.AddRange(new[] { "bergen_cuirass" });
            bergen.AllowedLegs.AddRange(new[] { "bergen_legs" });
            bergen.AllowedGloves.AddRange(new[] { "gothic_gloves" });
            bergen.AllowedWeaponsSlot0.AddRange(new[] { "mp_vlandian_heavy_lance" });
            // Именной меч ТОЛЬКО у барона Бергена!
            bergen.AllowedWeaponsSlot1.AddRange(new[] { "bergens_bastard_sword" });
            bergen.AllowedWeaponsSlot2.AddRange(new[] { "bergen_shield" });
            bergen.AllowedMounts.AddRange(new[] { "mp_vlandia_horse", "" });
            bergen.DefaultLoadout = bergen.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(bergen);

            // 12. Барон Валериан Финнард (Именной герой - строго фиксированное кастомное снаряжение)
            var finnard = new DWDN_ClassDefinition(
                "mp_Finnard_Valerian",
                "Барон Финнард",
                "Барон Валериан Финнард с фамильным копьем и готическими латами.",
                "mp_Finnard_Valerian_hero"
            );
            finnard.AllowedHelmets.AddRange(new[] { "finnard_helmet" });
            finnard.AllowedCapes.AddRange(new[] { "finard_cape", "" });
            finnard.AllowedBodyArmors.AddRange(new[] { "Baron_Finnard_body" });
            finnard.AllowedLegs.AddRange(new[] { "Baron_Finard_gothiclegs" });
            finnard.AllowedGloves.AddRange(new[] { "Baron_Finard_gauntlet" });
            finnard.AllowedWeaponsSlot0.AddRange(new[] { "mp_vlandia_cavalry_sword" });
            // Именное копьё ТОЛЬКО у барона Финнарда!
            finnard.AllowedWeaponsSlot1.AddRange(new[] { "finnard_spear" });
            finnard.AllowedWeaponsSlot2.AddRange(new[] { "finnard_shield" });
            finnard.AllowedMounts.AddRange(new[] { "mp_vlandia_horse", "" });
            finnard.DefaultLoadout = finnard.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(finnard);

            // 13. Король Альберик Девальд (Правитель Даевдена - строго фиксированное кастомное снаряжение)
            var king = new DWDN_ClassDefinition(
                "mp_ALberic_Devald_hero",
                "Король Альберик Девальд",
                "Повелитель Даевдена в королевских латах, вооруженный легендарным клинком Девальда.",
                "mp_ALberic_Devald_hero"
            );
            king.AllowedHelmets.AddRange(new[] { "armeds_bacinet_with_facemask", "" });
            king.AllowedBodyArmors.AddRange(new[] { "king_daewden_covered_cuirass" });
            king.AllowedLegs.AddRange(new[] { "king_plate_legs" });
            king.AllowedGloves.AddRange(new[] { "king_plate_gauntlet" });
            king.AllowedWeaponsSlot0.AddRange(new[] { "mp_vlandian_heavy_lance" });
            // Именной меч ТОЛЬКО у короля Девальда!
            king.AllowedWeaponsSlot1.AddRange(new[] { "devalds_bastard_sword" });
            king.AllowedWeaponsSlot2.AddRange(new[] { "zbroyniy_shield_silver1" });
            king.AllowedMounts.AddRange(new[] { "mp_battania_horse_war", "" });
            king.DefaultLoadout = king.CreateDefaultLoadout(banreich.Id);
            banreich.Classes.Add(king);

            Duchies.Add(banreich);

            // Заготовки под будущее расширение
            var rezia = new DWDN_Duchy(
                "rezia",
                "Рецезия",
                "Южное торговое герцогство со своими уникальными оружейными традициями.",
                "11.12.12.1536.1536.768.768.1.0.0.160.0.0.512.512.768.768.1.0.0",
                false
            );
            Duchies.Add(rezia);

            var creamor = new DWDN_Duchy(
                "creamor",
                "Креамор",
                "Восточные земли горных крепостей и суровых наёмников.",
                "11.10.10.1536.1536.768.768.1.0.0.160.0.0.512.512.768.768.1.0.0",
                false
            );
            Duchies.Add(creamor);

            _initialized = true;
        }

        public static DWDN_Duchy GetDuchy(string duchyId)
        {
            if (string.IsNullOrEmpty(duchyId)) return Duchies.Count > 0 ? Duchies[0] : null;
            return Duchies.Find(d => d.Id.Equals(duchyId, StringComparison.OrdinalIgnoreCase)) 
                   ?? (Duchies.Count > 0 ? Duchies[0] : null);
        }
    }
}

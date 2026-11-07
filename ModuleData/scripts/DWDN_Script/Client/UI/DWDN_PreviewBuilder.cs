using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using DWDN_Script.Common.Duchies;

namespace DWDN_Script.Client.UI
{
    public static class DWDN_PreviewBuilder
    {
        public static DWDN_PreviewInputs Build(DWDN_LoadoutSelection selection, MissionPeer localPeer)
        {
            if (selection == null || localPeer == null)
                return null;

            var duchy = DWDN_DuchyManager.GetDuchy(selection.DuchyId);
            var cls = duchy?.GetClass(selection.ClassId);

            BasicCharacterObject character = !string.IsNullOrEmpty(cls?.TroopId)
                ? MBObjectManager.Instance?.GetObject<BasicCharacterObject>(cls.TroopId)
                : null;

            if (character == null)
            {
                var culture = MBObjectManager.Instance?.GetObject<BasicCultureObject>("dwdn");
                if (culture != null)
                {
                    var heroClasses = MultiplayerClassDivisions.GetMPHeroClasses(culture)?.ToList();
                    if (heroClasses != null && heroClasses.Count > 0)
                    {
                        character = heroClasses[0].HeroCharacter ?? heroClasses[0].TroopCharacter;
                    }
                }
            }

            if (character == null)
                return null;

            Equipment equipment = BuildEquipment(selection, character);

            uint color1 = character.Culture?.Color ?? 0xFFFFFFFF;
            uint color2 = character.Culture?.Color2 ?? 0xFFFFFFFF;

            Banner banner = null;
            if (localPeer.Peer != null && !string.IsNullOrEmpty(localPeer.Peer.BannerCode))
            {
                banner = new Banner(localPeer.Peer.BannerCode);
            }
            else
            {
                banner = new Banner();
            }

            bool isFemale = localPeer.Peer != null && localPeer.Peer.IsFemale;
            BodyProperties body = localPeer.Peer != null
                ? localPeer.Peer.BodyProperties
                : character.GetBodyProperties(character.Equipment);

            return new DWDN_PreviewInputs
            {
                SkeletonCharacter = character,
                Equipment = equipment,
                Body = body,
                IsFemale = isFemale,
                ClothingColor1 = color1,
                ClothingColor2 = color2,
                Banner = banner,
                Monster = null,
                Race = character.Race
            };
        }

        public static Equipment BuildEquipment(DWDN_LoadoutSelection selection, BasicCharacterObject character)
        {
            Equipment equip = character != null && character.Equipment != null
                ? new Equipment(character.Equipment)
                : new Equipment();

            void SetSlot(EquipmentIndex index, string itemId)
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    equip[index] = EquipmentElement.Invalid;
                    return;
                }

                ItemObject item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
                if (item != null)
                {
                    equip[index] = new EquipmentElement(item);
                }
            }

            SetSlot(EquipmentIndex.Head, selection.HeadId);
            SetSlot(EquipmentIndex.Cape, selection.CapeId);
            SetSlot(EquipmentIndex.Body, selection.BodyId);
            SetSlot(EquipmentIndex.Gloves, selection.GlovesId);
            SetSlot(EquipmentIndex.Leg, selection.LegsId);

            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 0, selection.Weapon0Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 1, selection.Weapon1Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 2, selection.Weapon2Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 3, selection.Weapon3Id);

            equip[EquipmentIndex.Horse] = EquipmentElement.Invalid;
            equip[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

            return equip;
        }
    }
}

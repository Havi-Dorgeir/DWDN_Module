using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DWDN_Script
{
    public class DWDN_FirearmsBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        // Sound event IDs (cached)
        private int _soundCrack = -1;
        private int _soundPop = -1;
        private bool _soundsInitialized = false;

        private void InitializeSounds()
        {
            if (_soundsInitialized) return;

            // Light, sharp gunpowder crack and pop (handgonne sound)
            _soundCrack = SoundEvent.GetEventIdFromString("event:/mission/combat/impact/ballista_missile/stone");
            _soundPop = SoundEvent.GetEventIdFromString("event:/mission/combat/impact/missile/pot_breakable");
            _soundsInitialized = true;
        }

        private bool IsFirearm(ItemObject item)
        {
            if (item == null) return false;
            string id = item.StringId;
            if (string.IsNullOrEmpty(id)) return false;

            return id.Equals("hand_cannon", StringComparison.OrdinalIgnoreCase) ||
                   id.Equals("mp_gun_prototype", StringComparison.OrdinalIgnoreCase) ||
                   id.IndexOf("cannon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("bombard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("handgonne", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("firearm", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public override void OnAgentShootMissile(
            Agent shooterAgent,
            EquipmentIndex weaponIndex,
            Vec3 position,
            Vec3 velocity,
            Mat3 orientation,
            bool hasRigidBody,
            int forcedMissileIndex)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);

            if (shooterAgent == null || Mission == null)
                return;

            if (weaponIndex < EquipmentIndex.WeaponItemBeginSlot || weaponIndex >= EquipmentIndex.NumAllWeaponSlots)
                return;

            MissionWeapon weapon = shooterAgent.Equipment[weaponIndex];
            if (weapon.IsEmpty || weapon.Item == null || !IsFirearm(weapon.Item))
                return;

            // Calculate forward direction and muzzle transform
            Vec3 forward = velocity.NormalizedCopy();
            if (forward.LengthSquared < 0.001f)
            {
                forward = orientation.f;
            }

            // Muzzle position slightly ahead of the shooter
            Vec3 muzzlePos = position + forward * 0.70f;
            MatrixFrame muzzleFrame = new MatrixFrame(orientation, muzzlePos);

            if (!GameNetwork.IsDedicatedServer)
            {
                InitializeSounds();

                // ==========================================
                // 1. "ЛЕГКИЙ БАХ" — Clean, crisp handgonne gunshot
                // ==========================================
                if (_soundCrack != -1)
                {
                    Mission.MakeSound(_soundCrack, muzzlePos, false, true, shooterAgent.Index, -1);
                }
                if (_soundPop != -1)
                {
                    Mission.MakeSound(_soundPop, muzzlePos, false, true, shooterAgent.Index, -1);
                }

                // ==========================================
                // 2. ОГНЕННАЯ ВСПЫШКА И ДЫМ (Gunpowder flash, sparks & smoke)
                // ==========================================
                // Компактный мгновенный всплеск пламени у дула (0.20 сек, однократный burst, не зависает в воздухе)
                MatrixFrame flashFrame = new MatrixFrame(orientation, muzzlePos + forward * 0.10f);
                Mission.AddParticleSystemBurstByName("prt_fireball", flashFrame, true);

                // Густое облако порохового дыма у дула
                Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", muzzleFrame, true);
                Mission.AddParticleSystemBurstByName("psys_siege_door_falling_dust", muzzleFrame, true);

                // Выброс порохового дыма вперед по траектории выстрела (+0.6м и +1.2м)
                MatrixFrame smokePlume1 = new MatrixFrame(orientation, muzzlePos + forward * 0.60f);
                Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", smokePlume1, true);

                MatrixFrame smokePlume2 = new MatrixFrame(orientation, muzzlePos + forward * 1.20f);
                Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", smokePlume2, true);

                // Яркие пороховые искры
                Mission.AddParticleSystemBurstByName("psys_game_sparkle_a", muzzleFrame, true);

                // Дымок у запального отверстия
                MatrixFrame touchHoleFrame = new MatrixFrame(orientation, position - forward * 0.10f + new Vec3(0f, 0f, 0.04f));
                Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", touchHoleFrame, true);
            }

            // NOTE: Мы не спавним сторонние AddCustomMissile на сервере, так как нативный выстрел
            // движка Bannerlord уже идеально выпускает свинцовую пулю (165 урона, пробитие щитов).
            // Это гарантирует 100% стабильность выделенного сервера при любых залпах.
        }
    }
}

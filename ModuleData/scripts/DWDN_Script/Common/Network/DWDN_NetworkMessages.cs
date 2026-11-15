using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using DWDN_Script.Common.Duchies;

namespace DWDN_Script.Common.Network
{
    /// <summary>
    /// Сетевой пакет: запрос игрока на отцепку коня и перевод артиллерии в боевое положение.
    /// Отправляется клиентом на сервер при нажатии [F] возле упряжи/сцепки.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestDeployArtilleryMessage : GameNetworkMessage
    {
        public int EngineId { get; set; }

        public RequestDeployArtilleryMessage() { }
        public RequestDeployArtilleryMessage(int engineId)
        {
            EngineId = engineId;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(EngineId, CompressionBasic.DebugIntNonCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            EngineId = ReadIntFromPacket(CompressionBasic.DebugIntNonCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() => MultiplayerMessageFilter.GameMode;
        protected override string OnGetLogFormat() => $"RequestDeployArtilleryMessage: EngineId={EngineId}";
    }

    /// <summary>
    /// Сетевой пакет: запрос игрока на сцепку или отцепку коня (двустороннее действие).
    /// Отправляется клиентом на сервер при удержании [F] на дышле орудия.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestToggleArtilleryHitchMessage : GameNetworkMessage
    {
        public int EngineId { get; set; }
        public int HorseAgentIndex { get; set; } = -1;
        public bool Attach { get; set; }

        public RequestToggleArtilleryHitchMessage() { }
        public RequestToggleArtilleryHitchMessage(int engineId, int horseAgentIndex, bool attach)
        {
            EngineId = engineId;
            HorseAgentIndex = horseAgentIndex;
            Attach = attach;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(EngineId, CompressionBasic.DebugIntNonCompressionInfo);
            WriteAgentIndexToPacket(HorseAgentIndex);
            WriteBoolToPacket(Attach);
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            EngineId = ReadIntFromPacket(CompressionBasic.DebugIntNonCompressionInfo, ref bufferReadValid);
            HorseAgentIndex = ReadAgentIndexFromPacket(ref bufferReadValid);
            Attach = ReadBoolFromPacket(ref bufferReadValid);
            return bufferReadValid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() => MultiplayerMessageFilter.GameMode;
        protected override string OnGetLogFormat() => $"RequestToggleArtilleryHitchMessage: EngineId={EngineId}, Horse={HorseAgentIndex}, Attach={Attach}";
    }

    /// <summary>
    /// Сетевой пакет: полная рекурсивная синхронизация MissionObjectId всех вложенных
    /// узлов осадного орудия (StandingPoint, SiegeMachineStonePile, ammo_pos и др.) от сервера к клиентам.
    /// Также передает индекс упряжной лошади и флаг использования ванильной тележки.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class SyncArtilleryDescendantsMessage : GameNetworkMessage
    {
        public int EngineId { get; set; }
        public MissionObjectId RootObjectId { get; set; }
        public string PrefabName { get; set; } = "";
        public int TeamSide { get; set; }
        public MatrixFrame Frame { get; set; }
        public int HorseAgentIndex { get; set; } = -1;
        public bool UsesCart { get; set; }
        public List<MissionObjectId> DescendantObjectIds { get; set; } = new List<MissionObjectId>();

        public SyncArtilleryDescendantsMessage() { }
        public SyncArtilleryDescendantsMessage(int engineId, MissionObjectId rootObjectId, string prefabName, int teamSide, MatrixFrame frame, int horseAgentIndex, bool usesCart, List<MissionObjectId> descendantObjectIds)
        {
            EngineId = engineId;
            RootObjectId = rootObjectId;
            PrefabName = prefabName ?? "";
            TeamSide = teamSide;
            Frame = frame;
            HorseAgentIndex = horseAgentIndex;
            UsesCart = usesCart;
            DescendantObjectIds = descendantObjectIds ?? new List<MissionObjectId>();
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(EngineId, CompressionBasic.DebugIntNonCompressionInfo);
            WriteMissionObjectIdToPacket(RootObjectId);
            WriteStringToPacket(PrefabName ?? "");
            WriteIntToPacket(TeamSide, CompressionBasic.DebugIntNonCompressionInfo);
            WriteMatrixFrameToPacket(Frame);
            WriteAgentIndexToPacket(HorseAgentIndex);
            WriteBoolToPacket(UsesCart);
            int count = DescendantObjectIds?.Count ?? 0;
            WriteIntToPacket(count, CompressionBasic.DebugIntNonCompressionInfo);
            for (int i = 0; i < count; i++)
            {
                WriteMissionObjectIdToPacket(DescendantObjectIds[i]);
            }
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            EngineId = ReadIntFromPacket(CompressionBasic.DebugIntNonCompressionInfo, ref bufferReadValid);
            RootObjectId = ReadMissionObjectIdFromPacket(ref bufferReadValid);
            PrefabName = ReadStringFromPacket(ref bufferReadValid);
            TeamSide = ReadIntFromPacket(CompressionBasic.DebugIntNonCompressionInfo, ref bufferReadValid);
            Frame = ReadMatrixFrameFromPacket(ref bufferReadValid);
            HorseAgentIndex = ReadAgentIndexFromPacket(ref bufferReadValid);
            UsesCart = ReadBoolFromPacket(ref bufferReadValid);
            int count = ReadIntFromPacket(CompressionBasic.DebugIntNonCompressionInfo, ref bufferReadValid);
            DescendantObjectIds = new List<MissionObjectId>(count);
            for (int i = 0; i < count; i++)
            {
                DescendantObjectIds.Add(ReadMissionObjectIdFromPacket(ref bufferReadValid));
            }
            return bufferReadValid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() => MultiplayerMessageFilter.GameMode;
        protected override string OnGetLogFormat() => $"SyncArtilleryDescendantsMessage: EngineId={EngineId}, Prefab={PrefabName}, Horse={HorseAgentIndex}, Cart={UsesCart}, Children={DescendantObjectIds.Count}";
    }

    /// <summary>
    /// Сетевой пакет: трансляция перехода орудия в боевой режим (отцепка коня, активация боеприпасов и станций огня).
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class SetArtilleryCombatStateMessage : GameNetworkMessage
    {
        public int EngineId { get; set; }
        public bool IsCombatState { get; set; }
        public MatrixFrame Frame { get; set; }
        public int HorseAgentIndex { get; set; } = -1;
        public int RemainingAmmo { get; set; } = 25;

        public SetArtilleryCombatStateMessage() { }
        public SetArtilleryCombatStateMessage(int engineId, bool isCombatState, MatrixFrame frame)
        {
            EngineId = engineId;
            IsCombatState = isCombatState;
            Frame = frame;
            HorseAgentIndex = -1;
            RemainingAmmo = 25;
        }
        public SetArtilleryCombatStateMessage(int engineId, bool isCombatState, MatrixFrame frame, int horseAgentIndex, int remainingAmmo)
        {
            EngineId = engineId;
            IsCombatState = isCombatState;
            Frame = frame;
            HorseAgentIndex = horseAgentIndex;
            RemainingAmmo = remainingAmmo;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(EngineId, CompressionBasic.DebugIntNonCompressionInfo);
            WriteBoolToPacket(IsCombatState);
            WriteMatrixFrameToPacket(Frame);
            WriteAgentIndexToPacket(HorseAgentIndex);
            WriteIntToPacket(RemainingAmmo, CompressionBasic.DebugIntNonCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            EngineId = ReadIntFromPacket(CompressionBasic.DebugIntNonCompressionInfo, ref bufferReadValid);
            IsCombatState = ReadBoolFromPacket(ref bufferReadValid);
            Frame = ReadMatrixFrameFromPacket(ref bufferReadValid);
            HorseAgentIndex = ReadAgentIndexFromPacket(ref bufferReadValid);
            RemainingAmmo = ReadIntFromPacket(CompressionBasic.DebugIntNonCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() => MultiplayerMessageFilter.GameMode;
        protected override string OnGetLogFormat() => $"SetArtilleryCombatStateMessage: EngineId={EngineId}, Combat={IsCombatState}, Horse={HorseAgentIndex}, Ammo={RemainingAmmo}";
    }

    /// <summary>
    /// Сетевой пакет: отправка выбранного комплекта экипировки с клиента на сервер.
    /// Использует официальный Mod-атрибут Bannerlord: DefineGameNetworkMessageTypeForMod.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestCustomLoadoutMessage : GameNetworkMessage
    {
        public string DuchyId { get; set; } = "";
        public string ClassId { get; set; } = "";
        public string HeadId { get; set; } = "";
        public string CapeId { get; set; } = "";
        public string BodyId { get; set; } = "";
        public string GlovesId { get; set; } = "";
        public string LegsId { get; set; } = "";
        public string Weapon0Id { get; set; } = "";
        public string Weapon1Id { get; set; } = "";
        public string Weapon2Id { get; set; } = "";
        public string Weapon3Id { get; set; } = "";
        public string HorseId { get; set; } = "";

        public RequestCustomLoadoutMessage()
        {
        }

        public RequestCustomLoadoutMessage(DWDN_LoadoutSelection selection)
        {
            if (selection != null)
            {
                DuchyId = selection.DuchyId ?? "";
                ClassId = selection.ClassId ?? "";
                HeadId = selection.HeadId ?? "";
                CapeId = selection.CapeId ?? "";
                BodyId = selection.BodyId ?? "";
                GlovesId = selection.GlovesId ?? "";
                LegsId = selection.LegsId ?? "";
                Weapon0Id = selection.Weapon0Id ?? "";
                Weapon1Id = selection.Weapon1Id ?? "";
                Weapon2Id = selection.Weapon2Id ?? "";
                Weapon3Id = selection.Weapon3Id ?? "";
                HorseId = selection.HorseId ?? "";
            }
        }

        protected override void OnWrite()
        {
            WriteStringToPacket(DuchyId);
            WriteStringToPacket(ClassId);
            WriteStringToPacket(HeadId);
            WriteStringToPacket(CapeId);
            WriteStringToPacket(BodyId);
            WriteStringToPacket(GlovesId);
            WriteStringToPacket(LegsId);
            WriteStringToPacket(Weapon0Id);
            WriteStringToPacket(Weapon1Id);
            WriteStringToPacket(Weapon2Id);
            WriteStringToPacket(Weapon3Id);
            WriteStringToPacket(HorseId);
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            DuchyId = ReadStringFromPacket(ref bufferReadValid);
            ClassId = ReadStringFromPacket(ref bufferReadValid);
            HeadId = ReadStringFromPacket(ref bufferReadValid);
            CapeId = ReadStringFromPacket(ref bufferReadValid);
            BodyId = ReadStringFromPacket(ref bufferReadValid);
            GlovesId = ReadStringFromPacket(ref bufferReadValid);
            LegsId = ReadStringFromPacket(ref bufferReadValid);
            Weapon0Id = ReadStringFromPacket(ref bufferReadValid);
            Weapon1Id = ReadStringFromPacket(ref bufferReadValid);
            Weapon2Id = ReadStringFromPacket(ref bufferReadValid);
            Weapon3Id = ReadStringFromPacket(ref bufferReadValid);
            HorseId = ReadStringFromPacket(ref bufferReadValid);
            return bufferReadValid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.GameMode;
        }

        protected override string OnGetLogFormat()
        {
            return $"RequestCustomLoadoutMessage: Duchy={DuchyId}, Class={ClassId}, Horse={HorseId}, W0={Weapon0Id}, W1={Weapon1Id}, W2={Weapon2Id}, W3={Weapon3Id}, Head={HeadId}, Body={BodyId}";
        }

        public DWDN_LoadoutSelection ToSelection()
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

    /// <summary>
    /// Сетевой пакет: запрос административного действия с клиента на сервер.
    /// Отправляется при взаимодействии с кастомной админ-панелью DWDN Admin Tools.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestAdminActionMessage : GameNetworkMessage
    {
        public string Action { get; set; } = "";
        public string TargetPlayerId { get; set; } = "";
        public string Argument { get; set; } = "";

        public RequestAdminActionMessage() { }

        public RequestAdminActionMessage(string action, string targetPlayerId = "", string argument = "")
        {
            Action = action ?? "";
            TargetPlayerId = targetPlayerId ?? "";
            Argument = argument ?? "";
        }

        protected override void OnWrite()
        {
            WriteStringToPacket(Action ?? "");
            WriteStringToPacket(TargetPlayerId ?? "");
            WriteStringToPacket(Argument ?? "");
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Action = ReadStringFromPacket(ref bufferReadValid);
            TargetPlayerId = ReadStringFromPacket(ref bufferReadValid);
            Argument = ReadStringFromPacket(ref bufferReadValid);
            return bufferReadValid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() => MultiplayerMessageFilter.GameMode;
        protected override string OnGetLogFormat() => $"RequestAdminActionMessage: Action={Action}, Target={TargetPlayerId}, Arg={Argument}";
    }

    /// <summary>
    /// Сетевой пакет: синхронизация статуса администратора от сервера клиенту.
    /// Позволяет клиентскому UI знать, доступны ли права модератора/администратора.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class SyncAdminStateMessage : GameNetworkMessage
    {
        public bool IsAdmin { get; set; }

        public SyncAdminStateMessage() { }

        public SyncAdminStateMessage(bool isAdmin)
        {
            IsAdmin = isAdmin;
        }

        protected override void OnWrite()
        {
            WriteBoolToPacket(IsAdmin);
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            IsAdmin = ReadBoolFromPacket(ref bufferReadValid);
            return bufferReadValid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() => MultiplayerMessageFilter.GameMode;
        protected override string OnGetLogFormat() => $"SyncAdminStateMessage: IsAdmin={IsAdmin}";
    }
}

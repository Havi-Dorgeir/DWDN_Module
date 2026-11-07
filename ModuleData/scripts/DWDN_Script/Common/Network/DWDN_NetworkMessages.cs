using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using DWDN_Script.Common.Duchies;

namespace DWDN_Script.Common.Network
{
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
}

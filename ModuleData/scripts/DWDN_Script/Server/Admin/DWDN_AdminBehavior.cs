using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace DWDN_Script.Server.Admin
{
    public class DWDN_AdminBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private float _tickTimer = 0f;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (Mission == null || Mission.CurrentState != Mission.State.Continuing)
                return;

            _tickTimer += dt;
            if (_tickTimer >= 0.5f)
            {
                _tickTimer = 0f;
                DWDN_AdminService.TickMission(dt);
            }
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);

            if (affectedAgent == null || !affectedAgent.IsActive()) return;

            // Если у агента или его всадника включен режим бога, мгновенно восполняем здоровье
            NetworkCommunicator netPeer = affectedAgent.MissionPeer?.GetNetworkPeer();
            if (netPeer == null && affectedAgent.RiderAgent != null)
            {
                netPeer = affectedAgent.RiderAgent.MissionPeer?.GetNetworkPeer();
            }

            if (netPeer != null && DWDN_AdminService.IsGodMode(netPeer))
            {
                affectedAgent.Health = affectedAgent.HealthLimit;
            }
        }
    }
}

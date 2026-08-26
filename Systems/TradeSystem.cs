using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems
{
    public static class TradeSystem
    {
        public static bool AutoTrade(Agent agent)
        {
            if (agent == null)
                return false;

            var nearby = SpatialGrid.GetNearby(agent.Position, 2);

            Agent partner = null;

            foreach (var p in nearby)
            {
                if (p == null || p.Id == agent.Id)
                    continue;

                if (agent.Memory.GetTrust(p.Id) <= -20f)
                    continue;

                if (p.Body.Inventory.Any(o => o.Quantity >= 1f))
                {
                    partner = p;
                    break;
                }
            }

            if (partner == null)
                return false;

            var myItem = agent.Body.Inventory.FirstOrDefault(o => o.Quantity >= 1f);
            if (myItem == null)
                return false;

            var theirItem = partner.Body.Inventory.FirstOrDefault(o =>
                o.Quantity >= 1f &&
                o.MaterialId != myItem.MaterialId);

            if (theirItem == null)
                return false;

            if (!agent.Body.CanCarry(1f) || !partner.Body.CanCarry(1f))
                return false;

            myItem.Quantity -= 1f;
            theirItem.Quantity -= 1f;

            agent.Body.Inventory.Add(new WorldObject
            {
                MaterialId = theirItem.MaterialId,
                Quantity = 1f,
                HolderId = agent.Id
            });

            partner.Body.Inventory.Add(new WorldObject
            {
                MaterialId = myItem.MaterialId,
                Quantity = 1f,
                HolderId = partner.Id
            });

            if (myItem.Quantity <= 0f)
                agent.Body.Inventory.Remove(myItem);

            if (theirItem.Quantity <= 0f)
                partner.Body.Inventory.Remove(theirItem);

            agent.Memory.UpdateAgentMemory(partner.Id, "Trade", 15f);
            partner.Memory.UpdateAgentMemory(agent.Id, "Trade", 15f);

            EventBus.Publish(new SimEvent
            {
                Type = SimEventType.Trade,
                Tick = Simulation.Instance.TotalTicks,
                Actor = agent,
                Target = partner,
                Position = agent.Position
            });

            return true;
        }
    }
}
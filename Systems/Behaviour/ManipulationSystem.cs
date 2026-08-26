using System;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems.Behaviour
{
    public static class ManipulationSystem
    {
        public static bool PickUp(Agent agent, WorldObject obj)
            => PickUp(agent, obj, obj.Quantity);

        public static bool PickUp(Agent agent, WorldObject obj, float amount)
        {
            if (agent == null || obj == null) return false;
            if (obj.HolderId != Guid.Empty && obj.HolderId != agent.Id) return false;

            amount = Math.Min(amount, obj.Quantity);
            if (amount <= 0f) return false;

            if (!agent.Body.CanCarry(amount)) return false;

            // Если поднимаем весь объект
            if (amount >= obj.Quantity - 0.0001f)
            {
                if (obj.Position.HasValue)
                {
                    var tile = Simulation.Instance.World[obj.Position.Value.X, obj.Position.Value.Y];
                    tile.GroundObjects.Remove(obj);
                }

                obj.HolderId = agent.Id;
                obj.Position = null;
                agent.Body.Inventory.Add(obj);
                return true;
            }

            // Если поднимаем только часть
            obj.Quantity -= amount;

            var picked = new WorldObject
            {
                MaterialId = obj.MaterialId,
                Quantity = amount,
                HolderId = agent.Id,
                Position = null
            };

            agent.Body.Inventory.Add(picked);
            return true;
        }

        public static void Drop(Agent agent, WorldObject obj, Vector2 tilePos)
        {
            if (agent == null || obj == null) return;
            if (!agent.Body.Inventory.Contains(obj)) return;

            agent.Body.Inventory.Remove(obj);

            obj.HolderId = Guid.Empty;
            obj.Position = tilePos;

            var tile = Simulation.Instance.World[tilePos.X, tilePos.Y];
            tile.GroundObjects.Add(obj);
        }

        public static WorldObject Combine(Agent agent, WorldObject obj1, WorldObject obj2)
        {
            if (agent == null || obj1 == null || obj2 == null || obj1 == obj2)
                return null;

            if (!agent.Body.Inventory.Contains(obj1) || !agent.Body.Inventory.Contains(obj2))
                return null;

            if (obj1.Quantity < 1f || obj2.Quantity < 1f)
                return null;

            if (!MaterialDB.TryGet(obj1.MaterialId, out var spec1) ||
                !MaterialDB.TryGet(obj2.MaterialId, out var spec2))
                return null;

            var mixedSpec = MaterialDB.Mix(spec1, spec2);

            var newObj = new WorldObject
            {
                MaterialId = mixedSpec.Id,
                Quantity = 1f,
                HolderId = agent.Id,
                Position = null
            };

            obj1.Quantity -= 1f;
            if (obj1.Quantity <= 0f)
                agent.Body.Inventory.Remove(obj1);

            obj2.Quantity -= 1f;
            if (obj2.Quantity <= 0f)
                agent.Body.Inventory.Remove(obj2);

            agent.Body.Inventory.Add(newObj);

            return newObj;
        }
    }
}
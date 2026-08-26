using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems.Biology
{
    public class AgentBody
    {
        public float Energy = 100f;
        public float Hunger = 0f; // 0 = сыт, 100 = умирает от голода
        public float Health = 100f;
        public float MaxCarryWeight = 40f;

        public List<WorldObject> Inventory = new List<WorldObject>();

        public float CurrentCarryWeight => Inventory.Sum(o => o.Quantity);
        public bool CanCarry(float weight) => CurrentCarryWeight + weight <= MaxCarryWeight;

        // Эмерджентное потребление: агент ест только то, что имеет высокую Organic
        public void Consume(WorldObject obj, float amount = 1f)
        {
            if (obj == null) return;

            amount = Math.Min(amount, obj.Quantity);
            if (amount <= 0f) return;

            var props = obj.GetProperties();

            if (props.TryGetValue("Organic", out float organic) && organic > 0.5f)
            {
                float nourishment = organic * 20f * amount;
                float energyGain = organic * 15f * amount;

                Hunger = Math.Max(0f, Hunger - nourishment);
                Energy = Math.Min(100f, Energy + energyGain);

                obj.Quantity -= amount;
            }
        }

        public void Metabolize(float deltaTime)
        {
            Hunger = Math.Min(100, Hunger + (0.03f * deltaTime)); // Голод растёт со временем
            if (Hunger >= 100) Health -= 0.1f;
            Energy = Math.Max(0, Energy - (0.01f * deltaTime));
        }
    }
}
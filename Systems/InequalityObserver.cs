using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems
{
    /// <summary>
    /// Наблюдение за социальным неравенством.
    /// Не присваивает классы — просто измеряет распределение богатства.
    /// </summary>
    public static class InequalityObserver
    {
        /// <summary>
        /// Вычисляет коэффициент Джини (0 = полное равенство, 1 = полное неравенство).
        /// </summary>
        public static float CalculateGiniCoefficient(List<Agent> agents)
        {
            if (agents == null || agents.Count < 2) return 0f;

            // "Богатство" агента = количество предметов в инвентаре + качество nearby зданий
            var wealth = agents.Select(a => CalculateWealth(a)).OrderBy(w => w).ToList();
            int n = wealth.Count;
            float totalWealth = wealth.Sum();

            if (totalWealth <= 0f) return 0f;

            float gini = 0f;
            for (int i = 0; i < n; i++)
            {
                gini += (2f * (i + 1) - n - 1) * wealth[i];
            }
            gini /= (n * totalWealth);

            return Math.Clamp(gini, 0f, 1f);
        }

        private static float CalculateWealth(Agent agent)
        {
            float wealth = 0f;

            // Инвентарь
            foreach (var obj in agent.Body.Inventory)
            {
                if (MaterialDB.TryGet(obj.MaterialId, out var spec))
                {
                    wealth += obj.Quantity * (1f + spec.Rarity + spec.Hardness);
                }
            }

            // Nearby здания (агент "владеет" зданиями рядом)
            var nearby = SpatialGrid.GetNearby(agent.Position, 3);
            foreach (var other in nearby)
            {
                if (other.Id == agent.Id) continue;
                var tile = Simulation.Instance.World[other.Position.X, other.Position.Y];
                if (tile.Building != BuildingType.None)
                {
                    wealth += tile.BuildingQuality * 0.5f;
                }
            }

            return wealth;
        }

        /// <summary>
        /// Проверяет, есть ли риск восстания (высокое неравенство).
        /// </summary>
        public static bool IsRevoltRisk(List<Agent> agents)
        {
            float gini = CalculateGiniCoefficient(agents);
            return gini > 0.6f;  // Порог восстания
        }
    }
}
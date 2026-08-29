using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;

namespace GenesisEngine.Systems
{
    /// <summary>
    /// Эмерджентное определение ролей агентов на основе их действий.
    /// Роли не присваиваются — они наблюдаются.
    /// </summary>
    public static class RoleObserver
    {
        // Маппинг действий на роли
        private static readonly Dictionary<string, AgentRole> ActionToRole = new()
        {
            ["PickUp"] = AgentRole.Farmer,
            ["Forage"] = AgentRole.Farmer,
            ["Hunt"] = AgentRole.Farmer,
            ["Consume"] = AgentRole.Farmer,  // Если много потребляет — тоже добытчик

            ["Build"] = AgentRole.Builder,
            ["Combine"] = AgentRole.Builder,

            ["Trade"] = AgentRole.Trader,

            ["Attack"] = AgentRole.Soldier,
            ["Combat"] = AgentRole.Soldier,

            ["Teach"] = AgentRole.Scholar,
            ["Learn"] = AgentRole.Scholar,
            ["Read"] = AgentRole.Scholar,
            ["Write"] = AgentRole.Scholar,

            ["CreateArtifact"] = AgentRole.Artisan,
            ["Mourn"] = AgentRole.Artisan,
            ["Celebrate"] = AgentRole.Artisan,
        };

        /// <summary>
        /// Определяет роль агента на основе его действий за последние 500 тиков.
        /// Вызывается каждые 100 тиков из Simulation.Tick().
        /// </summary>
        public static void UpdateRoles(List<Agent> agents)
        {
            if (agents == null) return;

            foreach (var agent in agents)
            {
                if (agent.ActionHistory.Count == 0)
                {
                    agent.Role = AgentRole.None;
                    continue;
                }

                // Находим доминирующее действие
                string dominantAction = null;
                int maxCount = 0;

                foreach (var kv in agent.ActionHistory)
                {
                    if (kv.Value > maxCount)
                    {
                        maxCount = kv.Value;
                        dominantAction = kv.Key;
                    }
                }

                // Маппим действие на роль
                if (dominantAction != null &&
                    ActionToRole.TryGetValue(dominantAction, out var role))
                {
                    agent.Role = role;
                }
                else
                {
                    agent.Role = AgentRole.None;
                }
            }
        }

        /// <summary>
        /// Подсчёт количества агентов каждой роли для статистики.
        /// </summary>
        public static Dictionary<AgentRole, int> CountRoles(List<Agent> agents)
        {
            var counts = new Dictionary<AgentRole, int>();
            foreach (var role in Enum.GetValues(typeof(AgentRole)))
                counts[(AgentRole)role] = 0;

            foreach (var agent in agents)
            {
                if (counts.ContainsKey(agent.Role))
                    counts[agent.Role]++;
            }

            return counts;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;

namespace GenesisEngine.Systems
{
    public static class RoleObserver
    {
        // === Расширенный маппинг всех действий ===
        private static readonly Dictionary<string, AgentRole> ActionToRole = new()
        {
            // FARMER: добыча еды и ресурсов
            ["PickUp"] = AgentRole.Farmer,
            ["Forage"] = AgentRole.Farmer,
            ["Hunt"] = AgentRole.Farmer,
            ["Consume"] = AgentRole.Farmer,
            ["ConsumeStored"] = AgentRole.Farmer,
            ["StoreFood"] = AgentRole.Farmer,
            ["Mine"] = AgentRole.Farmer,
            ["ScatterSeed"] = AgentRole.Farmer,

            // BUILDER: строительство и крафт
            ["Build"] = AgentRole.Builder,
            ["Combine"] = AgentRole.Builder,

            // TRADER: торговля
            ["Trade"] = AgentRole.Trader,

            // SOLDIER: бой и агрессия
            ["Attack"] = AgentRole.Soldier,
            ["Combat"] = AgentRole.Soldier,
            ["Raid"] = AgentRole.Soldier,
            ["AvoidSick"] = AgentRole.Soldier,  // защитное поведение

            // SCHOLAR: обучение и манипуляции с символами
            ["Teach"] = AgentRole.Scholar,
            ["Learn"] = AgentRole.Scholar,
            ["Read"] = AgentRole.Scholar,
            ["Write"] = AgentRole.Scholar,
            ["SymbolicManipulation"] = AgentRole.Scholar,
            ["Experiment"] = AgentRole.Scholar,

            // ARTISAN: создание артефактов и ритуалы
            ["CreateArtifact"] = AgentRole.Artisan,
            ["CopyText"] = AgentRole.Artisan,
            ["Mourn"] = AgentRole.Artisan,
            ["Celebrate"] = AgentRole.Artisan,
        };

        // Приоритеты: редкие роли важнее Farmer (который заполонит всё)
        private static readonly Dictionary<AgentRole, int> RolePriority = new()
        {
            [AgentRole.Scholar] = 100,
            [AgentRole.Soldier] = 90,
            [AgentRole.Artisan] = 80,
            [AgentRole.Leader] = 95,
            [AgentRole.Trader] = 70,
            [AgentRole.Builder] = 60,
            [AgentRole.Farmer] = 10,
            [AgentRole.None] = 0,
        };

        public static void UpdateRoles(List<Agent> agents)
        {
            if (agents == null) return;

            foreach (var agent in agents)
            {
                agent.Role = Classify(agent);
            }
        }

        private static AgentRole Classify(Agent agent)
        {
            // 1. СНАЧАЛА проверяем текущий LastAction — это "активная" роль
            if (!string.IsNullOrEmpty(agent.LastAction) &&
                ActionToRole.TryGetValue(agent.LastAction, out var currentRole) &&
                currentRole != AgentRole.Farmer)
            {
                return currentRole;
            }

            // 2. Потом смотрим ActionHistory — с приоритетом
            if (agent.ActionHistory.Count > 0)
            {
                AgentRole bestRole = AgentRole.None;
                int bestScore = 0;

                foreach (var kv in agent.ActionHistory)
                {
                    if (ActionToRole.TryGetValue(kv.Key, out var role))
                    {
                        int score = RolePriority.GetValueOrDefault(role, 0) * kv.Value;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestRole = role;
                        }
                    }
                }

                if (bestRole != AgentRole.None)
                    return bestRole;
            }

            // 3. Fallback: что делал в последний раз (даже Farmer)
            if (!string.IsNullOrEmpty(agent.LastAction) &&
                ActionToRole.TryGetValue(agent.LastAction, out var fallbackRole))
                return fallbackRole;

            return AgentRole.None;
        }

        public static Dictionary<AgentRole, int> CountRoles(List<Agent> agents)
        {
            var counts = new Dictionary<AgentRole, int>();
            foreach (AgentRole r in Enum.GetValues(typeof(AgentRole)))
                counts[r] = 0;
            if (agents != null)
                foreach (var a in agents)
                    if (counts.ContainsKey(a.Role)) counts[a.Role]++;
            return counts;
        }
    }
}
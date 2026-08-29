using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.UI;
using GenesisEngine.Systems.Physics;
using GenesisEngine.Systems.Observers;
namespace GenesisEngine.Systems
{
    /// <summary>
    /// Эмерджентные революции: наблюдение за неравенством + разделение цивилизации.
    /// Не заставляет агентов атаковать — создаёт новое государство из недовольных.
    /// </summary>
    public static class RevoltSystem
    {
        // Пороги для триггера революции
        private const float GiniThreshold = 0.65f;      // Высокое неравенство
        private const float DespairThreshold = 50f;     // Среднее недовольство
        private const float WealthRatioThreshold = 3f;  // Элита в 3x богаче среднего

        // Процент населения, который уходит в восстание
        private const float RevoltPopulationRatio = 0.4f; // 40% уходит

        /// <summary>
        /// Вычисляет коэффициент Джини для цивилизации.
        /// 0 = полное равенство, 1 = полное неравенство.
        /// </summary>
        public static float CalculateGiniCoefficient(List<Agent> agents)
        {
            if (agents == null || agents.Count < 2) return 0f;

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

        /// <summary>
        /// Вычисляет "богатство" агента (инвентарь + здоровье + энергия).
        /// </summary>
        private static float CalculateWealth(Agent agent)
        {
            float wealth = 0f;

            foreach (var obj in agent.Body.Inventory)
            {
                if (MaterialDB.TryGet(obj.MaterialId, out var spec))
                {
                    wealth += obj.Quantity * (1f + spec.Rarity + spec.Hardness);
                }
            }

            wealth += agent.Body.Health / 10f;
            wealth += agent.Body.Energy / 10f;

            return wealth;
        }

        /// <summary>
        /// Проверяет, есть ли условия для революции.
        /// Вызывается каждые 500 тиков из Simulation.Tick().
        /// </summary>
        public static void CheckRevoltConditions(List<CivilizationSnapshot> civs)
        {
            if (civs == null) return;

            foreach (var civ in civs)
            {
                if (civ.Members.Count < 10) continue; // Слишком маленькая

                // 1. Считаем неравенство
                float gini = CalculateGiniCoefficient(civ.Members);
                if (gini < GiniThreshold) continue;

                // 2. Считаем среднее недовольство
                float avgDespair = civ.Members.Average(m => m.Despair);
                if (avgDespair < DespairThreshold) continue;

                // 3. Проверяем разрыв между элитой и средним классом
                var wealthSorted = civ.Members
                    .Select(a => CalculateWealth(a))
                    .OrderBy(w => w)
                    .ToList();

                float medianWealth = wealthSorted[wealthSorted.Count / 2];
                float eliteWealth = wealthSorted.Skip((int)(wealthSorted.Count * 0.9f)).Average();

                if (medianWealth <= 0f || eliteWealth / medianWealth < WealthRatioThreshold)
                    continue;

                // УСЛОВИЯ ВЫПОЛНЕНЫ — запускаем революцию!
                TriggerRevolt(civ, gini, avgDespair);
            }
        }

        /// <summary>
        /// Запускает революцию: разделяет цивилизацию на две.
        /// </summary>
        private static void TriggerRevolt(CivilizationSnapshot civ, float gini, float avgDespair)
        {
            var rng = RandomProvider.GetRandom();

            FileLogger.Log(
                $"[TICK {Simulation.Instance.TotalTicks}] 🚨 REVOLUTION in {civ.Name}! " +
                $"Gini={gini:F2}, Despair={avgDespair:F1}",
                FileLogger.LogLevel.War);

            // 1. Сортируем агентов по богатству
            var sortedByWealth = civ.Members
                .Select(a => (agent: a, wealth: CalculateWealth(a)))
                .OrderBy(x => x.wealth)
                .ToList();

            // 2. Определяем, кто уходит в восстание (бедные + недовольные)
            int revoltCount = Math.Max(3, (int)(civ.Members.Count * RevoltPopulationRatio));
            var rebels = new HashSet<Guid>();
            var elite = new HashSet<Guid>();
            var undecided = new List<(Agent agent, float wealth)>();

            // Бедные 40% → бунтари
            for (int i = 0; i < revoltCount && i < sortedByWealth.Count; i++)
            {
                var entry = sortedByWealth[i];
                if (entry.agent.Despair > 30f || entry.wealth < medianWealth(sortedByWealth))
                {
                    rebels.Add(entry.agent.Id);
                }
                else
                {
                    undecided.Add(entry);
                }
            }

            // Богатые 10% → элита
            int eliteCount = Math.Max(1, civ.Members.Count / 10);
            for (int i = sortedByWealth.Count - 1; i >= sortedByWealth.Count - eliteCount && i >= 0; i--)
            {
                elite.Add(sortedByWealth[i].agent.Id);
            }

            // Средний класс → выбирают сторону (50/50 или по недовольству)
            foreach (var entry in undecided)
            {
                float revoltChance = Math.Clamp(entry.agent.Despair / 100f, 0.2f, 0.8f);
                if (rng.NextDouble() < revoltChance)
                {
                    rebels.Add(entry.agent.Id);
                }
                else
                {
                    elite.Add(entry.agent.Id);
                }
            }

            // 3. Создаём новую цивилизацию из бунтарей
            string rebelCivId = Guid.NewGuid().ToString()[..8];
            int rebelCount = 0;
            int eliteCountFinal = 0;

            foreach (var agent in civ.Members)
            {
                if (rebels.Contains(agent.Id))
                {
                    agent.CivilizationId = rebelCivId;
                    agent.Despair = 0f; // Сбрасываем недовольство
                    agent.Loneliness = 30f; // Но немного одиноки
                    rebelCount++;
                }
                else if (elite.Contains(agent.Id))
                {
                    eliteCountFinal++;
                }
            }

            // 4. Объявляем войну между старой и новой цивилизацией
            DiplomacySystem.DeclareWar(civ.Id, rebelCivId, CasusBelli.IdeologicalWar);
            DiplomacySystem.ShiftRelation(civ.Id, rebelCivId, -100f);

            FileLogger.Log(
                $"[TICK {Simulation.Instance.TotalTicks}] ⚔️ CIVIL WAR: {civ.Name} vs Rebel Faction " +
                $"({eliteCountFinal} elites vs {rebelCount} rebels)",
                FileLogger.LogLevel.War);

            EventBus.Publish(new SimEvent
            {
                Type = SimEventType.Combat,
                Tick = Simulation.Instance.TotalTicks,
                Data = $"Revolution:{civ.Name}",
                Value = gini
            });
        }

        private static float medianWealth(List<(Agent agent, float wealth)> sorted)
        {
            if (sorted.Count == 0) return 0f;
            return sorted[sorted.Count / 2].wealth;
        }
    }
}
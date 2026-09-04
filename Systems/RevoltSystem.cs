using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    /// <summary>
    /// Эмерджентные революции: наблюдение за неравенством + раскол цивилизации.
    /// Расчет идёт на уровне АГЕНТОВ, семья даёт бафф/дебафф к индивидуальному богатству.
    /// Это позволяет расколоть даже одну династию — брат против брата.
    /// </summary>
    public static class RevoltSystem
    {
        // === ПОРОГИ ДЛЯ ТРИГГЕРА РЕВОЛЮЦИИ ===
        private const float GiniThreshold = 0.45f;         // Высокое неравенство (было 0.65)
        private const float DespairThreshold = 40f;        // Среднее недовольство (было 50)
        private const float WealthRatioThreshold = 2.5f;   // Элита в 2.5x богаче среднего (было 3)
        private const float RevoltPopulationRatio = 0.4f;  // 40% уходит в бунт
        private const int MinCivSizeForRevolt = 15;

        // === Кэш для оптимизации (строится один раз за проверку) ===
        private class RevoltCache
        {
            public Dictionary<string, List<Agent>> Families = new();
            public Dictionary<string, HashSet<string>> FamilyKnowledge = new();
            public Dictionary<string, float> FamilyAvgAge = new();
            public Dictionary<Guid, float> AgentKnowledgeCount = new();
        }

        /// <summary>
        /// Главный метод проверки. Вызывается каждые 500 тиков из Simulation.Tick().
        /// </summary>
        public static void CheckRevoltConditions(List<CivilizationSnapshot> civs)
        {
            if (civs == null) return;

            foreach (var civ in civs)
            {
                if (civ.Members == null || civ.Members.Count < MinCivSizeForRevolt) continue;

                // Строим кэш для этой цивилизации (оптимизация)
                var cache = BuildCache(civ.Members);

                // 1. Рассчитываем богатство каждого агента (индивидуальное + семейный модификатор)
                var agentWealth = new Dictionary<Guid, float>();
                foreach (var agent in civ.Members)
                {
                    float wealth = CalculateAgentWealth(agent, cache);
                    agentWealth[agent.Id] = wealth;
                }

                // 2. Считаем коэффициент Джини
                float gini = CalculateGini(agentWealth.Values.ToList());
                if (gini < GiniThreshold) continue;

                // 3. Проверяем среднее недовольство
                float avgDespair = civ.Members.Average(m => m.Despair);
                if (avgDespair < DespairThreshold) continue;

                // 4. Проверяем разрыв элита/медиана
                var sortedWealth = agentWealth.Values.OrderBy(w => w).ToList();
                float medianWealth = sortedWealth[sortedWealth.Count / 2];
                float eliteWealth = sortedWealth.Skip((int)(sortedWealth.Count * 0.9f)).Average();

                if (medianWealth <= 0f || eliteWealth / medianWealth < WealthRatioThreshold)
                    continue;

                // === УСЛОВИЯ ВЫПОЛНЕНЫ — РЕВОЛЮЦИЯ! ===
                TriggerRevolt(civ, gini, avgDespair, agentWealth, cache);
            }
        }

        /// <summary>
        /// Строит кэш семей и знаний для оптимизации расчетов.
        /// </summary>
        private static RevoltCache BuildCache(List<Agent> members)
        {
            var cache = new RevoltCache();

            // Группируем агентов по семьям
            foreach (var agent in members)
            {
                if (string.IsNullOrEmpty(agent.FamilyId)) continue;
                if (!cache.Families.TryGetValue(agent.FamilyId, out var list))
                {
                    list = new List<Agent>();
                    cache.Families[agent.FamilyId] = list;
                }
                list.Add(agent);
            }

            // Для каждой семьи считаем коллективные знания и средний возраст
            foreach (var (familyId, familyMembers) in cache.Families)
            {
                var familyKnowledge = new HashSet<string>();
                float ageSum = 0f;

                foreach (var member in familyMembers)
                {
                    ageSum += member.Age / member.MaxAge;

                    // Знания этого агента (кэшируем для повторного использования)
                    if (!cache.AgentKnowledgeCount.ContainsKey(member.Id))
                    {
                        int knowledgeCount = KnowledgeSystem.All.Count(k => k.Knowers.Contains(member.Id));
                        cache.AgentKnowledgeCount[member.Id] = knowledgeCount;
                    }

                    foreach (var k in KnowledgeSystem.All.Where(k => k.Knowers.Contains(member.Id)))
                        familyKnowledge.Add(k.Id);
                }

                cache.FamilyKnowledge[familyId] = familyKnowledge;
                cache.FamilyAvgAge[familyId] = ageSum / familyMembers.Count;
            }

            // Для агентов без семьи тоже кэшируем знания
            foreach (var agent in members)
            {
                if (!cache.AgentKnowledgeCount.ContainsKey(agent.Id))
                {
                    int knowledgeCount = KnowledgeSystem.All.Count(k => k.Knowers.Contains(agent.Id));
                    cache.AgentKnowledgeCount[agent.Id] = knowledgeCount;
                }
            }

            return cache;
        }

        /// <summary>
        /// Индивидуальное богатство агента + семейный модификатор.
        /// </summary>
        private static float CalculateAgentWealth(Agent agent, RevoltCache cache)
        {
            float wealth = 0f;

            // === 1. ИНДИВИДУАЛЬНОЕ БОГАТСТВО ===

            // 1.1 Знания (интеллектуальный капитал)
            float knowledgeCount = cache.AgentKnowledgeCount.GetValueOrDefault(agent.Id);
            wealth += knowledgeCount * 10f;

            // 1.2 Материальное богатство (инвентарь)
            foreach (var obj in agent.Body.Inventory)
            {
                if (MaterialDB.TryGet(obj.MaterialId, out var spec))
                    wealth += obj.Quantity * (1f + spec.Rarity + spec.Hardness);
            }

            // 1.3 Возраст (опыт)
            wealth += (agent.Age / agent.MaxAge) * 15f;

            // 1.4 Близость к институтам (инфраструктура)
            var world = Simulation.Instance.World;
            if (world != null && agent.Position.X >= 0 && agent.Position.X < world.GetLength(0)
                               && agent.Position.Y >= 0 && agent.Position.Y < world.GetLength(1))
            {
                var tile = world[agent.Position.X, agent.Position.Y];
                wealth += tile.InstitutionLevel * 8f;
            }

            // 1.5 Здоровье и энергия (жизнеспособность)
            wealth += agent.Body.Health / 15f;
            wealth += agent.Body.Energy / 15f;

            // === 2. СЕМЕЙНЫЙ МОДИФИКАТОР (бафф/дебафф) ===
            float familyModifier = CalculateFamilyModifier(agent.FamilyId, cache);
            wealth *= familyModifier;

            return Math.Max(0f, wealth);
        }

        /// <summary>
        /// Семейный модификатор богатства. Множитель от 0.6 до 2.0.
        /// Большие старые семьи с коллективными знаниями — сильный бафф.
        /// Маленькие молодые семьи без знаний — дебафф.
        /// </summary>
        private static float CalculateFamilyModifier(string familyId, RevoltCache cache)
        {
            if (string.IsNullOrEmpty(familyId) || !cache.Families.ContainsKey(familyId))
                return 1.0f; // Нет семьи = нейтральный множитель

            var familyMembers = cache.Families[familyId];
            int familySize = familyMembers.Count;

            // Базовый модификатор по размеру семьи
            float sizeModifier;
            if (familySize >= 15) sizeModifier = 2.0f;         // Большая династия
            else if (familySize >= 10) sizeModifier = 1.6f;    // Крупная семья
            else if (familySize >= 5) sizeModifier = 1.3f;     // Средняя семья
            else if (familySize >= 3) sizeModifier = 1.0f;     // Малая семья
            else sizeModifier = 0.8f;                           // Очень маленькая семья (дебафф)

            // Бонус за коллективные знания семьи
            int familyKnowledge = cache.FamilyKnowledge.GetValueOrDefault(familyId)?.Count ?? 0;
            float knowledgeBonus = Math.Min(0.4f, familyKnowledge * 0.02f);

            // Бонус за старость династии (опыт поколений)
            float familyAge = cache.FamilyAvgAge.GetValueOrDefault(familyId);
            float ageBonus = familyAge * 0.2f; // до 0.2 за старые роды

            float totalModifier = sizeModifier + knowledgeBonus + ageBonus;
            return Math.Clamp(totalModifier, 0.6f, 2.5f);
        }

        /// <summary>
        /// Классический коэффициент Джини. 0 = равенство, 1 = полное неравенство.
        /// </summary>
        private static float CalculateGini(List<float> wealth)
        {
            if (wealth == null || wealth.Count < 2) return 0f;

            var sorted = wealth.OrderBy(w => w).ToList();
            int n = sorted.Count;
            float totalWealth = sorted.Sum();
            if (totalWealth <= 0f) return 0f;

            float gini = 0f;
            for (int i = 0; i < n; i++)
                gini += (2f * (i + 1) - n - 1) * sorted[i];
            gini /= (n * totalWealth);

            return Math.Clamp(gini, 0f, 1f);
        }
        /// <summary>
        /// Публичный метод для аналитики (вызывается из CivArchive). 
        /// Считает коэффициент Джини по агентам цивилизации.
        /// </summary>
        public static float CalculateGiniCoefficient(List<Agent> agents)
        {
            if (agents == null || agents.Count < 2) return 0f;

            // Строим кэш семей и знаний для этой группы агентов
            var cache = BuildCache(agents);

            // Считаем индивидуальное богатство каждого агента (с учётом семейного модификатора)
            var agentWealth = new List<float>();
            foreach (var agent in agents)
            {
                float wealth = CalculateAgentWealth(agent, cache);
                agentWealth.Add(wealth);
            }

            // Возвращаем классический коэффициент Джини
            return CalculateGini(agentWealth);
        }
        /// <summary>
        /// Запускает революцию: разделяет цивилизацию на две.
        /// Семьи могут разделиться — брат против брата!
        /// </summary>
        private static void TriggerRevolt(
            CivilizationSnapshot civ, float gini, float avgDespair,
            Dictionary<Guid, float> agentWealth, RevoltCache cache)
        {
            var rng = RandomProvider.GetRandom();
            int tick = Simulation.Instance.TotalTicks;

            FileLogger.Log(
                $"[TICK {tick}] 🚨 REVOLUTION in {civ.Name}! " +
                $"Gini={gini:F2}, AvgDespair={avgDespair:F1}",
                FileLogger.LogLevel.War);

            // Сортируем агентов по богатству
            var sorted = civ.Members
                .Select(a => (agent: a, wealth: agentWealth.GetValueOrDefault(a.Id)))
                .OrderBy(x => x.wealth)
                .ToList();

            float medianWealth = sorted[sorted.Count / 2].wealth;

            // === РАСПРЕДЕЛЕНИЕ ПО СТОРОНАМ ===
            var rebels = new HashSet<Guid>();
            var loyalists = new HashSet<Guid>();
            var undecided = new List<Agent>();

            int revoltCount = Math.Max(3, (int)(civ.Members.Count * RevoltPopulationRatio));

            // Бедные 40% → в основном бунтари
            for (int i = 0; i < revoltCount && i < sorted.Count; i++)
            {
                var entry = sorted[i];
                if (entry.agent.Despair > 30f || entry.wealth < medianWealth * 0.7f)
                    rebels.Add(entry.agent.Id);
                else
                    undecided.Add(entry.agent);
            }

            // Богатые 10% → лоялисты (элита)
            int eliteCount = Math.Max(1, civ.Members.Count / 10);
            for (int i = sorted.Count - 1; i >= sorted.Count - eliteCount && i >= 0; i--)
                loyalists.Add(sorted[i].agent.Id);

            // Средний класс → выбирают сторону по Despair + семейным связям
            foreach (var entry in sorted.Skip(revoltCount).Take(sorted.Count - revoltCount - eliteCount))
            {
                if (loyalists.Contains(entry.agent.Id) || rebels.Contains(entry.agent.Id))
                    continue;

                float revoltChance = Math.Clamp(entry.agent.Despair / 100f, 0.2f, 0.8f);

                // Семейный фактор: если большинство семьи уже выбрало сторону, агент с большей вероятностью присоединится
                if (!string.IsNullOrEmpty(entry.agent.FamilyId) && cache.Families.TryGetValue(entry.agent.FamilyId, out var family))
                {
                    int familyRebels = family.Count(a => rebels.Contains(a.Id));
                    int familyLoyal = family.Count(a => loyalists.Contains(a.Id));

                    if (familyRebels > familyLoyal) revoltChance += 0.15f;
                    else if (familyLoyal > familyRebels) revoltChance -= 0.15f;
                }

                revoltChance = Math.Clamp(revoltChance, 0.05f, 0.95f);

                if (rng.NextDouble() < revoltChance)
                    rebels.Add(entry.agent.Id);
                else
                    loyalists.Add(entry.agent.Id);
            }

            // === СОЗДАЁМ НОВУЮ ЦИВИЛИЗАЦИЮ ИЗ БУНТАРЕЙ ===
            string rebelCivId = Guid.NewGuid().ToString()[..8];
            int rebelCount = 0;
            int loyalistCount = 0;
            int splitFamilies = 0;

            foreach (var agent in civ.Members)
            {
                if (rebels.Contains(agent.Id))
                {
                    agent.CivilizationId = rebelCivId;
                    agent.Despair = 0f;
                    agent.Loneliness = 30f;
                    rebelCount++;
                }
                else
                {
                    loyalistCount++;
                }
            }

            // Подсчитываем расколотые семьи (для логирования драматизма)
            foreach (var (familyId, members) in cache.Families)
            {
                bool hasRebel = members.Any(a => rebels.Contains(a.Id));
                bool hasLoyal = members.Any(a => loyalists.Contains(a.Id));
                if (hasRebel && hasLoyal) splitFamilies++;
            }

            // === ОБЪЯВЛЯЕМ ВОЙНУ ===
            DiplomacySystem.DeclareWar(civ.Id, rebelCivId, CasusBelli.IdeologicalWar);
            DiplomacySystem.ShiftRelation(civ.Id, rebelCivId, -100f);

            FileLogger.Log(
                $"[TICK {tick}] ⚔️ CIVIL WAR: {civ.Name} splits! " +
                $"{loyalistCount} loyalists vs {rebelCount} rebels. " +
                $"Families torn apart: {splitFamilies}",
                FileLogger.LogLevel.War);

            EventBus.Publish(new SimEvent
            {
                Type = SimEventType.Combat,
                Tick = tick,
                Data = $"Revolution:{civ.Name} (Gini={gini:F2})",
                Value = gini
            });
        }
    }
} 
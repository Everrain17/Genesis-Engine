using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Physics;
using GenesisEngine.World;
using GenesisEngine.UI;
namespace GenesisEngine.Systems
{
    public static class AdvancedCognitivePrimitives
    {
        // ============================================================
        // 1. ОБЪЕКТНАЯ ПЕРМАНЕНТНОСТЬ
        // Агент помнит объекты, которые видел раньше
        // ============================================================
        private class SeenObject
        {
            public string MaterialId;
            public Vector2 Position;
            public int LastSeenTick;
            public float Quantity;
            public float Importance;
        }

        // ============================================================
        // 4. ПРОСТРАНСТВЕННАЯ ПАМЯТЬ
        // Агент помнит важные места
        // ============================================================
        private class PlaceMemory
        {
            public Vector2 Position;
            public string Kind; // food, knowledge, shelter, sacred
            public int LastSeenTick;
            public float Importance;
        }

        private static readonly SortedDictionary<Guid, List<SeenObject>> ObjectMemory = new();
        private static readonly SortedDictionary<Guid, List<PlaceMemory>> _placeMemories = new();


        // ============================================================
        // 2. КАТЕГОРИЗАЦИЯ
        // ============================================================
        private static readonly SortedDictionary<Guid, Dictionary<string, int>> CategoryMemory = new();

        public static void Update(Agent agent, Tile tile, Random rng)
        {
            if (agent == null || tile == null || rng == null)
                return;

            var sim = Simulation.Instance;

            if (sim == null)
                return;

            int tick = sim.TotalTicks;

            // Фаза для каждого агента, чтобы не нагружать один тик
            int phase = agent.Id.GetHashCode() & 0x7fffffff;

            if ((tick + phase) % 5 == 0)
                UpdateObjectPermanence(agent, tile, tick);

            if ((tick + phase) % 25 == 0)
                UpdateCategorization(agent, tile);

            if ((tick + phase) % 30 == 0)
                UpdateCompositionality(agent, tile);

            if ((tick + phase) % 20 == 0)
                UpdateSpatial(agent, tile, tick);
               
        }

        // ============================================================
        // 1. ОБЪЕКТНАЯ ПЕРМАНЕНТНОСТЬ
        // ============================================================
        private static void UpdateObjectPermanence(Agent agent, Tile tile, int tick)
        {
            if (!ObjectMemory.TryGetValue(agent.Id, out var memory))
            {
                memory = new List<SeenObject>();
                ObjectMemory[agent.Id] = memory;
            }

            // Удаляем старые воспоминания
            memory.RemoveAll(m => tick - m.LastSeenTick > 2000);

            int scanned = 0;

            foreach (var obj in tile.GroundObjects)
            {
                if (scanned++ >= 12)
                    break;

                if (obj == null || obj.Quantity <= 0.1f)
                    continue;

                float importance = CalculateObjectImportance(agent, obj.MaterialId);

                RememberObject(
                    memory,
                    obj.MaterialId,
                    new Vector2(tile.X, tile.Y),
                    obj.Quantity,
                    importance,
                    tick);
            }

            TrimSeenObjects(memory);

            CognitionSystem.Record("object_permanence.memory", memory.Count);
        }

        private static void RememberObject(
            List<SeenObject> memory,
            string materialId,
            Vector2 position,
            float quantity,
            float importance,
            int tick)
        {
            var existing = memory.FirstOrDefault(m =>
                m.MaterialId == materialId &&
                m.Position == position);

            if (existing != null)
            {
                existing.LastSeenTick = tick;
                existing.Quantity = quantity;
                existing.Importance = Math.Max(existing.Importance * 0.8f, importance);
            }
            else
            {
                memory.Add(new SeenObject
                {
                    MaterialId = materialId,
                    Position = position,
                    LastSeenTick = tick,
                    Quantity = quantity,
                    Importance = importance
                });
            }
        }

        private static float CalculateObjectImportance(Agent agent, string materialId)
        {
            if (!MaterialDB.TryGet(materialId, out var spec))
                return 0.05f;

            float importance = 0.1f;

            if (spec.Organic > 0.5f)
                importance += 0.8f + agent.Body.Hunger / 150f;

            if (spec.Hardness > 0.6f)
                importance += 0.35f;

            if (spec.Conductivity > 0.6f)
                importance += 0.30f;

            if (spec.Logic > 0.5f)
                importance += 0.55f;

            if (spec.Rarity > 0.7f)
                importance += 0.35f;

            return importance;
        }

        private static void TrimSeenObjects(List<SeenObject> memory)
        {
            while (memory.Count > 32)
            {
                int worstIndex = 0;
                float worstImportance = float.MaxValue;

                for (int i = 0; i < memory.Count; i++)
                {
                    if (memory[i].Importance < worstImportance)
                    {
                        worstImportance = memory[i].Importance;
                        worstIndex = i;
                    }
                }

                memory.RemoveAt(worstIndex);
            }
        }

        // ============================================================
        // 2. КАТЕГОРИЗАЦИЯ
        // ============================================================
        private static void UpdateCategorization(Agent agent, Tile tile)
        {
            if (!CategoryMemory.TryGetValue(agent.Id, out var categories))
            {
                categories = new Dictionary<string, int>();
                CategoryMemory[agent.Id] = categories;
            }

            int scanned = 0;

            foreach (var obj in agent.Body.Inventory)
            {
                if (scanned++ >= 6)
                    break;

                if (!MaterialDB.TryGet(obj.MaterialId, out var spec))
                    continue;

                string category = Categorize(spec);

                if (!categories.ContainsKey(category))
                    categories[category] = 0;

                categories[category]++;

                CognitionSystem.Record("category." + category, 1f);
            }

            scanned = 0;

            foreach (var obj in tile.GroundObjects)
            {
                if (scanned++ >= 8)
                    break;

                if (!MaterialDB.TryGet(obj.MaterialId, out var spec))
                    continue;

                string category = Categorize(spec);

                if (!categories.ContainsKey(category))
                    categories[category] = 0;

                categories[category]++;

                CognitionSystem.Record("category." + category, 1f);
            }

            if (categories.Count > 24)
                categories.Clear();
        }

        private static string Categorize(ResourceSpec spec)
        {
            if (spec.Organic > 0.60f)
                return "organic";

            if (spec.Hardness > 0.65f)
                return "hard";

            if (spec.Conductivity > 0.60f)
                return "conductive";

            if (spec.Flexibility > 0.60f)
                return "flexible";

            if (spec.Logic > 0.55f)
                return "logic";

            if (spec.Rarity > 0.75f)
                return "rare";

            if (spec.HeatOutput > 0.60f)
                return "hot";

            if (spec.Buoyancy > 0.60f)
                return "buoyant";

            return "common";
        }

        // ============================================================
        // 3. КОМПОЗИЦИОННОСТЬ
        // Агент замечает, что композит состоит из частей
        // ============================================================
        private static void UpdateCompositionality(Agent agent, Tile tile)
        {
            int scanned = 0;

            foreach (var obj in agent.Body.Inventory)
            {
                if (scanned++ >= 6)
                    break;

                ScanComposite(agent, obj.MaterialId);
            }

            scanned = 0;

            foreach (var obj in tile.GroundObjects)
            {
                if (scanned++ >= 8)
                    break;

                ScanComposite(agent, obj.MaterialId);
            }
        }

        private static void ScanComposite(Agent agent, string materialId)
        {
            if (string.IsNullOrEmpty(materialId))
                return;

            if (!materialId.Contains("+"))
                return;

            if (!MaterialDB.TryGet(materialId, out var spec))
                return;

            int parts = materialId.Split('+').Length;

            CognitionSystem.Record("composition.parts", parts);
            CognitionSystem.Record("composition.depth", spec.Depth);

            if (spec.Depth > 2)
                CognitionSystem.Record("composition.deep", 1f);
        }

        // ============================================================
        // 4. ПРОСТРАНСТВЕННОЕ МЫШЛЕНИЕ
        // ============================================================
        private static void UpdateSpatial(Agent agent, Tile tile, int tick)
        {
            if (!_placeMemories.TryGetValue(agent.Id, out var places))
            {
                places = new List<PlaceMemory>();
                _placeMemories[agent.Id] = places;
            }
            places.RemoveAll(p => tick - p.LastSeenTick > 2500);

            float homeDistance = agent.Position.Distance(agent.HomePosition);
            CognitionSystem.Record("spatial.home_distance", homeDistance);
            if (homeDistance > 25f) CognitionSystem.Record("spatial.far", 1f);

            if (tile.TotalFood > 50f) RememberPlace(places, new Vector2(tile.X, tile.Y), "food", tick, 1f);
            if (tile.InstitutionLevel > 1f || tile.IsLibrary || tile.IsTemple) RememberPlace(places, new Vector2(tile.X, tile.Y), "knowledge", tick, 1f);
            if (tile.IsHouse) RememberPlace(places, new Vector2(tile.X, tile.Y), "shelter", tick, 0.8f);
            if (tile.SanctityLevel > 20f) RememberPlace(places, new Vector2(tile.X, tile.Y), "sacred", tick, 0.7f);

            TrimPlaces(places);
            CognitionSystem.Record("spatial.places", places.Count);
        }

        private static void RememberPlace(
            List<PlaceMemory> places,
            Vector2 position,
            string kind,
            int tick,
            float importance)
        {
            var existing = places.FirstOrDefault(p =>
                p.Kind == kind &&
                p.Position == position);

            if (existing != null)
            {
                existing.LastSeenTick = tick;
                existing.Importance = Math.Max(existing.Importance * 0.8f, importance);
            }
            else
            {
                places.Add(new PlaceMemory
                {
                    Position = position,
                    Kind = kind,
                    LastSeenTick = tick,
                    Importance = importance
                });
            }
        }

        private static void TrimPlaces(List<PlaceMemory> places)
        {
            while (places.Count > 16)
            {
                int oldestIndex = 0;
                int oldestTick = int.MaxValue;

                for (int i = 0; i < places.Count; i++)
                {
                    if (places[i].LastSeenTick < oldestTick)
                    {
                        oldestTick = places[i].LastSeenTick;
                        oldestIndex = i;
                    }
                }

                places.RemoveAt(oldestIndex);
            }
        }

        // ============================================================
        // ИСПОЛЬЗОВАНИЕ ПАМЯТИ ДЛЯ ПОВЕДЕНИЯ
        // ============================================================
        public static bool TryUseSpatialGoals(Agent agent, Tile[,] world, Random rng)
        {
            if (agent == null || world == null || rng == null)
                return false;

            // 1. Если голоден и помнит еду — идёт к запомненной еде
            if (agent.Body.Hunger > 60f &&
                ObjectMemory.TryGetValue(agent.Id, out var objects))
            {
                var foodMemory = objects
                    .Where(o =>
                        o.Importance > 0.8f &&
                        o.Quantity > 0.5f &&
                        o.Position != agent.Position)
                    .OrderBy(o => agent.Position.Distance(o.Position))
                    .FirstOrDefault();

                if (foodMemory != null &&
                    agent.Position.Distance(foodMemory.Position) <= 18f &&
                    rng.NextDouble() < 0.60f)
                {
                    if (MoveToward(agent, world, foodMemory.Position))
                    {
                        agent.LastAction = "SeekRememberedFood";
                        return true;
                    }
                }
            }

            // 2. Если очень одинок — может вернуться к дому
            if (agent.Loneliness > 80f &&
                rng.NextDouble() < 0.30f)
            {
                if (agent.Position.Distance(agent.HomePosition) > 3f)
                {
                    if (MoveToward(agent, world, agent.HomePosition))
                    {
                        agent.LastAction = "ReturnHome";
                        return true;
                    }
                }
            }

            // 3. Если любопытный и логичный — может идти к месту знания
            if (agent.Curiosity > 0.70f &&
                agent.Logic > 0.45f &&
                _placeMemories.TryGetValue(agent.Id, out var places))
            {
                var knowledgePlace = places
                    .Where(p =>
                        p.Kind == "knowledge" &&
                        p.Position != agent.Position)
                    .OrderBy(p => agent.Position.Distance(p.Position))
                    .FirstOrDefault();

                if (knowledgePlace != null &&
                    agent.Position.Distance(knowledgePlace.Position) <= 20f &&
                    rng.NextDouble() < 0.25f)
                {
                    if (MoveToward(agent, world, knowledgePlace.Position))
                    {
                        agent.LastAction = "SeekKnowledgePlace";
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool MoveToward(Agent agent, Tile[,] world, Vector2 target)
        {
            int dx = Math.Sign(target.X - agent.Position.X);
            int dy = Math.Sign(target.Y - agent.Position.Y);

            if (dx != 0 && TryStep(agent, world, agent.Position.X + dx, agent.Position.Y))
                return true;

            if (dy != 0 && TryStep(agent, world, agent.Position.X, agent.Position.Y + dy))
                return true;

            return false;
        }

        private static bool TryStep(Agent agent, Tile[,] world, int x, int y)
        {
            if (world == null)
                return false;

            if (x < 0 || y < 0 || x >= world.GetLength(0) || y >= world.GetLength(1))
                return false;

            var tile = world[x, y];

            bool canEnter = tile.IsPassable || CombinationEngine.CanCross(agent, tile.Terrain);

            if (!canEnter)
                return false;

            agent.Position = new Vector2(x, y);
            return true;
        }
        // ============================================================
        // 6. СОЦИАЛЬНОЕ СРАВНЕНИЕ
        // Агент сравнивает себя с другими и замечает неравенство
        // ============================================================
        private static void UpdateSocialComparison(Agent agent, Tile tile)
        {
            var nearby = SpatialGrid.GetNearby(agent.Position, 5);
            if (nearby.Count < 3) return;

            // Считаем своё "богатство"
            float myWealth = CalculateWealth(agent);

            // Сравниваем с другими
            int richer = 0;
            int poorer = 0;
            float avgWealth = 0f;

            foreach (var other in nearby)
            {
                if (other.Id == agent.Id) continue;
                float otherWealth = CalculateWealth(other);
                avgWealth += otherWealth;

                if (otherWealth > myWealth * 1.5f) richer++;
                if (otherWealth < myWealth * 0.5f) poorer++;
            }

            avgWealth /= Math.Max(1, nearby.Count);

            // Записываем когнитивные метрики
            if (richer > nearby.Count * 0.3f)
            {
                CognitionSystem.Record("social.richer_nearby", richer);
                CognitionSystem.Record("social.inequality_perceived", 1f);
            }

            // Агенты с низкой самосознанностью не замечают неравенства
            if (agent.Genome.SelfAwareness > 0.4f && richer > 2)
            {
                // НОВОЕ: Недовольство растёт
                agent.Despair = Math.Min(100f, agent.Despair + 0.05f * richer);
            }
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

            // Здоровье и энергия
            wealth += agent.Body.Health / 10f;
            wealth += agent.Body.Energy / 10f;

            // Близость к институтам
            var tile = Simulation.Instance.GetTile(agent.Position);
            if (tile.BuildingFunctional)
            {
                wealth += tile.BuildingQuality * 2f;
            }

            return wealth;
        }
        // ============================================================
        // ОЧИСТКА ПАМЯТИ ПРИ СМЕРТИ
        // ============================================================
        public static void OnAgentDeath(Guid agentId)
        {
            ObjectMemory.Remove(agentId);
            _placeMemories.Remove(agentId);
            CategoryMemory.Remove(agentId);
        }
    }

        public static class EmergentDeductionSystem
        {
            /// <summary>
            /// Агент пытается самостоятельно вывести свойства окружения на основе корреляций,
            /// БЕЗ хардкода понятий "холод", "ветер" или "вирус".
            /// </summary>
            public static void Update(Agent agent, Tile tile, Random rng)
            {
                if (agent == null || tile == null) return;

                int currentTick = Simulation.Instance.TotalTicks;

                // Проверяем корреляции не каждый тик, а раз в 50 тиков для экономии
                if (currentTick - agent.LastDeductionTick < 50) return;
                agent.LastDeductionTick = currentTick;

                // 1. Вычисляем дельту внутреннего состояния
                float healthDelta = agent.Body.Health - agent.LastHealth;
                float energyDelta = agent.Body.Energy - agent.LastEnergy;

                // Обновляем последние значения
                agent.LastHealth = agent.Body.Health;
                agent.LastEnergy = agent.Body.Energy;

                // Если изменений нет или они положительные (агент отдохнул/поел), дедукция не нужна
                float totalNegativeImpact = Math.Abs(Math.Min(0f, healthDelta)) + Math.Abs(Math.Min(0f, energyDelta)) * 0.5f;
                if (totalNegativeImpact < 0.1f) return;

                // 2. Формируем "Сырой Хеш Окружения" (Raw Environmental Hash)
                // Агент не знает названий, он видит только бинарные признаки
                string rawEnvHash = GetRawEnvironmentalHash(tile, agent);

                // 3. Записываем корреляцию: "Это окружение причинило мне X урона"
                if (!agent.EnvironmentalCorrelations.ContainsKey(rawEnvHash))
                {
                    agent.EnvironmentalCorrelations[rawEnvHash] = 0f;
                }
                agent.EnvironmentalCorrelations[rawEnvHash] += totalNegativeImpact;

                // 4. Эмерджентное открытие (Threshold)
                // Если накопленный "урон" от этого хеша превысил порог, агент "понимает" свойство
                float correlationScore = agent.EnvironmentalCorrelations[rawEnvHash];
                if (correlationScore > 5.0f) // Порог "осознания опасности/свойства"
                {
                    // Агент эмерджентно классифицирует это окружение как "Вредное" или "Опасное"
                    CognitionSystem.Record("deduction.environmental_hazard", correlationScore);

                    // Если агент достаточно умён (SelfAwareness), он пытается предупредить других
                    if (agent.Genome.SelfAwareness > 0.5f && rng.NextDouble() < 0.3f)
                    {
                        // Он излучает сигнал тревоги, привязанный к этому контексту
                        // Другие агенты, услышав его, тоже начнут формировать эту корреляцию быстрее (социальное обучение)
                        SignalSystem.EmitSignal(agent, SignalType.Danger, 0.8f, 12f, $"hazard_{rawEnvHash}");
                    }

                    // Сбрасываем счётчик, чтобы не спамить открытиями, но сохраняем знание
                    agent.EnvironmentalCorrelations[rawEnvHash] = 0f;

                    FileLogger.Log(
                        $"[TICK {currentTick}] EMERGENT DEDUCTION: Agent {agent.Id} correlated env hash '{rawEnvHash}' " +
                        $"with negative impact (Score: {correlationScore:F1}). Hazard recognized.",
                        FileLogger.LogLevel.Info);
                }

                // Очистка старых корреляций для экономии памяти
                if (agent.EnvironmentalCorrelations.Count > 20)
                {
                    var oldest = agent.EnvironmentalCorrelations.Keys.First();
                    agent.EnvironmentalCorrelations.Remove(oldest);
                }
            }


        /// <summary>
        /// Создаёт "сырое" описание окружения без использования понятий движка.
        /// Только бинарные признаки, которые агент может "ощутить".
        /// </summary>
        private static string GetRawEnvironmentalHash(Tile tile, Agent agent)
        {
            var parts = new List<string>();

            // Визуальные/тактильные признаки
            if (tile.Temperature < 0.3f) parts.Add("V_Cold");
            else if (tile.Temperature > 0.7f) parts.Add("V_Hot");

            if (tile.Moisture > 0.7f) parts.Add("V_Wet");
            else if (tile.Moisture < 0.2f) parts.Add("V_Dry");

            if (tile.WildnessLevel > 1.5f) parts.Add("V_Wild");

            // 1. Биологические признаки: Больные агенты (берём из SpatialGrid, так как там только агенты)
            var nearbyAgents = SpatialGrid.GetNearby(agent.Position, 2);
            int sickNearby = nearbyAgents.Count(a => a.Infected || a.Body.Health < 50f);
            if (sickNearby > 0)
                parts.Add($"B_SickNearby({sickNearby})");

            // 2. Биологические признаки: Хищники (ИСПРАВЛЕНО: ищем в глобальном списке существ)
            int predatorsNearby = Simulation.Instance.Creatures.Count(c =>
                c.Behavior == CreatureBehavior.Predator &&
                c.Position.Distance(agent.Position) <= 2f); // 2f - тот же радиус, что и в GetNearby

            if (predatorsNearby > 0)
                parts.Add($"B_PredatorNearby({predatorsNearby})");

            // Если признаков нет, возвращаем базовый хеш
            if (parts.Count == 0) return "Env_Normal";

            // Сортируем для детерминизма хеша (чтобы "V_Cold_V_Wet" и "V_Wet_V_Cold" были одним и тем же хешем)
            parts.Sort();
            return string.Join("_", parts);
        }
    }
    
}

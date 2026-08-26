using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Physics;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class CognitivePrimitives
    {
        // ============================================================
        // 1. СУБИТИЗАЦИЯ (Subitization)
        // Мгновенное распознавание 1-4 объектов без счета
        // ============================================================
        public class QuantityPerception
        {
            public string Context;           // "inventory.food", "tile.agents", "tile.objects"
            public int ExactSmall;           // точное значение для 1-4
            public float ApproximateLarge;   // приближенное для больших (с шумом)
            public float Confidence;
            public int Tick;
        }

        public static QuantityPerception PerceiveQuantity(Agent agent, string context, int count)
        {
            var perception = new QuantityPerception
            {
                Context = context,
                Tick = Simulation.Instance.TotalTicks
            };

            // Субитизация: 1-4 объекта распознаются точно
            if (count <= 4)
            {
                perception.ExactSmall = count;
                perception.ApproximateLarge = count;
                perception.Confidence = 1.0f;
            }
            else
            {
                // ANS: приближенное чувство числа с логарифмическим шумом
                // Закон Вебера: точность падает с ростом числа
                perception.ExactSmall = 0;
                float noise = (float)(Math.Log(count) * 0.15 * (RandomProvider.GetFloat() - 0.5f));
                perception.ApproximateLarge = count * (1f + noise);
                perception.Confidence = Math.Clamp(1f - (float)Math.Log(count) * 0.1f, 0.3f, 0.9f);
            }

            return perception;
        }

        // ============================================================
        // 2. СРАВНЕНИЕ (Comparison)
        // Автоматическое сравнение двух величин
        // ============================================================
        public class ComparisonObservation
        {
            public string ContextA;
            public string ContextB;
            public float ValueA;
            public float ValueB;
            public string Relation;  // "greater", "lesser", "equal"
            public float Confidence;
            public int Tick;
        }

        public static ComparisonObservation Compare(
            string contextA, float valueA,
            string contextB, float valueB)
        {
            var obs = new ComparisonObservation
            {
                ContextA = contextA,
                ContextB = contextB,
                ValueA = valueA,
                ValueB = valueB,
                Tick = Simulation.Instance.TotalTicks
            };

            float diff = Math.Abs(valueA - valueB);
            float avg = (valueA + valueB) / 2f;

            // Если разница мала относительно среднего — "равно"
            if (avg > 0 && diff / avg < 0.15f)
            {
                obs.Relation = "equal";
                obs.Confidence = 0.8f;
            }
            else if (valueA > valueB)
            {
                obs.Relation = "greater";
                obs.Confidence = Math.Clamp(diff / Math.Max(1f, avg), 0.5f, 1f);
            }
            else
            {
                obs.Relation = "lesser";
                obs.Confidence = Math.Clamp(diff / Math.Max(1f, avg), 0.5f, 1f);
            }

            return obs;
        }

        // ============================================================
        // 3. ПРИЧИННОСТЬ (Causality)
        // Замечание, что событие A предшествует событию B
        // ============================================================
        public class CausalPattern
        {
            public string Cause;
            public string Effect;
            public int CoOccurrences;
            public float Confidence;
            public int LastTick;
        }

        private static readonly SortedDictionary<string, CausalPattern> CausalPatterns = new();


        public static void RecordCausality(string cause, string effect)
        {
            string key = $"{cause}|{effect}";

            if (!CausalPatterns.TryGetValue(key, out var pattern))
            {
                pattern = new CausalPattern
                {
                    Cause = cause,
                    Effect = effect,
                    CoOccurrences = 0,
                    Confidence = 0f
                };
                CausalPatterns[key] = pattern;
            }

            pattern.CoOccurrences++;
            pattern.LastTick = Simulation.Instance.TotalTicks;

            // Уверенность растет с числом совпадений
            pattern.Confidence = Math.Clamp(pattern.CoOccurrences / 20f, 0f, 1f);
        }

        public static List<CausalPattern> GetStrongCausalPatterns(int minOccurrences = 5)
        {
            return CausalPatterns.Values
                .Where(p => p.CoOccurrences >= minOccurrences)
                .OrderByDescending(p => p.Confidence)
                .ToList();
        }

        // ============================================================
        // 4. ПОВТОРЯЕМОСТЬ (Repetition)
        // Замечание регулярных паттернов во времени
        // ============================================================
        public class RepetitionPattern
        {
            public string Event;
            public int Count;
            public int AverageIntervalTicks;
            public float Regularity;  // 0 = случайный, 1 = идеальный ритм
            public int LastTick;
        }

        private static readonly SortedDictionary<string, List<int>> EventHistory = new();
        private static readonly SortedDictionary<string, RepetitionPattern> RepetitionPatterns = new();

        public static void RecordEvent(string eventName)
        {
            int tick = Simulation.Instance.TotalTicks;

            if (!EventHistory.TryGetValue(eventName, out var history))
            {
                history = new List<int>();
                EventHistory[eventName] = history;
            }

            history.Add(tick);

            // Ограничиваем историю последними 50 событиями
            if (history.Count > 50)
                history.RemoveAt(0);

            // Анализируем повторяемость
            if (history.Count >= 5)
            {
                AnalyzeRepetition(eventName, history);
            }
        }

        private static void AnalyzeRepetition(string eventName, List<int> history)
        {
            var intervals = new List<int>();

            for (int i = 1; i < history.Count; i++)
            {
                intervals.Add(history[i] - history[i - 1]);
            }

            if (intervals.Count < 3)
                return;

            float avgInterval = (float)intervals.Average();
            float variance = intervals.Select(i => (i - avgInterval) * (i - avgInterval)).Average();
            float stdDev = MathF.Sqrt(variance);

            // Регулярность: низкая дисперсия = высокая регулярность
            float regularity = Math.Clamp(1f - stdDev / Math.Max(1f, avgInterval), 0f, 1f);

            var pattern = new RepetitionPattern
            {
                Event = eventName,
                Count = history.Count,
                AverageIntervalTicks = (int)avgInterval,
                Regularity = regularity,
                LastTick = history.Last()
            };

            RepetitionPatterns[eventName] = pattern;
        }

        public static List<RepetitionPattern> GetStrongRepetitionPatterns(float minRegularity = 0.5f)
        {
            return RepetitionPatterns.Values
                .Where(p => p.Regularity >= minRegularity && p.Count >= 5)
                .OrderByDescending(p => p.Regularity)
                .ToList();
        }

        // ============================================================
        // 5. АНАЛОГИЯ (Analogy)
        // Замечание структурного сходства между разными ситуациями
        // ============================================================
        public class AnalogyObservation
        {
            public string ContextA;
            public string ContextB;
            public string SharedProperty;
            public float Similarity;
            public int Tick;
        }

        public static AnalogyObservation DetectAnalogy(
            string contextA, ResourceSpec specA,
            string contextB, ResourceSpec specB)
        {
            // Сравниваем наблюдаемые свойства материалов
            float similarity = 0f;
            int matches = 0;

            // Проверяем основные свойства
            if (Math.Abs(specA.Hardness - specB.Hardness) < 0.2f)
            {
                similarity += 0.2f;
                matches++;
            }

            if (Math.Abs(specA.Conductivity - specB.Conductivity) < 0.2f)
            {
                similarity += 0.2f;
                matches++;
            }

            if (Math.Abs(specA.Organic - specB.Organic) < 0.2f)
            {
                similarity += 0.2f;
                matches++;
            }

            if (Math.Abs(specA.Logic - specB.Logic) < 0.2f)
            {
                similarity += 0.2f;
                matches++;
            }

            if (Math.Abs(specA.Flexibility - specB.Flexibility) < 0.2f)
            {
                similarity += 0.2f;
                matches++;
            }

            string sharedProperty = matches switch
            {
                0 => "none",
                1 => "single_property",
                2 => "partial_similarity",
                3 => "structural_similarity",
                4 => "strong_similarity",
                5 => "near_identical",
                _ => "unknown"
            };

            return new AnalogyObservation
            {
                ContextA = contextA,
                ContextB = contextB,
                SharedProperty = sharedProperty,
                Similarity = similarity,
                Tick = Simulation.Instance.TotalTicks
            };
        }

        // ============================================================
        // ИНТЕГРАЦИЯ: Запуск примитивов для агента
        // ============================================================
        public static void UpdateCognitivePrimitives(Agent agent)
        {
            if (agent == null)
                return;

            // Прореживание: запускаем не каждый тик
            if (Simulation.Instance.TotalTicks % 10 != 0)
                return;

            var tile = Simulation.Instance.GetTile(agent.Position);

            // 1. Субитизация: количество объектов в инвентаре
            int foodCount = agent.Body.Inventory.Count(o =>
                MaterialDB.TryGet(o.MaterialId, out var spec) && spec.Organic > 0.5f);

            if (foodCount > 0)
            {
                var perception = PerceiveQuantity(agent, "inventory.food", foodCount);
                RecordEvent($"quantity.inventory.food.{perception.ExactSmall}");
            }

            // 2. Субитизация: количество агентов рядом
            var nearby = SpatialGrid.GetNearby(agent.Position, 2);
            int nearbyCount = nearby.Count;

            if (nearbyCount > 0)
            {
                var perception = PerceiveQuantity(agent, "nearby.agents", nearbyCount);
                RecordEvent($"quantity.nearby.agents.{perception.ExactSmall}");
            }

            // 3. Субитизация: количество объектов на тайле
            int groundCount = tile.GroundObjects.Count;

            if (groundCount > 0)
            {
                var perception = PerceiveQuantity(agent, "tile.groundObjects", groundCount);
                RecordEvent($"quantity.tile.ground.{perception.ExactSmall}");
            }

            // 4. Причинность: после PickUp часто следует Consume
            if (agent.LastAction == "Consume")
            {
                // Проверяем, было ли недавно PickUp
                var recentPatterns = agent.Memory.Patterns
                    .Where(p => p.ActionType == "PickUp")
                    .OrderByDescending(p => p.Occurrences)
                    .FirstOrDefault();

                if (recentPatterns != null && recentPatterns.Occurrences > 3)
                {
                    RecordCausality("PickUp", "Consume");
                }
            }

            // 5. Причинность: после Combine часто следует Discovery
            if (agent.LastAction == "Combine")
            {
                RecordEvent("action.combine");
            }

            // 6. Повторяемость: регулярное потребление еды
            if (agent.LastAction == "Consume")
            {
                RecordEvent("action.consume");
            }

            // 7. Повторяемость: регулярное движение
            if (agent.LastAction == "Move")
            {
                RecordEvent("action.move");
            }

            // 8. Сравнение: текущий голод vs прошлый голод
            // (агент запоминает свой уровень голода)
            // Это можно расширить в будущем
        }

        public static void ClearOldPatterns(int maxAge = 10000)
        {
            int currentTick = Simulation.Instance.TotalTicks;

            // Очищаем старые причинные паттерны
            var oldCausal = CausalPatterns
                .Where(kv => currentTick - kv.Value.LastTick > maxAge)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in oldCausal)
                CausalPatterns.Remove(key);

            // Очищаем старые паттерны повторяемости
            var oldRepetition = RepetitionPatterns
                .Where(kv => currentTick - kv.Value.LastTick > maxAge)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in oldRepetition)
                RepetitionPatterns.Remove(key);
        }
    }
}
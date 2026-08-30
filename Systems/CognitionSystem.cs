using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.Systems.Physics;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public static class CognitionSystem
    {
        private class Stat
        {
            public int Count;
            public float Sum;
            public float SumSq;
            public float Min = float.MaxValue;
            public float Max = float.MinValue;
            public int LastTick;
        }

        private static readonly SortedDictionary<string, Stat> Stats = new();
        private static readonly SortedSet<string> MathDiscoveries = new();
        private static readonly SortedSet<string> TheoryDiscoveries = new();

        public static void Observe(SimEvent e)
        {
            if (e == null) return;

            switch (e.Type)
            {
                case SimEventType.AgentBorn:
                    Record("demography.birth", 1f);
                    break;

                case SimEventType.AgentDied:
                    Record("demography.death", 1f);
                    break;

                case SimEventType.Trade:
                    Record("economy.trade", 1f);
                    break;

                case SimEventType.Combat:
                    Record("conflict.combat", 1f);
                    break;

                case SimEventType.Hunt:
                    Record("ecology.hunt", Math.Max(0.1f, e.Value));
                    break;

                case SimEventType.BuildingCreated:
                    Record("construction.quality", Math.Max(0.01f, e.Value));
                    break;

                case SimEventType.ArtifactCreated:
                    Record("culture.artifact", Math.Max(0.01f, e.Value));
                    break;

                case SimEventType.MaterialMixed:
                    if (MaterialDB.TryGet(e.Data, out var spec))
                        Record("material.depth", spec.Depth);
                    break;
                case SimEventType.PlagueStarted:
                    Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "PlagueStarted", e.Actor?.CivilizationId, e.Data ?? "");
                    break;
            }
        }
        public static Dictionary<string, (int Count, float Avg)> SnapshotStats()
        {
            var snap = new Dictionary<string, (int, float)>();
            foreach (var kv in Stats)
                snap[kv.Key] = (kv.Value.Count, kv.Value.Sum / Math.Max(1, kv.Value.Count));
            return snap;
        }
        public static void Record(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            if (!Stats.TryGetValue(key, out var stat))
            {
                stat = new Stat();
                Stats[key] = stat;
            }

            stat.Count++;
            stat.Sum += value;
            stat.SumSq += value * value;
            stat.Min = Math.Min(stat.Min, value);
            stat.Max = Math.Max(stat.Max, value);
            stat.LastTick = Simulation.Instance.TotalTicks;
        }

        public static void RunMathAndScience(List<CivilizationSnapshot> civs, Random rng)
        {
            if (civs == null || rng == null)
                return;

            foreach (var civ in civs)
            {
                if (civ.Members.Count == 0)
                    continue;

                float avgLogic = civ.Members.Average(m =>
                    m.Genome.SelfAwareness * 0.5f +
                    m.Genome.Openness * 0.5f);

                int texts = CountTexts(civ);
                int knowledgePlaces = CountKnowledgePlaces(civ);

                float scienceCapacity =
                    avgLogic * civ.Members.Count * 0.10f +
                    texts * 0.50f +
                    knowledgePlaces * 0.75f +
                    civ.EducationLevel * 2f +
                    civ.GetCap("knowledge") * 3f +
                    LogicSystem.CivilizationComputationCapacity(civ) * 0.5f +
                    LogicAutomataSystem.GetCivComputation(civ.Id) * 0.75f;

                if (scienceCapacity < 2f)
                    continue;

                TryMathDiscovery(civ, avgLogic, scienceCapacity, rng);
                TryTheoryDiscovery(civ, avgLogic, scienceCapacity, texts, rng);
                TryQuarantineDiscovery(civ, avgLogic, scienceCapacity, rng);
                // === НОВОЕ: Открытия из когнитивных примитивов ===
                TryCognitivePrimitiveDiscovery(civ, avgLogic, scienceCapacity, rng);
            }
        }
        private static void TryQuarantineDiscovery(
    CivilizationSnapshot civ,
    float avgLogic,
    float scienceCapacity,
    Random rng)
        {
            // Проверяем, есть ли статистика по болезням
            var diseaseStats = CognitionSystem.SnapshotStats()
                .Where(kv => kv.Key.StartsWith("disease."))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (diseaseStats.Count < 3) return;

            // Ищем сильные корреляции
            bool hasContactCorrelation = diseaseStats.TryGetValue("disease.contact_with_sick", out var contactStat)
                && contactStat.Count > 10;
            bool hasAvoidanceBehavior = diseaseStats.TryGetValue("disease.avoidance_behavior", out var avoidStat)
                && avoidStat.Count > 5;

            if (!hasContactCorrelation) return;

            string discoveryKey = civ.Id + "|quarantine";
            if (!MathDiscoveries.Add(discoveryKey)) return;

            float chance = Math.Clamp(scienceCapacity / 40f, 0f, 0.5f) * Math.Clamp(avgLogic, 0f, 1f);
            if (rng.NextDouble() > chance)
            {
                MathDiscoveries.Remove(discoveryKey);
                return;
            }

            var scholars = civ.Members
                .OrderByDescending(m => m.Genome.SelfAwareness)
                .Take(3)
                .ToList();

            if (scholars.Count == 0)
            {
                MathDiscoveries.Remove(discoveryKey);
                return;
            }

            var knowledge = new Knowledge
            {
                Kind = "method",
                Branch = "healing",
                Sub = "quarantine",
                DominantAxis = "healing",
                Concept = "disease.isolation",
                Name = "quarantine-method",
                Power = Math.Clamp(contactStat.Count / 50f, 0.1f, 1f),
                Quality = avgLogic + 0.5f,
                CreatedTick = Simulation.Instance.TotalTicks
            };

            foreach (var scholar in scholars)
                knowledge.Knowers.Add(scholar.Id);

            KnowledgeSystem.All.Add(knowledge);

            civ.Discoveries.Add(new Discovery
            {
                Name = knowledge.Name,
                Branch = "healing",
                Capability = "quarantine",
                Quality = knowledge.Quality,
                Tick = Simulation.Instance.TotalTicks,
                AuthorId = scholars[0].Id.ToString()
            });

            FileLogger.Log(
                $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: EMERGENT QUARANTINE DISCOVERY " +
                $"(contact correlations: {contactStat.Count}, avoidance behaviors: {avoidStat.Count})",
                FileLogger.LogLevel.Info);
        }
        private static void TryCognitivePrimitiveDiscovery(
            CivilizationSnapshot civ,
            float avgLogic,
            float scienceCapacity,
            Random rng)
        {
            // Очищаем старые паттерны
            CognitivePrimitives.ClearOldPatterns();

            // 1. Причинные открытия
            var causalPatterns = CognitivePrimitives.GetStrongCausalPatterns(minOccurrences: 10);

            foreach (var pattern in causalPatterns.Take(3))
            {
                string discoveryKey = civ.Id + "|causality|" + pattern.Cause + "|" + pattern.Effect;

                if (!MathDiscoveries.Add(discoveryKey))
                    continue;

                float chance =
                    Math.Clamp(scienceCapacity / 25f, 0f, 0.6f) *
                    Math.Clamp(avgLogic, 0f, 1f) *
                    pattern.Confidence;

                if (rng.NextDouble() > chance)
                {
                    MathDiscoveries.Remove(discoveryKey);
                    continue;
                }

                var scholars = civ.Members
                    .OrderByDescending(m => m.Genome.SelfAwareness)
                    .Take(3)
                    .ToList();

                if (scholars.Count == 0)
                {
                    MathDiscoveries.Remove(discoveryKey);
                    continue;
                }

                var knowledge = new Knowledge
                {
                    Kind = "causality",
                    Branch = "knowledge",
                    Sub = "causality",
                    DominantAxis = "knowledge",
                    Concept = $"causality.{pattern.Cause}.{pattern.Effect}",
                    Name = $"observation-causality.{pattern.Cause}.{pattern.Effect}",
                    Power = Math.Clamp(pattern.Confidence, 0.1f, 1f),
                    Quality = avgLogic + 0.5f,
                    CreatedTick = Simulation.Instance.TotalTicks
                };

                foreach (var scholar in scholars)
                    knowledge.Knowers.Add(scholar.Id);

                KnowledgeSystem.All.Add(knowledge);

                civ.Discoveries.Add(new Discovery
                {
                    Name = knowledge.Name,
                    Branch = "causality",
                    Capability = "knowledge",
                    Quality = knowledge.Quality,
                    Tick = Simulation.Instance.TotalTicks,
                    AuthorId = scholars[0].Id.ToString()
                });

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: CAUSALITY OBSERVATION '{knowledge.Name}' " +
                    $"cause={pattern.Cause} → effect={pattern.Effect} confidence={pattern.Confidence:F2}",
                    FileLogger.LogLevel.Info);

                break;
            }

            // 2. Открытия повторяемости
            var repetitionPatterns = CognitivePrimitives.GetStrongRepetitionPatterns(minRegularity: 0.6f);

            foreach (var pattern in repetitionPatterns.Take(3))
            {
                string discoveryKey = civ.Id + "|repetition|" + pattern.Event;

                if (!MathDiscoveries.Add(discoveryKey))
                    continue;

                float chance =
                    Math.Clamp(scienceCapacity / 30f, 0f, 0.5f) *
                    Math.Clamp(avgLogic, 0f, 1f) *
                    pattern.Regularity;

                if (rng.NextDouble() > chance)
                {
                    MathDiscoveries.Remove(discoveryKey);
                    continue;
                }

                var scholars = civ.Members
                    .OrderByDescending(m => m.Genome.SelfAwareness)
                    .Take(3)
                    .ToList();

                if (scholars.Count == 0)
                {
                    MathDiscoveries.Remove(discoveryKey);
                    continue;
                }

                var knowledge = new Knowledge
                {
                    Kind = "repetition",
                    Branch = "knowledge",
                    Sub = "repetition",
                    DominantAxis = "knowledge",
                    Concept = $"repetition.{pattern.Event}",
                    Name = $"observation-repetition.{pattern.Event}",
                    Power = Math.Clamp(pattern.Regularity, 0.1f, 1f),
                    Quality = avgLogic + 0.5f,
                    CreatedTick = Simulation.Instance.TotalTicks
                };

                foreach (var scholar in scholars)
                    knowledge.Knowers.Add(scholar.Id);

                KnowledgeSystem.All.Add(knowledge);

                civ.Discoveries.Add(new Discovery
                {
                    Name = knowledge.Name,
                    Branch = "repetition",
                    Capability = "knowledge",
                    Quality = knowledge.Quality,
                    Tick = Simulation.Instance.TotalTicks,
                    AuthorId = scholars[0].Id.ToString()
                });

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: REPETITION OBSERVATION '{knowledge.Name}' " +
                    $"event={pattern.Event} interval={pattern.AverageIntervalTicks} regularity={pattern.Regularity:F2}",
                    FileLogger.LogLevel.Info);

                break;
            }
            // 3. ЭКОЛОГИЧЕСКИЕ ОТКРЫТИЯ (Влияние среды на выживание)
            var envCorrelations = CognitivePrimitives.GetStrongEnvCorrelations(minOccurrences: 15);
            foreach (var corr in envCorrelations.Take(3))
            {
                string discoveryKey = civ.Id + "|env|" + corr.Condition + "|" + corr.Effect;
                if (!MathDiscoveries.Add(discoveryKey)) continue;

                float chance = Math.Clamp(scienceCapacity / 30f, 0f, 0.6f) *
                               Math.Clamp(avgLogic, 0f, 1f) * corr.Confidence;

                if (rng.NextDouble() > chance)
                {
                    MathDiscoveries.Remove(discoveryKey);
                    continue;
                }

                var scholars = civ.Members
                    .OrderByDescending(m => m.Genome.SelfAwareness)
                    .Take(3)
                    .ToList();

                if (scholars.Count == 0)
                {
                    MathDiscoveries.Remove(discoveryKey);
                    continue;
                }

                // Эмерджентное определение оси знания на основе эффекта
                string axis = "knowledge";
                if (corr.Effect.Contains("hunger_loss")) axis = "food";       // Поняли, где еда
                else if (corr.Effect.Contains("health_loss")) axis = "healing"; // Поняли, где опасно
                else if (corr.Condition.Contains("cold") || corr.Condition.Contains("hot")) axis = "shelter"; // Поняли, где строить дом

                var knowledge = new Knowledge
                {
                    Kind = "method",
                    Branch = "ecology",
                    Sub = axis,
                    DominantAxis = axis,
                    Concept = $"env.{corr.Condition}.{corr.Effect}",
                    Name = $"observation-env-{corr.Condition}-{corr.Effect}",
                    Power = Math.Clamp(corr.Confidence, 0.1f, 1f),
                    Quality = avgLogic + 0.5f,
                    CreatedTick = Simulation.Instance.TotalTicks
                };

                foreach (var scholar in scholars)
                    knowledge.Knowers.Add(scholar.Id);

                KnowledgeSystem.All.Add(knowledge);

                civ.Discoveries.Add(new Discovery
                {
                    Name = knowledge.Name,
                    Branch = "ecology",
                    Capability = axis,
                    Quality = knowledge.Quality,
                    Tick = Simulation.Instance.TotalTicks,
                    AuthorId = scholars[0].Id.ToString()
                });

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: ECOLOGICAL DISCOVERY '{knowledge.Name}' " +
                    $"(condition={corr.Condition} → effect={corr.Effect}, confidence={corr.Confidence:F2})",
                    FileLogger.LogLevel.Info);

                break; // Одно открытие за раз, чтобы не спамить
            }
        }

        public static void UpdateAgentLogic(Agent a)
        {
            if (a == null) return;

            var sim = Simulation.Instance;
            if (sim == null) return;

            float baseLogic =
                a.Genome.SelfAwareness * 0.45f +
                a.Genome.Openness * 0.25f;

            float education = Math.Min(0.25f, a.Memory.Patterns.Count * 0.008f);

            float materialLogic = 0f;
            foreach (var obj in a.Body.Inventory)
            {
                if (MaterialDB.TryGet(obj.MaterialId, out var spec) && spec.Logic > 0.5f)
                {
                    materialLogic = 0.12f;
                    break;
                }
            }

            var tile = sim.GetTile(a.Position);

            float institution = 0f;
            if (tile != null && tile.InstitutionAxis == "knowledge")
            {
                institution = Math.Min(0.30f, tile.InstitutionLevel * 0.03f);  // было 0.20 / 0.02
            }

            float device = LogicSystem.HasLogicDeviceAt(tile) ? 0.15f : 0f;

            float target = Math.Clamp(
                baseLogic + education + materialLogic + institution + device,
                0f,
                1f);

            a.Logic = a.Logic * 0.95f + target * 0.05f;
        }

        private static void TryMathDiscovery(
            CivilizationSnapshot civ,
            float avgLogic,
            float scienceCapacity,
            Random rng)
        {
            foreach (var kv in Stats)
            {
                var s = kv.Value;
                if (s.Count < 80) continue;

                string discoveryKey = civ.Id + "|" + kv.Key;
                if (!MathDiscoveries.Add(discoveryKey))
                    continue;

                float chance =
                    Math.Clamp(scienceCapacity / 20f, 0f, 0.8f) *
                    Math.Clamp(avgLogic, 0f, 1f);

                if (rng.NextDouble() > chance)
                {
                    MathDiscoveries.Remove(discoveryKey);
                    continue;
                }

                var scholars = civ.Members
                    .OrderByDescending(m => m.Genome.SelfAwareness)
                    .Take(3)
                    .ToList();

                if (scholars.Count == 0)
                {
                    MathDiscoveries.Remove(discoveryKey);
                    continue;
                }

                var knowledge = new Knowledge
                {
                    Kind = "math",
                    Branch = "knowledge",
                    Sub = kv.Key,
                    DominantAxis = "knowledge",
                    Concept = kv.Key,
                    Name = $"observation-{kv.Key}",
                    Power = Math.Clamp(s.Count / 1000f, 0.05f, 1f),
                    Quality = avgLogic + 0.5f,
                    CreatedTick = Simulation.Instance.TotalTicks
                };

                foreach (var scholar in scholars)
                    knowledge.Knowers.Add(scholar.Id);

                KnowledgeSystem.All.Add(knowledge);

                civ.Discoveries.Add(new Discovery
                {
                    Name = knowledge.Name,
                    Branch = "math",
                    Capability = "knowledge",
                    Quality = knowledge.Quality,
                    Tick = Simulation.Instance.TotalTicks,
                    AuthorId = scholars[0].Id.ToString()
                });

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: MATH OBSERVATION '{knowledge.Name}' " +
                    $"samples={s.Count}, avg={s.Sum / Math.Max(1, s.Count):F2}",
                    FileLogger.LogLevel.Info);

                break;
            }
        }

        private static void TryTheoryDiscovery(
            CivilizationSnapshot civ,
            float avgLogic,
            float scienceCapacity,
            int texts,
            Random rng)
        {
            var axisCandidate = civ.Capabilities
                .Where(kv => kv.Value > 0.25f)
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .FirstOrDefault(axis => !TheoryDiscoveries.Contains(civ.Id + "|" + axis));

            if (string.IsNullOrEmpty(axisCandidate))
                return;

            float chance =
                Math.Clamp(scienceCapacity / 30f, 0f, 0.7f) *
                Math.Clamp(avgLogic, 0f, 1f);

            if (rng.NextDouble() > chance)
                return;

            TheoryDiscoveries.Add(civ.Id + "|" + axisCandidate);

            var scholars = civ.Members
                .OrderByDescending(m => m.Genome.SelfAwareness)
                .Take(3)
                .ToList();

            if (scholars.Count == 0)
                return;

            var theory = new Knowledge
            {
                Kind = "theory",
                Branch = "knowledge",
                Sub = axisCandidate,
                DominantAxis = axisCandidate,
                Concept = $"theory-{axisCandidate}",
                Name = $"theory-{axisCandidate}",
                Power = civ.GetCap(axisCandidate) * 0.5f + avgLogic * 0.5f,
                Quality = 1f + texts * 0.05f,
                CreatedTick = Simulation.Instance.TotalTicks
            };

            foreach (var scholar in scholars)
                theory.Knowers.Add(scholar.Id);

            KnowledgeSystem.All.Add(theory);

            civ.InnovationPoints = Math.Min(1000f, civ.InnovationPoints + 25f);

            civ.Discoveries.Add(new Discovery
            {
                Name = theory.Name,
                Branch = "theory",
                Capability = axisCandidate,
                Quality = theory.Quality,
                Tick = Simulation.Instance.TotalTicks,
                AuthorId = scholars[0].Id.ToString()
            });

            FileLogger.Log(
                $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: THEORY '{theory.Name}' " +
                $"power={theory.Power:F2}, quality={theory.Quality:F2}",
                FileLogger.LogLevel.Info);
        }

        private static int CountTexts(CivilizationSnapshot civ)
        {
            int count = 0;
            var world = Simulation.Instance.World;

            foreach (var m in civ.Members)
            {
                var tile = world[m.Position.X, m.Position.Y];
                count += tile.Texts.Count;
            }

            return count;
        }

        private static int CountKnowledgePlaces(CivilizationSnapshot civ)
        {
            int count = 0;
            var world = Simulation.Instance.World;
            foreach (var m in civ.Members)
            {
                var tile = world[m.Position.X, m.Position.Y];
                if (tile.IsLibrary || tile.IsTemple || tile.DominantAxis == "knowledge" || tile.SanctityLevel > 20f) count++;
            }
            return count;
        }
    }
}
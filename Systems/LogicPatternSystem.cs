using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public static class LogicPatternSystem
    {
        public class LogicPattern
        {
            public string Id;
            public float[] InputMask;
            public float[] OutputMask;
            public int ObservationCount;
            public float Confidence;
            public string Name;
            public HashSet<Guid> Discoverers = new();
        }

        private static readonly SortedDictionary<string, LogicPattern> Patterns = new();

        public static void TryExtractPatterns(Agent agent, CivilizationSnapshot civ, Random rng)
        {
            if (agent == null || civ == null || rng == null)
                return;

            var experiments = LogicExperimentSystem.GetExperiments(agent.Id);

            if (experiments.Count < 20)
                return;

            // Группируем эксперименты по устройству
            var grouped = experiments.GroupBy(e => e.DeviceId);

            foreach (var group in grouped)
            {
                var deviceExperiments = group.ToList();

                if (deviceExperiments.Count < 15)
                    continue;

                // Ищем устойчивые паттерны
                var patterns = FindPatterns(deviceExperiments);

                foreach (var pattern in patterns)
                {
                    if (pattern.Confidence < 0.85f)
                        continue;

                    // Генерируем имя паттерна из хеша
                    if (string.IsNullOrEmpty(pattern.Name))
                    {
                        pattern.Name = "pattern-" + HashPattern(pattern);
                    }

                    // Проверяем, известен ли уже этот паттерн
                    if (!Patterns.ContainsKey(pattern.Id))
                    {
                        Patterns[pattern.Id] = pattern;

                        // Создаём знание
                        var knowledge = new Knowledge
                        {
                            Kind = "logic_pattern",
                            Branch = "knowledge",
                            Sub = "logic",
                            DominantAxis = "knowledge",
                            Concept = pattern.Name,
                            Name = pattern.Name,
                            Power = pattern.Confidence,
                            Quality = 1f + civ.GetCap("knowledge"),
                            CreatedTick = Simulation.Instance.TotalTicks
                        };

                        foreach (var discoverer in pattern.Discoverers)
                            knowledge.Knowers.Add(discoverer);

                        KnowledgeSystem.All.Add(knowledge);

                        civ.InnovationPoints = Math.Min(1000f, civ.InnovationPoints + 25f);

                        civ.Discoveries.Add(new Discovery
                        {
                            Name = pattern.Name,
                            Branch = "logic_pattern",
                            Capability = "knowledge",
                            Quality = knowledge.Quality,
                            Tick = Simulation.Instance.TotalTicks,
                            AuthorId = agent.Id.ToString()
                        });

                        FileLogger.Log(
                            $"[TICK {Simulation.Instance.TotalTicks}] PATTERN: {civ.Name} stabilized logic pattern " +
                            $"'{pattern.Name}' inputs=[{string.Join(",", pattern.InputMask)}] " +
                            $"→ outputs=[{string.Join(",", pattern.OutputMask)}] " +
                            $"confidence={pattern.Confidence:F2}, observations={pattern.ObservationCount}",
                            FileLogger.LogLevel.Info);
                    }
                    else
                    {
                        // Обновляем существующий паттерн
                        var existing = Patterns[pattern.Id];
                        existing.ObservationCount += pattern.ObservationCount;
                        existing.Confidence = Math.Min(1f,
                            (existing.Confidence + pattern.Confidence) / 2f);

                        foreach (var discoverer in pattern.Discoverers)
                            existing.Discoverers.Add(discoverer);
                    }
                }
            }
        }

        private static List<LogicPattern> FindPatterns(List<LogicExperimentSystem.ExperimentRecord> experiments)
        {
            var patterns = new List<LogicPattern>();

            // Группируем по входам
            var inputGroups = experiments.GroupBy(e =>
                $"{e.Inputs[0]}:{e.Inputs[1]}");

            foreach (var group in inputGroups)
            {
                var inputs = group.First().Inputs;
                var outputs = group.Select(e => e.Outputs[0]).ToList();

                if (outputs.Count < 5)
                    continue;

                // Проверяем, стабилен ли выход
                float avgOutput = outputs.Average();
                float variance = outputs.Select(o => (o - avgOutput) * (o - avgOutput)).Average();

                // Если дисперсия низкая — паттерн устойчив
                if (variance < 0.1f)
                {
                    float confidence = 1f - variance;

                    var pattern = new LogicPattern
                    {
                        Id = $"{inputs[0]}:{inputs[1]}→{avgOutput:F2}",
                        InputMask = inputs,
                        OutputMask = new float[] { avgOutput > 0.5f ? 1f : 0f },
                        ObservationCount = outputs.Count,
                        Confidence = confidence,
                        Discoverers = new HashSet<Guid>(experiments.Select(e => e.AgentId))
                    };

                    patterns.Add(pattern);
                }
            }

            return patterns;
        }

        private static string HashPattern(LogicPattern pattern)
        {
            int hash = 17;

            foreach (float v in pattern.InputMask)
                hash = hash * 31 + (int)(v * 100);

            foreach (float v in pattern.OutputMask)
                hash = hash * 31 + (int)(v * 100);

            return Math.Abs(hash).ToString("X4");
        }

        public static int PatternCount()
        {
            return Patterns.Count;
        }

        public static int PatternCount(string civId)
        {
            return Patterns.Count(kv =>
                kv.Value.Discoverers.Any(id =>
                    Simulation.activeCivs
                        .FirstOrDefault(c => c.Id == civId)?
                        .Members.Any(m => m.Id == id) ?? false));
        }
    }
}
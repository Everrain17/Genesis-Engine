using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public static class SymbolicManipulationSystem
    {
        public class SymbolicCombination
        {
            public string CivId;
            public List<string> InputGraphemeIds;
            public string OutputGraphemeId;
            public string Context;
            public int Occurrences;
            public int LastTick;
        }

        public class SymbolicInvariant
        {
            public string Id;
            public string CivId;
            public List<string> InputGraphemeIds;
            public string OutputGraphemeId;
            public float Confidence;
            public int Observations;
            public bool IsAbstract;
        }

        private static readonly SortedDictionary<string, List<SymbolicCombination>> CivCombinations = new();
        private static readonly SortedDictionary<string, List<SymbolicInvariant>> CivInvariants = new();

        // ИСПРАВЛЕНО: теперь возвращает bool
        public static bool TryManipulateSymbols(Agent agent, Tile tile, Random rng)
        {
            if (agent == null || tile == null || rng == null) return false;

            if (tile.InstitutionAxis != "knowledge" || tile.InstitutionLevel < 2f) return false;
            if (agent.Logic < 0.55f || agent.Genome.SelfAwareness < 0.5f) return false;

            string civId = agent.CivilizationId ?? "wild";
            var graphemes = GraphemeSystem.GetGraphemes(civId);
            if (graphemes.Count < 3) return false;

            float chance = 0.01f + agent.Logic * 0.02f + tile.InstitutionLevel * 0.005f;
            if (rng.NextDouble() > chance) return false;

            string[] contexts = { "quantity.food", "quantity.stone", "quantity.agents" };
            string context = contexts[rng.Next(contexts.Length)];

            int val1 = rng.Next(1, 4);
            int val2 = rng.Next(1, 3);
            int result = val1 + val2;

            string sym1 = GetOrCreateConceptSymbol(civId, $"concept_{context}_{val1}", graphemes, rng);
            string sym2 = GetOrCreateConceptSymbol(civId, $"concept_{context}_{val2}", graphemes, rng);
            string symRes = GetOrCreateConceptSymbol(civId, $"concept_{context}_{result}", graphemes, rng);

            if (string.IsNullOrEmpty(sym1) || string.IsNullOrEmpty(sym2) || string.IsNullOrEmpty(symRes))
                return false;

            if (!CivCombinations.TryGetValue(civId, out var combinations))
            {
                combinations = new List<SymbolicCombination>();
                CivCombinations[civId] = combinations;
            }

            var inputs = new List<string> { sym1, sym2 }.OrderBy(x => x).ToList();
            var existing = combinations.FirstOrDefault(c =>
                c.InputGraphemeIds.SequenceEqual(inputs) &&
                c.OutputGraphemeId == symRes &&
                c.Context == context);

            if (existing != null)
            {
                existing.Occurrences++;
                existing.LastTick = Simulation.Instance.TotalTicks;
            }
            else
            {
                combinations.Add(new SymbolicCombination
                {
                    CivId = civId,
                    InputGraphemeIds = inputs,
                    OutputGraphemeId = symRes,
                    Context = context,
                    Occurrences = 1,
                    LastTick = Simulation.Instance.TotalTicks
                });
            }

            return true; // ИСПРАВЛЕНО: успешное выполнение
        }

        private static string GetOrCreateConceptSymbol(string civId, string concept, List<GraphemeSystem.Grapheme> graphemes, Random rng)
        {
            return $"SYM_{concept.GetHashCode():X4}";
        }

        public static void DetectInvariants(List<CivilizationSnapshot> civs)
        {
            if (civs == null) return;

            foreach (var civ in civs)
            {
                if (!CivCombinations.TryGetValue(civ.Id, out var combinations)) continue;

                if (!CivInvariants.TryGetValue(civ.Id, out var invariants))
                {
                    invariants = new List<SymbolicInvariant>();
                    CivInvariants[civ.Id] = invariants;
                }

                var grouped = combinations.GroupBy(c => new
                {
                    Inputs = string.Join(",", c.InputGraphemeIds),
                    Output = c.OutputGraphemeId
                });

                foreach (var group in grouped)
                {
                    int totalOccurrences = group.Sum(c => c.Occurrences);
                    int distinctContexts = group.Select(c => c.Context).Distinct().Count();

                    bool isAbstract = distinctContexts >= 2 && totalOccurrences >= 10;
                    float confidence = Math.Min(1f, totalOccurrences / 20f);

                    if (confidence < 0.7f) continue;

                    string invId = $"INV_{group.Key.Inputs}_TO_{group.Key.Output}";
                    var existing = invariants.FirstOrDefault(i => i.Id == invId);

                    if (existing == null)
                    {
                        var newInv = new SymbolicInvariant
                        {
                            Id = invId,
                            CivId = civ.Id,
                            InputGraphemeIds = group.Key.Inputs.Split(',').ToList(),
                            OutputGraphemeId = group.Key.Output,
                            Confidence = confidence,
                            Observations = totalOccurrences,
                            IsAbstract = isAbstract
                        };
                        invariants.Add(newInv);

                        if (isAbstract)
                        {
                            FileLogger.Log(
                                $"[TICK {Simulation.Instance.TotalTicks}] SYMBOLIC BREAKTHROUGH: civ {civ.Id} discovered abstract rule " +
                                $"'{string.Join("+", newInv.InputGraphemeIds)} -> {newInv.OutputGraphemeId}' " +
                                $"(observed in {distinctContexts} contexts, confidence={confidence:F2})",
                                FileLogger.LogLevel.Info);

                            var knowledge = new Knowledge
                            {
                                Kind = "symbolic_rule",
                                Branch = "knowledge",
                                Sub = "mathematics",
                                DominantAxis = "knowledge",
                                Concept = $"rule_{group.Key.Inputs}_to_{group.Key.Output}",
                                Name = $"rule_{group.Key.Inputs}_to_{group.Key.Output}",
                                Power = confidence,
                                Quality = 1f + civ.GetCap("knowledge"),
                                CreatedTick = Simulation.Instance.TotalTicks
                            };

                            var scholars = civ.Members.OrderByDescending(m => m.Logic).Take(3).ToList();
                            foreach (var s in scholars) knowledge.Knowers.Add(s.Id);

                            KnowledgeSystem.All.Add(knowledge);
                            civ.Discoveries.Add(new Discovery
                            {
                                Name = knowledge.Name,
                                Branch = "symbolic_rule",
                                Capability = "knowledge",
                                Quality = knowledge.Quality,
                                Tick = Simulation.Instance.TotalTicks,
                                AuthorId = scholars.FirstOrDefault()?.Id.ToString() ?? "unknown"
                            });
                        }
                    }
                    else
                    {
                        existing.Confidence = Math.Min(1f, (existing.Confidence + confidence) / 2f);
                        existing.Observations = totalOccurrences;
                        if (distinctContexts >= 2 && !existing.IsAbstract)
                        {
                            existing.IsAbstract = true;
                            FileLogger.Log(
                                $"[TICK {Simulation.Instance.TotalTicks}] SYMBOLIC ABSTRACTION: rule '{existing.Id}' is now context-independent!",
                                FileLogger.LogLevel.Info);
                        }
                    }
                }
            }
        }

        public static int AbstractInvariantCount(string civId)
        {
            if (string.IsNullOrEmpty(civId) || !CivInvariants.TryGetValue(civId, out var invs)) return 0;
            return invs.Count(i => i.IsAbstract && i.Confidence > 0.8f);
        }

        public static void Cleanup(int maxAge = 10000)
        {
            int currentTick = Simulation.Instance.TotalTicks;
            foreach (var kvp in CivCombinations)
            {
                kvp.Value.RemoveAll(c => currentTick - c.LastTick > maxAge && c.Occurrences < 5);
            }
        }
    }
}
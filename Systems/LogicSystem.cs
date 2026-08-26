using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.Systems.Physics;
using GenesisEngine.UI;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class LogicSystem
    {
        private static readonly HashSet<string> AlgorithmDiscoveries = new();

        public static float GlobalComputationCapacity()
        {
            float total = 0f;

            if (Simulation.activeCivs != null)
            {
                foreach (var civ in Simulation.activeCivs)
                    total += CivilizationComputationCapacity(civ);
            }

            total += CultureSystem.AllArtifacts
                .Count(a => a.Name != null && a.Name.StartsWith("logic-node")) * 0.25f;

            return total;
        }

        public static float CivilizationComputationCapacity(CivilizationSnapshot civ)
        {
            if (civ == null || civ.Members.Count == 0)
                return 0f;

            float capacity = 0f;

            foreach (var member in civ.Members)
            {
                foreach (var obj in member.Body.Inventory)
                {
                    if (!MaterialDB.TryGet(obj.MaterialId, out var spec))
                        continue;

                    if (spec.Logic > 0.25f)
                        capacity += spec.Logic * Math.Min(5f, obj.Quantity) * 0.05f;
                }

                var tile = Simulation.Instance.World[member.Position.X, member.Position.Y];

                capacity += tile.Artifacts
                    .Count(a => a.Name != null && a.Name.StartsWith("logic-node")) * 0.2f;
            }

            foreach (var kv in civ.MatStock)
            {
                if (!MaterialDB.TryGet(kv.Key, out var spec))
                    continue;

                if (spec.Logic > 0.25f)
                    capacity += spec.Logic * Math.Min(10f, kv.Value) * 0.02f;
            }

            capacity += civ.GetCap("knowledge") * 2f;
            capacity += InstitutionSystem.CountAxis(civ, "knowledge") * 0.6f;

            return capacity;
        }

        public static bool TryAssembleLogicDevice(Agent a, Tile tile, Random rng)
        {
            if (a == null || tile == null)
                return false;

            if (tile.InstitutionAxis != "knowledge" || tile.InstitutionLevel < 2f)
                return false;

            var candidates = new List<WorldObject>();

            foreach (var obj in a.Body.Inventory)
            {
                if (obj.Quantity < 1f)
                    continue;

                if (!MaterialDB.TryGet(obj.MaterialId, out var spec))
                    continue;

                if (spec.Logic > 0.4f && spec.Conductivity > 0.3f)
                    candidates.Add(obj);
            }

            if (candidates.Count < 2)
                return false;

            float chance =
                0.02f +
                a.Genome.SelfAwareness * 0.03f +
                tile.InstitutionLevel * 0.005f;

            if (rng.NextDouble() > chance)
                return false;

            var first = candidates[0];
            var second = candidates[1];

            first.Quantity -= 1f;
            if (first.Quantity <= 0f)
                a.Body.Inventory.Remove(first);

            second.Quantity -= 1f;
            if (second.Quantity <= 0f)
                a.Body.Inventory.Remove(second);

            var artifact = CultureSystem.CreateArtifact(a, tile, first.MaterialId);
            if (artifact == null)
                return false;

            artifact.Name = "logic-node-" + artifact.Id.ToString()[..4];
            artifact.CulturalValue += 25f;

            EventBus.Publish(new SimEvent
            {
                Type = SimEventType.Discovery,
                Tick = Simulation.Instance.TotalTicks,
                Actor = a,
                Position = a.Position,
                Data = artifact.Name,
                Value = tile.InstitutionLevel
            });

            FileLogger.Log(
                $"[TICK {Simulation.Instance.TotalTicks}] LOGIC DEVICE assembled by {a.Id} at ({tile.X},{tile.Y})",
                FileLogger.LogLevel.Info);

            return true;
        }

        public static void Run(List<CivilizationSnapshot> civs, Random rng)
        {
            if (civs == null || rng == null)
                return;

            foreach (var civ in civs)
            {
                float capacity = CivilizationComputationCapacity(civ);

                if (capacity < 8f)
                    continue;

                string key = civ.Id + "|algorithm";
                if (AlgorithmDiscoveries.Contains(key))
                    continue;

                float chance = Math.Clamp(capacity / 50f, 0f, 0.35f);

                if (rng.NextDouble() > chance)
                    continue;

                AlgorithmDiscoveries.Add(key);

                var scholars = civ.Members
                    .OrderByDescending(m => m.Genome.SelfAwareness)
                    .Take(3)
                    .ToList();

                if (scholars.Count == 0)
                    continue;

                var algorithm = new Knowledge
                {
                    Kind = "algorithm",
                    Branch = "knowledge",
                    Sub = "knowledge",
                    DominantAxis = "knowledge",
                    Concept = "algorithm",
                    Name = "algorithm-" + civ.Id[..4],
                    Power = Math.Min(1f, capacity / 25f),
                    Quality = 1f,
                    CreatedTick = Simulation.Instance.TotalTicks
                };

                foreach (var scholar in scholars)
                    algorithm.Knowers.Add(scholar.Id);

                KnowledgeSystem.All.Add(algorithm);

                civ.InnovationPoints = Math.Min(1000f, civ.InnovationPoints + 15f);

                civ.Discoveries.Add(new Discovery
                {
                    Name = algorithm.Name,
                    Branch = "algorithm",
                    Capability = "knowledge",
                    Quality = algorithm.Quality,
                    Tick = Simulation.Instance.TotalTicks,
                    AuthorId = scholars[0].Id.ToString()
                });

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: ALGORITHM '{algorithm.Name}' " +
                    $"power={algorithm.Power:F2}, capacity={capacity:F2}",
                    FileLogger.LogLevel.Info);
            }
        }
        public static bool HasLogicDeviceAt(Tile tile)
        {
            if (tile == null) return false;

            return tile.Artifacts.Any(a =>
                a.Name != null &&
                a.Name.StartsWith("logic-node"));
        }
    }
}
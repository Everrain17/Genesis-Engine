using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.Systems.Physics;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class ConstructionSystem
    {
        public static bool TryBuild(Agent agent, Tile tile, Random rng)
        {
            if (agent == null || tile == null) return false;
            if (tile.Building != BuildingType.None) return false;
            if (!tile.IsPassable) return false;
            if (agent.Body.Inventory.Count < 2) return false;

            string axis = ChooseAxis(agent, rng);
            string buildingConcept = EffectTables.AxisToBuilding(axis).ToString();

            var civ = Simulation.activeCivs?.FirstOrDefault(c => c.Id == agent.CivilizationId);

            bool knowsRecipe = civ != null && KnowledgeSystem.CivKnowsRecipe(civ, buildingConcept);

            // === НОВОЕ: границы территорий ===
            bool foreign = !string.IsNullOrEmpty(tile.OwnerCivId) && tile.OwnerCivId != agent.CivilizationId;
            if (foreign && !string.IsNullOrEmpty(agent.CivilizationId) && !DiplomacySystem.IsAtWar(agent.CivilizationId) && rng.NextDouble() < 0.6f)
                return false;

            if (!knowsRecipe)
            {
                float experimentChance =
                    agent.Genome.Openness *
                    agent.Genome.SelfAwareness *
                    0.20f;

                if (rng.NextDouble() > experimentChance)
                    return false;
            }

            var candidates = new List<(WorldObject obj, ResourceSpec spec, float score)>();

            foreach (var obj in agent.Body.Inventory)
            {
                if (obj.Quantity < 1f) continue;
                if (!MaterialDB.TryGet(obj.MaterialId, out var spec)) continue;

                float score = EffectTables.Compute(axis, spec) / 100f;
                candidates.Add((obj, spec, score));
            }

            if (candidates.Count < 2) return false;

            var best = candidates
                .OrderByDescending(c => c.score)
                .Take(2)
                .ToList();

            if (best[0].score + best[1].score <= 0.05f)
                return false;

            var mix = MaterialDB.Mix(best[0].spec, best[1].spec);
            float power = EffectTables.Compute(axis, mix) / 100f;

            if (power <= 0.05f) return false;

            best[0].obj.Quantity -= 1f;
            if (best[0].obj.Quantity <= 0f)
                agent.Body.Inventory.Remove(best[0].obj);

            best[1].obj.Quantity -= 1f;
            if (best[1].obj.Quantity <= 0f)
                agent.Body.Inventory.Remove(best[1].obj);

            var profile = new Dictionary<string, float>();
            string dominant = axis;
            float dominantValue = 0f;

            foreach (var ax in EffectTables.Axis.Keys)
            {
                float v = EffectTables.Compute(ax, mix) / 100f;
                profile[ax] = v;

                if (v > dominantValue)
                {
                    dominantValue = v;
                    dominant = ax;
                }
            }

            tile.Building = EffectTables.AxisToBuilding(axis);
            tile.OwnerCivId = agent.CivilizationId;
            tile.DominantAxis = axis;
            tile.BuildingProfile = profile;
            tile.BuildingComposition = new List<string>
            {
                best[0].spec.Id,
                best[1].spec.Id
            };

            tile.BuildingQuality =
                1f +
                agent.Genome.Conscientiousness * 0.30f +
                KnowledgeSystem.BuildingPower(civ, axis) * 0.20f;

            tile.Durability = 100f;
            tile.DevelopmentLevel += 5f;

            tile.BuildingName =
                $"{Root(best[0].spec)}{Root(best[1].spec)}-{EffectTables.AxisBuildingWord(axis)}";

            EventBus.Publish(new SimEvent
            {
                Type = SimEventType.BuildingCreated,
                Tick = Simulation.Instance.TotalTicks,
                Actor = agent,
                Position = new Vector2(tile.X, tile.Y),
                Data = tile.BuildingName,
                Value = power
            });

            agent.LastAction = "Build";
            return true;
        }

        private static string ChooseAxis(Agent agent, Random rng)
        {
            float hunger = agent.Body.Hunger / 100f;
            float loneliness = agent.Loneliness / 100f;
            float fear = agent.Fear / 100f;
            float full = agent.Body.CurrentCarryWeight / Math.Max(1f, agent.Body.MaxCarryWeight);

            Tile tile = Simulation.Instance?.GetTile(agent.Position);

            float institution = tile?.InstitutionLevel ?? 0f;
            float sanctity = tile?.SanctityLevel ?? 0f;

            bool atWar =
                !string.IsNullOrEmpty(agent.CivilizationId) &&
                DiplomacySystem.IsAtWar(agent.CivilizationId);

            float peace =
                string.IsNullOrEmpty(agent.CivilizationId)
                    ? 0f
                    : DiplomacySystem.PeaceStability(agent.CivilizationId);

            float warPressure =
                string.IsNullOrEmpty(agent.CivilizationId)
                    ? 0f
                    : DiplomacySystem.WarPressure(agent.CivilizationId);

            float knowledgeDemand =
                agent.Genome.SelfAwareness * 0.40f +
                agent.Genome.Openness * 0.25f +
                agent.Logic * 0.35f +
                institution * 0.03f +
                peace * 0.35f -
                warPressure * 0.25f;

            float tradeDemand =
                agent.Genome.Extraversion * 0.30f +
                agent.Genome.Openness * 0.20f +
                peace * 0.30f -
                warPressure * 0.15f;

            float cultureDemand =
                agent.Genome.Openness * 0.25f +
                peace * 0.25f -
                warPressure * 0.10f;

            float faithDemand =
                agent.Genome.Spirituality * 0.30f +
                (sanctity > 20f ? 0.15f : 0f);

            float healingDemand =
                Math.Max(0f, 1f - agent.Body.Health / 100f) +
                warPressure * 0.15f;

            var weights = new Dictionary<string, float>
            {
                ["food"] = hunger,
                ["growth"] = hunger * 0.40f,
                ["shelter"] = loneliness,
                ["comfort"] = loneliness * 0.40f,
                ["storage"] = full,

                ["defense"] =
                    fear *
                    (atWar ? 0.80f : 0.25f) *
                    (1f - peace * 0.40f),

                ["faith"] = faithDemand,
                ["knowledge"] = knowledgeDemand,
                ["trade"] = tradeDemand,
                ["culture"] = cultureDemand,
                ["healing"] = healingDemand,
                ["mobility"] = 0.05f
            };

            float total = 0f;

            foreach (var kv in weights)
                total += Math.Max(0f, kv.Value) + 0.02f;

            float roll = (float)rng.NextDouble() * total;

            foreach (var kv in weights)
            {
                roll -= Math.Max(0f, kv.Value) + 0.02f;

                if (roll <= 0f)
                    return kv.Key;
            }

            return "shelter";
        }

        private static string Root(ResourceSpec spec)
        {
            if (string.IsNullOrEmpty(spec.Id)) return "mat";
            return spec.Id.Length > 4 ? spec.Id[..4] : spec.Id;
        }
    }
}
using System;
using System.Collections.Generic;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Physics;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class HigherCognitivePrimitives
    {
        private static readonly SortedDictionary<Guid, List<string>> ActionHistory = new();

        public static void Update(Agent agent, Tile tile, Random rng)
        {
            if (agent == null || tile == null)
                return;

            var sim = Simulation.Instance;

            if (sim == null)
                return;

            int tick = sim.TotalTicks;

            int phase = agent.Id.GetHashCode() & 0x7fffffff;

            if ((tick + phase) % 15 == 0)
                UpdateAgency(agent, tile);

            if ((tick + phase) % 40 == 0)
                UpdateTheoryOfMind(agent);

            if ((tick + phase) % 35 == 0)
                UpdateHierarchy(agent, tile);

            if ((tick + phase) % 25 == 0)
                UpdateModality(agent, tile);

            if ((tick + phase) % 10 == 0)
                UpdateTemporal(agent);
        }

        // ============================================================
        // 1. АГЕНТНОСТЬ
        // Агент замечает, что другие агенты совершают действия
        // ============================================================
        private static void UpdateAgency(Agent agent, Tile tile)
        {
            var nearby = SpatialGrid.GetNearby(agent.Position, 2);

            bool sawAction = false;

            foreach (var other in nearby)
            {
                if (other.Id == agent.Id)
                    continue;

                if (!string.IsNullOrEmpty(other.LastAction) &&
                    other.LastAction != "Move")
                {
                    sawAction = true;
                    break;
                }
            }

            if (sawAction)
                CognitionSystem.Record("agency.observed_action", 1f);

            if (tile.Building != BuildingType.None)
                CognitionSystem.Record("agency.building_exists", 1f);

            if (tile.Artifacts.Count > 0)
                CognitionSystem.Record("agency.artifact_exists", 1f);
        }

        // ============================================================
        // 2. ТЕОРИЯ РАЗУМА
        // Агент замечает разницу знаний между собой и другим
        // ============================================================
        private static void UpdateTheoryOfMind(Agent agent)
        {
            var nearby = SpatialGrid.GetNearby(agent.Position, 1);

            Agent other = null;

            foreach (var n in nearby)
            {
                if (n.Id != agent.Id)
                {
                    other = n;
                    break;
                }
            }

            if (other == null)
                return;

            bool iKnow = KnowledgeSystem.AgentKnowsAnything(agent);
            bool otherKnows = KnowledgeSystem.AgentKnowsAnything(other);

            if (iKnow && !otherKnows)
                CognitionSystem.Record("tom.i_know_other_doesnt", 1f);
            else if (!iKnow && otherKnows)
                CognitionSystem.Record("tom.other_knows_i_dont", 1f);
            else if (iKnow && otherKnows)
                CognitionSystem.Record("tom.shared_knowledge", 1f);
        }

        // ============================================================
        // 3. ИЕРАРХИЧНОСТЬ
        // Агент замечает, что сложные вещи состоят из частей
        // ============================================================
        private static void UpdateHierarchy(Agent agent, Tile tile)
        {
            int scanned = 0;

            foreach (var obj in agent.Body.Inventory)
            {
                if (scanned++ >= 5)
                    break;

                if (string.IsNullOrEmpty(obj.MaterialId))
                    continue;

                if (!obj.MaterialId.Contains("+"))
                    continue;

                CognitionSystem.Record("hierarchy.composite", 1f);

                if (MaterialDB.TryGet(obj.MaterialId, out var spec) &&
                    spec.Depth > 2)
                {
                    CognitionSystem.Record("hierarchy.deep_composite", 1f);
                }

                break;
            }

            if (tile.Building != BuildingType.None &&
                tile.BuildingComposition.Count > 0)
            {
                CognitionSystem.Record("hierarchy.structure", 1f);
            }
        }

        // ============================================================
        // 4. МОДАЛЬНОСТЬ
        // Агент замечает возможности: "можно сделать", "возможно"
        // ============================================================
        private static void UpdateModality(Agent agent, Tile tile)
        {
            if (agent.Body.Inventory.Count >= 2)
                CognitionSystem.Record("modality.possible_combine", 1f);

            if (tile.InstitutionLevel > 1f &&
                !KnowledgeSystem.AgentKnowsAnything(agent))
            {
                CognitionSystem.Record("modality.possible_learn", 1f);
            }

            if (agent.Body.Hunger > 60f &&
                tile.GroundObjects.Count == 0)
            {
                CognitionSystem.Record("modality.possible_food", 1f);
            }
        }

        // ============================================================
        // 5. МЕНТАЛЬНАЯ ВРЕМЕННАЯ ЛИНИЯ
        // Агент замечает последовательности своих действий
        // ============================================================
        private static void UpdateTemporal(Agent agent)
        {
            if (string.IsNullOrEmpty(agent.LastAction) ||
                agent.LastAction == "Move")
            {
                return;
            }

            if (!ActionHistory.TryGetValue(agent.Id, out var history))
            {
                history = new List<string>();
                ActionHistory[agent.Id] = history;
            }

            history.Add(agent.LastAction);

            if (history.Count > 12)
                history.RemoveAt(0);

            if (history.Count >= 2)
                CognitionSystem.Record("temporal.sequence", 1f);

            if (history.Count >= 4)
            {
                string a1 = history[history.Count - 1];
                string b1 = history[history.Count - 2];
                string a2 = history[history.Count - 3];
                string b2 = history[history.Count - 4];

                if (a1 == a2 && b1 == b2)
                    CognitionSystem.Record("temporal.repetition", 1f);
            }
        }

        public static void OnAgentDeath(Guid agentId)
        {
            ActionHistory.Remove(agentId);
        }
    }
}
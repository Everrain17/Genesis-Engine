using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public static class LogicAutomataSystem
    {
        private static readonly Dictionary<string, float> Computation = new();

        public static float GetCivComputation(string civId)
        {
            if (string.IsNullOrEmpty(civId)) return 0f;
            return Computation.GetValueOrDefault(civId, 0f);
        }

        public static float GetTotalComputation()
        {
            return Computation.Values.Sum();
        }


        public static void Run(List<CivilizationSnapshot> civs, Random rng)
        {
            Computation.Clear();

            if (civs == null || rng == null)
                return;

            foreach (var civ in civs)
            {
                if (civ.Members.Count == 0)
                    continue;

                float devices = CountDevices(civ);
                float logicMaterials = CountLogicMaterials(civ);
                float institutions = InstitutionSystem.CountAxis(civ, "knowledge");

                float computation =
                    devices * 0.60f +
                    logicMaterials * 0.05f +
                    institutions * 0.40f +
                    civ.GetCap("knowledge") * 1.50f;

                computation = Math.Clamp(computation, 0f, 20f);

                Computation[civ.Id] = computation;

                if (devices > 0f)
                {
                    civ.InnovationPoints = Math.Min(
                        1000f,
                        civ.InnovationPoints + computation * 0.20f);
                }

                // Хардкод вентилей УДАЛЁН
                // Теперь вентили возникают эмерджентно через эксперименты
            }
        }

        private static int CountDevices(CivilizationSnapshot civ)
        {
            var seenTiles = new HashSet<(int x, int y)>();
            int count = 0;

            var world = Simulation.Instance.World;

            foreach (var member in civ.Members)
            {
                var tile = world[member.Position.X, member.Position.Y];

                if (tile == null)
                    continue;

                var key = (tile.X, tile.Y);

                if (!seenTiles.Add(key))
                    continue;

                count += tile.Artifacts.Count(a =>
                    a.Name != null &&
                    a.Name.StartsWith("logic-node"));
            }

            return count;
        }

        private static float CountLogicMaterials(CivilizationSnapshot civ)
        {
            float sum = 0f;

            foreach (var member in civ.Members)
            {
                foreach (var obj in member.Body.Inventory)
                {
                    if (!MaterialDB.TryGet(obj.MaterialId, out var spec))
                        continue;

                    if (spec.Logic > 0.40f)
                        sum += spec.Logic * Math.Min(5f, obj.Quantity);
                }
            }

            return sum;
        }
    }
}
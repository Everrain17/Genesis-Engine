using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public class CivilizationSnapshot
    {
        public string Id;
        public string Name;
        public List<Agent> Members = new();
        public List<Discovery> Discoveries = new();
        public Dictionary<AgentRole, int> RoleCounts = new();
        public float EducationLevel;
        public float AvgToolHardness;
        public int EmergentStructuresCount;
        public Dictionary<string, float> Capabilities = new();
        public Dictionary<string, float> MatStock = new();
        public HashSet<string> TriedPairs = new();
        public float InnovationPoints;
        public int HealingBuildingsCount = 0; // Новое поле для подсчета зданий медицины
        public int Population => Members.Count;

        public int ControlledTiles => Members
            .Select(a => (a.Position.X, a.Position.Y))
            .Distinct()
            .Count();

        public float TotalDevelopment => Members.Any()
            ? Members.Average(a => Simulation.Instance.World[a.Position.X, a.Position.Y].DevelopmentLevel)
            : 0;

        public float CultureScore => Discoveries.Count(d =>
            d.Branch == "item" &&
            (d.Capability == "culture" || d.Capability == "faith")) * 100;

        public float EconomyScore => Members.Sum(a =>
            a.Memory.AgentMemories.Count(m => m.LastAction == "Trade")) * 50;

        public float MilitaryPower => Members.Sum(a =>
            a.Body.Inventory.Count > 0
                ? a.Body.Inventory.Max(o => o.GetProperties().GetValueOrDefault("Hardness", 0.1f))
                : 0.1f);

        public float TotalScore => CultureScore + EconomyScore + MilitaryPower + ControlledTiles * 10;

        public float GetCap(string key) => Capabilities.GetValueOrDefault(key, 0f);

        public void CalculateStats(Tile[,] world)
        {
            RoleCounts.Clear();

            int scholars = 0;
            float totalHardness = 0;
            int itemCount = 0;
            int structures = 0;

            foreach (var a in Members)
            {
                if (a.Role != AgentRole.None)
                    RoleCounts[a.Role] = RoleCounts.GetValueOrDefault(a.Role, 0) + 1;

                if (a.Memory.Patterns.Count > 5)
                    scholars++;

                foreach (var item in a.Body.Inventory)
                {
                    if (item.GetProperties().TryGetValue("Hardness", out float h))
                    {
                        totalHardness += h;
                        itemCount++;
                    }
                }

                var t = world[a.Position.X, a.Position.Y];

                if (t.Building != BuildingType.None && t.OwnerCivId == Id)
                    structures++;
            }

            EducationLevel = Members.Count > 0 ? (float)scholars / Members.Count : 0;
            AvgToolHardness = itemCount > 0 ? totalHardness / itemCount : 0;
            EmergentStructuresCount = structures;
            HealingBuildingsCount = 0;
            var countedTiles = new HashSet<(int, int)>();
            foreach (var a in Members)
            {
                var t = world[a.Position.X, a.Position.Y];
                var key = (t.X, t.Y);
                // Считаем только функциональные здания медицины, принадлежащие этой цивилизации, без дубликатов
                if (!countedTiles.Contains(key) && t.BuildingFunctional && t.DominantAxis == "healing" && t.OwnerCivId == Id)
                {
                    HealingBuildingsCount++;
                    countedTiles.Add(key);
                }
            }
        }

        public bool HasAnyCentralBuilding() => EmergentStructuresCount > 0;

        public Tile GetHomeTile()
        {
            var world = Simulation.Instance.World;

            foreach (var a in Members)
            {
                var t = world[a.Position.X, a.Position.Y];

                if (t.OwnerCivId == Id && t.Building != BuildingType.None)
                    return t;
            }

            return null;
        }
    }

    public static class CivilizationDetector
    {
        public static List<CivilizationSnapshot> Detect(List<Agent> agents, Tile[,] world)
        {
            var civs = new List<CivilizationSnapshot>();
            var processed = new HashSet<Guid>();

            var existingGroups = agents
                .Where(a => !string.IsNullOrEmpty(a.CivilizationId))
                .GroupBy(a => a.CivilizationId)
                .ToList();

            foreach (var group in existingGroups)
            {
                var members = group.ToList();

                if (members.Count < 3)
                {
                    foreach (var a in members)
                        a.CivilizationId = "";

                    continue;
                }

                var civ = new CivilizationSnapshot
                {
                    Id = group.Key,
                    Name = CivilizationNaming.GenerateName(group.Key)
                };

                civ.Members.AddRange(members);

                foreach (var a in members)
                    processed.Add(a.Id);

                civ.CalculateStats(world);
                civs.Add(civ);
            }

            var unassigned = new HashSet<Agent>(
                agents.Where(a =>
                    string.IsNullOrEmpty(a.CivilizationId) &&
                    !processed.Contains(a.Id)));

            while (unassigned.Count > 0)
            {
                var seed = unassigned.First();

                var civ = new CivilizationSnapshot
                {
                    Id = Guid.NewGuid().ToString()[..8]
                };

                var queue = new Queue<Agent>();
                var enqueued = new HashSet<Agent>();

                queue.Enqueue(seed);
                enqueued.Add(seed);

                while (queue.Count > 0)
                {
                    var a = queue.Dequeue();

                    if (!unassigned.Remove(a))
                        continue;

                    civ.Members.Add(a);
                    a.CivilizationId = civ.Id;

                    var nearby = SpatialGrid.GetNearby(a.Position, 15);

                    foreach (var other in nearby)
                    {
                        if (other == null || other.Id == a.Id)
                            continue;

                        if (!unassigned.Contains(other))
                            continue;

                        if (enqueued.Contains(other))
                            continue;

                        float dist = a.Position.Distance(other.Position);
                        float trust = a.Memory.GetTrust(other.Id);

                        bool isClose = dist <= 8;
                        bool isTrustedAndNearby = trust > 30 && dist <= 15;

                        if (isClose || isTrustedAndNearby)
                        {
                            queue.Enqueue(other);
                            enqueued.Add(other);
                        }
                    }
                }

                if (civ.Members.Count >= 3)
                {
                    civ.Name = CivilizationNaming.GenerateName(civ.Id);
                    civ.CalculateStats(world);
                    civs.Add(civ);

                    FileLogger.Log(
                        $"[TICK {Simulation.Instance.TotalTicks}] Emergent civilization formed: {civ.Name} (Pop: {civ.Members.Count})",
                        FileLogger.LogLevel.Info);
                }
                else
                {
                    foreach (var a in civ.Members)
                        a.CivilizationId = "";
                }
            }

            return civs;
        }
    }
}
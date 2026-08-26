using System.Collections.Generic;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems.Emergence
{
    public static class PatternClassifier
    {
        public static string ClassifyTile(Tile tile, int agentsHere)
        {
            int organicCount = 0;
            int hardCount = 0;

            foreach (var obj in tile.GroundObjects)
            {
                if (!MaterialDB.TryGet(obj.MaterialId, out var spec))
                    continue;

                if (spec.Organic > 0.5f)
                    organicCount++;

                if (spec.Hardness > 0.6f)
                    hardCount++;
            }

            if (organicCount > 3 && tile.Fertility > 0.6f)
                return "Emergent_Farm";

            if (hardCount > 5)
                return "Emergent_Fortification";

            if (agentsHere > 4)
                return "Emergent_Settlement";

            return "Wild";
        }

        public static int CountEmergentStructures(List<Agent> agents, Tile[,] world, string structureType)
        {
            var agentCounts = new Dictionary<(int x, int y), int>();

            foreach (var a in agents)
            {
                var key = (a.Position.X, a.Position.Y);
                agentCounts.TryGetValue(key, out int currentCount);
                agentCounts[key] = currentCount + 1;
            }

            int count = 0;

            foreach (var tile in world)
            {
                int here = agentCounts.GetValueOrDefault((tile.X, tile.Y), 0);

                if (ClassifyTile(tile, here) == structureType)
                    count++;
            }

            return count;
        }

        public static (int farms, int settlements) CountFarmAndSettlement(List<Agent> agents, Tile[,] world)
        {
            var agentCounts = new Dictionary<(int x, int y), int>();

            foreach (var a in agents)
            {
                var key = (a.Position.X, a.Position.Y);
                agentCounts.TryGetValue(key, out int currentCount);
                agentCounts[key] = currentCount + 1;
            }

            int farms = 0;
            int settlements = 0;

            foreach (var tile in world)
            {
                int here = agentCounts.GetValueOrDefault((tile.X, tile.Y), 0);
                string type = ClassifyTile(tile, here);

                if (type == "Emergent_Farm")
                    farms++;
                else if (type == "Emergent_Settlement")
                    settlements++;
            }

            return (farms, settlements);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class InstitutionSystem
    {
        public static int ActiveInstitutions;

        public static void UpdateWorld(Tile[,] world, List<Agent> agents, int tick)
        {
            if (world == null)
                return;

            ActiveInstitutions = 0;

            foreach (var tile in world)
            {
                if (tile == null)
                    continue;

                bool hasPotential =
                    tile.BuildingFunctional ||
                    tile.Texts.Count > 0 ||
                    tile.Artifacts.Count > 0 ||
                    tile.SanctityLevel > 10f ||
                    tile.InstitutionLevel > 0.1f;

                if (!hasPotential)
                    continue;

                float presence = 0f;

                if (tile.BuildingFunctional)
                    presence += 1.5f;

                presence += tile.Texts.Count * 0.4f;
                presence += tile.Artifacts.Count * 0.15f;

                if (tile.SanctityLevel > 10f)
                    presence += 0.3f;

                var nearby = SpatialGrid.GetNearby(new Vector2(tile.X, tile.Y), 1);

                presence += nearby.Count(a => a.Genome.SelfAwareness > 0.45f) * 0.08f;

                if (presence >= 1f)
                {
                    tile.InstitutionLevel = Math.Clamp(
                        tile.InstitutionLevel + presence * 0.05f,
                        0f,
                        10f);

                    tile.InstitutionLastActiveTick = tick;
                    ActiveInstitutions++;
                }
                else
                {
                    tile.InstitutionLevel = Math.Max(0f, tile.InstitutionLevel - 0.03f);
                }

                tile.InstitutionAxis = ChooseAxis(tile);
            }
        }

        public static int CountAxis(CivilizationSnapshot civ, string axis)
        {
            if (civ == null || string.IsNullOrEmpty(axis))
                return 0;

            var world = Simulation.Instance.World;
            int count = 0;

            foreach (var member in civ.Members)
            {
                var tile = world[member.Position.X, member.Position.Y];

                if (tile.InstitutionLevel > 1f && tile.InstitutionAxis == axis)
                    count++;
            }

            return count;
        }

        private static string ChooseAxis(Tile tile)
        {
            if (tile.Building == BuildingType.Library || tile.DominantAxis == "knowledge")
                return "knowledge";

            if (tile.Building == BuildingType.Temple || tile.DominantAxis == "faith")
                return "faith";

            if (tile.Building == BuildingType.Market || tile.DominantAxis == "trade")
                return "trade";

            if (!string.IsNullOrEmpty(tile.DominantAxis))
                return tile.DominantAxis;

            return null;
        }
    }
}
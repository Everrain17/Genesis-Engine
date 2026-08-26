using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems.Emergence
{
    public static class ComplexityAnalyzer
    {
        public static string AnalyzeEra(List<Agent> agents)
        {
            float avgHardness = 0f;
            float avgConductivity = 0f;
            bool hasComposite = false;
            int totalItems = 0;

            foreach (var a in agents)
            {
                foreach (var item in a.Body.Inventory)
                {
                    if (!MaterialDB.TryGet(item.MaterialId, out var spec))
                        continue;

                    avgHardness += spec.Hardness;
                    avgConductivity += spec.Conductivity;

                    if (item.MaterialId.Contains('+') || spec.Depth > 0)
                        hasComposite = true;

                    totalItems++;
                }
            }

            if (totalItems == 0)
                return "Prehistoric";

            avgHardness /= totalItems;
            avgConductivity /= totalItems;

            // Эмерджентные эры без имён "Iron", "Copper"
            if (avgHardness > 0.65f && avgConductivity > 0.45f)
                return "Emergent Metal Age";

            if (hasComposite && avgHardness > 0.45f)
                return "Emergent Neolithic";

            if (avgHardness > 0.25f)
                return "Emergent Chalcolithic";

            return "Emergent Paleolithic";
        }
    }
}
using System.Collections.Generic;
using GenesisEngine.Core;
using GenesisEngine.Systems;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.World
{
    public class Tile
    {
        public int X, Y;
        public TerrainType Terrain;
        public float Elevation;
        public float Moisture;
        public float Temperature;
        public bool HasRiver;
        public static Tile[,] World;

        public Dictionary<string, float> BuildingProfile = new();
        public string DominantAxis;
        public float Profile(string axis) => BuildingProfile.GetValueOrDefault(axis, 0);
        public float StorageCap => Profile("storage") * 50f;

        // Ресурсы (оставляем для обратной совместимости с генерацией мира)
        public Dictionary<ResourceType, float> Resources = new();
        public float Fertility;
        public float SafetyBase;
        public float Exhaustion; // 0..1 истощение почвы

        // Развитие
        public float DevelopmentLevel;
        public float FortificationLevel;
        public float SanctityLevel;
        public float WildnessLevel = 1.0f;
        public float TradeFrequency;
        public float RoadLevel;
        public BuildingType Building = BuildingType.None;
        public string OwnerCivId;

        // === НОВОЕ: состояние здания ===
        public float Durability = 100f;     // 0..100; при 0 здание рушится
        public float BuildingQuality = 1f;  // растёт от методов/работы агентов
        public float InstitutionLevel;
        public string InstitutionAxis;
        public int InstitutionLastActiveTick;
        // === НОВОЕ: генеративный состав здания ===
        public List<string> BuildingComposition = new();  // из каких материалов построено
        public string BuildingName = "";                  // "woodstone-house" и т.п.

        // === НОВОЕ: Эмерджентные объекты на земле ===
        public List<WorldObject> GroundObjects = new();

        // Содержимое
        public List<Artifact> Artifacts = new();
        public List<WrittenText> Texts = new();

        // Вычисляемые
        public int MaxAgents
        {
            get
            {
                if (Building == BuildingType.House)
                {
                    int adjacentHouses = CountAdjacentBuildings(BuildingType.House);
                    int baseCap = adjacentHouses >= 3 ? 15 : 10;
                    return baseCap + (int)(Profile("shelter") * 10);
                }
                if (Building == BuildingType.Skyscraper) return 50;
                if (Building != BuildingType.None) return 5;
                return 5;
            }
        }

        public float Safety => SafetyBase + FortificationLevel * 2;
        public float TotalFood => Resources.GetValueOrDefault(ResourceType.Food, 0) + Resources.GetValueOrDefault(ResourceType.Meat, 0);

        public bool IsPassable
        {
            get
            {
                if (Building == BuildingType.Bridge) return true;
                return Terrain != TerrainType.DeepWater && Terrain != TerrainType.ShallowWater && Terrain != TerrainType.IcePeak;
            }
        }

        // Здание «работает», только пока живое
        public bool BuildingFunctional => Building != BuildingType.None && Durability > 0f;

        public int GetX() => X;
        public int GetY() => Y;
        public float GetFortification() => FortificationLevel;

        public bool HasCentralBuilding()
        {
            return Building == BuildingType.TribalHall
                || Building == BuildingType.VillageCouncil
                || Building == BuildingType.TownHall
                || Building == BuildingType.Capitol;
        }

        private int CalculateMaxAgents()
        {
            int baseCapacity = 5;
            if (Building == BuildingType.House)
            {
                baseCapacity = 10;
                int adjacentHouses = CountAdjacentBuildings(BuildingType.House);
                if (adjacentHouses >= 3) baseCapacity = 15;
                baseCapacity += (int)(BuildingQuality - 1f) * 2;
            }
            else if (Building != BuildingType.None)
            {
                baseCapacity = 5;
            }
            if (DevelopmentLevel > 50) baseCapacity += 10;
            else if (DevelopmentLevel > 10) baseCapacity += 5;
            return baseCapacity;
        }

        public int CountAdjacentBuildings(BuildingType type)
        {
            if (World == null) return 0;
            int count = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = X + dx, ny = Y + dy;
                    if (nx >= 0 && nx < World.GetLength(0) && ny >= 0 && ny < World.GetLength(1))
                    {
                        Tile neighbor = World[nx, ny];
                        if (neighbor.Building == type) count++;
                    }
                }
            return count;
        }
    }
}
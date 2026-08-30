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
        public bool IsContested;  // Территория оспорима (здание разрушилось)
        public bool HasRiver;
        public static Tile[,] World;
        public Dictionary<string, float> BuildingProfile = new();
        public string DominantAxis;
        public float Profile(string axis) => BuildingProfile.GetValueOrDefault(axis, 0);
        public float StorageCap => Profile("storage") * 50f;
        public Dictionary<string, int> CaptureProgress = new();  // Прогресс захвата по цивилизациям
        public Dictionary<ResourceType, float> Resources = new();
        public float Fertility;
        public WeatherSystem.WeatherState Weather;
        // Добавляем в Tile.cs после поля Fertility:
        // В классе Tile добавить новое свойство:
        // После поля GroundObjects добавь:
        public List<Corpse> Corpses = new();

        // И создай класс Corpse (можно в отдельном файле или в Tile.cs):
        public class Corpse
        {
            public Guid Id;
            public float Quantity;        // "Масса" трупа (размер агента)
            public int SpawnTick;         // Когда появился
            public int DecayTime = 500;   // Через сколько тиков разложится
            public bool IsDecayed => Simulation.Instance.TotalTicks - SpawnTick > DecayTime;
        }
        /// <summary>
        /// Несущая способность тайла — сколько агентов может прокормить этот биом.
        /// Зависит от плодородия, сезона и наличия построек.
        /// </summary>
        public float CarryingCapacity
        {
            get
            {
                float baseCapacity = Fertility * 50f; // Базовая ёмкость

                // Сезонный модификатор
                float seasonMod = SeasonSystem.GetFertilityModifier(
                    SeasonSystem.GetCurrentSeason(Simulation.Instance?.TotalTicks ?? 0));
                baseCapacity *= seasonMod;

                // Фермы увеличивают ёмкость
                if (BuildingFunctional && IsFarm)
                {
                    baseCapacity *= (1f + BuildingQuality * 2f);
                }

                return baseCapacity;
            }
        }

        /// <summary>
        /// Проверка перенаселения: сколько агентов сейчас на этом тайле и соседних.
        /// </summary>
        public float LocalPopulationDensity
        {
            get
            {
                if (World == null) return 0f;

                int count = 0;
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int nx = X + dx, ny = Y + dy;
                        if (nx >= 0 && nx < World.GetLength(0) &&
                            ny >= 0 && ny < World.GetLength(1))
                        {
                            var nearby = SpatialGrid.GetNearby(
                                new Vector2(nx, ny), 0);
                            count += nearby.Count;
                        }
                    }
                }
                return count;
            }
        }
        public float SafetyBase;
        public float Exhaustion;

        public float DevelopmentLevel;
        public float FortificationLevel;
        public float SanctityLevel;
        public float WildnessLevel = 1.0f;
        public float TradeFrequency;
        public float RoadLevel;
        public BuildingType Building = BuildingType.None;
        public string OwnerCivId;

        public float Durability = 100f;
        public float BuildingQuality = 1f;
        public float InstitutionLevel;
        public string InstitutionAxis;
        public int InstitutionLastActiveTick;

        public List<string> BuildingComposition = new();
        public string BuildingName = "";

        public List<WorldObject> GroundObjects = new();
        public List<Artifact> Artifacts = new();
        public List<WrittenText> Texts = new();

        // ============================================================
        // v3: ФУНКЦИЯ ЗДАНИЯ. Агенты строят только Structure,
        // а смысл здания = DominantAxis. Старые enum-значения тоже
        // маппятся на функцию (совместимость).
        // ============================================================
        public bool IsStructure => Building == BuildingType.Structure;

        public string Function
        {
            get
            {
                if (Building == BuildingType.None) return null;
                if (!string.IsNullOrEmpty(DominantAxis)) return DominantAxis;
                return LegacyAxis(Building);
            }
        }

        public bool HasFunction(string axis) => Function == axis;
        public bool IsFarm => HasFunction("food");
        public bool IsHouse => HasFunction("shelter");
        public bool IsLibrary => HasFunction("knowledge");
        public bool IsTemple => HasFunction("faith");
        public bool IsMarket => HasFunction("trade");
        public bool IsBarracks => HasFunction("defense");
        public bool IsBridge => HasFunction("mobility");
        public bool IsMine { get; set; }   // Шахта для добычи руды (теперь можно присваивать)
        private static string LegacyAxis(BuildingType b) => b switch
        {
            BuildingType.Farm => "food",
            BuildingType.House => "shelter",
            BuildingType.Library => "knowledge",
            BuildingType.Temple => "faith",
            BuildingType.Market => "trade",
            BuildingType.Barracks => "defense",
            BuildingType.Bridge => "mobility",
            BuildingType.Warehouse => "storage",
            BuildingType.MineShaft => "mining",
            _ => "shelter"
        };

        public int MaxAgents
        {
            get
            {
                if (IsHouse)
                {
                    int adjacentHouses = CountAdjacentHouses();
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
                if (IsBridge) return true;
                return Terrain != TerrainType.DeepWater && Terrain != TerrainType.ShallowWater && Terrain != TerrainType.IcePeak;
            }
        }

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

        private int CountAdjacentHouses()
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
                        if (World[nx, ny].IsHouse) count++;
                    }
                }
            return count;
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
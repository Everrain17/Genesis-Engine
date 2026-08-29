// ============================================================
// ФАЙЛ: Systems/DisasterSystem.cs
// Локальные бедствия: наводнение, пожар, засуха, метель
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.UI;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems
{
    public enum DisasterType
    {
        Flood,
        Fire,
        Drought,
        Blizzard
    }

    public class Disaster
    {
        public Guid Id = Guid.NewGuid();
        public DisasterType Type;
        public Vector2 Center;
        public int Radius;
        public int StartTick;
        public int Duration;
        public float Intensity;
        public List<(int x, int y)> AffectedTiles = new();

        public bool IsActive => Simulation.Instance.TotalTicks - StartTick < Duration;
    }

    public static class DisasterSystem
    {
        private static readonly List<Disaster> ActiveDisasters = new();

        // Храним оригинальные значения для восстановления после бедствия
        private static readonly Dictionary<(int, int), TerrainType> OriginalTerrain = new();
        private static readonly Dictionary<(int, int), float> OriginalFertility = new();
        private static readonly Dictionary<(int, int), float> OriginalWildness = new();

        private static int _lastSpawnCheckTick = 0;
        private const int SpawnCheckInterval = 500;

        // ============================================================
        // ГЛАВНЫЙ UPDATE — вызывается из Simulation.Tick()
        // ============================================================
        public static void Update(Tile[,] world, List<Agent> agents, int tick)
        {
            if (world == null) return;

            // Попытка создать новое бедствие
            if (tick - _lastSpawnCheckTick >= SpawnCheckInterval)
            {
                TrySpawnDisaster(world, tick);
                _lastSpawnCheckTick = tick;
            }

            // Обновляем активные бедствия
            for (int i = ActiveDisasters.Count - 1; i >= 0; i--)
            {
                var disaster = ActiveDisasters[i];

                if (!disaster.IsActive)
                {
                    EndDisaster(disaster, world);
                    ActiveDisasters.RemoveAt(i);
                    continue;
                }

                UpdateDisasterEffects(disaster, world, agents, tick);
            }
        }

        // ============================================================
        // СПАВН: ~2% шанс каждые 500 тиков
        // ============================================================
        private static void TrySpawnDisaster(Tile[,] world, int tick)
        {
            var rng = Simulation.Instance.Rng;

            // 2% шанс
            if (rng.NextDouble() > 0.02) return;

            int width = world.GetLength(0);
            int height = world.GetLength(1);

            // Ищем подходящую позицию (не DeepWater, не IcePeak)
            int x = 0, y = 0;
            int attempts = 0;
            while (attempts < 20)
            {
                x = rng.Next(width);
                y = rng.Next(height);
                var t = world[x, y];
                if (t.Terrain != TerrainType.DeepWater && t.Terrain != TerrainType.IcePeak)
                    break;
                attempts++;
            }

            var center = new Vector2(x, y);
            var type = (DisasterType)rng.Next(4);

            var disaster = new Disaster
            {
                Type = type,
                Center = center,
                Radius = rng.Next(3, 8),
                StartTick = tick,
                Duration = GetDuration(type),
                Intensity = 0.5f + (float)rng.NextDouble() * 0.5f
            };

            ApplyDisaster(disaster, world);
            ActiveDisasters.Add(disaster);

            FileLogger.Log(
                $"[TICK {tick}] 🌪️ DISASTER: {type} at ({x},{y}) " +
                $"radius={disaster.Radius} duration={disaster.Duration} " +
                $"tiles={disaster.AffectedTiles.Count}",
                FileLogger.LogLevel.Warning);
        }

        private static int GetDuration(DisasterType type)
        {
            return type switch
            {
                DisasterType.Flood => 2000,
                DisasterType.Fire => 1500,
                DisasterType.Drought => 3000,
                DisasterType.Blizzard => 1000,
                _ => 1000
            };
        }

        // ============================================================
        // ПРИМЕНЕНИЕ: проходим по всем тайлам в радиусе
        // ============================================================
        private static void ApplyDisaster(Disaster disaster, Tile[,] world)
        {
            int width = world.GetLength(0);
            int height = world.GetLength(1);
            int cx = (int)disaster.Center.X;
            int cy = (int)disaster.Center.Y;

            for (int dx = -disaster.Radius; dx <= disaster.Radius; dx++)
            {
                for (int dy = -disaster.Radius; dy <= disaster.Radius; dy++)
                {
                    // Круглый радиус
                    if (dx * dx + dy * dy > disaster.Radius * disaster.Radius)
                        continue;

                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || x >= width || y < 0 || y >= height)
                        continue;

                    var tile = world[x, y];
                    disaster.AffectedTiles.Add((x, y));

                    switch (disaster.Type)
                    {
                        case DisasterType.Flood: ApplyFlood(tile, x, y); break;
                        case DisasterType.Fire: ApplyFire(tile, x, y, world, disaster); break;
                        case DisasterType.Drought: ApplyDrought(tile, x, y); break;
                        case DisasterType.Blizzard: ApplyBlizzard(tile, x, y); break;
                    }
                }
            }
        }

        // ============================================================
        // 🌊 НАВОДНЕНИЕ
        // ============================================================
        private static void ApplyFlood(Tile tile, int x, int y)
        {
            var key = (x, y);
            if (!OriginalTerrain.ContainsKey(key))
                OriginalTerrain[key] = tile.Terrain;

            // Только проходимые сухопутные тайлы затапливаем
            if (tile.Terrain == TerrainType.Grassland ||
                tile.Terrain == TerrainType.Beach ||
                tile.Terrain == TerrainType.Forest ||
                tile.Terrain == TerrainType.Desert ||
                tile.Terrain == TerrainType.Swamp)
            {
                tile.Terrain = TerrainType.ShallowWater;

                // Уничтожаем все GroundObjects
                tile.GroundObjects.Clear();

                // Разрушаем здания (кроме мостов)
                if (tile.Building != BuildingType.None && !tile.IsBridge)
                {
                    tile.Building = BuildingType.None;
                    tile.BuildingProfile.Clear();
                    tile.BuildingQuality = 1f;
                    tile.Durability = 0f;
                }
            }
        }

        // ============================================================
        // 🔥 ПОЖАР (с каскадным распространением!)
        // ============================================================
        private static void ApplyFire(Tile tile, int x, int y, Tile[,] world, Disaster disaster)
        {
            var key = (x, y);

            if (tile.Terrain == TerrainType.Forest ||
                tile.Terrain == TerrainType.Taiga)
            {
                if (!OriginalTerrain.ContainsKey(key))
                    OriginalTerrain[key] = tile.Terrain;
                if (!OriginalFertility.ContainsKey(key))
                    OriginalFertility[key] = tile.Fertility;

                // Лес → выжженная земля (используем Desert как визуал)
                tile.Terrain = TerrainType.Desert;
                tile.Fertility = 0.05f;

                // Сжигаем ВСЕ Organic материалы
                tile.GroundObjects.RemoveAll(obj =>
                {
                    if (MaterialDB.TryGet(obj.MaterialId, out var spec))
                        return spec.Organic > 0.4f;
                    return false;
                });

                // Сжигаем деревянные здания
                if (tile.Building != BuildingType.None)
                {
                    // Здания с низкой Logic/Conductivity — «деревянные»
                    bool isWooden = tile.BuildingProfile.GetValueOrDefault("shelter", 0) > 0.3f;
                    if (isWooden)
                    {
                        tile.Building = BuildingType.None;
                        tile.BuildingProfile.Clear();
                        tile.Durability = 0f;
                    }
                }

                // КАСКАД: поджигаем соседей
                SpreadFire(x, y, world, disaster);
            }
            else if (tile.Terrain == TerrainType.Grassland)
            {
                // Трава тоже горит, но не распространяется
                if (!OriginalTerrain.ContainsKey(key))
                    OriginalTerrain[key] = tile.Terrain;
                if (!OriginalFertility.ContainsKey(key))
                    OriginalFertility[key] = tile.Fertility;

                tile.Terrain = TerrainType.Desert;
                tile.Fertility *= 0.3f;

                tile.GroundObjects.RemoveAll(obj =>
                {
                    if (MaterialDB.TryGet(obj.MaterialId, out var spec))
                        return spec.Organic > 0.4f;
                    return false;
                });
            }
        }

        private static void SpreadFire(int x, int y, Tile[,] world, Disaster disaster)
        {
            var rng = Simulation.Instance.Rng;
            int width = world.GetLength(0);
            int height = world.GetLength(1);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    var neighbor = world[nx, ny];

                    // 30% шанс поджечь соседний лес
                    if ((neighbor.Terrain == TerrainType.Forest ||
                         neighbor.Terrain == TerrainType.Taiga) &&
                        !disaster.AffectedTiles.Contains((nx, ny)) &&
                        rng.NextDouble() < 0.30 * disaster.Intensity)
                    {
                        disaster.AffectedTiles.Add((nx, ny));
                        ApplyFire(neighbor, nx, ny, world, disaster);
                    }
                }
            }
        }

        // ============================================================
        // 🏜️ ЗАСУХА
        // ============================================================
        private static void ApplyDrought(Tile tile, int x, int y)
        {
            var key = (x, y);
            if (!OriginalFertility.ContainsKey(key))
                OriginalFertility[key] = tile.Fertility;

            // Fertility *= 0.2
            tile.Fertility = OriginalFertility[key] * 0.2f;

            // Фермы теряют эффективность
            if (tile.Building == BuildingType.Farm)
            {
                tile.BuildingQuality *= 0.1f;
            }
        }

        // ============================================================
        // ❄️ МЕТЕЛЬ
        // ============================================================
        private static void ApplyBlizzard(Tile tile, int x, int y)
        {
            var key = (x, y);
            if (!OriginalWildness.ContainsKey(key))
                OriginalWildness[key] = tile.WildnessLevel;

            // Увеличиваем «дикость» — территория становится опасной
            tile.WildnessLevel = Math.Min(2.0f, tile.WildnessLevel + 0.5f);

            // Дороги заносит снегом
            if (tile.RoadLevel > 0)
                tile.RoadLevel *= 0.5f;
        }

        // ============================================================
        // ЕЖЕТИКОВЫЕ ЭФФЕКТЫ НА АГЕНТОВ
        // ============================================================
        private static void UpdateDisasterEffects(
            Disaster disaster, Tile[,] world, List<Agent> agents, int tick)
        {
            // Эффекты на агентов применяем не каждый тик, а каждые 10
            if (tick % 10 != 0) return;

            foreach (var (x, y) in disaster.AffectedTiles)
            {
                var agentsHere = SpatialGrid.GetNearby(new Vector2(x, y), 0);

                foreach (var agent in agentsHere)
                {
                    switch (disaster.Type)
                    {
                        case DisasterType.Flood:
                            // Тонут, теряют здоровье и энергию
                            agent.Body.Health -= 2f * disaster.Intensity;
                            agent.Body.Energy -= 5f * disaster.Intensity;
                            agent.Fear += 10f * disaster.Intensity;
                            break;

                        case DisasterType.Fire:
                            // Ожоги, паника
                            agent.Body.Health -= 3f * disaster.Intensity;
                            agent.Fear += 15f * disaster.Intensity;
                            break;

                        case DisasterType.Drought:
                            // Голод усиливается
                            agent.Body.Hunger += 5f * disaster.Intensity;
                            break;

                        case DisasterType.Blizzard:
                            // Холод, страх, потеря энергии
                            agent.Body.Energy -= 4f * disaster.Intensity;
                            agent.Fear += 8f * disaster.Intensity;
                            break;
                    }
                }
            }

            // Пожар: дополнительный каскад каждые 200 тиков
            if (disaster.Type == DisasterType.Fire &&
                (tick - disaster.StartTick) % 200 == 0 &&
                (tick - disaster.StartTick) > 0)
            {
                foreach (var (x, y) in disaster.AffectedTiles.ToList())
                {
                    SpreadFire(x, y, world, disaster);
                }
            }
        }

        // ============================================================
        // ЗАВЕРШЕНИЕ БЕДСТВИЯ: восстановление тайлов
        // ============================================================
        private static void EndDisaster(Disaster disaster, Tile[,] world)
        {
            foreach (var (x, y) in disaster.AffectedTiles)
            {
                var tile = world[x, y];
                var key = (x, y);

                if (OriginalTerrain.TryGetValue(key, out var origTerrain))
                {
                    tile.Terrain = origTerrain;
                    OriginalTerrain.Remove(key);
                }

                if (OriginalFertility.TryGetValue(key, out var origFert))
                {
                    tile.Fertility = origFert;
                    OriginalFertility.Remove(key);

                    // Восстанавливаем фермы
                    if (tile.Building == BuildingType.Farm)
                        tile.BuildingQuality = Math.Max(tile.BuildingQuality, 1f);
                }

                if (OriginalWildness.TryGetValue(key, out var origWild))
                {
                    tile.WildnessLevel = origWild;
                    OriginalWildness.Remove(key);
                }
            }

            FileLogger.Log(
                $"[TICK {Simulation.Instance.TotalTicks}] ✅ DISASTER ENDED: {disaster.Type} " +
                $"at ({disaster.Center.X},{disaster.Center.Y}) " +
                $"affected {disaster.AffectedTiles.Count} tiles",
                FileLogger.LogLevel.Info);
        }

        // ============================================================
        // ПУБЛИЧНЫЕ API
        // ============================================================
        public static List<Disaster> GetActiveDisasters()
        {
            return ActiveDisasters.Where(d => d.IsActive).ToList();
        }

        /// <summary>
        /// Находится ли агент в зоне метели? (для модификации EffectiveHearing)
        /// </summary>
        public static bool IsAffectedByBlizzard(Agent agent)
        {
            if (agent == null) return false;
            int ax = (int)agent.Position.X;
            int ay = (int)agent.Position.Y;
            return ActiveDisasters.Any(d =>
                d.Type == DisasterType.Blizzard &&
                d.IsActive &&
                d.AffectedTiles.Contains((ax, ay)));
        }

        /// <summary>
        /// Находится ли агент в зоне любого бедствия? (для UI)
        /// </summary>
        public static DisasterType? GetDisasterAt(Agent agent)
        {
            if (agent == null) return null;
            int ax = (int)agent.Position.X;
            int ay = (int)agent.Position.Y;
            var d = ActiveDisasters.FirstOrDefault(d =>
                d.IsActive && d.AffectedTiles.Contains((ax, ay)));
            return d?.Type;
        }
    }
}
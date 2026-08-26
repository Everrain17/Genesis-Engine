using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Systems;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.World
{
    public class WorldGenerator
    {
        private int width, height;
        private Random rng;
        private float[,] elevation;
        private float[,] moisture;
        private float[,] tempNoise;

        public Tile[,] Generate(int w, int h, int seed)
        {
            width = w; height = h;
            rng = new Random(seed);
            elevation = GenerateNoise(60f, 4);
            moisture = GenerateNoise(45f, 3);
            tempNoise = GenerateNoise(80f, 2);

            Tile[,] world = new Tile[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    world[x, y] = CreateTile(x, y);

            CarveIsthmuses(world);
            PlaceResources(world);
            PlaceFood(world);
            return world;
        }

        private void CarveIsthmuses(Tile[,] world)
        {
            int[] rows = { (int)(height * 0.25f), (int)(height * 0.75f) };
            foreach (int by in rows)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int y = by + dy;
                    if (y < 0 || y >= height) continue;
                    for (int x = 0; x < width; x++)
                    {
                        var t = world[x, y];
                        if (t.Terrain == TerrainType.DeepWater || t.Terrain == TerrainType.ShallowWater)
                        {
                            t.Terrain = TerrainType.Beach;
                            t.Fertility = 0.2f;
                            t.SafetyBase = 0.4f;
                            t.HasRiver = false;
                        }
                    }
                }
        }

        private Tile CreateTile(int x, int y)
        {
            Tile tile = new Tile { X = x, Y = y };
            float e = elevation[x, y];
            float m = moisture[x, y];
            float lat = MathF.Abs(y - height / 2f) / (height / 2f);
            float temp = Math.Clamp(1f - lat + (tempNoise[x, y] - 0.5f) * 0.3f, 0f, 1f);
            tile.Temperature = temp;

            float centerX = width * 0.5f;
            float oceanHalfWidth = width * 0.07f;
            float noiseX = MathF.Sin(y * 0.35f) * width * 0.04f;
            float dist = MathF.Abs(x - (centerX + noiseX));

            if (dist < oceanHalfWidth) { tile.Terrain = TerrainType.DeepWater; tile.Fertility = 0; tile.SafetyBase = 0; return tile; }
            if (dist < oceanHalfWidth + width * 0.025f) { tile.Terrain = TerrainType.ShallowWater; tile.Fertility = 0; tile.SafetyBase = 0.1f; return tile; }

            bool coastal = dist < oceanHalfWidth + width * 0.04f;
            if (coastal) tile.Terrain = TerrainType.Beach;

            if (temp < 0.16f) tile.Terrain = e > 0.7f ? TerrainType.IcePeak : TerrainType.Tundra;
            else if (temp < 0.32f) tile.Terrain = m > 0.45f ? TerrainType.Taiga : TerrainType.Tundra;
            else if (!coastal)
            {
                if (e > 0.88f) tile.Terrain = TerrainType.IcePeak;
                else if (e > 0.72f) tile.Terrain = TerrainType.Mountain;
                else if (e > 0.55f) tile.Terrain = m > 0.5f ? TerrainType.Forest : TerrainType.Hill;
                else if (temp > 0.62f && m < 0.28f) tile.Terrain = TerrainType.Desert;
                else if (temp > 0.62f && m > 0.72f) tile.Terrain = TerrainType.Swamp;
                else if (m > 0.6f) tile.Terrain = TerrainType.Forest;
                else tile.Terrain = TerrainType.Grassland;
            }

            tile.Fertility = tile.Terrain switch
            {
                TerrainType.Grassland => 0.5f + m * 0.5f,
                TerrainType.Forest => 0.4f,
                TerrainType.Swamp => 0.6f,
                TerrainType.Beach => 0.3f,
                TerrainType.Desert => 0.08f,
                TerrainType.Hill => 0.3f,
                TerrainType.Taiga => 0.25f,
                TerrainType.Tundra => 0.08f,
                TerrainType.Mountain => 0.05f,
                _ => 0.1f
            };

            tile.SafetyBase = tile.Terrain switch
            {
                TerrainType.Grassland => 0.6f,
                TerrainType.Beach => 0.5f,
                TerrainType.Forest => 0.3f,
                TerrainType.Taiga => 0.3f,
                TerrainType.Hill => 0.5f,
                TerrainType.Mountain => 0.2f,
                TerrainType.Swamp => 0.2f,
                TerrainType.Desert => 0.4f,
                TerrainType.Tundra => 0.35f,
                _ => 0.5f
            };

            tile.HasRiver = false;
            return tile;
        }

        private float[,] GenerateNoise(float scale, int octaves)
        {
            float[,] noise = new float[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    float value = 0f, amplitude = 1f, frequency = 1f, maxValue = 0f;
                    for (int o = 0; o < octaves; o++)
                    {
                        value += SmoothNoise(x / scale * frequency, y / scale * frequency) * amplitude;
                        maxValue += amplitude;
                        amplitude *= 0.5f; frequency *= 2f;
                    }
                    noise[x, y] = value / maxValue;
                }
            return noise;
        }

        private float SmoothNoise(float x, float y)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf);
            float v = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi);
            float c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        private float Hash(int x, int y)
        {
            float h = MathF.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return h - MathF.Floor(h);
        }

        // НОВОЕ: Полностью процедурное размещение ресурсов на основе свойств материалов
        private void PlaceResources(Tile[,] world)
        {
            var allMaterials = MaterialDB.Base.Values.ToList();
            string foodId = MaterialDB.GetFoodMaterialId();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Tile tile = world[x, y];

                    if (!tile.IsPassable ||
                        tile.Terrain == TerrainType.DeepWater ||
                        tile.Terrain == TerrainType.IcePeak)
                        continue;

                    var candidates = new List<(ResourceSpec mat, float score, float qty)>();

                    foreach (var mat in allMaterials)
                    {
                        float score = CalculateSuitability(mat, tile.Terrain);
                        if (score <= 0.02f) continue;

                        float qty = CalculateBaseQuantity(mat, tile.Terrain);
                        candidates.Add((mat, score, qty));
                    }

                    var selected = candidates
                        .OrderByDescending(c => c.score * (0.75f + (float)rng.NextDouble() * 0.5f))
                        .Take(6);

                    foreach (var c in selected)
                    {
                        tile.GroundObjects.Add(new WorldObject
                        {
                            MaterialId = c.mat.Id,
                            Quantity = c.qty,
                            Position = new Vector2(x, y)
                        });
                    }

                    // Гарантируем немного еды на плодородных тайлах
                    if (tile.Fertility > 0.35f &&
                        !tile.GroundObjects.Any(o =>
                            MaterialDB.TryGet(o.MaterialId, out var s) && s.Organic > 0.5f))
                    {
                        tile.GroundObjects.Add(new WorldObject
                        {
                            MaterialId = foodId,
                            Quantity = 20f * tile.Fertility,
                            Position = new Vector2(x, y)
                        });
                    }
                }
            }
        }
        private float CalculateSuitability(ResourceSpec mat, TerrainType terrain)
        {
            var p = mat.Observed;
            float score = 0f;

            // Органика: леса, луга, болота
            if (p.Organic > 0.55f)
            {
                score += terrain switch
                {
                    TerrainType.Forest => 0.90f,
                    TerrainType.Taiga => 0.70f,
                    TerrainType.Grassland => 0.60f,
                    TerrainType.Swamp => 0.50f,
                    TerrainType.Beach => 0.15f,
                    _ => 0.02f
                };
            }

            // Твёрдые неорганические материалы: горы, холмы
            if (p.Hardness > 0.55f && p.Organic < 0.35f)
            {
                score += terrain switch
                {
                    TerrainType.Mountain => 0.95f,
                    TerrainType.Hill => 0.65f,
                    TerrainType.Beach => 0.15f,
                    _ => 0.01f
                };
            }

            // Проводящие материалы: металлы/полупроводники
            if (p.Conductivity > 0.5f)
            {
                score += terrain switch
                {
                    TerrainType.Mountain => 0.40f,
                    TerrainType.Hill => 0.15f,
                    _ => 0.005f
                };
            }

            // Тепло/горючие материалы
            if (p.HeatOutput > 0.6f)
            {
                score += terrain switch
                {
                    TerrainType.Swamp => 0.30f,
                    TerrainType.Desert => 0.30f,
                    TerrainType.Mountain => 0.20f,
                    _ => 0.01f
                };
            }

            // Логика/кристаллы
            if (p.Logic > 0.5f)
            {
                score += terrain switch
                {
                    TerrainType.Mountain => 0.12f,
                    TerrainType.Hill => 0.05f,
                    _ => 0.002f
                };
            }

            // Соль/лёгкие/плавучие материалы
            if (p.Buoyancy > 0.5f || p.Salt > 0.5f)
            {
                score += terrain switch
                {
                    TerrainType.Beach => 0.60f,
                    TerrainType.Swamp => 0.35f,
                    TerrainType.Desert => 0.30f,
                    _ => 0.02f
                };
            }

            return Math.Clamp(score, 0f, 1f);
        }
        // Вычисляем шанс спавна материала на основе его свойств и типа местности
        private float CalculateSpawnChance(ResourceSpec mat, TerrainType terrain)
        {
            var props = mat.Observed;

            // Органические материалы (дерево, трава, волокна) → лес, луг, болото
            if (props.Organic > 0.6f)
            {
                return terrain switch
                {
                    TerrainType.Forest => 0.9f,
                    TerrainType.Taiga => 0.7f,
                    TerrainType.Grassland => 0.5f,
                    TerrainType.Swamp => 0.6f,
                    _ => 0.05f
                };
            }

            // Твёрдые материалы (камень, руда) → горы, холмы
            if (props.Hardness > 0.6f && props.Organic < 0.3f)
            {
                return terrain switch
                {
                    TerrainType.Mountain => 0.95f,
                    TerrainType.Hill => 0.7f,
                    TerrainType.Beach => 0.3f,
                    _ => 0.02f
                };
            }

            // Проводящие материалы (металлы) → горы (редко)
            if (props.Conductivity > 0.5f && props.Hardness > 0.4f)
            {
                return terrain switch
                {
                    TerrainType.Mountain => 0.4f,
                    TerrainType.Hill => 0.2f,
                    _ => 0.01f
                };
            }

            // Материалы с высоким тепловыделением (уголь, нефть) → болото, пустыня
            if (props.HeatOutput > 0.6f)
            {
                return terrain switch
                {
                    TerrainType.Swamp => 0.3f,
                    TerrainType.Desert => 0.25f,
                    TerrainType.Mountain => 0.2f,
                    _ => 0.02f
                };
            }

            // Материалы с высокой логикой (кристаллы, кремний) → горы (очень редко)
            if (props.Logic > 0.5f)
            {
                return terrain switch
                {
                    TerrainType.Mountain => 0.15f,
                    TerrainType.Hill => 0.05f,
                    _ => 0.005f
                };
            }

            // Материалы с высокой плавучестью (соль, глина) → пляж, болото
            if (props.Buoyancy > 0.5f || props.Salt > 0.5f)
            {
                return terrain switch
                {
                    TerrainType.Beach => 0.6f,
                    TerrainType.Swamp => 0.4f,
                    TerrainType.Desert => 0.3f,
                    _ => 0.05f
                };
            }

            // Дефолтный шанс для остальных материалов
            return 0.1f;
        }

        // Вычисляем базовое количество материала на тайле
        private float CalculateBaseQuantity(ResourceSpec mat, TerrainType terrain)
        {
            var p = mat.Observed;

            float rarityMultiplier = 1f - p.Rarity * 0.7f;

            float terrainMultiplier = terrain switch
            {
                TerrainType.Mountain => 2.2f,
                TerrainType.Hill => 1.7f,
                TerrainType.Forest => 1.9f,
                TerrainType.Taiga => 1.5f,
                TerrainType.Grassland => 1.3f,
                TerrainType.Swamp => 1.2f,
                TerrainType.Beach => 1.0f,
                TerrainType.Desert => 0.8f,
                _ => 1.0f
            };

            // Еда/биомасса
            if (p.Organic > 0.55f)
                return 30f * rarityMultiplier * terrainMultiplier;

            // Твёрдые материалы
            if (p.Hardness > 0.55f && p.Organic < 0.35f)
                return 40f * rarityMultiplier * terrainMultiplier;

            // Проводящие материалы
            if (p.Conductivity > 0.5f)
                return 12f * rarityMultiplier * terrainMultiplier;

            // Очень редкие материалы
            if (p.Rarity > 0.75f)
                return 3f * rarityMultiplier * terrainMultiplier;

            return 10f * rarityMultiplier * terrainMultiplier;
        }

        private void PlaceFood(Tile[,] world)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    Tile tile = world[x, y];
                    if (!tile.IsPassable) continue;
                    float baseFood = tile.Fertility * 50f;
                    tile.Resources[ResourceType.Food] = (float)Math.Round(baseFood + rng.NextDouble() * 30);
                }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.Systems.Physics;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class ConstructionSystem
    {
        public static bool TryBuild(Agent agent, Tile tile, Random rng)
        {
            if (agent == null || tile == null) return false;
            if (tile.Building != BuildingType.None) return false;
            if (!tile.IsPassable) return false;
            if (agent.Body.Inventory.Count < 2) return false;

            string axis = ChooseAxis(agent, rng);
            string buildingConcept = EffectTables.AxisToBuilding(axis).ToString();

            var civ = Simulation.activeCivs?.FirstOrDefault(c => c.Id == agent.CivilizationId);
            bool knowsRecipe = civ != null && KnowledgeSystem.CivKnowsRecipe(civ, buildingConcept);

            // Границы территорий
            bool foreign = !string.IsNullOrEmpty(tile.OwnerCivId) && tile.OwnerCivId != agent.CivilizationId;
            if (foreign && !string.IsNullOrEmpty(agent.CivilizationId) && !DiplomacySystem.IsAtWar(agent.CivilizationId) && rng.NextDouble() < 0.6f)
                return false;

            if (!knowsRecipe)
            {
                float experimentChance = agent.Genome.Openness * agent.Genome.SelfAwareness * 0.20f;
                if (rng.NextDouble() > experimentChance) return false;
            }

            var candidates = new List<(WorldObject obj, ResourceSpec spec, float score)>();
            foreach (var obj in agent.Body.Inventory)
            {
                if (obj.Quantity < 1f) continue;
                if (!MaterialDB.TryGet(obj.MaterialId, out var spec)) continue;
                float score = EffectTables.Compute(axis, spec) / 100f;
                candidates.Add((obj, spec, score));
            }
            if (candidates.Count < 2) return false;

            var best = candidates.OrderByDescending(c => c.score).Take(2).ToList();
            if (best[0].score + best[1].score <= 0.05f) return false;

            var mix = MaterialDB.Mix(best[0].spec, best[1].spec);
            float power = EffectTables.Compute(axis, mix) / 100f;
            if (power <= 0.05f) return false;

            best[0].obj.Quantity -= 1f;
            if (best[0].obj.Quantity <= 0f) agent.Body.Inventory.Remove(best[0].obj);
            best[1].obj.Quantity -= 1f;
            if (best[1].obj.Quantity <= 0f) agent.Body.Inventory.Remove(best[1].obj);

            var profile = new Dictionary<string, float>();
            foreach (var ax in EffectTables.Axis.Keys)
            {
                // Просто считаем профиль для ВСЕХ осей (для статистики)
                profile[ax] = EffectTables.Compute(ax, mix) / 100f;
            }

            // v3: строим ТОЛЬКО Structure; функцию несёт DominantAxis
            tile.Building = BuildingType.Structure;
            tile.OwnerCivId = agent.CivilizationId;
            tile.DominantAxis = axis;
            tile.BuildingProfile = profile;
            tile.BuildingComposition = new List<string> { best[0].spec.Id, best[1].spec.Id };

            tile.BuildingQuality =
                1f +
                agent.Genome.Conscientiousness * 0.30f +
                KnowledgeSystem.BuildingPower(civ, axis) * 0.20f;

            tile.Durability = 100f;
            tile.DevelopmentLevel += 5f;

            tile.BuildingName = $"{Root(best[0].spec)}{Root(best[1].spec)}-{EffectTables.AxisBuildingWord(axis)}";
            // === НОВОЕ: Распознавание шахт ===
            // Если рядом есть рудная жила и ось = mining, это шахта
            // === Распознавание шахт ===
            bool isMine = false;
            if (axis == "mining")
            {
                // Проверяем наличие "руды" в радиусе 3
                // Руда = материал с высокой твёрдостью (>0.55) и низкой органикой (<0.3)
                for (int sDx = -3; sDx <= 3; sDx++)
                {
                    for (int sDy = -3; sDy <= 3; sDy++)
                    {
                        int sNx = tile.X + sDx;
                        int sNy = tile.Y + sDy;
                        if (sNx >= 0 && sNx < Simulation.Instance.World.GetLength(0) &&
                            sNy >= 0 && sNy < Simulation.Instance.World.GetLength(1))
                        {
                            var neighbor = Simulation.Instance.World[sNx, sNy];
                            if (neighbor.GroundObjects.Any(o =>
                                MaterialDB.TryGet(o.MaterialId, out var spec) &&
                                spec.Hardness > 0.55f && spec.Organic < 0.3f))
                            {
                                isMine = true;
                                break;
                            }
                        }
                    }
                    if (isMine) break;
                }
            }

            if (isMine)
            {
                tile.IsMine = true;
                tile.DominantAxis = "mining";
                tile.BuildingName = $"{Root(best[0].spec)}{Root(best[1].spec)}-mine";
            }
            EventBus.Publish(new SimEvent
            {
                Type = SimEventType.BuildingCreated,
                Tick = Simulation.Instance.TotalTicks,
                Actor = agent,
                Position = new Vector2(tile.X, tile.Y),
                Data = tile.BuildingName,
                Value = power
            });
            // === НОВОЕ: Здание создает зону контроля вокруг себя ===
            int influenceRadius = CalculateInfluenceRadius(tile.Building, tile.BuildingQuality);
            SpreadCivilizationControl(tile, agent.CivilizationId, influenceRadius);
            agent.LastAction = "Build";
            return true;
        }

        private static string ChooseAxis(Agent agent, Random rng)
        {
            float hunger = agent.Body.Hunger / 100f;
            float loneliness = agent.Loneliness / 100f;
            float fear = agent.Fear / 100f;
            float full = agent.Body.CurrentCarryWeight / Math.Max(1f, agent.Body.MaxCarryWeight);
            Tile tile = Simulation.Instance?.GetTile(agent.Position);
            float institution = tile?.InstitutionLevel ?? 0f;
            float sanctity = tile?.SanctityLevel ?? 0f;

            bool atWar = !string.IsNullOrEmpty(agent.CivilizationId) && DiplomacySystem.IsAtWar(agent.CivilizationId);
            float peace = string.IsNullOrEmpty(agent.CivilizationId) ? 0f : DiplomacySystem.PeaceStability(agent.CivilizationId);
            float warPressure = string.IsNullOrEmpty(agent.CivilizationId) ? 0f : DiplomacySystem.WarPressure(agent.CivilizationId);

            float knowledgeDemand =
                agent.Genome.SelfAwareness * 0.40f + agent.Genome.Openness * 0.25f + agent.Logic * 0.35f +
                institution * 0.03f + peace * 0.35f - warPressure * 0.25f;
            float tradeDemand = agent.Genome.Extraversion * 0.30f + agent.Genome.Openness * 0.20f + peace * 0.30f - warPressure * 0.15f;
            float cultureDemand = agent.Genome.Openness * 0.25f + peace * 0.25f - warPressure * 0.10f;
            float faithDemand = agent.Genome.Spirituality * 0.30f + (sanctity > 20f ? 0.15f : 0f);
            float healingDemand = Math.Max(0f, 1f - agent.Body.Health / 100f) + warPressure * 0.15f;
            float warmthDemand =
                (SeasonSystem.GetCurrentSeason(Simulation.Instance.TotalTicks) == SeasonSystem.Season.Winter ? 0.90f :
                 SeasonSystem.GetCurrentSeason(Simulation.Instance.TotalTicks) == SeasonSystem.Season.Autumn ? 0.35f : 0.15f) +
                Math.Max(0f, 1f - agent.Body.Health / 100f) * 0.30f;
            float miningBonus = 0f;
            if (tile != null)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        int nx = tile.X + dx, ny = tile.Y + dy;
                        if (nx >= 0 && nx < Simulation.Instance.World.GetLength(0) &&
                            ny >= 0 && ny < Simulation.Instance.World.GetLength(1))
                        {
                            var neighbor = Simulation.Instance.World[nx, ny];
                            // Руда = материал с высокой твёрдостью и низкой органикой
                            bool hasOre = neighbor.GroundObjects.Any(o =>
                                MaterialDB.TryGet(o.MaterialId, out var spec) &&
                                spec.Hardness > 0.55f && spec.Organic < 0.3f);
                            if (hasOre)
                            {
                                miningBonus += 0.15f;
                                break;
                            }
                        }
                    }
                }
                miningBonus = Math.Min(miningBonus, 0.6f); // Кап бонуса
            }
            var weights = new Dictionary<string, float>
            {
                ["food"] = hunger,
                ["growth"] = hunger * 0.40f,
                ["shelter"] = loneliness,
                ["comfort"] = loneliness * 0.40f,
                ["storage"] = full,
                ["warmth"] = warmthDemand,
                ["defense"] = fear * (atWar ? 0.80f : 0.25f) * (1f - peace * 0.40f),
                ["faith"] = faithDemand,
                ["knowledge"] = knowledgeDemand,
                ["trade"] = tradeDemand,
                ["culture"] = cultureDemand,
                ["healing"] = healingDemand + 0.08f,
                ["mobility"] = 0.05f,
                ["mining"] = 0.10f + miningBonus,  // === НОВОЕ: базовый шанс + бонус за руду ===
            };

            float total = 0f;
            foreach (var kv in weights) total += Math.Max(0f, kv.Value) + 0.02f;
            float roll = (float)rng.NextDouble() * total;
            foreach (var kv in weights)
            {
                roll -= Math.Max(0f, kv.Value) + 0.02f;
                if (roll <= 0f) return kv.Key;
            }
            return "shelter";
        }
        private static int CalculateInfluenceRadius(BuildingType building, float quality)
        {
            // Базовый радиус зависит от типа здания
            int baseRadius = building switch
            {
                BuildingType.Farm => 3,           // Фермы влияют на окрестности
                BuildingType.House => 2,          // Дома — небольшой радиус
                BuildingType.Library => 4,        // Библиотеки — культурное влияние
                BuildingType.Temple => 5,         // Храмы — сильное влияние
                BuildingType.Barracks => 3,       // Казармы — военный контроль
                BuildingType.Market => 4,         // Рынки — экономическое влияние
                BuildingType.MineShaft => 2,      // Шахты — локальный контроль
                BuildingType.Bridge => 2,         // Мосты — контроль перехода
                BuildingType.Warehouse => 3,      // Склады — логистика
                BuildingType.Hospice => 3,        // Хосписы — медицинская зона
                _ => 2                            // По умолчанию
            };

            // Качество здания увеличивает радиус
            if (quality > 1.5f) baseRadius += 1;
            if (quality > 2.0f) baseRadius += 1;

            return Math.Min(baseRadius, 8);  // Максимум 8 тайлов
        }

        /// <summary>
        /// Здание создаёт зону контроля. Вызывается при постройке.
        /// </summary>
        public static void SpreadCivilizationControl(Tile center, string civId, int radius)
        {
            if (string.IsNullOrEmpty(civId)) return;

            var world = Simulation.Instance.World;
            int width = world.GetLength(0);
            int height = world.GetLength(1);
            int cx = center.X;
            int cy = center.Y;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;

                    int nx = cx + dx;
                    int ny = cy + dy;

                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                    var neighbor = world[nx, ny];
                    if (!neighbor.IsPassable) continue;

                    // НЕ перезаписываем активные территории других цивилизаций
                    if (!string.IsNullOrEmpty(neighbor.OwnerCivId) &&
                        neighbor.OwnerCivId != civId &&
                        !neighbor.IsContested)
                    {
                        continue;  // Активная чужая территория — не трогаем
                    }

                    neighbor.OwnerCivId = civId;
                    neighbor.IsContested = false;  // Снимаем флаг оспаривания
                }
            }
        }

        /// <summary>
        /// Проверяет и обновляет контроль территорий. Вызывается каждые 100 тиков.
        /// Если здание разрушилось — территория становится оспоримой.
        /// </summary>
        public static void UpdateTerritoryControl(Tile[,] world)
        {
            if (world == null) return;

            int width = world.GetLength(0);
            int height = world.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tile = world[x, y];

                    // Если тайл ничей — пропускаем
                    if (string.IsNullOrEmpty(tile.OwnerCivId)) continue;

                    // Проверяем, есть ли рядом активное здание этой цивилизации
                    bool hasActiveBuilding = false;
                    for (int dx = -5; dx <= 5; dx++)
                    {
                        for (int dy = -5; dy <= 5; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                            var neighbor = world[nx, ny];
                            if (neighbor.OwnerCivId == tile.OwnerCivId &&
                                neighbor.Building != BuildingType.None &&
                                neighbor.Durability > 0f)
                            {
                                hasActiveBuilding = true;
                                break;
                            }
                        }
                        if (hasActiveBuilding) break;
                    }

                    // Если активного здания нет — территория становится оспоримой
                    if (!hasActiveBuilding && !tile.IsContested)
                    {
                        tile.IsContested = true;
                    }
                    // Если здание появилось снова — снимаем оспаривание
                    else if (hasActiveBuilding && tile.IsContested)
                    {
                        tile.IsContested = false;
                    }
                }
            }
        }
        private static string Root(ResourceSpec spec)
        {
            if (string.IsNullOrEmpty(spec.Id)) return "mat";
            return spec.Id.Length > 4 ? spec.Id[..4] : spec.Id;
        }
    }
}
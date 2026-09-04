using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems.Observers;
using GenesisEngine.UI;
namespace GenesisEngine.Systems
{
    public static class TerritoryCaptureSystem
    {
        private const int BaseCaptureTicks = 25;
        private const int MaxCaptureTicks = 200;
        private const int MinDistanceToBorder = 1;
        private const int MaxDistanceToBorder = 20;

        public static void Update(List<Agent> agents, Tile[,] world)
        {
            if (agents == null || world == null) return;

            var civAgents = agents
                .Where(a => !string.IsNullOrEmpty(a.CivilizationId) && a.Body.Health > 0)
                .GroupBy(a => a.CivilizationId)
                .ToDictionary(g => g.Key, g => g.ToList());

            for (int x = 0; x < world.GetLength(0); x++)
            {
                for (int y = 0; y < world.GetLength(1); y++)
                {
                    var tile = world[x, y];
                    var agentsHere = SpatialGrid.CellAt(x, y);

                    if (agentsHere.Count == 0) continue;

                    foreach (var agent in agentsHere)
                    {
                        if (string.IsNullOrEmpty(agent.CivilizationId)) continue;
                        if (agent.Body.Health <= 0) continue;

                        // 1. Если тайл ничей — НЕ захватываем просто так (только здания создают территорию)
                        if (string.IsNullOrEmpty(tile.OwnerCivId))
                        {
                            continue;
                        }

                        // 2. Если тайл уже принадлежит агенту — он защищает его
                        if (tile.OwnerCivId == agent.CivilizationId)
                        {
                            tile.CaptureProgress.Clear();
                            continue;
                        }

                        // 3. Если тайл принадлежит другой цивилизации — проверяем, есть ли на нём здание
                        if (tile.OwnerCivId != agent.CivilizationId)
                        {
                            // МОЖНО захватывать ТОЛЬКО если на тайле НЕТ активного здания
                            if (tile.Building != BuildingType.None && tile.Durability > 0f)
                            {
                                // Здание защищает тайл — нельзя захватить
                                continue;
                            }

                            // Инициализируем счётчик захвата
                            if (!tile.CaptureProgress.ContainsKey(agent.CivilizationId))
                            {
                                tile.CaptureProgress[agent.CivilizationId] = 0;
                            }

                            tile.CaptureProgress[agent.CivilizationId]++;

                            int requiredTicks = tile.IsContested ?
                                BaseCaptureTicks / 2 :
                                CalculateCaptureTicks(tile, agent.CivilizationId, world, civAgents);

                            if (tile.CaptureProgress[agent.CivilizationId] >= requiredTicks)
                            {
                                string oldOwner = tile.OwnerCivId;
                                tile.OwnerCivId = agent.CivilizationId;
                                tile.CaptureProgress.Clear();

                                if (Simulation.Instance.TotalTicks % 100 == 0)
                                {
                                    FileLogger.Log(
                                        $"[TICK {Simulation.Instance.TotalTicks}] TERRITORY CAPTURE: " +
                                        $"tile ({tile.X},{tile.Y}) from {oldOwner} to {agent.CivilizationId}",
                                        FileLogger.LogLevel.War);
                                }

                                EventBus.Publish(new SimEvent
                                {
                                    Type = SimEventType.Combat,
                                    Tick = Simulation.Instance.TotalTicks,
                                    Actor = agent,
                                    Position = new Vector2(tile.X, tile.Y),
                                    Data = $"TerritoryCapture:{oldOwner}->{agent.CivilizationId}"
                                });
                            }
                        }
                    }
                }
            }
        }

        private static int CalculateCaptureTicks(Tile tile, string capturingCivId, Tile[,] world, Dictionary<string, List<Agent>> civAgents)
        {
            int minDistance = FindNearestFriendlyTile(tile, capturingCivId, world, civAgents);

            if (minDistance == -1) return BaseCaptureTicks;

            float distanceRatio = (float)(minDistance - MinDistanceToBorder) /
                                  Math.Max(1, MaxDistanceToBorder - MinDistanceToBorder);
            distanceRatio = Math.Clamp(distanceRatio, 0f, 1f);

            int captureTicks = (int)(BaseCaptureTicks + (MaxCaptureTicks - BaseCaptureTicks) * distanceRatio);
            return Math.Clamp(captureTicks, BaseCaptureTicks, MaxCaptureTicks);
        }

        private static int FindNearestFriendlyTile(Tile tile, string civId, Tile[,] world, Dictionary<string, List<Agent>> civAgents)
        {
            int minDistance = int.MaxValue;

            if (civAgents.TryGetValue(civId, out var agents))
            {
                foreach (var agent in agents)
                {
                    int distance = Math.Abs(tile.X - agent.Position.X) + Math.Abs(tile.Y - agent.Position.Y);
                    if (distance < minDistance) minDistance = distance;
                }
            }

            if (minDistance > 5)
            {
                for (int x = 0; x < world.GetLength(0); x++)
                {
                    for (int y = 0; y < world.GetLength(1); y++)
                    {
                        if (world[x, y].OwnerCivId == civId)
                        {
                            int distance = Math.Abs(tile.X - x) + Math.Abs(tile.Y - y);
                            if (distance < minDistance) minDistance = distance;
                        }
                    }
                }
            }

            return minDistance == int.MaxValue ? -1 : minDistance;
        }
    }
}

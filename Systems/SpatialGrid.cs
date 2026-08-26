using System;
using System.Collections.Generic;
using GenesisEngine.Core;
using GenesisEngine.Entities;

namespace GenesisEngine.Systems
{
    public static class SpatialGrid
    {
        private const int CellSize = 10;

        private static List<Agent>[,] _cells;
        private static int _cellsW;
        private static int _cellsH;

        private static int _lastUpdateTick = -1;

        public static void Initialize(int worldWidth, int worldHeight)
        {
            if (worldWidth <= 0)
                worldWidth = 1;

            if (worldHeight <= 0)
                worldHeight = 1;

            _cellsW = (worldWidth + CellSize - 1) / CellSize;
            _cellsH = (worldHeight + CellSize - 1) / CellSize;

            if (_cellsW < 1)
                _cellsW = 1;

            if (_cellsH < 1)
                _cellsH = 1;

            _cells = new List<Agent>[_cellsW, _cellsH];

            for (int x = 0; x < _cellsW; x++)
            {
                for (int y = 0; y < _cellsH; y++)
                {
                    _cells[x, y] = new List<Agent>();
                }
            }

            _lastUpdateTick = -1;
        }

        private static void EnsureInitialized()
        {
            if (_cells == null)
                Initialize(120, 80);
        }

        public static void Update(List<Agent> agents, int currentTick)
        {
            if (_lastUpdateTick == currentTick)
                return;

            ForceUpdate(agents);
            _lastUpdateTick = currentTick;
        }

        public static void ForceUpdate(List<Agent> agents)
        {
            if (agents == null)
                return;

            EnsureInitialized();

            for (int x = 0; x < _cellsW; x++)
            {
                for (int y = 0; y < _cellsH; y++)
                {
                    _cells[x, y].Clear();
                }
            }

            foreach (var agent in agents)
            {
                if (agent == null)
                    continue;

                int cx = agent.Position.X / CellSize;
                int cy = agent.Position.Y / CellSize;

                if (cx < 0 || cy < 0 || cx >= _cellsW || cy >= _cellsH)
                    continue;

                _cells[cx, cy].Add(agent);
            }

            _lastUpdateTick = Simulation.Instance?.TotalTicks ?? _lastUpdateTick;
        }

        public static List<Agent> GetNearby(Vector2 pos, int radius)
        {
            var result = new List<Agent>();

            if (radius < 0)
                return result;

            EnsureInitialized();

            int cx = pos.X / CellSize;
            int cy = pos.Y / CellSize;

            int cellRadius = radius / CellSize + 1;
            int radiusSq = radius * radius;

            int minX = Math.Max(0, cx - cellRadius);
            int minY = Math.Max(0, cy - cellRadius);
            int maxX = Math.Min(_cellsW - 1, cx + cellRadius);
            int maxY = Math.Min(_cellsH - 1, cy + cellRadius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var list = _cells[x, y];

                    foreach (var agent in list)
                    {
                        if (agent == null)
                            continue;

                        int dx = agent.Position.X - pos.X;
                        int dy = agent.Position.Y - pos.Y;

                        if (dx * dx + dy * dy <= radiusSq)
                            result.Add(agent);
                    }
                }
            }

            return result;
        }
    }
}
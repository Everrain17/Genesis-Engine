using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems;

namespace GenesisEngine.UI
{
    public class GraphicWindow
    {
        private int tileSize = 8;
        private int worldW, worldH, screenW, screenH;
        private Dictionary<string, Color> civColors = new();
        private Color[] palette = {
            Color.Lime, Color.Yellow, Color.Magenta, Color.DarkBlue,
            Color.DarkGreen, Color.Orange, Color.Purple, Color.Red,
            Color.Maroon, Color.White
        };
        private bool paused = false;
        private int speed = 1;
        private int tickCounter = 0;
        private Simulation sim;
        private RenderTexture2D renderTexture;
        private bool showCivWindow, showCivDetail, showStatsWindow;
        private CivilizationSnapshot selectedCiv;
        private float cameraZoom = 1.0f;
        private float cameraX = 0f, cameraY = 0f;
        private bool isDragging = false;
        private float dragStartMouseX, dragStartMouseY;
        private float dragStartCamX, dragStartCamY;
        private bool _lowDetail = false;

        public GraphicWindow(Simulation sim)
        {
            this.sim = sim;
            worldW = sim.World.GetLength(0);
            worldH = sim.World.GetLength(1);
            screenW = worldW * tileSize;
            screenH = worldH * tileSize;
        }

        public void Run()
        {
            Raylib.InitWindow(screenW, screenH, "Genesis Engine");
            Raylib.SetTargetFPS(60);
            Raylib.SetExitKey(KeyboardKey.Null);
            renderTexture = Raylib.LoadRenderTexture(screenW, screenH);

            while (!Raylib.WindowShouldClose())
            {
                HandleInput();
                if (!paused && !showCivWindow && !showCivDetail && !showStatsWindow)
                {
                    for (int i = 0; i < speed; i++)
                    {
                        sim.Tick();
                        tickCounter++;
                    }
                }
                Render();
            }

            Raylib.UnloadRenderTexture(renderTexture);
            Raylib.CloseWindow();
        }

        private void HandleInput()
        {
            if (showStatsWindow || showCivDetail)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.R) || Raylib.IsKeyPressed(KeyboardKey.V))
                {
                    showStatsWindow = false;
                    showCivDetail = false;
                }
                return;
            }

            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
                cameraZoom = Math.Clamp(cameraZoom + wheel * 0.1f, 0.5f, 4.0f);

            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                isDragging = true;
                dragStartMouseX = Raylib.GetMouseX();
                dragStartMouseY = Raylib.GetMouseY();
                dragStartCamX = cameraX;
                dragStartCamY = cameraY;
            }

            if (Raylib.IsMouseButtonReleased(MouseButton.Left))
                isDragging = false;

            if (isDragging)
            {
                cameraX = dragStartCamX - (Raylib.GetMouseX() - dragStartMouseX) / cameraZoom;
                cameraY = dragStartCamY - (Raylib.GetMouseY() - dragStartMouseY) / cameraZoom;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Space)) paused = !paused;
            if (Raylib.IsKeyPressed(KeyboardKey.One)) speed = 1;
            if (Raylib.IsKeyPressed(KeyboardKey.Two)) speed = 10;
            if (Raylib.IsKeyPressed(KeyboardKey.Three)) speed = 100;
            if (Raylib.IsKeyPressed(KeyboardKey.R))
            {
                paused = true;
                showStatsWindow = true;
            }
        }

        private void Render()
        {
            _lowDetail = cameraZoom < 0.85f || speed > 10;

            Raylib.BeginTextureMode(renderTexture);
            Raylib.ClearBackground(Color.Black);

            float viewW = screenW / cameraZoom;
            float viewH = screenH / cameraZoom;

            int minX = Math.Clamp((int)(cameraX / tileSize) - 1, 0, worldW - 1);
            int minY = Math.Clamp((int)(cameraY / tileSize) - 1, 0, worldH - 1);
            int maxX = Math.Clamp((int)((cameraX + viewW) / tileSize) + 2, 0, worldW);
            int maxY = Math.Clamp((int)((cameraY + viewH) / tileSize) + 2, 0, worldH);

            // СЛОЙ 1: Базовый ландшафт, ресурсы и здания
            for (int x = minX; x < maxX; x++)
            {
                for (int y = minY; y < maxY; y++)
                {
                    DrawTile(sim.World[x, y], x, y);
                }
            }

            // СЛОЙ 2: Подсветка территорий цивилизаций и границы (по OwnerCivId)
            if (!_lowDetail && Simulation.activeCivs != null)
            {
                for (int x = minX; x < maxX; x++)
                {
                    for (int y = minY; y < maxY; y++)
                    {
                        var tile = sim.World[x, y];
                        if (string.IsNullOrEmpty(tile.OwnerCivId)) continue;

                        Color civColor = GetCivColor(tile.OwnerCivId);

                        // Проверяем, является ли тайл граничным
                        bool hasBorder = false;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx, ny = y + dy;
                                if (nx >= 0 && nx < sim.World.GetLength(0) && ny >= 0 && ny < sim.World.GetLength(1))
                                {
                                    var neighbor = sim.World[nx, ny];
                                    if (neighbor.OwnerCivId != tile.OwnerCivId)
                                    {
                                        hasBorder = true;
                                        break;
                                    }
                                }
                            }
                            if (hasBorder) break;
                        }

                        // Рисуем полупрозрачную заливку (границы чуть ярче для чёткости)
                        Color fill = civColor;
                        fill.A = (byte)(hasBorder ? 70 : 35);
                        Raylib.DrawRectangle(x * tileSize, y * tileSize, tileSize, tileSize, fill);

                        // Рисуем чёткую чёрную границу только на стыке цивилизаций
                        if (hasBorder)
                        {
                            Raylib.DrawRectangleLines(x * tileSize, y * tileSize, tileSize, tileSize, Color.Black);
                        }
                    }
                }
            }

            // СЛОЙ 3: Тепловая карта плотности населения (тонкие рамки, чтобы не конфликтовать с цветом цивилизации)
            if (Simulation.activeCivs != null && cameraZoom > 1.5f)
            {
                var densityMap = new Dictionary<(int x, int y), int>();
                foreach (var civ in Simulation.activeCivs)
                {
                    foreach (var agent in civ.Members)
                    {
                        var key = (agent.Position.X, agent.Position.Y);
                        densityMap[key] = densityMap.GetValueOrDefault(key, 0) + 1;
                    }
                }

                foreach (var kvp in densityMap)
                {
                    var (x, y) = kvp.Key;
                    int count = kvp.Value;

                    if (count > 3)
                    {
                        // Используем яркие контуры вместо заливки, чтобы было видно на любом фоне
                        Color highlight = count > 10 ? Color.Red : count > 5 ? Color.Orange : Color.Yellow;
                        Raylib.DrawRectangleLines(x * tileSize, y * tileSize, tileSize, tileSize, highlight);
                    }
                }
            }

            // СЛОЙ 4: Агенты и существа (всегда поверх всего)
            foreach (var a in sim.Agents)
            {
                if (a.Position.X >= minX && a.Position.X < maxX && a.Position.Y >= minY && a.Position.Y < maxY)
                {
                    DrawAgent(a);
                }
            }

            foreach (var c in sim.Creatures)
            {
                if (c.Position.X >= minX && c.Position.X < maxX && c.Position.Y >= minY && c.Position.Y < maxY)
                {
                    DrawCreature(c);
                }
            }

            Raylib.EndTextureMode();

            // СЛОЙ 5: UI
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            Raylib.DrawTexturePro(
                renderTexture.Texture,
                new Rectangle(0, 0, screenW, screenH),
                new Rectangle(-cameraX * cameraZoom, -cameraY * cameraZoom, screenW * cameraZoom, screenH * cameraZoom),
                new System.Numerics.Vector2(0, 0),
                0,
                Color.White);

            Raylib.DrawText($"Tick: {sim.TotalTicks}  Agents: {sim.Agents.Count}  Speed: x{speed}  {(paused ? "PAUSED" : "RUNNING")}", 10, screenH - 30, 20, Color.White);
            Raylib.DrawText("SPACE pause | 1/2/3 speed | R stats", 10, screenH - 55, 14, Color.Gold);

            if (showStatsWindow) DrawStatsWindow();
            if (showCivDetail) DrawCivDetailWindow();

            Raylib.EndDrawing();
        }

        private void DrawTile(Tile tile, int x, int y)
        {
            // Базовый ландшафт
            Color c = tile.Terrain switch
            {
                TerrainType.Taiga => new Color(45, 110, 95, 255),
                TerrainType.DeepWater => new Color(20, 40, 120, 255),
                TerrainType.ShallowWater => new Color(40, 100, 180, 255),
                TerrainType.Beach => new Color(210, 180, 140, 255),
                TerrainType.Grassland => new Color(100, 160, 60, 255),
                TerrainType.Forest => new Color(30, 90, 30, 255),
                TerrainType.Hill => new Color(140, 120, 80, 255),
                TerrainType.Mountain => new Color(120, 110, 100, 255),
                TerrainType.Swamp => new Color(60, 80, 40, 255),
                TerrainType.Desert => new Color(220, 200, 120, 255),
                TerrainType.Tundra => new Color(200, 210, 220, 255),
                TerrainType.IcePeak => new Color(230, 240, 255, 255),
                _ => Color.Gray
            };
            Raylib.DrawRectangle(x * tileSize, y * tileSize, tileSize, tileSize, c);

            if (tile.HasRiver)
                Raylib.DrawLine(x * tileSize, y * tileSize + tileSize / 2, (x + 1) * tileSize, y * tileSize + tileSize / 2, Color.Blue);

            if (!_lowDetail && tile.SanctityLevel > 10)
                Raylib.DrawCircle(x * tileSize + tileSize / 2, y * tileSize + tileSize / 2, tileSize / 2, new Color(255, 215, 0, 100));

            // Ресурсы на тайле
            if (!_lowDetail)
            {
                float cx = x * tileSize + tileSize / 2f;
                float cy = y * tileSize + tileSize / 2f;
                bool hasOrganic = false, hasHard = false, hasConductive = false, hasLogic = false;

                foreach (var obj in tile.GroundObjects)
                {
                    if (!MaterialDB.TryGet(obj.MaterialId, out var spec)) continue;
                    if (spec.Organic > 0.5f) hasOrganic = true;
                    if (spec.Hardness > 0.6f) hasHard = true;
                    if (spec.Conductivity > 0.6f) hasConductive = true;
                    if (spec.Logic > 0.5f) hasLogic = true;
                }

                if (hasOrganic) Raylib.DrawCircle((int)cx, (int)cy - 2, 2, new Color(80, 220, 80, 220));
                if (hasHard) Raylib.DrawCircle((int)cx + 2, (int)cy + 2, 2, new Color(150, 150, 150, 220));
                if (hasConductive) Raylib.DrawCircle((int)cx - 2, (int)cy + 2, 2, new Color(220, 160, 60, 220));
                if (hasLogic) Raylib.DrawCircle((int)cx, (int)cy, 2, new Color(180, 80, 255, 220));
            }

            // Индикаторы развития и дорог
            if (tile.DevelopmentLevel > 10f && tile.DevelopmentLevel <= 50f)
            {
                Raylib.DrawRectangle(x * tileSize + 1, y * tileSize + 1, tileSize - 2, tileSize - 2, new Color(150, 150, 150, 200));
                Raylib.DrawRectangleLines(x * tileSize, y * tileSize, tileSize, tileSize, Color.White);
            }
            else if (tile.DevelopmentLevel > 50f)
            {
                Raylib.DrawRectangle(x * tileSize, y * tileSize, tileSize, tileSize, new Color(255, 215, 0, 200));
                Raylib.DrawRectangleLines(x * tileSize, y * tileSize, tileSize, tileSize, Color.Red);
            }

            if (tile.RoadLevel > 0.25f && tile.Building == BuildingType.None)
                Raylib.DrawRectangle(x * tileSize + 2, y * tileSize + 2, tileSize - 4, tileSize - 4, new Color(200, 190, 140, (int)(tile.RoadLevel * 120)));

            // Здания
            if (tile.Building != BuildingType.None)
            {
                Color bc = EffectTables.AxisSymbol(tile.DominantAxis ?? "shelter") switch
                {
                    'F' => Color.Yellow,
                    'G' => Color.Green,
                    'H' => Color.Brown,
                    'S' => Color.DarkBrown,
                    'W' => Color.Red,
                    'M' => Color.Orange,
                    'R' => Color.Pink,
                    'T' => Color.Maroon,
                    'K' => Color.Gray,
                    '$' => Color.Gold,
                    '?' => Color.Blue,
                    'X' => Color.Purple,
                    'A' => Color.Violet,
                    'Z' => Color.Beige,
                    '+' => Color.White,
                    '#' => Color.LightGray,
                    _ => Color.White
                };

                Raylib.DrawRectangle(x * tileSize, y * tileSize, tileSize, tileSize, bc);
                char sym = EffectTables.AxisSymbol(tile.DominantAxis ?? "shelter");
                string symStr = sym.ToString();
                int fs = Math.Max(8, tileSize - 2);
                int tw = Raylib.MeasureText(symStr, fs);
                int tx = x * tileSize + (tileSize - tw) / 2;
                int ty = y * tileSize + (tileSize - fs) / 2;
                Raylib.DrawText(symStr, tx, ty, fs, TextColorFor(bc));

                if (tile.BuildingQuality > 1.15f)
                    Raylib.DrawRectangleLines(x * tileSize, y * tileSize, tileSize, tileSize, Color.White);
                if (tile.BuildingQuality > 1.4f)
                    Raylib.DrawRectangleLines(x * tileSize + 1, y * tileSize + 1, tileSize - 2, tileSize - 2, Color.Gold);
            }
        }

        private static Color TextColorFor(Color bg)
        {
            float lum = 0.299f * bg.R + 0.587f * bg.G + 0.114f * bg.B;
            return lum > 140 ? Color.Black : Color.White;
        }

        private void DrawAgent(Agent a)
        {
            int cx = a.Position.X * tileSize + tileSize / 2 + (a.Id.GetHashCode() % 6) - 3;
            int cy = a.Position.Y * tileSize + tileSize / 2 + (a.Id.GetHashCode() / 6 % 6) - 3;

            Color color = !string.IsNullOrEmpty(a.CivilizationId) ? GetCivColor(a.CivilizationId) : Color.SkyBlue;
            int radius = Math.Max(2, tileSize / 2);

            Raylib.DrawCircle(cx, cy, radius, color);
            Raylib.DrawCircleLines(cx, cy, radius, Color.Black);
        }

        private void DrawCreature(Creature cr)
        {
            int cx = cr.Position.X * tileSize + tileSize / 2;
            int cy = cr.Position.Y * tileSize + tileSize / 2;
            Color color = cr.Behavior == CreatureBehavior.Predator ? new Color(255, 200, 200, 255) : Color.White;
            int radius = Math.Max(2, (int)(cr.Size * 1.2f));

            Raylib.DrawCircle(cx, cy, radius, color);
            Raylib.DrawCircleLines(cx, cy, radius, Color.Black);
        }

        private Color GetCivColor(string id)
        {
            if (!civColors.ContainsKey(id))
                civColors[id] = palette[civColors.Count % palette.Length];
            return civColors[id];
        }

        private void DrawStatsWindow()
        {
            int w = 520, h = 460, x = screenW / 2 - w / 2, y = screenH / 2 - h / 2;
            Raylib.DrawRectangle(x, y, w, h, new Color(20, 20, 40, 240));
            Raylib.DrawRectangleLines(x, y, w, h, Color.Yellow);
            Raylib.DrawText("EMERGENCE ANALYTICS", x + 150, y + 10, 20, Color.Yellow);
            Raylib.DrawLine(x + 10, y + 35, x + w - 10, y + 35, Color.Gray);

            int yy = y + 50;
            Raylib.DrawText($"Tick: {sim.TotalTicks}  Agents: {sim.Agents.Count}", x + 20, yy, 14, Color.White); yy += 25;
            Raylib.DrawText("─── EMERGENT CLUSTERS ───", x + 20, yy, 14, Color.Yellow); yy += 20;

            var civs = Simulation.activeCivs?.OrderByDescending(c => c.Members.Count).Take(6).ToList() ?? new List<CivilizationSnapshot>();
            foreach (var c in civs)
            {
                string roles = "";
                if (c.RoleCounts.ContainsKey(AgentRole.Farmer)) roles += $"F:{c.RoleCounts[AgentRole.Farmer]} ";
                if (c.RoleCounts.ContainsKey(AgentRole.Builder)) roles += $"B:{c.RoleCounts[AgentRole.Builder]} ";

                Raylib.DrawText($"[{c.Name}] Pop: {c.Members.Count} | Struct: {c.EmergentStructuresCount}", x + 20, yy, 13, Color.White); yy += 16;
                Raylib.DrawText($"  Roles: {roles} | Hardness: {c.AvgToolHardness:F2}", x + 20, yy, 12, Color.LightGray); yy += 18;
            }
            Raylib.DrawText("R - close", x + 20, y + h - 30, 14, Color.Gray);
        }

        private void DrawCivDetailWindow()
        {
            if (selectedCiv == null) return;
            int w = 500, h = 440, x = screenW / 2 - w / 2, y = screenH / 2 - h / 2;
            Raylib.DrawRectangle(x, y, w, h, new Color(20, 20, 40, 240));
            Raylib.DrawRectangleLines(x, y, w, h, Color.Yellow);
            Raylib.DrawText(selectedCiv.Name, x + 20, y + 10, 20, Color.Yellow);
            Raylib.DrawLine(x + 10, y + 35, x + w - 10, y + 35, Color.Gray);

            int yy = y + 50;
            string emergentEra = selectedCiv.AvgToolHardness > 0.6f ? "Metal Age" : selectedCiv.AvgToolHardness > 0.3f ? "Neolithic" : "Paleolithic";

            Raylib.DrawText($"Era: {emergentEra} (Hardness: {selectedCiv.AvgToolHardness:F2})", x + 20, yy, 14, Color.White); yy += 20;

            string roles = "";
            foreach (var kvp in selectedCiv.RoleCounts) roles += $"{kvp.Key}: {kvp.Value} ";
            Raylib.DrawText($"Roles: {roles}", x + 20, yy, 14, Color.White); yy += 20;

            Raylib.DrawText($"Pop: {selectedCiv.Population}  Structures: {selectedCiv.EmergentStructuresCount}", x + 20, yy, 14, Color.White); yy += 20;
            Raylib.DrawText($"Development: {selectedCiv.TotalDevelopment:F1}", x + 20, yy, 14, Color.White); yy += 25;

            Raylib.DrawText($"Discoveries ({selectedCiv.Discoveries.Count}):", x + 20, yy, 14, Color.Yellow);
            yy += 18;

            if (selectedCiv.Discoveries.Count > 0)
            {
                foreach (var d in selectedCiv.Discoveries.OrderByDescending(dd => dd.Tick).Take(10))
                {
                    Color cc = d.Branch == "method" ? Color.SkyBlue : d.Branch == "item" ? Color.Green : Color.Orange;
                    Raylib.DrawText($"  - {d.Branch.ToUpper()}: {d.Name} [{d.Capability}] q{d.Quality:F2}", x + 25, yy, 12, cc);
                    yy += 14;
                    if (yy > y + h - 60) break;
                }
            }
            else { Raylib.DrawText("  None", x + 25, yy, 12, Color.Gray); yy += 14; }

            Raylib.DrawText("V - back", x + 20, y + h - 30, 14, Color.Gray);
        }
    }
}
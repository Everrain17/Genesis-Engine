using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public static class GraphemeSystem
    {
        public class Grapheme
        {
            public string Id; // Например, "GR_001"
            public string CivId;
            public string PhonemeId; // Ссылка на фонему, которую этот символ обозначает
            public string SymbolShape; // Визуальное описание, например, "circle+line"
            public int Occurrences;
            public float Stability;
            public int LastUsedTick;
        }

        private static readonly SortedDictionary<string, List<Grapheme>> CivGraphemes = new();
        private static int _graphemeCounter = 0;

        public static bool TryCreateGrapheme(Agent agent, Phoneme phoneme, Tile tile, Random rng)
        {
            if (agent == null || phoneme == null || tile == null || rng == null)
                return false;

            // Условие 1: Нужен институт знания (библиотека, храм и т.д.)
            if (tile.InstitutionAxis != "knowledge" || tile.InstitutionLevel < 1.5f)
                return false;

            // Условие 2: Фонема должна быть достаточно устойчивой (минимум 5 использований)
            if (phoneme.Occurrences < 5)
                return false;

            // Условие 3: Агент должен иметь достаточную логику и самосознание
            if (agent.Logic < 0.45f || agent.Genome.SelfAwareness < 0.4f)
                return false;

            // Шанс создания графемы (растет с логикой и уровнем института)
            float chance = 0.02f + agent.Logic * 0.05f + tile.InstitutionLevel * 0.01f;

            if (rng.NextDouble() > chance)
                return false;

            string civId = agent.CivilizationId ?? "wild";

            if (!CivGraphemes.TryGetValue(civId, out var graphemes))
            {
                graphemes = new List<Grapheme>();
                CivGraphemes[civId] = graphemes;
            }

            // Проверяем, не создана ли уже графема для этой фонемы
            var existing = graphemes.FirstOrDefault(g => g.PhonemeId == phoneme.Id);

            if (existing != null)
            {
                existing.Occurrences++;
                existing.Stability = Math.Min(1f, existing.Stability + 0.1f);
                existing.LastUsedTick = Simulation.Instance.TotalTicks;
            }
            else
            {
                var newGrapheme = new Grapheme
                {
                    Id = $"GR_{_graphemeCounter++:D3}",
                    CivId = civId,
                    PhonemeId = phoneme.Id,
                    SymbolShape = GenerateSymbolShape(rng),
                    Occurrences = 1,
                    Stability = 0.2f,
                    LastUsedTick = Simulation.Instance.TotalTicks
                };

                graphemes.Add(newGrapheme);

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] GRAPHEME: civ {civId} created visual symbol '{newGrapheme.SymbolShape}' " +
                    $"for phoneme '{phoneme.Id}' pattern=[{string.Join(">", phoneme.Pattern)}]",
                    FileLogger.LogLevel.Info);
            }

            return true;
        }

        private static string GenerateSymbolShape(Random rng)
        {
            // Простая генерация визуального описания символа из базовых элементов
            string[] shapes = { "line", "circle", "dot", "cross", "arc", "wave" };
            int count = rng.Next(1, 3); // 1 или 2 элемента для простоты
            var selected = new List<string>();
            for (int i = 0; i < count; i++)
            {
                selected.Add(shapes[rng.Next(shapes.Length)]);
            }
            return string.Join("+", selected);
        }

        public static List<Grapheme> GetGraphemes(string civId)
        {
            if (string.IsNullOrEmpty(civId))
                return new List<Grapheme>();

            return CivGraphemes.GetValueOrDefault(civId, new List<Grapheme>())
                .Where(g => g.Occurrences >= 2)
                .ToList();
        }

        public static int GraphemeCount(string civId)
        {
            return GetGraphemes(civId).Count;
        }

        public static void CleanupOldGraphemes(int maxAge = 10000)
        {
            int currentTick = Simulation.Instance.TotalTicks;

            foreach (var graphemes in CivGraphemes.Values)
            {
                graphemes.RemoveAll(g =>
                {
                    // 1. Если графема устоялась (использована 10+ раз), мы её НЕ трогаем.
                    if (g.Occurrences >= 10)
                        return false;

                    // 2. Если она использовалась недавно, не трогаем.
                    if (currentTick - g.LastUsedTick < maxAge)
                        return false;

                    // 3. Удаляем ТОЛЬКО старые и редко используемые графемы.
                    return true;
                });
            }
        }
    }
}
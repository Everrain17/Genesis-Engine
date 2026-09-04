using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems.Analytics;

namespace GenesisEngine.Systems
{
    /// <summary>
    /// ФАЗА 1: "Религиозный паспорт" цивилизации.
    /// Чистая аналитика, НЕ влияет на поведение агентов.
    /// Религия эмерджентна: складывается из храмов (доминирующие оси),
    /// сакральности мест и средней духовности популяции.
    /// Имя религии генерируется из собственных фонем цивилизации.
    /// </summary>
    public class ReligionProfile
    {
        public string Cult = "animism";
        public string Name = "";
        public float Piety;
        public int Temples;
        public bool Formed;
        public readonly Dictionary<string, float> Axes = new();
    }

    public static class ReligionSystem
    {
        private static readonly Dictionary<string, ReligionProfile> Profiles = new();
        private static readonly Dictionary<string, string> PrevCult = new();

        public static ReligionProfile GetProfile(string civId)
        {
            if (!Profiles.TryGetValue(civId, out var p))
            {
                p = new ReligionProfile();
                Profiles[civId] = p;
            }
            return p;
        }

        public static void UpdateAll(Simulation sim, Tile[,] world, int tick)
        {
            var civs = Simulation.activeCivs;
            if (civs == null || world == null) return;

            var axisByCiv = new Dictionary<string, Dictionary<string, float>>();
            var templeByCiv = new Dictionary<string, int>();

            int w = world.GetLength(0), h = world.GetLength(1);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var t = world[x, y];
                    if (t.Building == BuildingType.None) continue;
                    if (string.IsNullOrEmpty(t.OwnerCivId)) continue;

                    // === ИСПРАВЛЕНИЕ: ось выводим из DominantAxis, а если пусто — из типа здания ===
                    // (храмы не хранят DominantAxis, у них "faith" выводится из BuildingType — как в Tile.cs)
                    string axis = t.DominantAxis;
                    if (string.IsNullOrEmpty(axis))
                    {
                        axis = t.Building switch
                        {
                            BuildingType.Temple => "faith",
                            BuildingType.Hospice => "healing",
                            BuildingType.Library => "knowledge",
                            BuildingType.Farm => "food",
                            BuildingType.House => "warmth",
                            BuildingType.Market => "trade",
                            BuildingType.Barracks => "war_melee",
                            BuildingType.MineShaft => "mining",
                            BuildingType.Warehouse => "storage",
                            BuildingType.Bridge => "mobility",
                            _ => null
                        };
                    }
                    if (string.IsNullOrEmpty(axis)) continue;

                    if (!axisByCiv.TryGetValue(t.OwnerCivId, out var axes))
                    {
                        axes = new Dictionary<string, float>();
                        axisByCiv[t.OwnerCivId] = axes;
                    }

                    float weight = (t.Building == BuildingType.Temple ? 2.5f : 1f) + t.BuildingQuality * 0.5f;
                    axes[axis] = axes.GetValueOrDefault(axis) + weight;

                    if (t.Building == BuildingType.Temple)
                        templeByCiv[t.OwnerCivId] = templeByCiv.GetValueOrDefault(t.OwnerCivId) + 1;
                }
            }

            foreach (var c in civs)
            {
                if (c == null || c.Members == null || c.Members.Count == 0) continue;

                var p = GetProfile(c.Id);

                // --- Благочестие ---
                float avgSpirit = c.Members.Average(a => a.Genome.Spirituality);

                float sanctSum = 0f;
                foreach (var a in c.Members)
                    sanctSum += world[a.Position.X, a.Position.Y].SanctityLevel;
                float avgSanct = sanctSum / c.Members.Count;

                int temples = templeByCiv.GetValueOrDefault(c.Id);
                p.Temples = temples;

                p.Piety = Math.Clamp(
                    avgSpirit * 0.5f +
                    Math.Min(1f, temples / 5f) * 0.3f +
                    Math.Min(1f, avgSanct / 10f) * 0.2f,
                    0f, 1f);

                // --- Доминирующий культ ---
                string cult = "animism";
                if (axisByCiv.TryGetValue(c.Id, out var axes) && axes.Count > 0)
                {
                    var top = axes.OrderByDescending(kv => kv.Value).First();
                    if (top.Value > 0f) cult = top.Key;
                }

                // === ИСПРАВЛЕНИЕ: религия оформляется не только через храм, но и через сакральность ===
                bool canForm = temples > 0 || avgSanct >= 5f;

                if (canForm && !p.Formed)
                {
                    p.Formed = true;
                    if (string.IsNullOrEmpty(p.Name)) p.Name = GenerateNameFromPhonemes(c.Id);
                    ExtendedMetricsLogger.LogEvent(tick, "ReligionFormed", c.Id,
                        $"{p.Name}: {CultLabel(cult)}");
                }

                if (p.Formed && PrevCult.TryGetValue(c.Id, out var old) && old != cult)
                {
                    ExtendedMetricsLogger.LogEvent(tick, "ReligionReform", c.Id,
                        $"{CultLabel(old)} -> {CultLabel(cult)}");
                }
                PrevCult[c.Id] = cult;
                p.Cult = cult;

                p.Axes.Clear();
                if (axisByCiv.TryGetValue(c.Id, out var ax2))
                    foreach (var kv in ax2) p.Axes[kv.Key] = kv.Value;
            }
        }

        // === ЧЕЛОВЕКОЧИТАЕМЫЕ ЯРЛЫКИ КУЛЬТОВ ===
        public static string CultLabel(string axis)
        {
            switch (axis)
            {
                case "animism": return "Анимизм (культ природы)";
                case "faith": return "Культ Веры";
                case "healing": return "Культ Исцеления";
                case "food": return "Культ Урожая";
                case "growth": return "Культ Плодородия";
                case "knowledge": return "Культ Мудрости";
                case "trade": return "Культ Торговли";
                case "mining": return "Культ Земли";
                case "shelter":
                case "comfort":
                case "warmth": return "Культ Очага и Тепла";
                case "defense":
                case "war_melee":
                case "war_siege":
                case "war_ranged": return "Культ Войны";
                case "mobility": return "Культ Странствий";
                case "storage": return "Культ Изобилия";
                case "culture": return "Культ Искусств";
                default: return "Культ '" + axis + "'";
            }
        }

        // === ГЕНЕРАЦИЯ ИМЕНИ РЕЛИГИИ ИЗ ФОНЕМ ЦИВИЛИЗАЦИИ ===
        // Имя веры рождается из языка культуры — чистая эмерджентность.
        private static string GenerateNameFromPhonemes(string civId)
        {
            // 1. Берём фонемы цивилизации
            var phonemes = PhonemeSystem.GetPhonemes(civId);
            if (phonemes != null && phonemes.Count >= 2)
            {
                var rnd = new Random(civId.GetHashCode() ^ 0xC0FFEE);
                // Детерминированная выборка фонем по весу (чаще используемые = чаще в имени)
                var pool = phonemes
                    .OrderByDescending(ph => ph.Occurrences)
                    .Take(Math.Min(8, phonemes.Count))
                    .ToList();

                int count = rnd.Next(2, 4); // 2-3 слога
                string name = "";
                for (int i = 0; i < count; i++)
                {
                    var ph = pool[rnd.Next(pool.Count)];
                    // Используем Id фонемы как звук (короткое сочетание)
                    name += ExtractSound(ph.Id, rnd);
                }

                if (name.Length >= 2)
                    return Capitalize(name);
            }

            // 2. Fallback: детерминированное имя из слогов
            return GenerateFallbackName(civId);
        }

        // Превращаем Id фонемы в читаемый "звук"
        private static string ExtractSound(string phonemeId, Random rnd)
        {
            if (string.IsNullOrEmpty(phonemeId)) return "а";

            // PhonemeSystem обычно даёт Id вида "ph_001" или хеш — берём последние символы
            string clean = phonemeId.Replace("ph_", "").Replace("-", "").ToLowerInvariant();

            // Сопоставляем с приятными звуками на основе хеша
            string[] sounds = { "на", "ра", "ут", "ше", "ар", "ин", "та", "эль", "ор",
                                 "ум", "аш", "ир", "ан", "ил", "ат", "ус", "ен", "ок",
                                 "уль", "им", "ас", "он", "ек", "ур", "ос" };

            int hash = Math.Abs(clean.GetHashCode());
            return sounds[hash % sounds.Length];
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1).ToLower();
        }

        private static string GenerateFallbackName(string civId)
        {
            var rnd = new Random(civId.GetHashCode());
            string[] syl = { "на", "ра", "ут", "ше", "ар", "ин", "та", "эль",
                             "ор", "ум", "аш", "ир", "ан", "ил" };
            string s = syl[rnd.Next(syl.Length)] + syl[rnd.Next(syl.Length)];
            if (rnd.NextDouble() < 0.5f) s += syl[rnd.Next(syl.Length)];
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
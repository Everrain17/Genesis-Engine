using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems;

namespace GenesisEngine.Systems.Analytics
{
    /// <summary>
    /// Полный архив цивилизаций: индивидуальное досье на каждую циву.
    /// Timeline ~40 колонок + Summary с пиками, средними и финальным срезом.
    /// </summary>
    public static class CivArchive
    {
        // === ЗАГОЛОВКИ TIMELINE (порядок = порядку в row!) ===
        private static readonly string[] TimelineHeader = {
            "Tick","Population","Males","Females","Children","Youth","Adults","MiddleAge","Elderly",
            "Farmers","Builders","Traders","Soldiers","Scholars","Artisans",
            "Farms","Houses","Libraries","Temples","Markets","Barracks","Mines","Bridges","Warehouses","Hospices",
            "Lexicon","Grammar","Phonemes","Graphemes",
            "AvgToolHardness","TotalDevelopment","Structures","TotalScore",
            "Infected","Gini","AvgDespair","AvgLoneliness","EducatedRate","AvgSanctity",
            "Religion","Cult","Piety"
        };

        private class CivRecord
        {
            public string Id; public string Name = "";
            public int BirthTick; public int LastSeenTick; public bool Alive = true;

            public int PeakPop; public int PeakPopTick;
            public float PeakHardness; public string PeakEra = "Paleolithic";
            public float PeakDev; public int PeakStructures;
            public int PeakLexicon; public int PeakGrammar;
            public float PeakGini; public int PeakTemples;

            public double SumGini; public double SumDespair; public int StatTicks;

            public int FinalDiscoveries;
            public string FinalReligion = "-"; public string FinalCult = "-";
            public float FinalPiety; public int FinalTemples;
            public int FinalFamilies; public int LargestFamily;

            public object[] FinalRow;
            public readonly List<object[]> Rows = new();
        }

        private static readonly Dictionary<string, CivRecord> _records = new();
        public static void Reset() => _records.Clear();

        /// <summary>Слепок раз в 1000 тиков. Вызывается из ExtendedMetricsLogger.</summary>
        public static void Snapshot(int tick, List<CivilizationSnapshot> civs, Tile[,] world)
        {
            if (civs == null) return;
            var seen = new HashSet<string>();

            // --- Один проход по миру: здания по цивилизациям ---
            var buildingsByCiv = new Dictionary<string, Dictionary<BuildingType, int>>();
            if (world != null)
            {
                int w = world.GetLength(0), h = world.GetLength(1);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        var t = world[x, y];
                        if (t.Building == BuildingType.None || string.IsNullOrEmpty(t.OwnerCivId)) continue;
                        if (!buildingsByCiv.TryGetValue(t.OwnerCivId, out var d))
                        { d = new Dictionary<BuildingType, int>(); buildingsByCiv[t.OwnerCivId] = d; }
                        d[t.Building] = d.GetValueOrDefault(t.Building) + 1;
                    }
            }

            foreach (var c in civs)
            {
                if (c?.Members == null || c.Members.Count == 0) continue;
                seen.Add(c.Id);

                if (!_records.TryGetValue(c.Id, out var r))
                { r = new CivRecord { Id = c.Id, BirthTick = tick }; _records[c.Id] = r; }
                r.Name = c.Name ?? ("Cluster " + c.Id);
                r.LastSeenTick = tick; r.Alive = true;

                var m = c.Members;
                int n = m.Count;

                // --- Демография: пол + возрастная пирамида ---
                int males = 0, ch = 0, yo = 0, ad = 0, mid = 0, el = 0;
                foreach (var a in m)
                {
                    if (a.BiologicalSex == Sex.Male) males++;
                    float na = a.Age / a.MaxAge;
                    if (na < 0.25f) ch++;
                    else if (na < 0.45f) yo++;
                    else if (na < 0.70f) ad++; else if (na < 0.90f) mid++; else el++;
                }

                // --- Профессии ---
                int farmers = 0, builders = 0, traders = 0, soldiers = 0, scholars = 0, artisans = 0;
                foreach (var a in m)
                {
                    // Если поле называется иначе (Role/Job) — поправь switch
                    switch (a.Role.ToString())
                    {
                        case "Farmer": farmers++; break;
                        case "Builder": builders++; break;
                        case "Trader": traders++; break;
                        case "Soldier": soldiers++; break;
                        case "Scholar": scholars++; break;
                        case "Artisan": artisans++; break;
                    }
                }

                // --- Здания цивилизации ---
                buildingsByCiv.TryGetValue(c.Id, out var bd);
                int B(BuildingType bt) => bd != null ? bd.GetValueOrDefault(bt) : 0;
                int farms = B(BuildingType.Farm), houses = B(BuildingType.House),
                    libs = B(BuildingType.Library), temples = B(BuildingType.Temple),
                    markets = B(BuildingType.Market), barracks = B(BuildingType.Barracks),
                    mines = B(BuildingType.MineShaft), bridges = B(BuildingType.Bridge),
                    whs = B(BuildingType.Warehouse), hosps = B(BuildingType.Hospice);

                // --- Социальное расслоение и психология ---
                float gini = RevoltSystem.CalculateGiniCoefficient(m);
                float avgDespair = m.Average(a => a.Despair);
                float avgLonely = m.Average(a => a.Loneliness);
                float avgSpirit = m.Average(a => a.Genome.Spirituality);
                float educated = (float)m.Count(a => KnowledgeSystem.AgentKnowsAnything(a)) / n;
                int infected = m.Count(a => a.Infected);

                float sanctSum = 0f;
                if (world != null)
                    foreach (var a in m) sanctSum += world[a.Position.X, a.Position.Y].SanctityLevel;
                float avgSanct = sanctSum / n;

                // --- Династии ---
                var fams = m.Where(a => !string.IsNullOrEmpty(a.FamilyId))
                            .GroupBy(a => a.FamilyId).ToList();
                int families = fams.Count;
                int largestFam = fams.Count > 0 ? fams.Max(g => g.Count()) : 0;

                // --- Культура ---
                int lex = LanguageSystem.StableWordCount(c.Id);
                int gram = GrammarSystem.RuleCount(c.Id);
                int phon = PhonemeSystem.PhonemeCount(c.Id);
                int graph = GraphemeSystem.GraphemeCount(c.Id);

                var rel = ReligionSystem.GetProfile(c.Id);
                string relName = string.IsNullOrEmpty(rel.Name) ? "-" : rel.Name;
                string cult = ReligionSystem.CultLabel(rel.Cult);

                // --- Пики и средние за жизнь ---
                if (n > r.PeakPop) { r.PeakPop = n; r.PeakPopTick = tick; }
                if (c.AvgToolHardness > r.PeakHardness) { r.PeakHardness = c.AvgToolHardness; r.PeakEra = EraOf(c.AvgToolHardness); }
                if (c.TotalDevelopment > r.PeakDev) r.PeakDev = c.TotalDevelopment;
                if (c.EmergentStructuresCount > r.PeakStructures) r.PeakStructures = c.EmergentStructuresCount;
                if (lex > r.PeakLexicon) r.PeakLexicon = lex;
                if (gram > r.PeakGrammar) r.PeakGrammar = gram;
                if (gini > r.PeakGini) r.PeakGini = gini;
                if (temples > r.PeakTemples) r.PeakTemples = temples;
                r.SumGini += gini; r.SumDespair += avgDespair; r.StatTicks++;

                r.FinalReligion = relName; r.FinalCult = cult;
                r.FinalPiety = rel.Piety; r.FinalTemples = temples;
                r.FinalFamilies = families; r.LargestFamily = largestFam;
                r.FinalDiscoveries = c.Discoveries.Count;

                // --- Строка таймлайна (порядок = TimelineHeader!) ---
                var row = new object[] {
                    tick, n, males, n - males, ch, yo, ad, mid, el,
                    farmers, builders, traders, soldiers, scholars, artisans,
                    farms, houses, libs, temples, markets, barracks, mines, bridges, whs, hosps,
                    lex, gram, phon, graph,
                    Round3(c.AvgToolHardness), Round2(c.TotalDevelopment), c.EmergentStructuresCount, (int)c.TotalScore,
                    infected, Round3(gini), Round1(avgDespair), Round1(avgLonely), Round3(educated), Round2(avgSanct),
                    relName, cult, Round3(rel.Piety)
                };
                r.FinalRow = row;
                r.Rows.Add(row);
            }

            foreach (var r in _records.Values)
                if (r.Alive && !seen.Contains(r.Id)) r.Alive = false;
        }

        /// <summary>В конце запуска: индекс + досье на каждую циву.</summary>
        public static void ExportAll(string runDir, int finalTick)
        {
            try
            {   
                string dir = Path.Combine(runDir, "civs");
                Directory.CreateDirectory(dir);

                // ---------- ИНДЕКС ВСЕХ НИТЕЙ ----------
                using (var idx = new XLWorkbook())
                {
                    var ws = idx.Worksheets.Add("Civilizations");
                    string[] head = { "CivId","Name","BirthTick","LastSeenTick","Status",
                        "PeakPopulation","PeakPopTick","PeakEra","PeakToolHardness","PeakDevelopment",
                        "PeakStructures","PeakLexicon","PeakGini","PeakTemples",
                        "Families","LargestFamily","Religion","Cult","Piety" };
                    for (int i = 0; i < head.Length; i++) ws.Cell(1, i + 1).Value = head[i];

                    int row = 2;
                    foreach (var r in _records.Values.OrderBy(x => x.BirthTick))
                    {
                        ws.Cell(row, 1).Value = r.Id;
                        ws.Cell(row, 2).Value = r.Name;
                        ws.Cell(row, 3).Value = r.BirthTick;
                        ws.Cell(row, 4).Value = r.LastSeenTick;
                        ws.Cell(row, 5).Value = r.Alive ? $"alive at end ({finalTick})" : $"EXTINCT (~{r.LastSeenTick})";
                        ws.Cell(row, 6).Value = r.PeakPop;
                        ws.Cell(row, 7).Value = r.PeakPopTick;
                        ws.Cell(row, 8).Value = r.PeakEra;
                        ws.Cell(row, 9).Value = Round3(r.PeakHardness);
                        ws.Cell(row, 10).Value = Round2(r.PeakDev);
                        ws.Cell(row, 11).Value = r.PeakStructures;
                        ws.Cell(row, 12).Value = r.PeakLexicon;
                        ws.Cell(row, 13).Value = Round3(r.PeakGini);
                        ws.Cell(row, 14).Value = r.PeakTemples;
                        ws.Cell(row, 15).Value = r.FinalFamilies;
                        ws.Cell(row, 16).Value = r.LargestFamily;
                        ws.Cell(row, 17).Value = r.FinalReligion;
                        ws.Cell(row, 18).Value = r.FinalCult;
                        ws.Cell(row, 19).Value = Round3(r.FinalPiety);
                        row++;
                    }
                    idx.SaveAs(Path.Combine(dir, "_index.xlsx"));
                }

                // ---------- ДОСЬЕ КАЖДОЙ ЦИВИЛИЗАЦИИ ----------
                foreach (var r in _records.Values)
                {
                    using var wb = new XLWorkbook();
                    var s = wb.Worksheets.Add("Summary");
                    int rr = 1;
                    void KV(string k, string v) { s.Cell(rr, 1).Value = k; s.Cell(rr, 2).Value = v; rr++; }

                    KV("=== ПАСПОРТ ===", "");
                    KV("Civilization", r.Name);
                    KV("CivId", r.Id);
                    KV("Born (tick)", r.BirthTick.ToString());
                    KV("Status", r.Alive ? $"ALIVE at end (tick {finalTick})" : $"EXTINCT, last seen {r.LastSeenTick}");
                    KV("Lifespan (ticks)", (r.LastSeenTick - r.BirthTick).ToString());

                    KV("=== ПИКИ ЗА ВСЮ ЖИЗНЬ ===", "");
                    KV("Peak population", $"{r.PeakPop} (tick {r.PeakPopTick})");
                    KV("Peak era", r.PeakEra);
                    KV("Peak tool hardness", Round3(r.PeakHardness).ToString());
                    KV("Peak development", Round2(r.PeakDev).ToString());
                    KV("Peak structures", r.PeakStructures.ToString());
                    KV("Peak lexicon / grammar", $"{r.PeakLexicon} / {r.PeakGrammar}");
                    KV("Peak Gini (расслоение)", Round3(r.PeakGini).ToString());
                    KV("Peak temples", r.PeakTemples.ToString());

                    KV("=== СРЕДНЕЕ ЗА ЖИЗНЬ ===", "");
                    KV("Avg Gini", (r.StatTicks > 0 ? Round3((float)(r.SumGini / r.StatTicks)) : 0).ToString());
                    KV("Avg Despair", (r.StatTicks > 0 ? Round1((float)(r.SumDespair / r.StatTicks)) : 0).ToString());

                    KV("=== ОБЩЕСТВО (финал) ===", "");
                    KV("Religion", r.FinalReligion);
                    KV("Cult", r.FinalCult);
                    KV("Piety", Round3(r.FinalPiety).ToString());
                    KV("Temples", r.FinalTemples.ToString());
                    KV("Dynasties (families)", r.FinalFamilies.ToString());
                    KV("Largest dynasty", r.LargestFamily.ToString());
                    KV("Discoveries", r.FinalDiscoveries.ToString());

                    KV("=== ФИНАЛЬНЫЙ СРЕЗ (tick " + r.LastSeenTick + ") ===", "");
                    if (r.FinalRow != null)
                        for (int i = 0; i < TimelineHeader.Length && i < r.FinalRow.Length; i++)
                            KV(TimelineHeader[i], r.FinalRow[i]?.ToString() ?? "");

                    // ---------- TIMELINE ----------
                    var t = wb.Worksheets.Add("Timeline");
                    for (int i = 0; i < TimelineHeader.Length; i++) t.Cell(1, i + 1).Value = TimelineHeader[i];
                    int tr = 2;
                    foreach (var row in r.Rows)
                    {
                        for (int i = 0; i < row.Length; i++)
                        {
                            var cell = t.Cell(tr, i + 1);
                            switch (row[i])
                            {
                                case int iv: cell.Value = iv; break;
                                case float fv: cell.Value = fv; break;
                                case double dv: cell.Value = dv; break;
                                default: cell.Value = row[i]?.ToString() ?? ""; break;
                            }
                        }
                        tr++;
                    }

                    wb.SaveAs(Path.Combine(dir, $"civ_{r.Id}.xlsx"));
                }

                Console.WriteLine($"[CivArchive] Exported {_records.Count} civilization archives to {dir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CivArchive] Error: {ex.Message}");
            }
        }

        private static string EraOf(float h) =>
            h > 0.65f ? "MetalAge" : h > 0.45f ? "Neolithic" : h > 0.25f ? "Chalcolithic" : "Paleolithic";

        private static double Round1(float v) => Math.Round(v, 1);
        private static double Round2(float v) => Math.Round(v, 2);
        private static double Round3(float v) => Math.Round(v, 3);
    }
}
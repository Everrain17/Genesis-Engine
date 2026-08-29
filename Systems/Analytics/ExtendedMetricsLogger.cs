using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems.Observers;

namespace GenesisEngine.Systems.Analytics
{
    /// <summary>
    /// Полный сбор метрик. Все файлы лежат в одной папке запуска.
    /// </summary>
    public static class ExtendedMetricsLogger
    {
        private static string _runId;
        private static readonly Dictionary<string, StreamWriter> _w = new();
        private static readonly ConcurrentQueue<(string file, string line)> _queue = new();
        private static Thread _thread;
        private static volatile bool _running;

        private static int _prevBorn, _prevHunger, _prevPredator, _prevCombat, _prevNatural, _prevPlague, _prevCold;

        private static readonly Dictionary<string, string> Headers = new()
        {
            ["extended_data"] = "RunId,Tick,Population,LiteracyRate,AvgKnowersPerKnowledge,Reads100,Teachings100," +
    "AvgInstitutionLevel,LexiconSize,GrammarRules,Phonemes,Graphemes,Invariants," +
    "TotalBuildings,Farms,Houses,Libraries,Temples,Markets,Barracks,Mines,Bridges,Warehouses,LogicDevices,Hospices," +
    "AvgAggression,AvgConscientiousness,AvgSelfAwareness,AvgLogic,WarsActive,Infected,HerdImmunity,GiniCoefficient",

            ["civ_snapshots"] = "RunId,Tick,CivId,CivName,Population,Era,LexiconSize,GrammarRules,Phonemes,Graphemes," +
                "AvgToolHardness,TotalDevelopment,EmergentStructures,TotalScore," +
                "Infected,HerdImmunity,AvgAggression",

            ["events"] = "RunId,Tick,EventType,CivId,Data",
            ["cognitive_data"] = "RunId,Tick,Key,Count,Avg",
            ["diplomacy_data"] = "RunId,Tick,Relation,Pairs,WarsActive,AvgWarPressure,AvgPeaceStability",
            ["materials_data"] = "RunId,Tick,BaseMaterials,Composites,Breakthroughs,Analogs,GlobalComputation,AutomataComputation",
            ["culture_data"] = "RunId,Tick,Artifacts,SacredArtifacts,Texts,SacredTexts,TextsWithKnowledge,KnownSymbols,AvgSanctity",

            ["demography_data"] = "RunId,Tick,Births100,DeathsHunger100,DeathsPredator100,DeathsCombat100,DeathsNatural100,DeathsPlague100,DeathsCold100," +
    "AvgAge,AvgGeneration,Males,Females,Farmers,Builders,Traders,Soldiers,Scholars,Artisans",

            ["signals_data"] = "RunId,Tick,Alarm,Food,Come,Danger,Trade,Help,Bond,Mourn,Celebrate",
            ["technology_data"] = "RunId,Tick,Axis,AvgCap",
            ["performance_data"] = "RunId,Tick,TickMs,AgentsMs,CreaturesMs,CivMs,EventsProcessed"
        };


        public static void Initialize(string runId, string directory)
        {
            _runId = runId;
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            foreach (var kv in Headers)
            {
                string path = Path.Combine(directory, kv.Key + ".csv");
                if (!File.Exists(path))
                    File.WriteAllText(path, kv.Value + "\n");
                _w[kv.Key] = new StreamWriter(path, true) { AutoFlush = false };
            }

            _running = true;
            _thread = new Thread(Worker) { IsBackground = true, Name = "ExtendedMetricsWorker" };
            _thread.Start();
        }

        private static void Worker()
        {
            int flushCounter = 0;
            while (_running || !_queue.IsEmpty)
            {
                if (_queue.TryDequeue(out var item))
                {
                    try
                    {
                        if (_w.TryGetValue(item.file, out var writer))
                            writer.WriteLine(item.line);
                        if (++flushCounter >= 50)
                        {
                            foreach (var w in _w.Values) w.Flush();
                            flushCounter = 0;
                        }
                    }
                    catch { }
                }
                else Thread.Sleep(10);
            }
            try { foreach (var writer in _w.Values) writer.Flush(); } catch { }
        }

        private static void Enq(string file, string line) => _queue.Enqueue((file, line));

        // ============================================================
        // ГЛАВНЫЙ МЕТОД: вызывается каждые 100 тиков
        // ============================================================
        public static void LogAll(int tick, List<Agent> agents, Tile[,] world)
        {
            if (_w.Count == 0) return;
            try
            {
                var sim = Simulation.Instance;
                var civs = Simulation.activeCivs;
                int pop = Math.Max(1, agents.Count);

                var scan = ScanWorld(world);

                // ---------- extended_data ----------
                int literate = agents.Count(a => KnowledgeSystem.AgentKnowsAnything(a));
                float literacyRate = (float)literate / pop;
                float avgKnowers = KnowledgeSystem.All.Count > 0
                    ? KnowledgeSystem.All.Average(k => (float)k.Knowers.Count) : 0f;
                int reads = KnowledgeSystem.ReadsSinceLastCsv();
                int teachings = KnowledgeSystem.TeachingsSinceLastCsv();
                int lexicon = LanguageSystem.StableWordCount();
                int grammar = GrammarSystem.RuleCount();
                int phonemes = 0, graphemes = 0, invariants = 0;
                if (civs != null)
                    foreach (var c in civs)
                    {
                        phonemes += PhonemeSystem.PhonemeCount(c.Id);
                        graphemes += GraphemeSystem.GraphemeCount(c.Id);
                        invariants += SymbolicManipulationSystem.AbstractInvariantCount(c.Id);
                    }
                float avgAggr = agents.Count > 0 ? agents.Average(a => a.Genome.Aggression) : 0f;
                float avgConsc = agents.Count > 0 ? agents.Average(a => a.Genome.Conscientiousness) : 0f;
                float avgSelfAw = agents.Count > 0 ? agents.Average(a => a.Genome.SelfAwareness) : 0f;
                float avgLogic = agents.Count > 0 ? agents.Average(a => a.Logic) : 0f;
                int warsActive = civs != null ? civs.Count(c => DiplomacySystem.IsAtWar(c.Id)) : 0;
                float herdImmunity = EpidemicSystem.GetHerdImmunity(agents);
                int infectedCount = agents.Count(a => a.Infected);

                float gini = InequalityObserver.CalculateGiniCoefficient(agents);

                Enq("extended_data",
                    $"{_runId},{tick},{agents.Count},{literacyRate:F4},{avgKnowers:F2},{reads},{teachings}," +
                    $"{(scan.InstCount > 0 ? scan.InstSum / scan.InstCount : 0f):F3},{lexicon},{grammar},{phonemes},{graphemes},{invariants}," +
                    $"{scan.Total},{scan.Farms},{scan.Houses},{scan.Libraries},{scan.Temples},{scan.Markets}," +
                    $"{scan.Barracks},{scan.Mines},{scan.Bridges},{scan.Warehouses},{scan.Logic},{scan.Hospices}," +
                    $"{avgAggr:F3},{avgConsc:F3},{avgSelfAw:F3},{avgLogic:F3},{warsActive},{infectedCount},{herdImmunity:F3},{gini:F3}");

                // ---------- demography_data ----------
                int births = Math.Max(0, sim.TotalBorn - _prevBorn);
                int dHunger = Math.Max(0, sim.TotalDiedHunger - _prevHunger);
                int dPredator = Math.Max(0, sim.TotalDiedPredator - _prevPredator);
                int dCombat = Math.Max(0, sim.TotalDiedCombat - _prevCombat);
                int dNatural = Math.Max(0, sim.TotalDiedNatural - _prevNatural);
                int dPlague = Math.Max(0, sim.TotalDiedPlague - _prevPlague);
                int dCold = Math.Max(0, sim.TotalDiedCold - _prevCold);

                _prevBorn = sim.TotalBorn; _prevHunger = sim.TotalDiedHunger;
                _prevPredator = sim.TotalDiedPredator; _prevCombat = sim.TotalDiedCombat;
                _prevNatural = sim.TotalDiedNatural;
                _prevPlague = sim.TotalDiedPlague;
                _prevCold = sim.TotalDiedCold;

                float avgAge = agents.Count > 0 ? (float)agents.Average(a => a.Age) : 0f;
                float avgGen = agents.Count > 0 ? (float)agents.Average(a => a.Generation) : 0f;
                int males = agents.Count(a => a.BiologicalSex == Sex.Male);
                var roleCounts = RoleObserver.CountRoles(agents);
                int farmers = roleCounts[AgentRole.Farmer];
                int builders = roleCounts[AgentRole.Builder];
                int traders = roleCounts[AgentRole.Trader];
                int soldiers = roleCounts[AgentRole.Soldier];
                int scholars = roleCounts[AgentRole.Scholar];
                int artisans = roleCounts[AgentRole.Artisan];

                Enq("demography_data",
                    $"{_runId},{tick},{births},{dHunger},{dPredator},{dCombat},{dNatural},{dPlague},{dCold}," +
                    $"{avgAge:F0},{avgGen:F2},{males},{agents.Count - males}," +
                    $"{farmers},{builders},{traders},{soldiers},{scholars},{artisans}");

                // ---------- signals_data ----------
                var sig = new Dictionary<SignalType, int>();
                foreach (var s in SignalSystem.ActiveSignals)
                    sig[s.Type] = sig.GetValueOrDefault(s.Type) + 1;
                Enq("signals_data",
                    $"{_runId},{tick},{sig.GetValueOrDefault(SignalType.Alarm)},{sig.GetValueOrDefault(SignalType.Food)}," +
                    $"{sig.GetValueOrDefault(SignalType.Come)},{sig.GetValueOrDefault(SignalType.Danger)}," +
                    $"{sig.GetValueOrDefault(SignalType.Trade)},{sig.GetValueOrDefault(SignalType.Help)}," +
                    $"{sig.GetValueOrDefault(SignalType.Bond)},{sig.GetValueOrDefault(SignalType.Mourn)}," +
                    $"{sig.GetValueOrDefault(SignalType.Celebrate)}");

                // ---------- culture_data ----------
                int artifacts = CultureSystem.AllArtifacts.Count;
                int sacredArt = CultureSystem.AllArtifacts.Count(a => a.IsSacred);
                int texts = CultureSystem.AllTexts.Count;
                int sacredTexts = CultureSystem.AllTexts.Count(t => t.IsSacred);
                int textsWithKnow = CultureSystem.AllTexts.Count(t => t.KnowledgeIds.Count > 0);
                float avgSanctity = scan.SanctCount > 0 ? scan.SanctSum / scan.SanctCount : 0f;
                Enq("culture_data",
                    $"{_runId},{tick},{artifacts},{sacredArt},{texts},{sacredTexts},{textsWithKnow}," +
                    $"{SymbolSystem.TotalKnownSymbols()},{avgSanctity:F3}");

                // ---------- materials_data ----------
                Enq("materials_data",
                    $"{_runId},{tick},{MaterialDB.Base.Count},{MaterialDB.Composites.Count}," +
                    $"{ObserverCoordinator.MaterialBreakthroughs},{ObserverCoordinator.MaterialAnalogs}," +
                    $"{LogicSystem.GlobalComputationCapacity():F2},{LogicAutomataSystem.GetTotalComputation():F2}");

                // ---------- diplomacy_data ----------
                if (civs != null && civs.Count > 0)
                {
                    float avgWar = civs.Average(c => DiplomacySystem.WarPressure(c.Id));
                    float avgPeace = civs.Average(c => DiplomacySystem.PeaceStability(c.Id));
                    var relCounts = DiplomacySystem.AllTreaties
                        .GroupBy(t => t.Relation)
                        .ToDictionary(g => g.Key, g => g.Count());
                    foreach (var kv in relCounts)
                    {
                        if (kv.Value <= 0) continue;
                        Enq("diplomacy_data",
                            $"{_runId},{tick},{kv.Key},{kv.Value},{warsActive},{avgWar:F3},{avgPeace:F3}");
                    }
                }

                // ---------- performance_data ----------
                Enq("performance_data",
                    $"{_runId},{tick},{Simulation.LastTickMs:F2},{Simulation.LastAgentsMs:F2}," +
                    $"{Simulation.LastCreaturesMs:F2},{Simulation.LastCivilizationBlockMs:F2}," +
                    $"{ObserverCoordinator.LastProcessedEvents}");

                // ---------- cognitive_data (каждые 500) ----------
                if (tick % 500 == 0)
                {
                    foreach (var kv in CognitionSystem.SnapshotStats())
                        Enq("cognitive_data", $"{_runId},{tick},{kv.Key},{kv.Value.Count},{kv.Value.Avg:F3}");
                }

                // ---------- technology_data (каждые 500) ----------
                if (tick % 500 == 0 && civs != null && civs.Count > 0)
                {
                    foreach (var axis in EffectTables.Axis.Keys)
                    {
                        float avgCap = civs.Average(c => c.GetCap(axis));
                        if (avgCap > 0.001f)
                            Enq("technology_data", $"{_runId},{tick},{axis},{avgCap:F3}");
                    }
                }

                // ---------- civ_snapshots (каждые 1000) ----------
                if (tick % 1000 == 0 && civs != null)
                {
                    foreach (var c in civs)
                    {
                        string name = (c.Name ?? "").Replace("\"", "'");

                        // Считаем метрики эпидемии и агрессии конкретно для этой цивилизации
                        int civInfected = c.Members.Count(a => a.Infected);
                        float civHerd = c.Members.Count > 0 ? (float)c.Members.Count(a => a.Genome.ImmuneStrength > 0.8f) / c.Members.Count : 0f;
                        float civAggr = c.Members.Count > 0 ? c.Members.Average(a => a.Genome.Aggression) : 0f;

                        Enq("civ_snapshots",
                            $"{_runId},{tick},{c.Id},\"{name}\",{c.Population},{EraLabel(c.AvgToolHardness)}," +
                            $"{LanguageSystem.StableWordCount(c.Id)},{GrammarSystem.RuleCount(c.Id)}," +
                            $"{PhonemeSystem.PhonemeCount(c.Id)},{GraphemeSystem.GraphemeCount(c.Id)}," +
                            $"{c.AvgToolHardness:F3},{c.TotalDevelopment:F2},{c.EmergentStructuresCount},{c.TotalScore:F0}," +
                            $"{civInfected},{civHerd:F3},{civAggr:F3}");
                    }
                }
            }
            catch { }
        }

        // ============================================================
        // Поток событий (вызывается из ObserverCoordinator)
        // ============================================================
        public static void LogEvent(int tick, string eventType, string civId, string data)
        {
            if (_w.Count == 0) return;
            string safe = (data ?? "").Replace(",", ";").Replace("\n", " ").Replace("\"", "'");
            Enq("events", $"{_runId},{tick},{eventType},{civId ?? ""},\"{safe}\"");
        }

        // ============================================================
        private sealed class WorldScan
        {
            public int Total, Farms, Houses, Libraries, Temples, Markets, Barracks, Mines, Bridges, Warehouses, Logic, Hospices;
            public float InstSum; public int InstCount;
            public float SanctSum; public int SanctCount;
        }

        private static WorldScan ScanWorld(Tile[,] world)
        {
            var s = new WorldScan();
            int w = world.GetLength(0), h = world.GetLength(1);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var t = world[x, y];

                    // Считаем ВСЕ здания (независимо от типа)
                    if (t.Building != BuildingType.None)
                    {
                        s.Total++;

                        // === v3: ЭМЕРДЖЕНТНЫЙ ПОДСЧЁТ через DominantAxis и флаги ===

                        // Шахты — специальное здание (флаг IsMine)
                        if (t.IsMine)
                            s.Mines++;
                        // Фермы — ось "food" или "growth"
                        else if (t.IsFarm)
                            s.Farms++;
                        // Дома — ось "shelter", "comfort" или "warmth"
                        else if (t.IsHouse)
                            s.Houses++;
                        // Библиотеки — ось "knowledge"
                        else if (t.IsLibrary)
                            s.Libraries++;
                        // Храмы — ось "faith"
                        else if (t.IsTemple)
                            s.Temples++;
                        // Рынки — ось "trade"
                        else if (t.IsMarket)
                            s.Markets++;
                        // Казармы — ось "defense"
                        else if (t.IsBarracks)
                            s.Barracks++;
                        // Мосты — ось "mobility"
                        else if (t.IsBridge)
                            s.Bridges++;
                        // Склады — ось "storage"
                        else if (t.HasFunction("storage"))
                            s.Warehouses++;
                        // Хосписы — ось "healing"
                        else if (t.HasFunction("healing"))
                            s.Hospices++;
                    }

                    // Логические устройства считаются отдельно (это артефакты, не здания)
                    foreach (var a in t.Artifacts)
                    {
                        if (a.Name != null && a.Name.StartsWith("logic-node"))
                            s.Logic++;
                    }

                    if (t.InstitutionLevel > 0f) { s.InstSum += t.InstitutionLevel; s.InstCount++; }
                    if (t.SanctityLevel > 0f) { s.SanctSum += t.SanctityLevel; s.SanctCount++; }
                }
            }
            return s;
        }

        private static string EraLabel(float hardness) =>
            hardness > 0.65f ? "MetalAge" :
            hardness > 0.45f ? "Neolithic" :
            hardness > 0.25f ? "Chalcolithic" : "Paleolithic";

        public static void Flush()
        {
            int wait = 0;
            while (!_queue.IsEmpty && wait < 100) { Thread.Sleep(10); wait++; }
        }

        public static void Close()
        {
            _running = false;
            if (_thread != null && _thread.IsAlive) _thread.Join(5000);
            foreach (var writer in _w.Values)
            {
                try { writer.Flush(); writer.Close(); } catch { }
            }
            _w.Clear();
        }
    }
}
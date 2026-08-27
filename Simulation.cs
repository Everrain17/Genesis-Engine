using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems;
using GenesisEngine.UI;
using GenesisEngine.Systems.Physics;
using GenesisEngine.Systems.Analytics;
using GenesisEngine.Systems.Observers;

namespace GenesisEngine
{
    public class Simulation
    {
        public Tile[,] World;
        public List<Agent> Agents = new();
        public List<Creature> Creatures = new();
        public int TotalTicks;
        public Random Rng;

        public bool SimulationEnded = false;
        public string EndReason = "";

        public int TotalTrades = 0;

        public int TotalBorn;
        public int TotalDiedNatural;
        public int TotalDiedHunger;
        public int TotalDiedPredator;
        public int TotalDiedCombat;

        public static List<CivilizationSnapshot> activeCivs = new();
        public List<string> EventLog = new();
        public List<Agent> BornAgents = new();

        private WorldGenerator worldGen = new();

        public static Simulation Instance { get; private set; }

        public static bool EnableProfiling = false;

        public static double LastTickMs;
        public static double LastAgentsMs;
        public static double LastCreaturesMs;
        public static double LastCivilizationBlockMs;

        public Simulation()
        {
            Instance = this;
        }

        public Tile GetTile(Vector2 pos) => World[pos.X, pos.Y];
        public Tile GetTile(int x, int y) => World[x, y];

        public void Initialize(int width, int height, int initialAgents, int seed = 42)
        {
            Rng = new Random(seed);
            RandomProvider.SetSeed(seed);
            MaterialDB.SetSeed(seed); 
            World = worldGen.Generate(width, height, seed);
            Tile.World = World;

            SpatialGrid.Initialize(width, height);

            var passableTiles = new List<Vector2>();

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (World[x, y].IsPassable)
                        passableTiles.Add(new Vector2(x, y));

            for (int i = 0; i < initialAgents; i++)
                Agents.Add(new Agent(passableTiles[Rng.Next(passableTiles.Count)], Rng, 0));

            SpawnCreatures();
        }

        public void Tick()
        {    
            // НОВОЕ: Проверка завершения симуляции
            if (Agents.Count == 0)
            {
                SimulationEnded = true;
                EndReason = "All agents died";
                return;
            }

            Stopwatch tickSw = EnableProfiling ? Stopwatch.StartNew() : null;
            Stopwatch agentsSw = EnableProfiling ? Stopwatch.StartNew() : null;

            SpatialGrid.Update(Agents, TotalTicks);
            SignalSystem.CleanupSignals();

            var dead = new HashSet<Agent>();

            foreach (var a in Agents)
            {
                if (a.Body.Health <= 0)
                {
                    dead.Add(a);
                    continue;
                }

                a.Update(World, Agents, Creatures);

                if (a.Body.Health <= 0)
                    dead.Add(a);
            }

            if (EnableProfiling && agentsSw != null)
            {
                agentsSw.Stop();
                LastAgentsMs = agentsSw.Elapsed.TotalMilliseconds;
            }

            if (BornAgents.Count > 0)
            {
                foreach (var b in BornAgents)
                {
                    EventBus.Publish(new SimEvent
                    {
                        Type = SimEventType.AgentBorn,
                        Tick = TotalTicks,
                        Actor = b,
                        Position = b.Position
                    });
                }

                Agents.AddRange(BornAgents);
                BornAgents.Clear();
            }

            if (TotalTicks % 100 == 0)
                RegenerateFood();

            // Принудительное обновление сетки после движения агентов
            SpatialGrid.ForceUpdate(Agents);

            Stopwatch creaturesSw = EnableProfiling ? Stopwatch.StartNew() : null;

            for (int i = Creatures.Count - 1; i >= 0; i--)
            {
                var c = Creatures[i];

                if (c.Energy <= 0 || c.Age > c.MaxAge)
                {
                    Creatures.RemoveAt(i);
                    continue;
                }

                c.Update(World, Agents, Creatures);
                // Хищники атакуют агентов
                if (c.Behavior == CreatureBehavior.Predator)
                {
                    var target = SpatialGrid.GetNearby(c.Position, 1)
                        .FirstOrDefault(a => a != null && a.Body.Health > 0);
                    if (target != null)
                    {
                        float damage = c.Size * 3f;
                        target.Body.Health -= damage;
                        target.Fear += 30f;
                        target.LastAction = "Predated";
                        if (target.Body.Health <= 0)
                        {
                            TotalDiedPredator++;
                            EventBus.Publish(new SimEvent
                            {
                                Type = SimEventType.AgentDied,
                                Tick = TotalTicks,
                                Actor = target,
                                Position = target.Position,
                                Data = "Predated"
                            });
                        }
                    }
                }
                if (c.Energy <= 0 || c.Age > c.MaxAge)
                    Creatures.RemoveAt(i);
            }

            if (EnableProfiling && creaturesSw != null)
            {
                creaturesSw.Stop();
                LastCreaturesMs = creaturesSw.Elapsed.TotalMilliseconds;
            }

            foreach (var a in Agents)
            {
                if (a.Body.Health <= 0)
                    dead.Add(a);
            }

            foreach (var a in dead)
            {
                var nearby = SpatialGrid.GetNearby(a.Position, 3);
                CultureSystem.OnDeath(a, nearby);

                if (!Agents.Remove(a))
                    continue;

                if (a.LastAction == "Predated")
                    TotalDiedPredator++;
                else if (a.LastAction == "Age")
                    TotalDiedNatural++;
                else if (a.LastAction == "Combat")
                    TotalDiedCombat++;
                else
                    TotalDiedHunger++;

                KnowledgeSystem.OnAgentDeath(a.Id);
                AdvancedCognitivePrimitives.OnAgentDeath(a.Id);
                HigherCognitivePrimitives.OnAgentDeath(a.Id);
                EventBus.Publish(new SimEvent
                {
                    Type = SimEventType.AgentDied,
                    Tick = TotalTicks,
                    Actor = a,
                    Position = a.Position,
                    Data = a.LastAction
                });

                Tile t = World[a.Position.X, a.Position.Y];

                t.GroundObjects.Add(new WorldObject
                {
                    MaterialId = MaterialDB.GetFoodMaterialId(),
                    Quantity = 3f,
                    Position = a.Position
                });
            }

            ObserverCoordinator.ProcessEvents(this);

            if (TotalTicks > 0 && TotalTicks % 100 == 0)
            {
                Stopwatch civSw = EnableProfiling ? Stopwatch.StartNew() : null;

                // Обновляем сетку перед детекцией цивилизаций
                SpatialGrid.ForceUpdate(Agents);

                activeCivs = CivilizationDetector.Detect(Agents, World);

                foreach (var civ in activeCivs)
                {
                    civ.CalculateStats(World);
                    KnowledgeSystem.SeedBaseline(civ);

                    civ.InnovationPoints = Math.Min(1000f,
                        civ.InnovationPoints +
                        civ.Members.Sum(m => m.Genome.SelfAwareness + m.Genome.Openness) * 0.25f +
                        civ.EducationLevel * 5f);

                    CombinationEngine.RunExperiments(civ, World, Rng);

                    // === НОВОЕ (ПАКЕТ 6): Извлечение логических паттернов ===
                    foreach (var member in civ.Members)
                    {
                        LogicPatternSystem.TryExtractPatterns(member, civ, Rng);
                    }
                }

                DiplomacySystem.UpdateDiplomacy(activeCivs, Rng);
                // === НОВОЕ: лидеры принимают решения о войне/союзах ===
                if (TotalTicks % 500 == 0)
                {
                    foreach (var civ in activeCivs)
                    {
                        if (civ.Members.Count == 0) continue;

                        // Эмерджентный лидер: самое влиятельное и агрессивное лицо
                        Agent leader = civ.Members[0];
                        float best = float.MinValue;
                        foreach (var m in civ.Members)
                        {
                            float score = m.Genome.BaseInfluence + m.Genome.Aggression;
                            if (score > best) { best = score; leader = m; }
                        }

                        DiplomacySystem.LeaderDecideDiplomacy(civ, leader, activeCivs, Rng);
                    }
                }
                CultureSystem.UpdateWorld(World);
                InstitutionSystem.UpdateWorld(World, Agents, TotalTicks);

                LanguageSystem.Consolidate(activeCivs, TotalTicks);
                // НОВОЕ: Очистка старых фонем
                if (TotalTicks % 1000 == 0)
                {
                    PhonemeSystem.CleanupOldPhonemes();
                    GraphemeSystem.CleanupOldGraphemes();
                }
                GrammarSystem.Consolidate(activeCivs, TotalTicks);

                CognitionSystem.RunMathAndScience(activeCivs, Rng);
                LogicSystem.Run(activeCivs, Rng);
                LogicAutomataSystem.Run(activeCivs, Rng);
                // === НОВОЕ (ПАКЕТ 10): Анализ символических инвариантов ===
                SymbolicManipulationSystem.DetectInvariants(activeCivs);

                if (TotalTicks % 1000 == 0)
                {
                    SymbolicManipulationSystem.Cleanup();
                }

                if (TotalTicks % 1000 == 0)
                {
                    string epoch = ScienceEpochAnalyzer.Analyze(activeCivs);
                    FileLogger.Log($"[TICK {TotalTicks}] EPOCH OBSERVER: {epoch}", FileLogger.LogLevel.Info);

                    // === ВРЕМЕННЫЙ: проверка языка ===
                    foreach (var civ in activeCivs)
                    {
                        int lexicon = LanguageSystem.StableWordCount(civ.Id);
                        int grammar = GrammarSystem.RuleCount(civ.Id);
                        int phonemes = PhonemeSystem.PhonemeCount(civ.Id);
                        int graphemes = GraphemeSystem.GraphemeCount(civ.Id);
                        int invariants = SymbolicManipulationSystem.AbstractInvariantCount(civ.Id);

                        if (lexicon > 0 || grammar > 0 || phonemes > 0 || graphemes > 0 || invariants > 0)
                        {
                            FileLogger.Log(
                                $"[TICK {TotalTicks}] LANGUAGE STATUS: {civ.Name} — " +
                                $"lexicon={lexicon}, grammar={grammar}, phonemes={phonemes}, " +
                                $"graphemes={graphemes}, invariants={invariants}",
                                FileLogger.LogLevel.Info);
                        }
                    }
                }

                SimulationLogger.LogTick(TotalTicks, Agents, World);
                ExtendedMetricsLogger.LogAll(TotalTicks, Agents, World);

                if (TotalTicks % 500 == 0)
                    FileLogger.LogTick(this, TotalTicks);

                if (EnableProfiling && civSw != null)
                {
                    civSw.Stop();
                    LastCivilizationBlockMs = civSw.Elapsed.TotalMilliseconds;
                }
            }
            else
            {
                LastCivilizationBlockMs = 0f;
            }

            if (TotalTicks % 1000 == 0 && Creatures.Count < 80)
                SpawnCreatures();

            TotalTicks++;

            if (EnableProfiling && tickSw != null)
            {
                tickSw.Stop();
                LastTickMs = tickSw.Elapsed.TotalMilliseconds;
            }
        }

        private void RegenerateFood()
        {
            string foodId = MaterialDB.GetFoodMaterialId();

            foreach (var tile in World)
            {
                if (!tile.IsPassable || tile.Fertility <= 0.05f)
                    continue;

                float currentFood = tile.Resources.GetValueOrDefault(ResourceType.Food, 0f);
                float regeneration = tile.Fertility * 3f;
                bool isFarm = tile.Building == BuildingType.Farm && tile.BuildingFunctional;

                // Ферма умножает базовую регенерацию тайла
                if (isFarm)
                    regeneration *= (0.1f + tile.BuildingQuality * 4f);

                tile.Resources[ResourceType.Food] = Math.Min(100f, currentFood + regeneration);

                if (tile.GroundObjects.Count > 12)
                    tile.GroundObjects.RemoveRange(0, tile.GroundObjects.Count - 12);

                // === НОВОЕ: Ферма напрямую производит урожай (не зависит от Fertility) ===
                if (isFarm)
                {
                    tile.GroundObjects.Add(new WorldObject
                    {
                        MaterialId = foodId,
                        Quantity = 0.1f * tile.BuildingQuality,
                        Position = new Vector2(tile.X, tile.Y)
                    });
                }

                float organicAmount = tile.GroundObjects
                    .Where(o => MaterialDB.TryGet(o.MaterialId, out var spec) && spec.Organic > 0.5f)
                    .Sum(o => o.Quantity);

                float target = tile.Fertility * 40f;
                if (isFarm)
                    target *= (1f + tile.BuildingQuality * 2f);

                if (organicAmount < target)
                {
                    float add = Math.Min(20f, target - organicAmount);
                    if (add > 1f)
                    {
                        tile.GroundObjects.Add(new WorldObject
                        {
                            MaterialId = foodId,
                            Quantity = add,
                            Position = new Vector2(tile.X, tile.Y)
                        });
                    }
                }
            }
        }

        private void SpawnCreatures()
        {
            int w = World.GetLength(0);
            int h = World.GetLength(1);

            int herbivoresToSpawn = Math.Max(10, 30 - Creatures.Count / 2);

            for (int i = 0; i < herbivoresToSpawn; i++)
            {
                var pos = new Vector2(Rng.Next(w), Rng.Next(h));

                if (World[pos.X, pos.Y].IsPassable &&
                    World[pos.X, pos.Y].Fertility > 0.2f)
                {
                    Creatures.Add(new Creature(pos, Rng, CreatureSpecies.Rabbit));
                }
            }

            if (Agents.Count > 80 &&
                Creatures.Count(c => c.Behavior == CreatureBehavior.Predator) < 5)
            {
                for (int i = 0; i < 2; i++)
                {
                    var pos = new Vector2(Rng.Next(w), Rng.Next(h));
                    var tile = World[pos.X, pos.Y];

                    if (tile.IsPassable &&
                        (tile.Terrain == TerrainType.Forest ||
                         tile.Terrain == TerrainType.Taiga ||
                         tile.Terrain == TerrainType.Hill))
                    {
                        Creatures.Add(new Creature(pos, Rng, CreatureSpecies.Wolf));
                    }
                }
            }
        }
      
     

        public static void Main(string[] args)
        {
            bool headless = false;
            bool quiet = false;
            int ticks = int.MaxValue; // По умолчанию бесконечно
            int agents = 150;
            int seed = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
            bool seedSpecified = false;
            // НОВОЕ: точка в десятичных во всех файлах и логах, независимо от языка Windows
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
            // Парсим аргументы командной строки
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--headless":
                    case "--no-window":
                    case "--nogui":
                        headless = true;
                        break;

                    case "--quiet":
                        quiet = true;
                        break;

                    case "--ticks":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int t))
                        {
                            ticks = t;
                            i++; // Пропускаем значение
                        }
                        break;

                    case "--agents":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int a))
                        {
                            agents = a;
                            i++;
                        }
                        break;

                    case "--seed":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int s))
                        {
                            seed = s;
                            seedSpecified = true;
                            i++;
                        }
                        break;
                }
            }

            // Генерируем уникальный ID прогона
            DateTime now = DateTime.Now;
            string runId = now.ToString("yyyy-MM-dd_HH-mm-ss");
            string runDir = Path.Combine("data", $"BigData_{now:dd.MM.yyyy_HH.mm.ss}");

            // Флаг для graceful shutdown
            bool running = true;

            // Обработка Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Не завершаем процесс сразу
                running = false;
                Console.WriteLine("\n[INFO] Stopping simulation gracefully...");
            };

            try
            {
                Console.WriteLine("Genesis Engine (Emergent Core) starting...");

                if (seedSpecified)
                {
                    Console.WriteLine($"Using custom seed: {seed}");
                }
                else
                {
                    Console.WriteLine($"Using random seed: {seed}");
                }

                Console.WriteLine($"Run ID: {runId}");
                Console.WriteLine($"Mode: {(headless ? "HEADLESS" : "GRAPHIC")}");
                Console.WriteLine($"Agents: {agents}");

                if (headless)
                {
                    if (ticks == int.MaxValue)
                    {
                        Console.WriteLine("Ticks: ∞ (press Ctrl+C to stop)");
                    }
                    else
                    {
                        Console.WriteLine($"Ticks: {ticks}");
                    }
                }

                var sim = new Simulation();
                sim.Initialize(120, 80, agents, seed);

                // Инициализация логгеров
                FileLogger.Init("logs", headless);
                FileLogger.Log($"Run ID: {runId}");
                FileLogger.Log($"Seed: {seed}");
                FileLogger.Log($"Mode: {(headless ? "HEADLESS" : "GRAPHIC")}");
                FileLogger.Log($"Agents: {agents}");
                if (headless && ticks != int.MaxValue)
                {
                    FileLogger.Log($"Ticks limit: {ticks}");
                }

                SimulationLogger.Initialize(runId, runDir);
                ExtendedMetricsLogger.Initialize(runId, runDir);

                Console.WriteLine($"Initialized. Agents={sim.Agents.Count}, Creatures={sim.Creatures.Count}");

                if (headless)
                {
                    EnableProfiling = true;

                    SimulationLogger.LogHeadlessStatus(
                        0,
                        sim.Agents.Count,
                        Simulation.activeCivs?.Count ?? 0,
                        0f);

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    int currentTick = 0;

                    while (running && currentTick < ticks)
                    {
                        sim.Tick();
                        currentTick++;

                        // НОВОЕ: Проверка завершения симуляции
                        if (sim.SimulationEnded)
                        {
                            Console.WriteLine($"\n[INFO] Simulation ended at tick {currentTick}: {sim.EndReason}");
                            break;
                        }

                        if (!quiet && sim.TotalTicks % 100 == 0)
                        {
                            int civCount = Simulation.activeCivs?.Count ?? 0;

                            Console.WriteLine(
                                $"tick={sim.TotalTicks} " +
                                $"agents={sim.Agents.Count} " +
                                $"civs={civCount} " +
                                $"tickMs={LastTickMs:F2} " +
                                $"agentsMs={LastAgentsMs:F2} " +
                                $"creaturesMs={LastCreaturesMs:F2} " +
                                $"civMs={LastCivilizationBlockMs:F2}");

                            SimulationLogger.LogHeadlessStatus(
                                sim.TotalTicks,
                                sim.Agents.Count,
                                civCount,
                                LastTickMs);
                        }
                    }

                    sw.Stop();

                    Console.WriteLine();
                    if (sim.SimulationEnded)
                    {
                        Console.WriteLine($"Simulation ended at tick {currentTick}");
                        Console.WriteLine($"Reason: {sim.EndReason}");
                        Console.WriteLine($"Final population: {sim.Agents.Count}");
                        Console.WriteLine($"Final civilizations: {Simulation.activeCivs?.Count ?? 0}");
                        Console.WriteLine($"Final knowledge count: {KnowledgeSystem.All.Count}");
                        Console.WriteLine($"Final text count: {CultureSystem.AllTexts.Count}");
                    }
                    else
                    {
                        Console.WriteLine($"Finished {currentTick} ticks in {sw.Elapsed.TotalMilliseconds:F1} ms");
                        Console.WriteLine($"Average tick: {sw.Elapsed.TotalMilliseconds / Math.Max(1, currentTick):F4} ms");
                    }
                }
                else
                {
                    Console.WriteLine("Press Enter to start visual window...");
                    Console.ReadLine();

                    var window = new GraphicWindow(sim);
                    window.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}\n{ex.StackTrace}");
                FileLogger.Log($"FATAL ERROR: {ex.Message}", FileLogger.LogLevel.Error);
            }
            finally
            {
                Console.WriteLine("Cleaning up...");
                SimulationLogger.Flush();
                SimulationLogger.Close();
                ExtendedMetricsLogger.Flush();
                ExtendedMetricsLogger.Close();
                Console.WriteLine("Building Excel report...");
                ExcelExporter.ExportFolder(runDir);
                FileLogger.Flush();
                FileLogger.Close();

                Console.WriteLine("Done. Press any key to exit.");
                if (headless && running)
                {
                    Console.ReadKey();
                }
            }
        }

        private static int GetIntArg(string[] args, string name, int defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name && int.TryParse(args[i + 1], out int value))
                    return value;
            }

            return defaultValue;
        }

        private static FileLogger.LogLevel ParseLogLevel(string[] args)
        {
            string level = GetStringArg(args, "--loglevel", "info").ToLowerInvariant();

            return level switch
            {
                "warning" => FileLogger.LogLevel.Warning,
                "warn" => FileLogger.LogLevel.Warning,
                "war" => FileLogger.LogLevel.War,
                "death" => FileLogger.LogLevel.Death,
                "error" => FileLogger.LogLevel.Error,
                _ => FileLogger.LogLevel.Info
            };
        }

        private static string GetStringArg(string[] args, string name, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return defaultValue;
        }
    }
}
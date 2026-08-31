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
        public const string CodeVersion = "v8 - Science-Essay"; //v5 - DeadliestWeather-and-AdvancePrimitives
        public Tile[,] World;
        public List<Agent> Agents = new();
        public List<Creature> Creatures = new();
        public int TotalTicks;
        public SeasonSystem.Season CurrentSeason => SeasonSystem.GetCurrentSeason(TotalTicks);  // НОВОЕ
        public Random Rng;
        public int seed = 0;
        public bool SimulationEnded = false;
        public string EndReason = "";

        public int TotalTrades = 0;

        public int TotalBorn;
        public int TotalDiedNatural;
        public int TotalDiedHunger;
        public int TotalDiedPredator;
        public int TotalDiedCombat;
        public int TotalDiedCold;
        public int TotalDiedPlague;  // === НОВОЕ ===
        public int TotalDiedDisaster;  // === НОВОЕ: смерти от катастроф ===
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
            TerritoryCaptureSystem.Update(Agents, World);  // <-- НОВОЕ: захват территорий
            WeatherSystem.Update(World, Agents, TotalTicks);
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
            // === НОВОЕ: Разложение трупов ===
            if (TotalTicks % 100 == 0)
            {
                foreach (var tile in World)
                {
                    for (int i = tile.Corpses.Count - 1; i >= 0; i--)
                    {
                        var corpse = tile.Corpses[i];

                        if (corpse.IsDecayed)
                        {
                            // Труп разложился — улучшаем фертильность
                            tile.Fertility = Math.Min(1f, tile.Fertility + corpse.Quantity * 0.001f);
                            tile.Corpses.RemoveAt(i);

                            // Удаляем соответствующий WorldObject
                            var corpseObj = tile.GroundObjects.FirstOrDefault(o => o.IsCorpse && o.Quantity == corpse.Quantity);
                            if (corpseObj != null)
                                tile.GroundObjects.Remove(corpseObj);
                        }
                    }
                }
            }
            // === v3: эпидемии ===
            if (TotalTicks % 100 == 0)
            {
                EpidemicSystem.TrySpark(this, Rng);
                EpidemicSystem.CleanupOutbreaks(TotalTicks);
                RoleObserver.UpdateRoles(Agents);
                
            }
            // Принудительное обновление сетки после движения агентов
            SpatialGrid.ForceUpdate(Agents);
            TerritoryCaptureSystem.Update(Agents, World);

            Stopwatch creaturesSw = EnableProfiling ? Stopwatch.StartNew() : null;

            for (int i = Creatures.Count - 1; i >= 0; i--)
            {
                var c = Creatures[i];

                if (c.Energy <= 0 || c.Age > c.MaxAge)
                {
                    Creatures.RemoveAt(i);
                    continue;
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
                // 1. Удаляем агента из симуляции
                if (!Agents.Remove(a))
                    continue;

                var tile = World[a.Position.X, a.Position.Y];
                var nearby = SpatialGrid.GetNearby(a.Position, 3);

                // 2. Культурные и когнитивные последствия смерти
                CultureSystem.OnDeath(a, nearby);
                KnowledgeSystem.OnAgentDeath(a.Id);
                AdvancedCognitivePrimitives.OnAgentDeath(a.Id);
                HigherCognitivePrimitives.OnAgentDeath(a.Id);

                // 3. Статистика причин смерти
                if (a.LastAction == "Predated")
                    TotalDiedPredator++;
                else if (a.LastAction == "Age")
                    TotalDiedNatural++;
                else if (a.LastAction == "Combat")
                    TotalDiedCombat++;
                else if (a.LastAction == "Plague")
                    TotalDiedPlague++;
                else if (a.LastAction == "Cold")
                    TotalDiedCold++;
                else
                    TotalDiedHunger++; // Голод или неизвестная причина

                // 4. Публикация события для логгеров и наблюдателей
                EventBus.Publish(new SimEvent
                {
                    Type = SimEventType.AgentDied,
                    Tick = TotalTicks,
                    Actor = a,
                    Position = a.Position,
                    Data = a.LastAction
                });

                // 5. Создание трупа (единый источник мяса и будущего удобрения)
                // Чем больше размер агента, тем больше мяса


                var corpse = new Corpse
                {
                    Id = a.Id,
                    Quantity = 8f,
                    SpawnTick = TotalTicks
                };

                // Добавляем в список трупов тайла (для последующего разложения и бонуса к фертильности)
                tile.Corpses.Add(corpse);

                // Добавляем мясо на землю как WorldObject, чтобы его могли подобрать хищники, падальщики или другие агенты
                tile.GroundObjects.Add(new WorldObject
                {
                    MaterialId = MaterialDB.GetFoodMaterialId(),
                    Quantity = corpse.Quantity,
                    Position = a.Position,
                    IsCorpse = true // Флаг для системы разложения
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
                DiplomacySystem.UpdateWarTargets(activeCivs);
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
                        // НОВОЕ: Проверяем условия для революций
                       
                    }
                    RevoltSystem.CheckRevoltConditions(activeCivs);
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
                if (!tile.IsPassable || tile.Fertility <= 0.05f) continue;

                float currentFood = tile.Resources.GetValueOrDefault(ResourceType.Food, 0f);

                // Регенерация зависит от плодородия
                float baseRegeneration = tile.Fertility * 12f;
                // НОВОЕ: Сезонный модификатор регенерации
                float seasonMod = SeasonSystem.GetCurrentSeason(TotalTicks) switch
                {
                    SeasonSystem.Season.Winter => 0.4f,      // Зимой регенерация 40% от летней
                    SeasonSystem.Season.Autumn => 0.7f,      // Осенью 70%
                    SeasonSystem.Season.Spring => 1.0f,       // Весной 100%
                    SeasonSystem.Season.Summer => 1.2f,       // Летом 120%
                    _ => 1.0f
                };
                float exhaustionPenalty = 1f - (tile.Exhaustion * 0.5f);
                float regeneration = baseRegeneration * exhaustionPenalty * seasonMod;

                bool isFarm = tile.BuildingFunctional && tile.IsFarm;
                if (isFarm)
                {
                    regeneration *= (1f + tile.BuildingQuality * 4f);
                    tile.Exhaustion = Math.Max(0f, tile.Exhaustion - 0.005f);
                }

                tile.Resources[ResourceType.Food] = Math.Min(100f, currentFood + regeneration);

                // === ИСПРАВЛЕНИЕ: Мы НЕ удаляем старые объекты! ===
                // Вместо этого мы просто не добавляем новые, если на тайле уже есть "свалка" (например, > 15 объектов)
                bool isTileCluttered = tile.GroundObjects.Count > 15;

                // Фермы производят еду (только если тайл не перегружен)
                if (isFarm && !isTileCluttered)
                {
                    tile.GroundObjects.Add(new WorldObject
                    {
                        MaterialId = foodId,
                        Quantity = 8f * tile.BuildingQuality,
                        Position = new Vector2(tile.X, tile.Y)
                    });
                }

                // Экологическая обратная связь
                float organicAmount = tile.GroundObjects
                    .Where(o => MaterialDB.TryGet(o.MaterialId, out var spec) && spec.Organic > 0.5f)
                    .Sum(o => o.Quantity);

                float targetOrganic = tile.Fertility * 40f;
                if (isFarm) targetOrganic *= (1f + tile.BuildingQuality * 2f);

                if (organicAmount < targetOrganic * 0.5f)
                {
                    tile.Fertility = Math.Max(0.05f, tile.Fertility * 0.998f);
                }
                else if (organicAmount > targetOrganic * 0.8f && tile.Exhaustion < 0.3f)
                {
                    tile.Fertility = Math.Min(1f, tile.Fertility * 1.001f);
                }

                // Пополнение органики, если её мало (И ТОЛЬКО если на тайле не свалка!)
                if (organicAmount < targetOrganic && !isTileCluttered)
                {
                    float add = Math.Min(20f, targetOrganic - organicAmount);
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
                // === НОВОЕ: Весеннее восстановление после зимы ===
                var currentSeason = SeasonSystem.GetCurrentSeason(TotalTicks);
                if (currentSeason == SeasonSystem.Season.Spring && tile.Exhaustion > 0.3f)
                {
                    // Весной истощение снижается быстрее
                    tile.Exhaustion = Math.Max(0f, tile.Exhaustion - 0.02f);

                    // И плодородие восстанавливается
                    if (tile.Fertility < 0.5f)
                    {
                        tile.Fertility = Math.Min(1f, tile.Fertility * 1.005f);
                    }
                }
            }
        }

        private void SpawnCreatures()
        {
            int w = World.GetLength(0);
            int h = World.GetLength(1);
            var rng = Rng;

            // Эмерджентный лимит: чем больше агентов, тем меньше места для дикой природы, 
            // но экосистема старается поддерживать базовый уровень
            int mapArea = World.GetLength(0) * World.GetLength(1);
            int maxCreatures = Math.Max(300, (int)(mapArea * 0.04f) + Agents.Count);

            if (Creatures.Count >= maxCreatures) return;

            // Спавним новое "гнездо" или "стаю"
            int herdsToSpawn = Math.Max(1, (maxCreatures - Creatures.Count) / 6);

            for (int i = 0; i < herdsToSpawn; i++)
            {
                // Ищем безопасное место с едой (высокая фертильность)
                Vector2 spawnPos = new Vector2(0, 0);
                int attempts = 0;
                while (attempts < 50)
                {
                    spawnPos = new Vector2(rng.Next(w), rng.Next(h));
                    if (World[spawnPos.X, spawnPos.Y].IsPassable && World[spawnPos.X, spawnPos.Y].Fertility > 0.3f)
                        break;
                    attempts++;
                }

                if (attempts >= 50) continue;

                // Генерируем базовый геном для этой стаи
                CreatureGenome herdBaseGenome = CreatureGenome.Random(rng);
                Guid newHerdId = Guid.NewGuid();

                // Спавним 6 особей: 3 самца, 3 самки
                for (int j = 0; j < 6; j++)
                {
                    Sex sex = j < 3 ? Sex.Male : Sex.Female;
                    Creatures.Add(new Creature(spawnPos, rng, herdBaseGenome, newHerdId, sex));
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
            string runDir = Path.Combine("data", $"BigData_{CodeVersion}_{now:dd.MM.yyyy_HH.mm.ss}");

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
                sim.seed = seed;
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

                string pathogenCsvPath = Path.Combine(runDir, "pathogen_data.csv");
                PathogenTracker.ExportFinalData(pathogenCsvPath, runId);
                Console.WriteLine($"[PathogenTracker] Exported {PathogenTracker.GetAllRecords().Count} pathogen strains to {pathogenCsvPath}");

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
       
    }
}
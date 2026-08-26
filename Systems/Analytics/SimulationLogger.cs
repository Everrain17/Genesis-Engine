using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems.Emergence;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems.Analytics
{
    public static class SimulationLogger
    {
        private static string _filePath;
        private static string _diagnosticsPath;
        private static string _runId;

        private static StreamWriter _writer;
        private static StreamWriter _diagWriter;

        // === НОВОЕ: очереди и фоновый поток ===
        private static readonly ConcurrentQueue<string> _csvQueue = new();
        private static Thread _csvThread;
        private static volatile bool _running;

        public static void Initialize(string runId, string directory = "data")
        {
            _runId = runId ?? DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            _filePath = Path.Combine(directory, "emergence_data.csv");
            _diagnosticsPath = Path.Combine(directory, "headless_status.csv");

            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, "RunId,Tick,Population,Era,Farms,Settlements,AvgToolHardness\n");
            }

            if (!File.Exists(_diagnosticsPath))
            {
                File.WriteAllText(_diagnosticsPath, "RunId,Tick,Population,Civilizations,KnowledgeCount,TextCount,SignalCount,TickMs\n");
            }

            _writer = new StreamWriter(_filePath, true) { AutoFlush = false };
            _diagWriter = new StreamWriter(_diagnosticsPath, true) { AutoFlush = false };

            // === НОВОЕ: запускаем фоновый поток ===
            _running = true;
            _csvThread = new Thread(CsvWorker)
            {
                IsBackground = true,
                Name = "CsvLoggerWorker"
            };
            _csvThread.Start();
        }

        // === НОВОЕ: фоновый поток записи CSV ===
        private static void CsvWorker()
        {
            int flushCounter = 0;

            while (_running || !_csvQueue.IsEmpty)
            {
                if (_csvQueue.TryDequeue(out string line))
                {
                    try
                    {
                        // Определяем, в какой файл писать по префиксу
                        if (line.StartsWith("DIAG:"))
                        {
                            _diagWriter?.WriteLine(line.Substring(5));
                        }
                        else
                        {
                            _writer?.WriteLine(line);
                        }

                        flushCounter++;
                        if (flushCounter >= 20)
                        {
                            _writer?.Flush();
                            _diagWriter?.Flush();
                            flushCounter = 0;
                        }
                    }
                    catch { }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }

            try
            {
                _writer?.Flush();
                _diagWriter?.Flush();
            }
            catch { }
        }

        public static void LogTick(int tick, List<Agent> agents, Tile[,] world)
        {
            if (_writer == null) return;

            AnalyzeEraAndHardness(agents, out string era, out float avgToolHardness);
            var counts = CountFarmAndSettlement(agents, world);

            string line = $"{_runId},{tick},{agents.Count},{era},{counts.farms},{counts.settlements},{avgToolHardness:F2}";

            // В очередь вместо прямой записи
            _csvQueue.Enqueue(line);
        }

        public static void LogHeadlessStatus(int tick, int population, int civilizationCount, double tickMs)
        {
            if (_diagWriter == null) return;

            int knowledgeCount = KnowledgeSystem.All.Count;
            int textCount = CultureSystem.AllTexts.Count;
            int signalCount = SignalSystem.ActiveSignals.Count;

            string line = $"{_runId},{tick},{population},{civilizationCount},{knowledgeCount},{textCount},{signalCount},{tickMs:F4}";

            // Префикс DIAG: чтобы фоновый поток знал, в какой файл писать
            _csvQueue.Enqueue("DIAG:" + line);
        }

        public static void Flush()
        {
            int waitCount = 0;
            while (!_csvQueue.IsEmpty && waitCount < 100)
            {
                Thread.Sleep(10);
                waitCount++;
            }
        }

        public static void Close()
        {
            _running = false;

            if (_csvThread != null && _csvThread.IsAlive)
            {
                _csvThread.Join(5000);
            }

            try
            {
                _writer?.Flush();
                _writer?.Close();
            }
            catch { }
            finally
            {
                _writer = null;
            }

            try
            {
                _diagWriter?.Flush();
                _diagWriter?.Close();
            }
            catch { }
            finally
            {
                _diagWriter = null;
            }
        }

        private static (int farms, int settlements) CountFarmAndSettlement(List<Agent> agents, Tile[,] world)
        {
            var agentCounts = new Dictionary<(int x, int y), int>();

            foreach (var a in agents)
            {
                var key = (a.Position.X, a.Position.Y);
                agentCounts.TryGetValue(key, out int currentCount);
                agentCounts[key] = currentCount + 1;
            }

            int farms = 0;
            int settlements = 0;

            foreach (var tile in world)
            {
                int here = agentCounts.GetValueOrDefault((tile.X, tile.Y), 0);
                string type = PatternClassifier.ClassifyTile(tile, here);

                if (type == "Emergent_Farm") farms++;
                else if (type == "Emergent_Settlement") settlements++;
            }

            return (farms, settlements);
        }

        private static void AnalyzeEraAndHardness(List<Agent> agents, out string era, out float avgToolHardness)
        {
            float globalHardness = 0f;
            float globalConductivity = 0f;
            float agentHardnessSum = 0f;
            int totalItems = 0;
            bool hasComposite = false;

            foreach (var a in agents)
            {
                float agentHardness = 0f;
                int agentItemCount = 0;

                foreach (var item in a.Body.Inventory)
                {
                    if (!MaterialDB.TryGet(item.MaterialId, out var spec)) continue;

                    totalItems++;
                    globalHardness += spec.Hardness;
                    globalConductivity += spec.Conductivity;
                    agentHardness += spec.Hardness;
                    agentItemCount++;

                    if (item.MaterialId.Contains('+') || spec.Depth > 0) hasComposite = true;
                }

                if (agentItemCount > 0) agentHardnessSum += agentHardness / agentItemCount;
            }

            avgToolHardness = agents.Count > 0 ? agentHardnessSum / agents.Count : 0f;

            if (totalItems == 0) { era = "Prehistoric"; return; }

            float avgHardness = globalHardness / totalItems;
            float avgConductivity = globalConductivity / totalItems;

            if (avgHardness > 0.65f && avgConductivity > 0.45f) era = "Emergent Metal Age";
            else if (hasComposite && avgHardness > 0.45f) era = "Emergent Neolithic";
            else if (avgHardness > 0.25f) era = "Emergent Chalcolithic";
            else era = "Emergent Paleolithic";
        }
    }
}
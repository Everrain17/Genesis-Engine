using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GenesisEngine.Systems;

namespace GenesisEngine.UI
{
    public static class FileLogger
    {
        private static string _logPath;
        private static StreamWriter _writer;
        private static bool _immediateFlush;

        public static bool HeadlessMode;
        public static bool ConsoleMirror = true;
        public static LogLevel MinLevel = LogLevel.Info;

        // === НОВОЕ: очередь и фоновый поток ===
        private static readonly ConcurrentQueue<string> _logQueue = new();
        private static Thread _logThread;
        private static volatile bool _running;

        public enum LogLevel
        {
            Info,
            Warning,
            War,
            Death,
            Error
        }

        public static void Init(string directory = "logs")
        {
            Init(directory, false);
        }

        public static void Init(string directory, bool immediateFlush)
        {
            try
            {
                if (_writer != null)
                    Close();

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                _logPath = Path.Combine(directory, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                _writer = new StreamWriter(_logPath)
                {
                    AutoFlush = false
                };
                _immediateFlush = immediateFlush;

                // === НОВОЕ: запускаем фоновый поток записи ===
                _running = true;
                _logThread = new Thread(LogWorker)
                {
                    IsBackground = true,
                    Name = "FileLoggerWorker"
                };
                _logThread.Start();

                // Заголовок через очередь
                _logQueue.Enqueue("=== GENESIS ENGINE LOG ===");
                _logQueue.Enqueue($"Started: {DateTime.Now}");
                _logQueue.Enqueue($"Headless: {HeadlessMode}");
                _logQueue.Enqueue($"ImmediateFlush: {_immediateFlush}");
                _logQueue.Enqueue("");
            }
            catch
            {
                _writer = null;
            }
        }

        // === НОВОЕ: фоновый поток записи ===
        private static void LogWorker()
        {
            int flushCounter = 0;

            while (_running || !_logQueue.IsEmpty)
            {
                if (_logQueue.TryDequeue(out string line))
                {
                    try
                    {
                        _writer?.WriteLine(line);
                        flushCounter++;

                        if (_immediateFlush || flushCounter >= 50)
                        {
                            _writer?.Flush();
                            flushCounter = 0;
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки записи
                    }
                }
                else
                {
                    Thread.Sleep(5); // Не жечь CPU
                }
            }

            // Финальный flush перед закрытием
            try { _writer?.Flush(); } catch { }
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (level < MinLevel)
                return;

            string prefix = level switch
            {
                LogLevel.War => "⚔️ WAR",
                LogLevel.Death => "💀 DEATH",
                LogLevel.Error => "❌ ERROR",
                LogLevel.Warning => "⚠️ WARN",
                _ => "ℹ️ INFO"
            };

            string line = $"[{DateTime.Now:HH:mm:ss}] [{prefix}] {message}";

            // Консольный вывод остаётся в главном потоке (для headless)
            if (HeadlessMode && ConsoleMirror)
            {
                try { Console.WriteLine(line); } catch { }
            }

            // Файловая запись идёт в очередь — НЕ блокирует главный поток!
            _logQueue.Enqueue(line);
        }

        public static void LogTick(Simulation sim, int tick)
        {
            if (tick % 100 != 0) // Или % 500, зависит от того, как часто вызывается
                return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"--- TICK {tick} ---");
                sb.AppendLine($"  Population: {sim.Agents.Count} agents, {sim.Creatures.Count} creatures");

                var civs = Simulation.activeCivs;
                if (civs != null && civs.Count > 0)
                {
                    sb.AppendLine($"  Civilizations: {civs.Count}");
                    foreach (var c in civs.OrderByDescending(c => c.TotalScore).Take(5))
                    {
                        sb.AppendLine($"    {c.Name}: {c.Population} pop, Dev: {c.TotalDevelopment:F1}, Score: {c.TotalScore:F0}");
                    }
                }

                // === Эпидемия ===
                int infected = sim.Agents.Count(a => a.Infected);
                float herd = EpidemicSystem.GetHerdImmunity(sim.Agents);
                if (infected > 0)
                {
                    sb.AppendLine($"  Epidemic: {infected} infected, herd immunity {herd:P0}");
                }

                // === Сезон ===
                var season = SeasonSystem.GetCurrentSeason(tick);
                sb.AppendLine($"  Season: {SeasonSystem.GetSeasonName(season)} (tick {tick % SeasonSystem.TicksPerYear}/{SeasonSystem.TicksPerYear})");

                sb.AppendLine();
                _logQueue.Enqueue(sb.ToString());
            }
            catch { }
        }

        public static void Flush()
        {
            // Ждём, пока очередь опустеет (максимум 1 секунда)
            int waitCount = 0;
            while (!_logQueue.IsEmpty && waitCount < 100)
            {
                Thread.Sleep(10);
                waitCount++;
            }
        }

        public static void Close()
        {
            // Сигнализируем потоку остановиться
            _running = false;

            // Ждём, пока поток допишет всё из очереди
            if (_logThread != null && _logThread.IsAlive)
            {
                _logThread.Join(5000); // Максимум 5 секунд
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
        }
    }
}
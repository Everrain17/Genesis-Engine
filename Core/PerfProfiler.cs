using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GenesisEngine.Diagnostics
{
    public static class PerfProfiler
    {
        public static bool Enabled = false;

        private static readonly object Sync = new();
        private static readonly Dictionary<string, double> MsAccumulator = new();
        private static readonly Dictionary<string, int> CallAccumulator = new();

        private static long _frameStart;
        private static int _frames;

        public static void BeginFrame()
        {
            if (!Enabled)
                return;

            _frameStart = Stopwatch.GetTimestamp();
        }

        public static void EndFrame(int tick)
        {
            if (!Enabled)
                return;

            if (_frameStart != 0)
            {
                double ms = GetMs(_frameStart, Stopwatch.GetTimestamp());
                AddSample("Frame", ms);
                _frameStart = 0;
            }

            _frames++;

            if (_frames >= 100)
            {
                Report(tick);
            }
        }

        public static ProfileScope Measure(string name)
        {
            if (!Enabled)
                return default;

            return new ProfileScope(name);
        }

        private static double GetMs(long start, long end)
        {
            if (end <= start)
                return 0d;

            return (end - start) * 1000d / Stopwatch.Frequency;
        }

        private static void AddSample(string name, double ms)
        {
            if (string.IsNullOrEmpty(name))
                return;

            lock (Sync)
            {
                MsAccumulator.TryGetValue(name, out double total);
                MsAccumulator[name] = total + ms;

                CallAccumulator.TryGetValue(name, out int calls);
                CallAccumulator[name] = calls + 1;
            }
        }

        private static void Report(int tick)
        {
            lock (Sync)
            {
                if (_frames <= 0)
                    return;

                double frameAvg = MsAccumulator.TryGetValue("Frame", out double frameTotal)
                    ? frameTotal / _frames
                    : 0d;

                Console.WriteLine($"[PERF] tick={tick} frames={_frames} avgFrame={frameAvg:F3} ms");

                var ordered = MsAccumulator
                    .OrderByDescending(kv => kv.Value)
                    .Take(25)
                    .ToList();

                foreach (var kv in ordered)
                {
                    if (kv.Key == "Frame")
                        continue;

                    double avgMsPerFrame = kv.Value / _frames;
                    int calls = CallAccumulator.GetValueOrDefault(kv.Key, 0);

                    Console.WriteLine(
                        $"  {kv.Key,-28} {avgMsPerFrame,10:F3} ms/frame   calls={calls}");
                }

                Console.WriteLine();

                MsAccumulator.Clear();
                CallAccumulator.Clear();
                _frames = 0;
            }
        }

        public struct ProfileScope : IDisposable
        {
            private readonly string _name;
            private readonly long _start;

            public ProfileScope(string name)
            {
                _name = name;
                _start = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (_start == 0 || string.IsNullOrEmpty(_name))
                    return;

                double ms = GetMsStatic(_start, Stopwatch.GetTimestamp());
                AddSampleStatic(_name, ms);
            }

            private static double GetMsStatic(long start, long end)
            {
                if (end <= start)
                    return 0d;

                return (end - start) * 1000d / Stopwatch.Frequency;
            }

            private static void AddSampleStatic(string name, double ms)
            {
                PerfProfiler.AddSample(name, ms);
            }
        }
    }
}
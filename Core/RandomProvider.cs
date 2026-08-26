using System;

namespace GenesisEngine.Core
{
    public static class RandomProvider
    {
        private static Random _rng = new Random();
        public static void SetSeed(int seed) => _rng = new Random(seed);
        public static float GetFloat() => (float)_rng.NextDouble();
        public static int GetInt(int max) => _rng.Next(max);
        public static int GetInt(int min, int max) => _rng.Next(min, max);
        public static Random GetRandom() => _rng;
    }
}
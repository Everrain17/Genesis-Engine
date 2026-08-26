using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems
{
    public static class SymbolSystem
    {
        private static readonly SortedDictionary<string, float> Familiarity = new();

        public static int TotalKnownSymbols(string civId = null)
        {
            return Familiarity.Count(kv =>
                kv.Value >= 1f &&
                (civId == null || kv.Key.StartsWith(civId + "|")));
        }

        public static List<string> EncodeKnowledge(Knowledge k)
        {
            var symbols = new List<string>();

            if (k == null)
                return symbols;

            symbols.Add("kind:" + (k.Kind ?? "unknown"));
            symbols.Add("axis:" + (k.DominantAxis ?? "none"));

            foreach (var materialId in k.MaterialIds.Take(3))
            {
                string encoded = EncodeReferent(materialId);
                if (!string.IsNullOrEmpty(encoded))
                    symbols.Add(encoded);
            }

            if (!string.IsNullOrEmpty(k.Name))
                symbols.Add("root:" + HashToken(k.Name));

            return symbols;
        }

        public static string EncodeReferent(string referent)
        {
            if (string.IsNullOrWhiteSpace(referent))
                return null;

            if (MaterialDB.TryGet(referent, out var spec))
            {
                string dominant = DominantProperty(spec);
                return $"{dominant}:{HashToken(referent)}";
            }

            return $"ref:{HashToken(referent)}";
        }

        public static void RegisterWriting(string civId, List<string> symbols)
        {
            if (symbols == null || symbols.Count == 0)
                return;

            if (string.IsNullOrEmpty(civId))
                civId = "wild";

            foreach (var symbol in symbols)
            {
                string key = civId + "|" + symbol;

                Familiarity.TryGetValue(key, out float value);
                Familiarity[key] = value + 0.25f;
            }
        }

        public static float ReadChance(Agent reader, List<string> symbols)
        {
            if (reader == null)
                return 0f;

            if (symbols == null)
                symbols = new List<string>();

            string civId = reader.CivilizationId ?? "wild";

            float familiarity = symbols.Count == 0
                ? 0.3f
                : symbols.Average(s =>
                    Math.Min(1f, Familiarity.GetValueOrDefault(civId + "|" + s, 0f)));

            float materialLogic = HasLogicMaterial(reader) ? 0.2f : 0f;

            float chance =
                0.10f +
                familiarity * 0.45f +
                reader.Genome.SelfAwareness * 0.20f +
                reader.Genome.Openness * 0.15f +
                materialLogic;

            return Math.Clamp(chance, 0f, 0.95f);
        }

        private static bool HasLogicMaterial(Agent a)
        {
            return a.Body.Inventory.Any(o =>
                MaterialDB.TryGet(o.MaterialId, out var spec) &&
                spec.Logic > 0.5f);
        }

        private static string DominantProperty(ResourceSpec spec)
        {
            var candidates = new[]
            {
                ("hard", spec.Hardness),
                ("cond", spec.Conductivity),
                ("org", spec.Organic),
                ("flex", spec.Flexibility),
                ("logic", spec.Logic),
                ("heat", spec.HeatOutput),
                ("rare", spec.Rarity)
            };

            var best = candidates.OrderByDescending(x => x.Item2).First();
            return best.Item1;
        }

        private static string HashToken(string value)
        {
            int hash = 17;

            foreach (char c in value)
                hash = hash * 31 + c;

            return Math.Abs(hash).ToString("X4");
        }
    }
}
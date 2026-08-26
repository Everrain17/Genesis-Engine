using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.Systems.Physics;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public static class LanguageSystem
    {
        public class Word
        {
            public string CivId;
            public SignalType Signal;
            public string Referent;
            public float Strength;
            public int Uses;
            public bool Logged;
        }

        private static readonly Dictionary<string, Word> Words = new();

        private static string Key(string civId, SignalType signal, string referent)
        {
            return $"{civId}|{signal}|{referent}";
        }

        public static void ObserveEmission(SimEvent e)
        {
            if (e.Actor == null || string.IsNullOrEmpty(e.Actor.CivilizationId))
                return;

            if (!Enum.TryParse<SignalType>(e.Data, out var signalType))
                return;

            string referent = e.Payload as string;

            if (string.IsNullOrWhiteSpace(referent))
                return;

            AddAssociation(
                e.Actor.CivilizationId,
                signalType,
                referent,
                e.Value * 0.15f);
        }

        public static void ObserveReception(Agent listener, SignalInstance signal, float clarity)
        {
            if (listener == null || string.IsNullOrEmpty(listener.CivilizationId))
                return;

            if (string.IsNullOrWhiteSpace(signal.Data))
                return;

            AddAssociation(
                listener.CivilizationId,
                signal.Type,
                signal.Data,
                clarity * 0.10f);
        }

        private static void AddAssociation(
            string civId,
            SignalType signal,
            string referent,
            float weight)
        {
            if (string.IsNullOrEmpty(civId) ||
                string.IsNullOrWhiteSpace(referent) ||
                weight <= 0f)
            {
                return;
            }

            string key = Key(civId, signal, referent);

            if (!Words.TryGetValue(key, out var word))
            {
                word = new Word
                {
                    CivId = civId,
                    Signal = signal,
                    Referent = referent,
                    Strength = 0f,
                    Uses = 0,
                    Logged = false
                };

                Words[key] = word;
            }

            float before = word.Strength;

            word.Strength += weight;
            word.Uses++;

            if (!word.Logged && before < 3f && word.Strength >= 3f && word.Uses >= 5)
            {
                word.Logged = true;

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] LEXICON: civ {civId} stabilized signal association " +
                    $"{signal} -> {referent} (strength={word.Strength:F2}, uses={word.Uses})",
                    FileLogger.LogLevel.Info);
            }
        }

        public static void TrySpeakContext(Agent a, Random rng)
        {
            if (a == null || string.IsNullOrEmpty(a.CivilizationId))
                return;

            string referent = InferReferent(a);

            if (string.IsNullOrWhiteSpace(referent))
                return;

            float chance =
                0.015f +
                a.Genome.Extraversion * 0.035f +
                a.Genome.SelfAwareness * 0.030f;

            if (rng.NextDouble() > chance)
                return;

            var type = ChooseSignal(a, referent);

            SignalSystem.EmitSignal(
                a,
                type,
                0.45f + a.Genome.Extraversion * 0.30f,
                10f,
                referent);
        }

        public static void Consolidate(List<CivilizationSnapshot> civs, int tick)
        {
            if (tick % 1000 != 0)
                return;

            if (OptimizationSettings.EnableLexiconPrune)
                Prune();

            if (civs == null)
                return;

            foreach (var civ in civs)
            {
                int stableWords = Words.Count(kv =>
                    kv.Value.CivId == civ.Id &&
                    kv.Value.Strength >= 1.5f);

                if (stableWords > 0)
                {
                    FileLogger.Log(
                        $"[TICK {tick}] LANGUAGE: {civ.Name} has approx lexicon size {stableWords}",
                        FileLogger.LogLevel.Info);
                }
            }
        }

        public static string BestReferent(string civId, SignalType signal)
        {
            return Words.Values
                .Where(w => w.CivId == civId && w.Signal == signal)
                .OrderByDescending(w => w.Strength)
                .Select(w => w.Referent)
                .FirstOrDefault();
        }

        public static int StableWordCount(string civId = null)
        {
            return Words.Count(kv =>
                kv.Value.Strength >= 1.5f &&
                (civId == null || kv.Value.CivId == civId));
        }

        private static void Prune()
        {
            if (!OptimizationSettings.EnableLexiconPrune)
                return;

            if (Words.Count <= OptimizationSettings.MaxWords)
                return;

            int target = OptimizationSettings.PruneTargetWords;

            if (target <= 0)
                target = OptimizationSettings.MaxWords / 2;

            if (target >= Words.Count)
                return;

            int removeCount = Words.Count - target;

            var removeKeys = Words
                .OrderBy(kv => kv.Value.Strength)
                .Take(removeCount)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in removeKeys)
                Words.Remove(key);
        }

        private static string InferReferent(Agent a)
        {
            if (a.Fear > 65f)
                return "danger";

            if (a.Loneliness > 75f)
                return "bond";

            if (a.Body.Hunger > 70f)
                return "food";

            var inventorySalient = a.Body.Inventory
                .Where(o => o.Quantity > 0.1f)
                .OrderByDescending(o => Salience(a, o.MaterialId))
                .FirstOrDefault();

            if (inventorySalient != null)
                return inventorySalient.MaterialId;

            var tile = Simulation.Instance.GetTile(a.Position);

            var groundSalient = tile.GroundObjects
                .Where(o => o.Quantity > 0.1f)
                .OrderByDescending(o => Salience(a, o.MaterialId))
                .FirstOrDefault();

            if (groundSalient != null)
                return groundSalient.MaterialId;

            return null;
        }

        private static float Salience(Agent a, string materialId)
        {
            if (!MaterialDB.TryGet(materialId, out var spec))
                return 0f;

            if (a.Body.Hunger > 50f && spec.Organic > 0.5f)
                return spec.Organic * 2f;

            if (a.Role == AgentRole.Builder && spec.Hardness > 0.6f)
                return spec.Hardness;

            if (spec.Rarity > 0.7f)
                return spec.Rarity;

            return (spec.Hardness + spec.Flexibility + spec.Conductivity) / 3f;
        }

        private static SignalType ChooseSignal(Agent a, string referent)
        {
            if (referent == "danger")
                return SignalType.Alarm;

            if (referent == "bond")
                return SignalType.Bond;

            if (referent == "food")
                return SignalType.Food;

            if (MaterialDB.TryGet(referent, out var spec))
            {
                if (spec.Organic > 0.55f)
                    return SignalType.Food;

                if (spec.Rarity > 0.7f)
                    return SignalType.Celebrate;

                if (spec.Conductivity > 0.6f)
                    return SignalType.Trade;

                if (spec.Hardness > 0.65f)
                    return SignalType.Come;
            }

            return SignalType.Come;
        }
    }
}
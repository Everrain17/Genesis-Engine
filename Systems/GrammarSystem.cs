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
    public static class GrammarSystem
    {
        public class GrammarRule
        {
            public string CivId;
            public string SequenceKey;
            public string Referents;
            public float Weight;
            public int Uses;
            public float Valence;
            public bool Logged;
        }

        private static readonly Dictionary<string, GrammarRule> Rules = new();

        public static int RuleCount(string civId = null)
        {
            return Rules.Count(kv =>
                kv.Value.Weight >= 1.5f &&
                (civId == null || kv.Value.CivId == civId));
        }

        public static GrammarRule GetRule(string civId, List<SignalType> sequence)
        {
            if (string.IsNullOrEmpty(civId) || sequence == null || sequence.Count < 2)
                return null;

            string key = Key(civId, sequence);
            Rules.TryGetValue(key, out var rule);
            return rule;
        }

        public static void ObserveEmission(SimEvent e)
        {
            if (e.Actor == null || string.IsNullOrEmpty(e.Actor.CivilizationId))
                return;

            if (e.Payload is SignalSequencePayload payload &&
                payload.Types != null &&
                payload.Types.Count >= 2)
            {
                string referents = payload.Referents == null
                    ? string.Empty
                    : string.Join("|", payload.Referents.Where(r => !string.IsNullOrEmpty(r)));

                Add(
                    e.Actor.CivilizationId,
                    payload.Types,
                    referents,
                    InferValence(e.Actor),
                    0.12f);
            }
        }

        public static void ObserveReception(Agent listener, SignalInstance signal, float clarity)
        {
            if (listener == null ||
                string.IsNullOrEmpty(listener.CivilizationId) ||
                signal.Sequence.Count < 2)
            {
                return;
            }

            string context = InferContext(listener);

            Add(
                listener.CivilizationId,
                signal.Sequence,
                context,
                0f,
                clarity * 0.12f);
        }

        public static void TrySpeakGrammar(Agent a, Random rng)
        {
            if (a == null || string.IsNullOrEmpty(a.CivilizationId))
                return;

            int stableWords = LanguageSystem.StableWordCount(a.CivilizationId);

            if (stableWords < 1 && rng.NextDouble() > 0.10f)
                return;

            float chance =
                0.006f +
                a.Genome.Extraversion * 0.020f +
                a.Genome.SelfAwareness * 0.020f +
                a.Logic * 0.030f;

            if (rng.NextDouble() > chance)
                return;

            var seq = BuildSequence(a, rng);

            if (seq.types.Count >= 3)
            {
                SignalSystem.EmitSequence(
                    a,
                    seq.types,
                    seq.referents,
                    0.5f + a.Genome.Extraversion * 0.3f,
                    14f);
            }
        }

        public static void ModifyResponse(
            Agent listener,
            SignalInstance signal,
            float clarity,
            ref SignalResponse response)
        {
            if (listener == null || signal.Sequence.Count < 2)
                return;

            var rule = GetRule(listener.CivilizationId, signal.Sequence);
            if (rule == null)
                return;

            float strength = Math.Clamp(rule.Weight / 5f, 0f, 1f);
            string refs = rule.Referents ?? string.Empty;

            if (rule.Valence < -0.2f)
            {
                response.Flee = Math.Clamp(response.Flee + clarity * 0.25f * strength, 0f, 1f);
            }

            if (rule.Valence > 0.2f)
            {
                response.Approach = Math.Clamp(response.Approach + clarity * 0.15f * strength, 0f, 1f);
            }

            if (refs.Contains("danger"))
            {
                response.Flee = Math.Clamp(response.Flee + clarity * 0.25f * strength, 0f, 1f);
            }

            if (refs.Contains("food"))
            {
                response.Approach = Math.Clamp(response.Approach + clarity * 0.20f * strength, 0f, 1f);
            }

            if (refs.Contains("bond"))
            {
                response.Social = Math.Clamp(response.Social + clarity * 0.20f * strength, 0f, 1f);
            }

            if (refs.Contains("knowledge") || refs.Contains("logic"))
            {
                response.Approach = Math.Clamp(response.Approach + clarity * 0.10f * strength, 0f, 1f);
                response.Social = Math.Clamp(response.Social + clarity * 0.10f * strength, 0f, 1f);
            }
        }

        public static void Consolidate(List<CivilizationSnapshot> civs, int tick)
        {
            if (tick % 1000 != 0)
                return;

            Prune(2500);

            if (civs == null)
                return;

            foreach (var civ in civs)
            {
                int count = RuleCount(civ.Id);

                if (count > 0)
                {
                    FileLogger.Log(
                        $"[TICK {tick}] GRAMMAR: {civ.Name} has approx grammar rules {count}",
                        FileLogger.LogLevel.Info);
                }
            }
        }

        private static void Prune(int maxRules)
        {
            if (Rules.Count <= maxRules)
                return;

            var removeKeys = Rules
                .OrderBy(kv => kv.Value.Weight)
                .Take(Rules.Count - maxRules)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in removeKeys)
                Rules.Remove(key);
        }

        private static void Add(
            string civId,
            List<SignalType> sequence,
            string referents,
            float valence,
            float weight = 0.1f)
        {
            if (string.IsNullOrEmpty(civId) ||
                sequence == null ||
                sequence.Count < 2 ||
                weight <= 0f)
            {
                return;
            }

            string key = Key(civId, sequence);

            if (!Rules.TryGetValue(key, out var rule))
            {
                rule = new GrammarRule
                {
                    CivId = civId,
                    SequenceKey = string.Join(">", sequence),
                    Referents = referents ?? string.Empty,
                    Weight = 0f,
                    Uses = 0,
                    Valence = valence,
                    Logged = false
                };

                Rules[key] = rule;
            }

            float before = rule.Weight;

            rule.Weight += weight;
            rule.Uses++;

            if (!string.IsNullOrWhiteSpace(referents))
                rule.Referents = referents;

            rule.Valence = rule.Valence * 0.9f + valence * 0.1f;

            if (!rule.Logged && before < 2f && rule.Weight >= 2f && rule.Uses >= 8)
            {
                rule.Logged = true;

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] GRAMMAR: civ {civId} stabilized sequence " +
                    $"{rule.SequenceKey} referents={rule.Referents} " +
                    $"weight={rule.Weight:F2}, uses={rule.Uses}, valence={rule.Valence:F2}",
                    FileLogger.LogLevel.Info);
            }
        }

        private static (List<SignalType> types, List<string> referents) BuildSequence(Agent a, Random rng)
        {
            var types = new List<SignalType>();
            var refs = new List<string>();

            string need = InferInternalNeed(a);
            AddPrimarySignal(need, types, refs);

            string obj = InferObjectReferent(a);
            if (!string.IsNullOrEmpty(obj))
                AddObjectSignal(obj, types, refs);

            string social = InferSocialIntent(a);
            AddSocialSignal(social, types, refs);

            if (a.Logic > 0.55f && rng.NextDouble() < 0.35f)
                AddAbstractModifier(a, types, refs);

            while (types.Count < 3)
            {
                types.Add(SignalType.Come);
                refs.Add("cohere");
            }

            if (types.Count > 5)
            {
                types = types.Take(5).ToList();
                refs = refs.Take(5).ToList();
            }

            return (types, refs);
        }

        private static void AddPrimarySignal(string need, List<SignalType> types, List<string> refs)
        {
            switch (need)
            {
                case "danger":
                    types.Add(SignalType.Alarm);
                    refs.Add("danger");
                    break;

                case "help":
                    types.Add(SignalType.Help);
                    refs.Add("help");
                    break;

                case "food":
                    types.Add(SignalType.Food);
                    refs.Add("food");
                    break;

                case "bond":
                    types.Add(SignalType.Bond);
                    refs.Add("bond");
                    break;

                case "trade":
                    types.Add(SignalType.Trade);
                    refs.Add("trade");
                    break;

                case "knowledge":
                    types.Add(SignalType.Come);
                    refs.Add("knowledge");
                    break;

                default:
                    types.Add(SignalType.Come);
                    refs.Add("come");
                    break;
            }
        }

        private static void AddObjectSignal(string referent, List<SignalType> types, List<string> refs)
        {
            if (MaterialDB.TryGet(referent, out var spec))
            {
                if (spec.Organic > 0.55f)
                {
                    types.Add(SignalType.Food);
                }
                else if (spec.Hardness > 0.65f)
                {
                    types.Add(SignalType.Come);
                }
                else if (spec.Conductivity > 0.60f || spec.Logic > 0.60f)
                {
                    types.Add(SignalType.Trade);
                }
                else if (spec.Rarity > 0.70f)
                {
                    types.Add(SignalType.Celebrate);
                }
                else
                {
                    types.Add(SignalType.Come);
                }

                refs.Add(referent);
            }
            else
            {
                types.Add(SignalType.Come);
                refs.Add(referent);
            }
        }

        private static void AddSocialSignal(string intent, List<SignalType> types, List<string> refs)
        {
            switch (intent)
            {
                case "bond":
                    types.Add(SignalType.Bond);
                    refs.Add("bond");
                    break;

                case "help":
                    types.Add(SignalType.Help);
                    refs.Add("help");
                    break;

                case "trade":
                    types.Add(SignalType.Trade);
                    refs.Add("trade");
                    break;

                case "danger":
                    types.Add(SignalType.Alarm);
                    refs.Add("danger");
                    break;

                default:
                    types.Add(SignalType.Come);
                    refs.Add("come");
                    break;
            }
        }

        private static void AddAbstractModifier(Agent a, List<SignalType> types, List<string> refs)
        {
            var tile = Simulation.Instance.GetTile(a.Position);

            if (tile.InstitutionAxis == "knowledge")
            {
                types.Add(SignalType.Trade);
                refs.Add("knowledge");
            }
            else if (LogicSystem.HasLogicDeviceAt(tile))
            {
                types.Add(SignalType.Celebrate);
                refs.Add("logic");
            }
            else if (a.Memory.Patterns.Count > 10)
            {
                types.Add(SignalType.Help);
                refs.Add("pattern");
            }
        }

        private static string InferInternalNeed(Agent a)
        {
            if (a.Fear > 65f)
                return "danger";

            if (a.Body.Health < 35f)
                return "help";

            if (a.Body.Hunger > 65f)
                return "food";

            if (a.Loneliness > 70f)
                return "bond";

            var tile = Simulation.Instance.GetTile(a.Position);

            if (a.Curiosity > 0.65f && tile.InstitutionAxis == "knowledge")
                return "knowledge";

            if (a.Body.Inventory.Any(o =>
                MaterialDB.TryGet(o.MaterialId, out var spec) &&
                (spec.Rarity > 0.7f || spec.Conductivity > 0.6f || spec.Logic > 0.6f)))
            {
                return "trade";
            }

            return "come";
        }

        private static string InferObjectReferent(Agent a)
        {
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

        private static string InferSocialIntent(Agent a)
        {
            if (a.Loneliness > 60f)
                return "bond";

            if (a.Body.Health < 40f)
                return "help";

            if (a.Fear > 60f)
                return "danger";

            if (a.Body.Inventory.Any(o => o.Quantity > 1f))
                return "trade";

            return "come";
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

            if (spec.Logic > 0.6f)
                return spec.Logic;

            return (spec.Hardness + spec.Flexibility + spec.Conductivity) / 3f;
        }

        private static string InferContext(Agent a)
        {
            return InferInternalNeed(a) + "|" + (InferObjectReferent(a) ?? "none");
        }

        private static float InferValence(Agent a)
        {
            if (a.Fear > 60f)
                return -1f;

            if (a.Body.Health < 35f)
                return -0.5f;

            if (a.Body.Hunger > 70f)
                return 0.2f;

            if (a.Loneliness > 70f)
                return 0.4f;

            return 0f;
        }

        private static string Key(string civId, List<SignalType> sequence)
        {
            return $"{civId}|{string.Join(">", sequence)}";
        }
    }
}
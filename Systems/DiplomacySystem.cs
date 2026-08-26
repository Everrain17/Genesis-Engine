using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.UI;
using GenesisEngine.Entities;

namespace GenesisEngine.Systems
{
    public static class DiplomacySystem
    {
        private static readonly Dictionary<string, float> Relations = new();
        private static readonly Dictionary<string, DiplomaticRelation> States = new();
        private static readonly Dictionary<string, float> Losses = new();

        // Новые стимулы: мир/война как длительные состояния.
        private static readonly Dictionary<string, int> PeaceTicks = new();
        private static readonly Dictionary<string, int> WarTicks = new();

        static string Key(string a, string b) =>
            string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

        public static float GetRelation(string a, string b) =>
            Relations.GetValueOrDefault(Key(a, b), 0);

        public static void ShiftRelation(string a, string b, float d)
        {
            var k = Key(a, b);
            Relations[k] = Math.Clamp(Relations.GetValueOrDefault(k, 0) + d, -100, 100);
        }

        public static DiplomaticRelation GetState(string a, string b) =>
            States.GetValueOrDefault(Key(a, b), DiplomaticRelation.Neutral);

        public static void SetState(string a, string b, DiplomaticRelation s) =>
            States[Key(a, b)] = s;

        public static void RecordLoss(string civId) =>
            Losses[civId] = Losses.GetValueOrDefault(civId, 0) + 1;

        public static float Weariness(string civId, float pop) =>
            Math.Min(1f, Losses.GetValueOrDefault(civId, 0) / Math.Max(10f, pop));

        public static void DeclareWar(string a, string b, CasusBelli cb)
        {
            SetState(a, b, DiplomaticRelation.War);
            ShiftRelation(a, b, -40f);
        }

        public static bool IsAtWar(string civId)
        {
            if (string.IsNullOrEmpty(civId))
                return false;

            return WarTicks.GetValueOrDefault(civId, 0) > 0;
        }

        public static float PeaceStability(string civId)
        {
            if (string.IsNullOrEmpty(civId))
                return 0f;

            int peace = PeaceTicks.GetValueOrDefault(civId, 0);
            return Math.Clamp(peace / 5000f, 0f, 1f);
        }

        public static float WarPressure(string civId)
        {
            if (string.IsNullOrEmpty(civId))
                return 0f;

            int war = WarTicks.GetValueOrDefault(civId, 0);
            return Math.Clamp(war / 2000f, 0f, 1f);
        }

        private static void UpdateCivStates(List<CivilizationSnapshot> civs)
        {
            if (civs == null)
                return;

            foreach (var civ in civs)
            {
                bool atWar = false;

                foreach (var other in civs)
                {
                    if (other.Id == civ.Id)
                        continue;

                    if (GetState(civ.Id, other.Id) == DiplomaticRelation.War)
                    {
                        atWar = true;
                        break;
                    }
                }

                if (atWar)
                {
                    WarTicks[civ.Id] = WarTicks.GetValueOrDefault(civ.Id, 0) + 100;
                    PeaceTicks[civ.Id] = 0;
                }
                else
                {
                    PeaceTicks[civ.Id] = PeaceTicks.GetValueOrDefault(civ.Id, 0) + 100;

                    if (WarTicks.TryGetValue(civ.Id, out int warTicks))
                    {
                        WarTicks[civ.Id] = Math.Max(0, warTicks - 40);
                    }
                }
            }
        }

        private static float CalcCivStrength(CivilizationSnapshot civ)
        {
            if (civ.Members.Count == 0)
                return 0f;

            float total = 0f;

            foreach (var a in civ.Members)
            {
                float agentPower = 0.1f;

                if (a.Body.Inventory.Count > 0)
                {
                    agentPower += a.Body.Inventory.Max(o =>
                        o.GetProperties().GetValueOrDefault("Hardness", 0.1f));
                }

                total += agentPower;
            }

            total += civ.GetCap("war_melee") * 2f;
            total += civ.GetCap("defense") * 1.5f;
            total += civ.GetCap("war_siege") * 1.2f;
            total += civ.Members.Count * 0.05f;

            return total;
        }

        public static void ObserveTrade(Agent a, Agent b)
        {
            if (a == null || b == null)
                return;

            if (string.IsNullOrEmpty(a.CivilizationId) ||
                string.IsNullOrEmpty(b.CivilizationId))
                return;

            if (a.CivilizationId == b.CivilizationId)
                return;

            ShiftRelation(a.CivilizationId, b.CivilizationId, +2f);
        }

        public static void ObserveCombat(Agent a, Agent b)
        {
            if (a == null || b == null)
                return;

            if (string.IsNullOrEmpty(a.CivilizationId) ||
                string.IsNullOrEmpty(b.CivilizationId))
                return;

            if (a.CivilizationId == b.CivilizationId)
                return;

            ShiftRelation(a.CivilizationId, b.CivilizationId, -6f);
            RecordLoss(b.CivilizationId);
        }

        public static void UpdateDiplomacy(List<CivilizationSnapshot> civs, Random rng)
        {
            if (civs == null || rng == null)
                return;

            UpdateCivStates(civs);

            for (int i = 0; i < civs.Count; i++)
            {
                for (int j = i + 1; j < civs.Count; j++)
                {
                    var a = civs[i];
                    var b = civs[j];

                    var st = GetState(a.Id, b.Id);
                    float rel = GetRelation(a.Id, b.Id);

                    float wear =
                        (Weariness(a.Id, a.Members.Count) +
                         Weariness(b.Id, b.Members.Count)) * 0.5f;

                    float open =
                        ((a.Members.Count > 0 ? a.Members.Average(m => m.Genome.Openness) : 0.5f) +
                         (b.Members.Count > 0 ? b.Members.Average(m => m.Genome.Openness) : 0.5f)) * 0.5f;

                    if (st == DiplomaticRelation.War)
                    {
                        float longWarPressure =
                            (WarPressure(a.Id) + WarPressure(b.Id)) * 0.5f;

                        float peaceChance =
                            0.04f +
                            wear * 0.50f +
                            longWarPressure * 0.45f;

                        if (rel > -20f)
                            peaceChance += 0.08f;

                        if (rng.NextDouble() < peaceChance)
                        {
                            SetState(a.Id, b.Id, DiplomaticRelation.Neutral);
                            ShiftRelation(a.Id, b.Id, +25f);

                            Losses[a.Id] = 0;
                            Losses[b.Id] = 0;

                            FileLogger.Log(
                                $"[TICK {Simulation.Instance.TotalTicks}] PEACE between {a.Name} and {b.Name}",
                                FileLogger.LogLevel.Info);
                        }
                    }
                    else
                    {
                        float peaceA = PeaceStability(a.Id);
                        float peaceB = PeaceStability(b.Id);

                        float improve =
                            open * 0.25f +
                            (rel > 0f ? 0.10f : 0.03f) +
                            (peaceA + peaceB) * 0.10f;

                        if (rng.NextDouble() < improve)
                        {
                            var next =
                                rel > 60 ? DiplomaticRelation.Alliance :
                                rel > 30 ? DiplomaticRelation.TradeAgreement :
                                rel > 0 ? DiplomaticRelation.NonAggressionPact :
                                DiplomaticRelation.Neutral;

                            if (next > st)
                            {
                                SetState(a.Id, b.Id, next);
                                ShiftRelation(a.Id, b.Id, +10f);
                            }
                        }

                        // Медленное естественное затухание негатива.
                        if (rel < 0f)
                            ShiftRelation(a.Id, b.Id, +0.25f);
                        else if (rel > 0f && rng.NextDouble() < 0.25f)
                            ShiftRelation(a.Id, b.Id, -0.05f);
                    }
                }
            }
        }

        public static void LeaderDecideDiplomacy(
            CivilizationSnapshot civ,
            Agent leader,
            List<CivilizationSnapshot> allCivs,
            Random rng)
        {
            if (civ == null || leader == null || allCivs == null || rng == null)
                return;

            foreach (var other in allCivs)
            {
                if (other.Id == civ.Id)
                    continue;

                var state = GetState(civ.Id, other.Id);
                float rel = GetRelation(civ.Id, other.Id);

                float wear =
                    (Weariness(civ.Id, civ.Members.Count) +
                     Weariness(other.Id, other.Members.Count)) * 0.5f;

                float myStr = CalcCivStrength(civ);
                float theirStr = CalcCivStrength(other);

                if (state == DiplomaticRelation.War)
                {
                    float peaceChance =
                        0.10f +
                        wear * 0.80f +
                        WarPressure(civ.Id) * 0.50f +
                        (myStr < theirStr * 0.7f ? 0.35f : 0.05f);

                    if (rng.NextDouble() < peaceChance)
                    {
                        SetState(civ.Id, other.Id, DiplomaticRelation.Neutral);
                        ShiftRelation(civ.Id, other.Id, +30f);

                        Losses[civ.Id] = 0;
                        Losses[other.Id] = 0;

                        FileLogger.Log(
                            $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name} (leader) makes PEACE with {other.Name}",
                            FileLogger.LogLevel.Info);
                    }
                }
                else
                {
                    float warChance = 0f;

                    bool exhausted = WarPressure(civ.Id) > 0.45f;

                    if (!exhausted &&
                        rel < -45f &&
                        myStr > theirStr * 1.4f &&
                        leader.Genome.Aggression > 0.70f)
                    {
                        warChance = 0.12f;
                    }

                    if (!exhausted &&
                        rel < -70f &&
                        myStr > theirStr * 1.2f)
                    {
                        warChance = 0.20f;
                    }

                    if (rng.NextDouble() < warChance)
                    {
                        DeclareWar(civ.Id, other.Id, CasusBelli.Expansion);

                        FileLogger.Log(
                            $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name} (leader) DECLARES WAR on {other.Name}",
                            FileLogger.LogLevel.War);
                    }
                    else if (rel > 60f && rng.NextDouble() < 0.20f)
                    {
                        SetState(civ.Id, other.Id, DiplomaticRelation.Alliance);
                        ShiftRelation(civ.Id, other.Id, +15f);

                        FileLogger.Log(
                            $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name} (leader) forms ALLIANCE with {other.Name}",
                            FileLogger.LogLevel.Info);
                    }
                    else if (rel > 20f &&
                             state < DiplomaticRelation.TradeAgreement &&
                             rng.NextDouble() < 0.30f)
                    {
                        SetState(civ.Id, other.Id, DiplomaticRelation.TradeAgreement);
                        ShiftRelation(civ.Id, other.Id, +10f);

                        FileLogger.Log(
                            $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name} (leader) signs TRADE AGREEMENT with {other.Name}",
                            FileLogger.LogLevel.Info);
                    }
                    else if (rel > 0f &&
                             state < DiplomaticRelation.NonAggressionPact &&
                             rng.NextDouble() < 0.20f)
                    {
                        SetState(civ.Id, other.Id, DiplomaticRelation.NonAggressionPact);
                        ShiftRelation(civ.Id, other.Id, +5f);
                    }
                }
            }
        }

        public static List<Treaty> AllTreaties
        {
            get
            {
                var treaties = new List<Treaty>();

                foreach (var kv in States)
                {
                    var parts = kv.Key.Split('|');

                    treaties.Add(new Treaty
                    {
                        CivilizationA = parts[0],
                        CivilizationB = parts[1],
                        Relation = kv.Value
                    });
                }

                return treaties;
            }
        }
    }

    public class Treaty
    {
        public string CivilizationA, CivilizationB;
        public DiplomaticRelation Relation;
        public bool IsActive = true;
    }
}
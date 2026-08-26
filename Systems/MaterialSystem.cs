using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems.Physics;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public struct ResourceSpec
    {
        public string Id;
        public string Name;
        public FundamentalParams Fundamental;
        public ObservableProperties Observed;
        public int Depth;

        // Безопасные геттеры
        public float Hardness => Observed?.Hardness ?? 0;
        public float Conductivity => Observed?.Conductivity ?? 0;
        public float Buoyancy => Observed?.Buoyancy ?? 0;
        public float Flexibility => Observed?.Flexibility ?? 0;
        public float Organic => Observed?.Organic ?? 0;
        public float HeatOutput => Observed?.HeatOutput ?? 0;
        public float Logic => Observed?.Logic ?? 0;
        public float Rarity => Observed?.Rarity ?? 0.5f;
        public float Durability => Observed?.Durability ?? 0;
        public float Salt => Observed?.Salt ?? 0;
    }
    public class Knowledge
    {
        public string Id = Guid.NewGuid().ToString()[..8];
        public string Kind;
        public string Branch;
        public string Sub;
        public string DominantAxis;
        public string Concept;
        public string Name;
        public Dictionary<string, float> Profile = new();
        public List<string> MaterialIds = new();
        public float Power, Quality;
        public HashSet<Guid> Knowers = new();
        public bool RecordedInText;
        public int CreatedTick;
    }

    public class Discovery
    {
        public string Id = Guid.NewGuid().ToString()[..8];
        public string Name, Branch, Capability, Archetype;
        public float Quality;
        public List<string> Components = new();
        public int Tick;
        public string AuthorId;
    }
    public static class PropertyEffects
    {
        public static float GetProp(ResourceSpec s, string p) => p switch
        {
            "Hardness" => s.Hardness,
            "Conductivity" => s.Conductivity,
            "Buoyancy" => s.Buoyancy,
            "Flexibility" => s.Flexibility,
            "Organic" => s.Organic,
            "HeatOutput" => s.HeatOutput,
            "Logic" => s.Logic,
            "Rarity" => s.Rarity,
            "Durability" => s.Durability,
            "Salt" => s.Salt,
            _ => 0
        };
    }

    public static class NameBank
    {
        public static readonly string[] Melee = { "sword", "axe", "mace", "spear", "dagger", "club", "bulava", "sabre", "warhammer" };
        public static readonly string[] Ranged = { "bow", "longbow", "crossbow", "sling", "javelin", "dart" };
        public static readonly string[] Siege = { "catapult", "trebuchet", "ram", "ballista", "mangonel" };

        public static string WarName(string cat, Random r) => cat switch
        {
            "melee" => Melee[r.Next(Melee.Length)],
            "ranged" => Ranged[r.Next(Ranged.Length)],
            "siege" => Siege[r.Next(Siege.Length)],
            _ => "blade"
        };
    }



    public static class KnowledgeSystem
    {
        public static List<Knowledge> All = new();

        private static readonly SortedDictionary<string, Knowledge> _byId = new();
        private static readonly SortedDictionary<Guid, List<Knowledge>> _byKnower = new();
        private static readonly SortedDictionary<string, List<Knowledge>> _byKindConcept = new();
        private static readonly SortedDictionary<string, List<Knowledge>> _byKindAxis = new();

        private static int _indexedCount = -1;
        private static bool _dirty = true;

        private static int _readCountSinceLastLog = 0;
        private static int _teachCountSinceLastLog = 0;
        private static int _lastReadLogTick = 0;
        private static int _lastTeachLogTick = 0;
        private static readonly Dictionary<string, int> _readByKnowledge = new();
        private static readonly Dictionary<string, int> _teachByKnowledge = new();

        private static void MarkDirty()
        {
            _dirty = true;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }

        private static void EnsureIndexes()
        {
            if (!_dirty && _indexedCount == All.Count)
                return;

            _byId.Clear();
            _byKnower.Clear();
            _byKindConcept.Clear();
            _byKindAxis.Clear();

            foreach (var k in All)
            {
                if (k == null)
                    continue;

                _byId[Safe(k.Id)] = k;

                foreach (var knower in k.Knowers)
                {
                    if (!_byKnower.TryGetValue(knower, out var list))
                    {
                        list = new List<Knowledge>();
                        _byKnower[knower] = list;
                    }

                    if (!list.Contains(k))
                        list.Add(k);
                }

                string conceptKey = Safe(k.Kind) + "|" + Safe(k.Concept);

                if (!_byKindConcept.TryGetValue(conceptKey, out var conceptList))
                {
                    conceptList = new List<Knowledge>();
                    _byKindConcept[conceptKey] = conceptList;
                }

                if (!conceptList.Contains(k))
                    conceptList.Add(k);

                string axisKey = Safe(k.Kind) + "|" + Safe(k.DominantAxis);

                if (!_byKindAxis.TryGetValue(axisKey, out var axisList))
                {
                    axisList = new List<Knowledge>();
                    _byKindAxis[axisKey] = axisList;
                }

                if (!axisList.Contains(k))
                    axisList.Add(k);
            }

            _indexedCount = All.Count;
            _dirty = false;
        }

        private static void AddKnowerIndexed(Knowledge k, Guid id)
        {
            if (k == null)
                return;

            if (!k.Knowers.Add(id))
                return;

            if (_dirty || _indexedCount != All.Count)
                return;

            if (!_byKnower.TryGetValue(id, out var list))
            {
                list = new List<Knowledge>();
                _byKnower[id] = list;
            }

            if (!list.Contains(k))
                list.Add(k);
        }

        public static float MethodBuff(Agent a, string axis)
        {
            if (a == null)
                return 0f;

            EnsureIndexes();

            if (!_byKnower.TryGetValue(a.Id, out var known))
                return 0f;

            float best = 0f;

            foreach (var k in known)
            {
                if (k.Kind != "method")
                    continue;

                if (k.DominantAxis != axis)
                    continue;

                best = Math.Max(best, k.Power * k.Quality);
            }

            return best;
        }

        public static WrittenText WriteKnowledge(Agent author, Tile tile, Knowledge k)
        {
            if (author == null || tile == null || k == null)
                return null;

            if (!k.Knowers.Contains(author.Id))
                return null;

            if (k.RecordedInText)
                return null;

            bool knowledgePlace =
                tile.Building == BuildingType.Library ||
                tile.Building == BuildingType.Temple ||
                tile.DominantAxis == "knowledge" ||
                tile.SanctityLevel > 20f ||
                tile.InstitutionLevel > 1f;

            float chance =
                0.30f +
                author.Genome.SelfAwareness * 0.35f +
                (knowledgePlace ? 0.20f : 0f) +
                tile.InstitutionLevel * 0.02f;

            chance = Math.Clamp(chance, 0f, 0.95f);

            if (RandomProvider.GetFloat() > chance)
                return null;

            var text = CultureSystem.CreateKnowledgeText(author, tile, k);

            if (text == null)
                return null;

            k.RecordedInText = true;

            FileLogger.Log(
                $"[TICK {Simulation.Instance.TotalTicks}] KNOWLEDGE RECORDED: '{k.Name}' [{k.DominantAxis}] by {author.Id}",
                FileLogger.LogLevel.Info);

            return text;
        }

        public static bool TryReadFromText(Agent reader, Tile tile, Random rng)
        {
            if (reader == null || tile == null || tile.Texts.Count == 0)
                return false;

            EnsureIndexes();

            var text = tile.Texts[rng.Next(tile.Texts.Count)];

            if (text.KnowledgeIds.Count == 0)
                return false;

            float symbolChance = SymbolSystem.ReadChance(reader, text.EncodedSymbols ?? new List<string>());

            bool learnedAnything = false;

            foreach (var knowledgeId in text.KnowledgeIds)
            {
                if (!_byId.TryGetValue(Safe(knowledgeId), out var knowledge))
                    continue;

                if (knowledge.Knowers.Contains(reader.Id))
                    continue;

                float chance =
                    symbolChance +
                    reader.Genome.Openness * 0.10f +
                    tile.InstitutionLevel * 0.02f;

                if (tile.Building == BuildingType.Library)
                    chance += 0.15f;

                if (tile.DominantAxis == "knowledge")
                    chance += 0.10f;

                chance = Math.Clamp(chance, 0f, 0.95f);

                if (rng.NextDouble() < chance)
                {
                    AddKnowerIndexed(knowledge, reader.Id);
                    learnedAnything = true;

                    _readCountSinceLastLog++;
                    if (!_readByKnowledge.ContainsKey(knowledge.Name))
                        _readByKnowledge[knowledge.Name] = 0;
                    _readByKnowledge[knowledge.Name]++;
                    LogReadsIfNeeded();
                }
            }

            return learnedAnything;
        }

        private static void LogReadsIfNeeded()
        {
            int currentTick = Simulation.Instance?.TotalTicks ?? 0;
            if (currentTick - _lastReadLogTick < 500)
                return;

            if (_readCountSinceLastLog > 0)
            {
                var top = _readByKnowledge
                    .OrderByDescending(kv => kv.Value)
                    .Take(5)
                    .Select(kv => $"{kv.Key}({kv.Value})")
                    .ToList();

                FileLogger.Log(
                    $"[TICK {currentTick}] KNOWLEDGE READS: {_readCountSinceLastLog} reads in last 500 ticks. " +
                    $"Top: {string.Join(", ", top)}",
                    FileLogger.LogLevel.Info);
            }

            _readCountSinceLastLog = 0;
            _readByKnowledge.Clear();
            _lastReadLogTick = currentTick;
        }

        public static bool AgentKnowsAnything(Agent a)
        {
            if (a == null)
                return false;

            EnsureIndexes();

            return _byKnower.TryGetValue(a.Id, out var list) && list.Count > 0;
        }

        public static bool TryTeachFromTeacher(Agent teacher, Agent student, Tile tile, Random rng)
        {
            if (teacher == null || student == null)
                return false;

            EnsureIndexes();

            if (!_byKnower.TryGetValue(teacher.Id, out var known))
                return false;

            var candidates = known
                .Where(k => !k.Knowers.Contains(student.Id))
                .OrderByDescending(k =>
                    k.Power * k.Quality +
                    (k.RecordedInText ? 0.25f : 0f))
                .Take(6)
                .ToList();

            if (candidates.Count == 0)
                return false;

            foreach (var k in candidates)
            {
                float chance =
                    0.12f +
                    teacher.Logic * 0.30f +
                    student.Genome.Openness * 0.20f +
                    student.Logic * 0.15f;

                if (k.RecordedInText)
                    chance += 0.10f;

                if (tile != null)
                    chance += tile.InstitutionLevel * 0.02f;

                chance = Math.Clamp(chance, 0f, 0.90f);

                if (rng.NextDouble() < chance)
                {
                    AddKnowerIndexed(k, student.Id);

                    // Агрегируем вместо индивидуального лога
                    _teachCountSinceLastLog++;
                    if (!_teachByKnowledge.ContainsKey(k.Name))
                        _teachByKnowledge[k.Name] = 0;
                    _teachByKnowledge[k.Name]++;
                    LogTeachingIfNeeded();

                    return true;
                }
            }

            return false;
        }

        private static void LogTeachingIfNeeded()
        {
            int currentTick = Simulation.Instance?.TotalTicks ?? 0;
            if (currentTick - _lastTeachLogTick < 500)
                return;

            if (_teachCountSinceLastLog > 0)
            {
                var top = _teachByKnowledge
                    .OrderByDescending(kv => kv.Value)
                    .Take(5)
                    .Select(kv => $"{kv.Key}({kv.Value})")
                    .ToList();

                FileLogger.Log(
                    $"[TICK {currentTick}] TEACHING: {_teachCountSinceLastLog} teachings in last 500 ticks. " +
                    $"Top: {string.Join(", ", top)}",
                    FileLogger.LogLevel.Info);
            }

            _teachCountSinceLastLog = 0;
            _teachByKnowledge.Clear();
            _lastTeachLogTick = currentTick;
        }

        public static void InheritFromParents(Agent child, Agent mother, Agent father)
        {
            if (child == null)
                return;

            foreach (var k in All)
            {
                bool m = mother != null && k.Knowers.Contains(mother.Id);
                bool f = father != null && k.Knowers.Contains(father.Id);

                if ((m || f) && RandomProvider.GetFloat() < 0.7f)
                    k.Knowers.Add(child.Id);
            }

            MarkDirty();
        }

        public static bool CivKnowsRecipe(CivilizationSnapshot civ, string buildingName)
        {
            if (civ == null)
                return false;

            EnsureIndexes();

            string key = "recipe|" + Safe(buildingName);

            return _byKindConcept.TryGetValue(key, out var list) &&
                   list.Any(k => k.Knowers.Count > 0);
        }

        public static float BuildingPower(CivilizationSnapshot civ, string axis)
        {
            if (civ == null)
                return 0f;

            EnsureIndexes();

            string key = "recipe|" + Safe(axis);

            if (!_byKindAxis.TryGetValue(key, out var list))
                return 0f;

            return list
                .Where(k => k.Knowers.Count > 0)
                .Select(k => k.Power * k.Quality)
                .DefaultIfEmpty(0f)
                .Max();
        }

        public static WeaponStats BestWeapon(Agent a)
        {
            var best = CombatSystem.Fist;

            if (a == null)
                return best;

            EnsureIndexes();

            if (!_byKnower.TryGetValue(a.Id, out var known))
                return best;

            foreach (var k in known)
            {
                if (k.Kind != "item")
                    continue;

                if (k.DominantAxis != "war_melee" &&
                    k.DominantAxis != "war_ranged" &&
                    k.DominantAxis != "war_siege")
                {
                    continue;
                }

                float dmg = 1 + k.Power * k.Quality * 8;
                int range = k.DominantAxis == "war_ranged" ? 3 : 0;
                float siege = k.DominantAxis == "war_siege" ? k.Power * k.Quality * 10 : 0;
                float fear = Math.Min(0.5f, k.Power);

                if (dmg > best.Damage || range > best.Range || siege > best.Siege)
                {
                    best = new WeaponStats
                    {
                        Damage = dmg,
                        Range = range,
                        FearReduction = fear,
                        Siege = siege,
                        Name = k.Name
                    };
                }
            }

            return best;
        }

        public static void SeedBaseline(CivilizationSnapshot civ)
        {
            if (civ == null)
                return;

            foreach (var bt in new[] { BuildingType.Farm, BuildingType.House })
            {
                string concept = bt.ToString();

                if (CivKnowsRecipe(civ, concept))
                    continue;

                string axis = bt == BuildingType.Farm ? "food" : "shelter";

                var profile = new Dictionary<string, float>
                {
                    [axis] = 0.05f
                };

                var k = new Knowledge
                {
                    Kind = "recipe",
                    Branch = "building",
                    Sub = axis,
                    DominantAxis = axis,
                    Concept = concept,
                    Name = "ancestral-" + (bt == BuildingType.Farm ? "plow" : "house"),
                    Profile = profile,
                    Power = 0.05f,
                    Quality = 1
                };

                foreach (var m in civ.Members)
                    k.Knowers.Add(m.Id);

                All.Add(k);
            }

            MarkDirty();
        }

        public static void OnAgentDeath(Guid id)
        {
            foreach (var k in All)
                k.Knowers.Remove(id);

            var lost = All
                .Where(k => k.Knowers.Count == 0 && !k.RecordedInText)
                .ToList();

            foreach (var k in lost)
            {
                All.Remove(k);

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] LOST KNOWLEDGE '{k.Name}' [{k.DominantAxis}]",
                    FileLogger.LogLevel.Warning);
            }

            MarkDirty();
        }

        public static void TryTeach(Agent t, Agent s)
        {
            if (t == null || s == null)
                return;

            EnsureIndexes();

            if (!_byKnower.TryGetValue(t.Id, out var known))
                return;

            var candidates = known
                .Where(k => !k.Knowers.Contains(s.Id))
                .ToList();

            foreach (var k in candidates)
            {
                float chance =
                    (t.Genome.Extraversion * 0.5f + t.Genome.SelfAwareness * 0.5f) * 0.6f;

                if (RandomProvider.GetFloat() < chance)
                    AddKnowerIndexed(k, s.Id);
            }
        }
    }

    public static class CombinationEngine
    {
        private static Dictionary<string, (Dictionary<string, float> needs, int tick)> needsCache = new();

        private const int MaxNeedsCacheEntries = 200;
        private const int MaxCompositeMaterials = 20000;

        private static bool _compositeCapLogged = false;

        public static Dictionary<string, float> AxisNeeds(CivilizationSnapshot c)
        {
            if (needsCache.TryGetValue(c.Id, out var entry) &&
                Simulation.Instance.TotalTicks - entry.tick < 50)
            {
                return entry.needs;
            }

            float A(Func<Agent, float> f) =>
                c.Members.Count > 0 ? c.Members.Average(f) : 0f;

            float hunger = A(a => a.Body.Hunger) / 100f;
            float fear = A(a => a.Fear) / 100f;
            float lone = A(a => a.Loneliness) / 100f;

            float full = c.Members.Count(a =>
                a.Body.Inventory.Sum(o => o.Quantity) >= a.Body.MaxCarryWeight * 0.9f) /
                (float)Math.Max(1, c.Members.Count);

            float openness = A(a => a.Genome.Openness);
            float selfAwareness = A(a => a.Genome.SelfAwareness);
            float extraversion = A(a => a.Genome.Extraversion);
            float spirituality = A(a => a.Genome.Spirituality);
            float health = A(a => a.Body.Health) / 100f;

            bool atWar = DiplomacySystem.IsAtWar(c.Id);
            float peace = DiplomacySystem.PeaceStability(c.Id);
            float warPressure = DiplomacySystem.WarPressure(c.Id);

            float computation = LogicAutomataSystem.GetCivComputation(c.Id);
            float knowledgeCap = c.GetCap("knowledge");

            float scienceDemand =
                selfAwareness * 0.45f +
                c.EducationLevel * 0.80f +
                knowledgeCap * 1.40f +
                computation * 0.12f +
                peace * 0.55f -
                warPressure * 0.45f;

            scienceDemand = Math.Max(0.05f, scienceDemand);

            float tradeDemand =
                openness * 0.40f +
                extraversion * 0.25f +
                full * 0.25f +
                peace * 0.50f -
                warPressure * 0.25f;

            tradeDemand = Math.Max(0.03f, tradeDemand);

            float warDemand =
                fear *
                (atWar ? 0.85f : 0.22f) *
                (1f - peace * 0.50f);

            var result = new Dictionary<string, float>
            {
                ["food"] = hunger,
                ["growth"] = hunger * 0.60f,
                ["shelter"] = lone,
                ["comfort"] = lone * 0.50f,
                ["storage"] = full,

                ["defense"] =
                    fear *
                    (atWar ? 0.80f : 0.25f) *
                    (1f - peace * 0.40f),

                ["war_melee"] = warDemand,
                ["war_ranged"] = warDemand * 0.80f,
                ["war_siege"] = warDemand * 0.40f,

                ["mining"] = 0.35f,

                ["trade"] = tradeDemand,

                ["knowledge"] = scienceDemand,

                ["faith"] =
                    spirituality * 0.35f +
                    peace * 0.15f,

                ["culture"] =
                    openness * 0.35f +
                    peace * 0.30f,

                ["healing"] =
                    Math.Max(0f, 1f - health) +
                    warPressure * 0.20f,

                ["mobility"] = 0.15f
            };

            needsCache[c.Id] = (result, Simulation.Instance.TotalTicks);

            return result;
        }

        private static void PruneNeedsCache()
        {
            if (!OptimizationSettings.EnableNeedsCachePrune)
                return;

            if (needsCache.Count == 0)
                return;

            int currentTick = Simulation.Instance?.TotalTicks ?? 0;

            if (needsCache.Count > OptimizationSettings.MaxNeedsCacheEntries)
            {
                var stale = needsCache
                    .Where(kv => currentTick - kv.Value.tick > 1000)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in stale)
                    needsCache.Remove(key);
            }

            if (needsCache.Count > OptimizationSettings.MaxNeedsCacheEntries * 2)
            {
                var oldest = needsCache
                    .OrderBy(kv => kv.Value.tick)
                    .Take(needsCache.Count - OptimizationSettings.MaxNeedsCacheEntries)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in oldest)
                    needsCache.Remove(key);
            }
        }

        static float ScienceCapacity(CivilizationSnapshot c)
        {
            float baseCapacity =
                c.Members.Sum(a =>
                    a.Genome.SelfAwareness +
                    a.Logic * 0.75f);

            float peaceBonus = DiplomacySystem.PeaceStability(c.Id) * 6f;
            float warPenalty = DiplomacySystem.WarPressure(c.Id) * 8f;

            float capacity =
                baseCapacity +
                c.GetCap("knowledge") * 10f +
                LogicAutomataSystem.GetCivComputation(c.Id) * 2f +
                peaceBonus -
                warPenalty;

            return Math.Max(0f, capacity);
        }

        private static string PickWeighted(Dictionary<string, float> w, Random r)
        {
            float t = 0;

            foreach (var v in w.Values)
                t += v + 0.05f;

            float x = (float)r.NextDouble() * t;

            foreach (var kv in w)
            {
                x -= kv.Value + 0.05f;

                if (x <= 0)
                    return kv.Key;
            }

            return "food";
        }

        private static string ClassifyWar(ResourceSpec s)
        {
            if (s.Flexibility > 0.55f && s.Flexibility >= s.Hardness)
                return "war_ranged";

            if (s.Hardness > 0.7f && s.Durability > 0.7f)
                return "war_siege";

            return "war_melee";
        }

        private static string Root(ResourceSpec s, string id)
        {
            return string.IsNullOrEmpty(s.Name)
                ? (id.Length > 4 ? id[..4] : id)
                : (s.Name.Length > 4 ? s.Name[..4] : s.Name);
        }

        public static void RunExperiments(CivilizationSnapshot civ, Tile[,] world, Random rng)
        {
            PruneNeedsCache();

            var thinkers = civ.Members
                .Where(a => a.Age > 500 && a.Age < a.MaxAge * 0.7f)
                .OrderByDescending(a => a.Genome.SelfAwareness + a.Genome.Openness + a.Logic)
                .Take(3)
                .ToList();

            if (thinkers.Count == 0)
                return;

            var stock = AvailableInputs(civ);

            if (stock.Count < 1)
                return;

            float cap = ScienceCapacity(civ);
            var needs = AxisNeeds(civ);

            foreach (var t in thinkers)
            {
                if (rng.NextDouble() > 0.6f * (1 + civ.GetCap("knowledge")))
                    continue;

                string axis = PickWeighted(needs, rng);

                if (rng.NextDouble() < 0.4f)
                {
                    TryDiscoverMethod(civ, t, stock[rng.Next(stock.Count)], axis, rng);
                }
                else if (stock.Count >= 2)
                {
                    var a = stock[rng.Next(stock.Count)];
                    var b = stock[rng.Next(stock.Count)];

                    if (a != b)
                        TryCombine(civ, a, b, axis, t, cap, rng);
                }
            }
        }

        private static void TryDiscoverMethod(
            CivilizationSnapshot civ,
            Agent author,
            string matId,
            string axis,
            Random rng)
        {
            if (!MaterialDB.TryGet(matId, out var mat))
                return;

            if (string.IsNullOrEmpty(mat.Name))
                mat.Name = matId;

            float power = EffectTables.Compute(axis, mat) / 100f;

            if (power <= 0)
                return;

            string root = Root(mat, matId);
            string name = root + "-" + EffectTables.AxisMethodWord(axis);

            if (KnowledgeSystem.All.Any(k => k.Kind == "method" && k.Name == name))
                return;

            float quality = Math.Clamp(
                0.5f + author.Genome.SelfAwareness * 0.5f + (float)rng.NextDouble() * 0.5f,
                0f,
                2f);

            var profile = new Dictionary<string, float>();

            foreach (var ax in EffectTables.Axis.Keys)
                profile[ax] = EffectTables.Compute(ax, mat) / 100f;

            var k = new Knowledge
            {
                Kind = "method",
                Branch = "method",
                Sub = axis,
                DominantAxis = axis,
                Concept = EffectTables.AxisWord(axis),
                Name = name,
                Profile = profile,
                Power = power,
                Quality = quality,
                MaterialIds = { matId },
                CreatedTick = Simulation.Instance.TotalTicks
            };

            k.Knowers.Add(author.Id);

            KnowledgeSystem.All.Add(k);

            civ.Discoveries.Add(new Discovery
            {
                Name = name,
                Branch = "method",
                Capability = axis,
                Quality = quality,
                Components = { matId },
                Tick = Simulation.Instance.TotalTicks,
                AuthorId = author.Id.ToString()
            });

            FileLogger.Log(
                $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: METHOD '{name}' [{axis}] +{power * quality * 100:F1}%",
                FileLogger.LogLevel.Info);
        }

        private static void TryCombine(
            CivilizationSnapshot civ,
            string idA,
            string idB,
            string axis,
            Agent author,
            float cap,
            Random rng)
        {
            if (!MaterialDB.TryGet(idA, out var sa) ||
                !MaterialDB.TryGet(idB, out var sb))
            {
                return;
            }

            string pair = MaterialDB.CompositeId(sa, sb);

            if (!civ.TriedPairs.Add(pair + axis))
                return;

            if (MaterialDB.Composites.Count >= MaxCompositeMaterials &&
                !MaterialDB.Composites.ContainsKey(pair))
            {
                if (!_compositeCapLogged)
                {
                    _compositeCapLogged = true;

                    FileLogger.Log(
                        $"MATERIAL CAP: composite count reached {MaxCompositeMaterials}. New composite creation is limited.",
                        FileLogger.LogLevel.Warning);
                }

                return;
            }

            int depth = Math.Max(sa.Depth, sb.Depth) + 1;

            if (cap < (depth - 1) * 3f)
                return;

            float cost = (sa.Rarity + sb.Rarity) * 15f;

            cost *= 1f - Math.Clamp(
                LogicAutomataSystem.GetCivComputation(civ.Id) * 0.03f,
                0f,
                0.6f);

            if (civ.InnovationPoints < cost)
                return;

            if (!Consume(civ, idA) || !Consume(civ, idB))
                return;

            civ.InnovationPoints -= cost;

            var mix = MaterialDB.Mix(sa, sb);

            pair = mix.Id;

            civ.MatStock[pair] = civ.MatStock.GetValueOrDefault(pair, 0) + 1;

            float itemPower = mix.Observed switch
            {
                var o when axis == "war_melee" => o.Hardness,
                var o when axis == "war_ranged" => o.Flexibility,
                var o when axis == "war_siege" => o.Hardness * 0.7f + o.Durability * 0.3f,
                _ => EffectTables.Compute(axis, mix) / 100f
            };

            string rootA = Root(sa, idA);
            string rootB = Root(sb, idB);

            string itemAxis = axis.StartsWith("war") ? ClassifyWar(mix) : axis;

            string itemName = itemAxis.StartsWith("war")
                ? rootA + rootB + "-" + NameBank.WarName(itemAxis.Substring(4), rng)
                : rootA + rootB + "-" + EffectTables.AxisItemWord(itemAxis);

            civ.Capabilities[itemAxis] = Math.Max(civ.GetCap(itemAxis), itemPower);

            if (!KnowledgeSystem.All.Any(k => k.Kind == "item" && k.Name == itemName))
            {
                var profile = new Dictionary<string, float>();

                foreach (var ax in EffectTables.Axis.Keys)
                    profile[ax] = EffectTables.Compute(ax, mix) / 100f;

                var ki = new Knowledge
                {
                    Kind = "item",
                    Branch = "item",
                    Sub = itemAxis,
                    DominantAxis = itemAxis,
                    Concept = EffectTables.AxisItemWord(itemAxis),
                    Name = itemName,
                    Profile = profile,
                    Power = itemPower,
                    Quality = 1f + mix.Rarity,
                    MaterialIds = { idA, idB },
                    CreatedTick = Simulation.Instance.TotalTicks
                };

                ki.Knowers.Add(author.Id);

                KnowledgeSystem.All.Add(ki);

                civ.Discoveries.Add(new Discovery
                {
                    Name = itemName,
                    Branch = "item",
                    Capability = itemAxis,
                    Quality = 1f + mix.Rarity,
                    Components = { idA, idB },
                    Tick = Simulation.Instance.TotalTicks,
                    AuthorId = author.Id.ToString()
                });

                FileLogger.Log(
                    $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: ITEM '{itemName}' [{itemAxis}] +{itemPower * 100:F1}%",
                    FileLogger.LogLevel.Info);
            }

            var dominantProfile = new Dictionary<string, float>();

            string dominant = null;
            float dv = 0f;

            foreach (var ax in EffectTables.Axis.Keys)
            {
                float v = EffectTables.Compute(ax, mix) / 100f;

                dominantProfile[ax] = v;

                if (v > dv)
                {
                    dv = v;
                    dominant = ax;
                }
            }

            if (dominant != null &&
                dv > 0.02f &&
                dominant != "war_melee" &&
                dominant != "war_ranged")
            {
                string rn = rootA + rootB + "-" + EffectTables.AxisBuildingWord(dominant);
                var bt = EffectTables.AxisToBuilding(dominant).ToString();

                if (!KnowledgeSystem.All.Any(k => k.Kind == "recipe" && k.Name == rn))
                {
                    var kr = new Knowledge
                    {
                        Kind = "recipe",
                        Branch = "building",
                        Sub = dominant,
                        DominantAxis = dominant,
                        Concept = bt,
                        Name = rn,
                        Profile = dominantProfile,
                        Power = dv,
                        Quality = 1f + mix.Rarity,
                        MaterialIds = { idA, idB },
                        CreatedTick = Simulation.Instance.TotalTicks
                    };

                    kr.Knowers.Add(author.Id);

                    KnowledgeSystem.All.Add(kr);

                    civ.Capabilities[dominant] = Math.Max(civ.GetCap(dominant), dv);

                    civ.Discoveries.Add(new Discovery
                    {
                        Name = rn,
                        Branch = "building",
                        Capability = dominant,
                        Quality = 1f + mix.Rarity,
                        Components = { idA, idB },
                        Tick = Simulation.Instance.TotalTicks,
                        AuthorId = author.Id.ToString()
                    });

                    FileLogger.Log(
                        $"[TICK {Simulation.Instance.TotalTicks}] {civ.Name}: BUILDING '{rn}' [{dominant}] +{dv * 100:F1}%",
                        FileLogger.LogLevel.Info);
                }
            }
        }

        private static List<string> AvailableInputs(CivilizationSnapshot civ)
        {
            var s = new HashSet<string>();

            foreach (var m in civ.Members)
            {
                foreach (var obj in m.Body.Inventory)
                {
                    if (obj.Quantity > 1 && MaterialDB.TryGet(obj.MaterialId, out _))
                        s.Add(obj.MaterialId);
                }
            }

            foreach (var kv in civ.MatStock)
            {
                if (kv.Value > 0)
                    s.Add(kv.Key);
            }

            return s.ToList();
        }

        private static bool Consume(CivilizationSnapshot civ, string id)
        {
            foreach (var m in civ.Members)
            {
                var obj = m.Body.Inventory.FirstOrDefault(o =>
                    o.MaterialId == id &&
                    o.Quantity >= 1);

                if (obj != null)
                {
                    obj.Quantity -= 1;

                    if (obj.Quantity <= 0)
                        m.Body.Inventory.Remove(obj);

                    return true;
                }
            }

            if (civ.MatStock.GetValueOrDefault(id, 0) < 1)
                return false;

            civ.MatStock[id] -= 1;

            return true;
        }

        public static bool CanCross(Agent a, TerrainType t)
        {
            if (string.IsNullOrEmpty(a.CivilizationId))
                return false;

            var civ = Simulation.activeCivs?.FirstOrDefault(c => c.Id == a.CivilizationId);

            if (civ == null)
                return false;

            if (t == TerrainType.ShallowWater)
                return civ.GetCap("mobility") > 0.5f;

            if (t == TerrainType.DeepWater)
                return civ.GetCap("mobility") > 1.0f;

            return false;
        }

    }
    public static class MaterialDB
    {
        public static readonly Dictionary<string, ResourceSpec> Base = new();
        public static Dictionary<string, ResourceSpec> Composites = new();

        private static Random _rng; 

        static MaterialDB()
        {
            _rng = new Random(42);  // Дефолтный сид, если не задан
            InitializeBaseMaterials();
            EnsureBaselineOrganic();
        }
        public static void SetSeed(int seed)
        {
            _rng = new Random(seed);
            Base.Clear();
            Composites.Clear();
            InitializeBaseMaterials();
            EnsureBaselineOrganic();
        }

        private static void InitializeBaseMaterials()
        {
            for (int i = 0; i < 50; i++)
            {
                var (fp, props) = MaterialPhysics.GenerateBaseMaterial(_rng);
                string id = $"M_{i:D4}";

                Base[id] = new ResourceSpec
                {
                    Id = id,
                    Name = $"Material-{i}",
                    Fundamental = fp,
                    Observed = props,
                    Depth = 0
                };
            }
        }

        private static void EnsureBaselineOrganic()
        {
            int organicCount = Base.Values.Count(x => x.Organic > 0.55f);
            if (organicCount >= 5) return;

            for (int i = organicCount; i < 5; i++)
            {
                var fp = new FundamentalParams
                {
                    BondEnergy = 0.03f + (float)_rng.NextDouble() * 0.08f,
                    ElectronDensity = 0.05f + (float)_rng.NextDouble() * 0.10f,
                    LatticeSymmetry = 0.55f + (float)_rng.NextDouble() * 0.25f,
                    AtomicMass = 0.03f + (float)_rng.NextDouble() * 0.08f,
                    ThermalVibration = 0.35f + (float)_rng.NextDouble() * 0.20f,
                    QuantumCoherence = 0f
                };

                var props = MaterialPhysics.DeriveProperties(fp, _rng);

                // Гарантируем, что это действительно биомасса/еда.
                props.Organic = Math.Max(props.Organic, 0.78f);
                props.Rarity = Math.Min(props.Rarity, 0.25f);

                string id = $"M_BIO_{i}";

                Base[id] = new ResourceSpec
                {
                    Id = id,
                    Name = id,
                    Fundamental = fp,
                    Observed = props,
                    Depth = 0
                };
            }
        }

        public static bool TryGet(string id, out ResourceSpec s)
            => Base.TryGetValue(id, out s) || Composites.TryGetValue(id, out s);

        public static string GetFoodMaterialId()
        {
            EnsureBaselineOrganic();

            return Base.Values
                .OrderByDescending(x => x.Organic)
                .First()
                .Id;
        }

        private static IEnumerable<string> Leaves(string id)
        {
            return id.Split('+', StringSplitOptions.RemoveEmptyEntries);
        }

        public static string CompositeId(ResourceSpec a, ResourceSpec b)
        {
            var parts = Leaves(a.Id)
                .Concat(Leaves(b.Id))
                .Distinct()
                .OrderBy(x => x, StringComparer.Ordinal);

            return string.Join("+", parts);
        }

        public static ResourceSpec Mix(ResourceSpec a, ResourceSpec b)
        {
            string id = CompositeId(a, b);

            if (Composites.TryGetValue(id, out var existing))
                return existing;

            var (mixedFP, mixedProps) = MaterialPhysics.Mix(a.Fundamental, b.Fundamental, 0.5f, _rng);
            mixedProps = MaterialPhysics.ApplySpecialEffects(mixedFP, mixedProps, _rng);

            var spec = new ResourceSpec
            {
                Id = id,
                Name = id,
                Fundamental = mixedFP,
                Observed = mixedProps,
                Depth = Math.Max(a.Depth, b.Depth) + 1
            };

            Composites[id] = spec;

            Observers.EventBus.Publish(new Observers.SimEvent
            {
                Type = Observers.SimEventType.MaterialMixed,
                Tick = Simulation.Instance?.TotalTicks ?? 0,
                Data = id,
                Position = default
            });

            return spec;
        }

        
    }
}
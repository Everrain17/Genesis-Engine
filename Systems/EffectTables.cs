using System.Collections.Generic;
using GenesisEngine.Core;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class EffectTables
    {
        public static readonly Dictionary<string, Dictionary<string, float>> Axis = new()
        {
            ["food"] = new() { ["Organic"] = 10, ["Hardness"] = 6, ["Durability"] = 3 },
            ["growth"] = new() { ["Organic"] = 8, ["Flexibility"] = 5, ["Buoyancy"] = 2 },
            ["shelter"] = new() { ["Flexibility"] = 6, ["Hardness"] = 5, ["Durability"] = 5, ["Organic"] = 2 },
            ["storage"] = new() { ["Hardness"] = 8, ["Durability"] = 8, ["Rarity"] = 2 },
            ["defense"] = new() { ["Hardness"] = 10, ["Durability"] = 8, ["HeatOutput"] = 2 },
            ["war_melee"] = new() { ["Hardness"] = 10, ["Flexibility"] = 4, ["Durability"] = 6 },
            ["war_ranged"] = new() { ["Flexibility"] = 10, ["Hardness"] = 4, ["Durability"] = 3 },
            ["war_siege"] = new() { ["Hardness"] = 8, ["Durability"] = 8, ["HeatOutput"] = 4 },
            ["mining"] = new() { ["Hardness"] = 8, ["Durability"] = 6, ["Logic"] = 2 },
            ["trade"] = new() { ["Rarity"] = 6, ["Conductivity"] = 5, ["Flexibility"] = 2 },
            ["knowledge"] = new() { ["Logic"] = 10, ["Conductivity"] = 6, ["Rarity"] = 2 },
            ["faith"] = new() { ["Rarity"] = 8, ["Logic"] = 4, ["Organic"] = 2 },
            ["culture"] = new() { ["Rarity"] = 6, ["Flexibility"] = 5, ["Logic"] = 3 },
            ["comfort"] = new() { ["Flexibility"] = 8, ["Organic"] = 5, ["Rarity"] = 2 },
            ["healing"] = new() { ["Organic"] = 9, ["Logic"] = 4, ["Rarity"] = 2 },
            ["mobility"] = new() { ["Flexibility"] = 9, ["Buoyancy"] = 5, ["Hardness"] = 2 },
        };

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

        public static float Compute(string axis, ResourceSpec s)
        {
            if (!Axis.TryGetValue(axis, out var t)) return 0;
            float sum = 0; foreach (var kv in t) sum += GetProp(s, kv.Key) * kv.Value;
            return sum;
        }

        // Здания: только "строительные" оси. war_melee/war_ranged — это оружие (предметы), не здания.
        public static BuildingType AxisToBuilding(string ax) => ax switch
        {
            "food" or "growth" => BuildingType.Farm,
            "shelter" or "comfort" => BuildingType.House,
            "storage" => BuildingType.Warehouse,
            "defense" or "war_siege" => BuildingType.Barracks,
            "mining" => BuildingType.MineShaft,
            "trade" => BuildingType.Market,
            "knowledge" => BuildingType.Library,
            "faith" or "healing" or "culture" => BuildingType.Temple,
            "mobility" => BuildingType.Bridge,
            _ => BuildingType.House
        };

        public static string AxisWord(string ax) => AxisItemWord(ax);
        public static string AxisItemWord(string ax) => ax switch
        {
            "food" => "plow",
            "growth" => "sickle",
            "shelter" => "tent",
            "storage" => "bin",
            "defense" => "shield",
            "war_melee" => "sword",
            "war_ranged" => "bow",
            "war_siege" => "ram",
            "mining" => "pick",
            "trade" => "coin",
            "knowledge" => "lens",
            "faith" => "idol",
            "culture" => "lyre",
            "comfort" => "hearth",
            "healing" => "salve",
            "mobility" => "cart",
            _ => "tool"
        };
        public static string AxisBuildingWord(string ax) => ax switch
        {
            "food" => "farm",
            "growth" => "field",
            "shelter" => "house",
            "storage" => "warehouse",
            "defense" => "fort",
            "war_siege" => "siege-work",
            "mining" => "mine",
            "trade" => "market",
            "knowledge" => "library",
            "faith" => "temple",
            "culture" => "hall",
            "comfort" => "lodge",
            "healing" => "hospice",
            "mobility" => "bridge",
            _ => "hall"
        };
        public static string AxisMethodWord(string ax) => ax switch
        {
            "food" => "husbandry",
            "growth" => "irrigation",
            "shelter" => "masonry",
            "storage" => "stockpiling",
            "defense" => "fortification",
            "war_melee" => "drill",
            "war_ranged" => "marksmanship",
            "war_siege" => "siegecraft",
            "mining" => "mining",
            "trade" => "barter",
            "knowledge" => "inquiry",
            "faith" => "ritual",
            "culture" => "arts",
            "comfort" => "hearthcraft",
            "healing" => "herbalism",
            "mobility" => "pathfinding",
            _ => "craft"
        };
        public static char AxisSymbol(string ax) => ax switch
        {
            "food" => 'F',
            "growth" => 'G',
            "shelter" => 'H',
            "storage" => 'S',
            "defense" => 'W',
            "war_melee" => 'M',
            "war_ranged" => 'R',
            "war_siege" => 'T',
            "mining" => 'K',
            "trade" => '$',
            "knowledge" => '?',
            "faith" => 'X',
            "culture" => 'A',
            "comfort" => 'Z',
            "healing" => '+',
            "mobility" => '#',
            _ => ' '
        };
    }
}
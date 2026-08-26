namespace GenesisEngine.Core
{
    public enum TerrainType { DeepWater, ShallowWater, Beach, Grassland, Forest, Hill, Mountain, Swamp, Desert, Tundra, Taiga, IcePeak }
    public enum ResourceType { Food, Wood, Stone, Fiber, Iron, Copper, Coal, Sulfur, Saltpeter, Clay, Crystal, Herb, Meat, Hide, Gold, Gem, Tin, Silver, Lead, Salt, Resin, Oil, Silicon, Silk }
    public enum AgentRole { None, Farmer, Scholar, Soldier, Trader, Builder, Leader, Artisan }

    public enum SignalType { Alarm, Food, Come, Danger, Trade, Help, Bond, Mourn, Celebrate }
    public enum LifeStage { Infant, Child, Adolescent, Adult, Elder }
    public enum Sex { Male, Female }
    public enum WeaponType { Fist, SharpStick, StoneAxe, Spear, Bow, BronzeSword, IronSword, Crossbow, Musket }
    public enum CreatureSpecies { Rabbit, Deer, Boar, Bison, Elephant, Wolf, Bear, Tiger, Crocodile, Vulture, Hyena, GiantSpider, Dragon, SeaSerpent, Troll }
    public enum CreatureBehavior { Herbivore, Predator, Scavenger, Monster }
    public enum BuildingType { None, House, Farm, LumberMill, Market, Barracks, Temple, Campfire, MineShaft, Foundry, BlastFurnace, Library, WaterMill, Windmill, PrintingPress, University, Observatory, Bank, Factory, Laboratory, SteamEngine, RailwayStation, SteelMill, Skyscraper, Bridge, Warehouse, TribalHall, VillageCouncil, TownHall, Capitol }
    public enum DiplomaticRelation { War, Neutral, NonAggressionPact, TradeAgreement, DefensePact, Alliance, Vassalage, Union }
    public enum CasusBelli { None, BorderDispute, ResourceWar, IdeologicalWar, Revenge, PreemptiveStrike, Betrayal, Expansion }
}
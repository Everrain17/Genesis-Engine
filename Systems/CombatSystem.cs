using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    // Структура для характеристик оружия (нужна для MaterialSystem.BestWeapon)
    public struct WeaponStats
    {
        public float Damage;
        public int Range;
        public float FearReduction;
        public float Siege;
        public string Name;
    }

    public static class CombatSystem
    {
        // Базовое оружие "голые руки"
        public static WeaponStats Fist => new WeaponStats
        {
            Damage = 1f,
            Range = 0,
            FearReduction = 0f,
            Siege = 0f,
            Name = "fist"
        };

        public static void Fight(Agent a, Agent b, Tile[,] world)
        {
            if (a == null || b == null) return;

            float aHard = a.Body.Inventory.Count > 0
                ? a.Body.Inventory.Max(o => o.GetProperties().GetValueOrDefault("Hardness", 0.1f))
                : 0.1f;

            float bHard = b.Body.Inventory.Count > 0
                ? b.Body.Inventory.Max(o => o.GetProperties().GetValueOrDefault("Hardness", 0.1f))
                : 0.1f;

            var aWeapon = KnowledgeSystem.BestWeapon(a);
            var bWeapon = KnowledgeSystem.BestWeapon(b);

            float aPower = aHard + aWeapon.Damage * 0.08f;
            float bPower = bHard + bWeapon.Damage * 0.05f;

            float attack =
                aPower *
                (0.5f + a.Body.Energy / 200f) *
                (0.7f + a.Genome.Aggression * 0.6f);

            float defense = 1f / (1f + bPower * 0.12f);

            float damage = attack * defense;

            b.Body.Health -= damage * 6f;
            b.Body.Energy -= damage * 4f;
            b.Fear += 12f;

            a.Body.Energy -= 4f;

            if (b.Body.Health <= 0)
            {
                b.LastAction = "Combat";
                b.RecordAction("Combat");
            }
        }

        public static bool Hunt(Agent a, Creature c)
        {
            if (a == null || c == null) return false;

            float toolHardness = a.Body.Inventory.Count > 0
                ? a.Body.Inventory.Max(o => o.GetProperties().GetValueOrDefault("Hardness", 0.1f))
                : 0.1f;

            var weapon = KnowledgeSystem.BestWeapon(a);

            float chance =
                0.25f +
                toolHardness * 0.35f +
                weapon.Damage * 0.02f +
                a.Genome.Aggression * 0.15f +
                a.Genome.Courage * 0.15f;

            if (c.Behavior == CreatureBehavior.Predator)
                chance -= 0.25f;

            chance -= c.Size * 0.03f;

            chance = Math.Clamp(chance, 0.02f, 0.95f);

            return RandomProvider.GetFloat() < chance;
        }
    }
}
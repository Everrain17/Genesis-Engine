using System;
using System.Collections.Generic;
using GenesisEngine.Core;
using GenesisEngine.Systems;
using GenesisEngine.World;

namespace GenesisEngine.Entities
{
    public class Creature
    {
        public Guid Id;
        public CreatureSpecies Species;
        public CreatureBehavior Behavior;
        public float Energy = 100, Age, MaxAge, Hunger, Fear, Aggression;
        public Vector2 Position;
        public float Size, Speed, Fertility, HerdInstinct;
        public WeaponType AttackType = WeaponType.Fist;


        public Creature(Vector2 pos, Random rng, CreatureSpecies species)
        {
            byte[] guidBytes = new byte[16];
            rng.NextBytes(guidBytes);
            Id = new Guid(guidBytes);
            Position = pos;
            Species = species;

            switch (species)
            {
                case CreatureSpecies.Rabbit:
                    Behavior = CreatureBehavior.Herbivore;
                    Size = 1f;
                    Speed = 1.5f;
                    Fertility = 0.9f;
                    MaxAge = 500;
                    break;

                case CreatureSpecies.Deer:
                    Behavior = CreatureBehavior.Herbivore;
                    Size = 3f;
                    Speed = 1.2f;
                    Fertility = 0.5f;
                    MaxAge = 1500;
                    HerdInstinct = 0.8f;
                    break;

                case CreatureSpecies.Boar:
                    Behavior = CreatureBehavior.Herbivore;
                    Size = 4f;
                    Speed = 0.8f;
                    Aggression = 0.6f;
                    MaxAge = 2000;
                    break;

                case CreatureSpecies.Wolf:
                    Behavior = CreatureBehavior.Predator;
                    Size = 3f;
                    Speed = 1.3f;
                    Aggression = 0.8f;
                    MaxAge = 2500;
                    HerdInstinct = 0.9f;
                    AttackType = WeaponType.SharpStick;
                    break;

                case CreatureSpecies.Bear:
                    Behavior = CreatureBehavior.Predator;
                    Size = 5f;
                    Speed = 0.7f;
                    Aggression = 0.9f;
                    MaxAge = 4000;
                    AttackType = WeaponType.StoneAxe;
                    break;

                case CreatureSpecies.Tiger:
                    Behavior = CreatureBehavior.Predator;
                    Size = 4f;
                    Speed = 1.2f;
                    Aggression = 0.85f;
                    MaxAge = 3000;
                    AttackType = WeaponType.StoneAxe;
                    break;

                default:
                    Behavior = CreatureBehavior.Herbivore;
                    Size = 2;
                    Speed = 1;
                    MaxAge = 2000;
                    break;
            }
        }

        public void Update(Tile[,] world, List<Agent> agents, List<Creature> creatures)
        {
            var rng = RandomProvider.GetRandom();

            Hunger += 0.05f;
            Age++;

            if (Energy <= 0)
                return;

            // Движение
            if (Energy > 10 && rng.NextDouble() < 0.3f)
            {
                int dx = rng.Next(-1, 2);
                int dy = rng.Next(-1, 2);

                int nx = Position.X + dx;
                int ny = Position.Y + dy;

                if (nx >= 0 && nx < world.GetLength(0) &&
                    ny >= 0 && ny < world.GetLength(1) &&
                    world[nx, ny].IsPassable)
                {
                    Position = new Vector2(nx, ny);
                    Energy -= 1;
                }
            }

            if (Hunger <= 30)
                return;

            if (Behavior == CreatureBehavior.Herbivore)
            {
                Tile tile = world[Position.X, Position.Y];
                float food = tile.Resources.GetValueOrDefault(ResourceType.Food, 0f);

                if (food > 0f)
                {
                    float eaten = Math.Min(Size, food);
                    tile.Resources[ResourceType.Food] = food - eaten;
                    Energy = Math.Min(100f, Energy + eaten * 3f);
                    Hunger = Math.Max(0f, Hunger - eaten * 5f);
                }
            }
            else if (Behavior == CreatureBehavior.Predator)
            {
                bool hunted = false;

                var nearbyAgents = SpatialGrid.GetNearby(Position, 1);

                foreach (var a in nearbyAgents)
                {
                    if (a == null)
                        continue;

                    if (a.Body.Health <= 0)
                        continue;

                    if (a.Position.Distance(Position) <= 1f)
                    {
                        a.LastAction = "Predated";

                        float damage = Size * (2f + Aggression);

                        a.Body.Health -= damage;
                        a.Body.Energy -= Size * 4f;
                        a.Fear += 40f;

                        Energy = Math.Min(100f, Energy + Size * 4f);
                        Hunger = Math.Max(0f, Hunger - 30f);

                        hunted = true;
                        break;
                    }
                }

                if (!hunted)
                {
                    foreach (var c in creatures)
                    {
                        if (c == null)
                            continue;

                        if (c.Behavior == CreatureBehavior.Herbivore &&
                            c.Position.Distance(Position) <= 1f)
                        {
                            c.Energy -= Size * 3f;
                            Energy = Math.Min(100f, Energy + Size * 2f);
                            Hunger = Math.Max(0f, Hunger - 20f);
                            break;
                        }
                    }
                }
            }
        }
    }
}
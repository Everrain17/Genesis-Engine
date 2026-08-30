using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.World;
using GenesisEngine.Systems.Physics;
using GenesisEngine.Systems;

namespace GenesisEngine.Entities
{
    public class Creature
    {
        public Guid Id;
        public Guid HerdId;          // Тег стаи/гнезда для распознавания "своих"
        public Sex BiologicalSex;
        public CreatureGenome Genome;
        public CreatureBehavior Behavior => Genome.CarnivoreDrive > 0.4f ? CreatureBehavior.Predator : CreatureBehavior.Herbivore;
        public float Size => Genome.Size;
        public float MaxAge => Genome.MaxAge;
        public float Speed => Genome.Speed;
        public float Fertility => Genome.Fertility;
        public float HerdInstinct => Genome.HerdInstinct;
        public float Aggression => Genome.Aggression;
        public float Energy = 100f, Hunger, Age;
        public Vector2 Position;
        public float MateCooldown;   // Задержка после размножения

        public Creature(Vector2 pos, Random rng, CreatureGenome baseGenome, Guid herdId, Sex sex)
        {
            byte[] guidBytes = new byte[16];
            rng.NextBytes(guidBytes);
            Id = new Guid(guidBytes);

            Position = pos;
            HerdId = herdId;
            BiologicalSex = sex;

            // Небольшая мутация при рождении даже для первого поколения
            Genome = baseGenome.Mutate(rng);
        }

        public void Update(Tile[,] world, List<Agent> agents, List<Creature> creatures)
        {
            var rng = RandomProvider.GetRandom();
            Age++;
            Hunger += 0.04f + (Genome.Size * 0.02f); // Большие тратят больше энергии

            if (Energy <= 0 || Age > MaxAge)
            {
                // === НОВОЕ: Создаём труп животного ===
                var corpse = new Corpse
                {
                    Id = Id,
                    Quantity = Genome.Size * 5f, // Чем больше животное, тем больше мяса
                    SpawnTick = Simulation.Instance.TotalTicks
                };

                var tile = Simulation.Instance.World[Position.X, Position.Y];
                tile.Corpses.Add(corpse);

                // Добавляем мясо как WorldObject
                tile.GroundObjects.Add(new WorldObject
                {
                    MaterialId = MaterialDB.GetFoodMaterialId(),
                    Quantity = corpse.Quantity,
                    Position = Position,
                    IsCorpse = true
                });

                return; // Помечено на удаление внешним циклом
            }

            // 1. ПРИОРИТЕТ: Размножение (если сыт, полон сил и созрел)
            if (Hunger < 30f && Energy > 80f && Age > Genome.MaxAge * 0.3f && MateCooldown <= 0f)
            {
                var mate = creatures.FirstOrDefault(c =>
                    c != this &&
                    c.BiologicalSex != BiologicalSex &&
                    c.HerdId == HerdId && // Предпочитает своих
                    c.Position.Distance(Position) <= 2f &&
                    c.Energy > 70f && c.Hunger < 40f && c.MateCooldown <= 0f);

                if (mate != null)
                {
                    // Эмерджентное размножение!
                    var childGenome = CreatureGenome.Combine(this.Genome, mate.Genome, rng);
                    var childSex = rng.NextDouble() < 0.5f ? Sex.Male : Sex.Female;

                    var child = new Creature(Position, rng, childGenome, HerdId, childSex);
                    creatures.Add(child); // Добавляем в общий список (Simulation обработает)

                    Energy -= 40f;
                    mate.Energy -= 40f;
                    MateCooldown = Genome.MaxAge * 0.2f; // Перерыв
                    mate.MateCooldown = Genome.MaxAge * 0.2f;
                    return; // Пропускаем остальной апдейт в этот тик
                }
            }

            if (MateCooldown > 0f) MateCooldown--;

            // 2. Движение (случайное блуждание или движение к цели)
            if (Energy > 10f && rng.NextDouble() < 0.4f * Genome.Speed)
            {
                int dx = rng.Next(-1, 2);
                int dy = rng.Next(-1, 2);
                int nx = Position.X + dx;
                int ny = Position.Y + dy;

                if (nx >= 0 && nx < world.GetLength(0) && ny >= 0 && ny < world.GetLength(1) && world[nx, ny].IsPassable)
                {
                    Position = new Vector2(nx, ny);
                    Energy -= 0.5f * Genome.Speed;
                }
            }

            // 3. Питание
            if (Hunger > 20f)
            {
                if (Genome.CarnivoreDrive > 0.4f) // ХИЩНИК
                {
                    // Приоритет 1: Травоядные животные (CarnivoreDrive < 0.4)
                    var preyCreature = creatures.FirstOrDefault(c =>
                        c != this && c.Genome.CarnivoreDrive < 0.4f && c.Position.Distance(Position) <= 1.5f && c.Energy > 0);

                    // Приоритет 2: Агенты (только если очень голоден или рядом нет травоядных)
                    var preyAgent = agents.FirstOrDefault(a =>
                        a.Body.Health > 0 && a.Position.Distance(Position) <= 1.5f);

                    if (preyCreature != null || (preyAgent != null && Hunger > 85f))
                    {
                        if (preyCreature != null)
                        {
                            // Атака травоядного
                            float damage = Genome.Size * (1f + Genome.Aggression) - preyCreature.Genome.Defense * 2f;
                            preyCreature.Energy -= Math.Max(5f, damage * 3f);
                            Energy = Math.Min(100f, Energy + Genome.Size * 3f);
                            Hunger = Math.Max(0f, Hunger - 40f);
                        }
                        else if (preyAgent != null)
                        {
                            // Атака агента
                            float damage = Genome.Size * (1f + Genome.Aggression);
                            preyAgent.Body.Health -= damage;
                            preyAgent.Body.Energy -= Genome.Size * 3f;
                            preyAgent.Fear += 30f;
                            preyAgent.LastAction = "Predated";

                            Energy = Math.Min(100f, Energy + Genome.Size * 4f);
                            Hunger = Math.Max(0f, Hunger - 50f);
                        }
                    }
                }
                else // ТРАВОЯДНОЕ / ВСЕЯДНОЕ
                {
                    Tile tile = world[Position.X, Position.Y];
                    float food = tile.Resources.GetValueOrDefault(ResourceType.Food, 0f);

                    if (food > 0f)
                    {
                        float eaten = Math.Min(Genome.Size, food);
                        tile.Resources[ResourceType.Food] = food - eaten;
                        Energy = Math.Min(100f, Energy + eaten * 2f);
                        Hunger = Math.Max(0f, Hunger - eaten * 4f);
                    }
                }
            }
        }
    }
}
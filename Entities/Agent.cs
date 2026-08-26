using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Systems;
using GenesisEngine.Systems.Physics;
using GenesisEngine.Systems.Biology;
using GenesisEngine.Systems.Behaviour;
using GenesisEngine.World;

namespace GenesisEngine.Entities
{
    public class Agent
    {
        public Guid Id;
        public int BirthTick, Generation;
        public Guid? MotherId, FatherId;
        public List<Guid> ChildrenIds = new();
        public Sex BiologicalSex;
        public Vector2 Position, HomePosition;
        public Guid? BondedPartner;
        public AgentGenome Genome;
        public MemorySystem Memory = new();
        public string CivilizationId;
        public AgentBody Body = new AgentBody();
        public AgentRole Role = AgentRole.None;
        public float Age, MaxAge;
        public float Fear, Curiosity, Loneliness, Despair;
        public string LastAction;
        public float Logic;

        public int EffectiveVision => (int)(Genome.BaseVision * 0.4f);
        public int EffectiveHearing => (int)(Genome.BaseHearing * 0.5f);

        public Agent(Vector2 pos, Random rng, int birthTick, int gen = 0)
        {
            byte[] guidBytes = new byte[16];
            rng.NextBytes(guidBytes);
            Id = new Guid(guidBytes);
            Position = HomePosition = pos;
            BirthTick = birthTick;
            Generation = gen;
            BiologicalSex = rng.NextDouble() < 0.5f ? Sex.Male : Sex.Female;
            Genome = AgentGenome.Random(rng);
            MaxAge = Genome.MaxAge;
            Curiosity = 0.2f + (float)rng.NextDouble() * 0.6f;
        }

        public void Update(Tile[,] world, List<Agent> agents, List<Creature> creatures)
        {
            Age++;

            if (Age > MaxAge)
            {
                Body.Health -= 0.5f;
                LastAction = "Age";
            }

            Body.Metabolize(1f);

            if (Body.Hunger >= 100f && Body.Health <= 0f)
                LastAction = "Hunger";

            Loneliness = Math.Min(100f, Loneliness + 0.02f);
            Fear = Math.Max(0f, Fear - 0.05f);

            if (Body.Hunger > 80f)
                Despair = Math.Min(100f, Despair + 0.04f);
            else
                Despair = Math.Max(0f, Despair - 0.02f);

            DetermineRole();

            if (Body.Health <= 0)
                return;

            var rng = RandomProvider.GetRandom();
            Tile currentTile = world[Position.X, Position.Y];
            // === НОВОЕ: Запуск когнитивных примитивов ===
            CognitivePrimitives.UpdateCognitivePrimitives(this);
            AdvancedCognitivePrimitives.Update(this, currentTile, rng);
            // === ПАКЕТ 9: высшие когнитивные примитивы ===
            HigherCognitivePrimitives.Update(this, currentTile, rng);
            if (Simulation.Instance.TotalTicks % 10 == 0)
                CognitionSystem.UpdateAgentLogic(this);

            // Эмерджентные сигналы потребностей
            AgentEmergence.EmitNeedsSignals(this, rng);
            LanguageSystem.TrySpeakContext(this, rng);
            GrammarSystem.TrySpeakGrammar(this, rng);

            // Реакция на чужие сигналы
            if (AgentEmergence.HandleSignals(this, rng))
                return;

            // Локальные соседи.
            // Используется и для одиночества, и для размножения.
            var nearby = SpatialGrid.GetNearby(Position, 2);

            if (nearby.Any(a => a.Id != Id && Memory.GetTrust(a.Id) > 0f))
            {
                Loneliness = Math.Max(0f, Loneliness - 1.5f);
            }

            // 1. Размножение
            if (Body.Hunger < 30 && Body.Energy > 50 &&
                Age > 500 && Age < MaxAge * 0.7f &&
                Genome.BondingDrive > 0.3f)
            {
                var partner = nearby.FirstOrDefault(a =>
                    a != null &&
                    a.Id != Id &&
                    a.Body.Health > 0f &&
                    a.BiologicalSex != BiologicalSex &&
                    a.Position.Distance(Position) <= 2 &&
                    a.Body.Hunger < 40 &&
                    a.Body.Energy > 40 &&
                    a.Age > 500 &&
                    a.Age < a.MaxAge * 0.7f);

                if (partner != null)
                {
                    float chance = Genome.Fertility * Genome.BondingDrive * 0.02f;

                    if (rng.NextDouble() < chance)
                    {
                        var mother = BiologicalSex == Sex.Female ? this : partner;
                        var father = BiologicalSex == Sex.Male ? this : partner;

                        // Определяем цивилизацию ребёнка
                        string childCivId = "";
                        if (!string.IsNullOrEmpty(mother.CivilizationId) && mother.CivilizationId == partner.CivilizationId)
                        {
                            // Оба родителя из одной цивилизации
                            childCivId = mother.CivilizationId;
                        }
                        else if (!string.IsNullOrEmpty(mother.CivilizationId))
                        {
                            // Только мать из цивилизации
                            childCivId = mother.CivilizationId;
                        }
                        else if (!string.IsNullOrEmpty(partner.CivilizationId))
                        {
                            // Только отец из цивилизации
                            childCivId = partner.CivilizationId;
                        }

                        var child = new Agent(Position, rng, Simulation.Instance.TotalTicks,
                            Math.Max(Generation, partner.Generation) + 1)
                        {
                            Genome = AgentGenome.Combine(mother.Genome, father.Genome, rng),
                            MotherId = mother.Id,
                            FatherId = father.Id,
                            CivilizationId = childCivId
                        };

                        KnowledgeSystem.InheritFromParents(child, mother, father);

                        Simulation.Instance.BornAgents.Add(child);
                        Simulation.Instance.TotalBorn++;

                        ChildrenIds.Add(child.Id);

                        Body.Energy -= 25f;
                        partner.Body.Energy -= 25f;
                        Loneliness = 0;

                        if (BondedPartner == null)
                            BondedPartner = partner.Id;
                    }
                }
            }

            // Если очень голоден и тащит неорганический мусор — выбросить его
            if (Body.Hunger > 50f)
            {
                var nonFood = Body.Inventory.FirstOrDefault(o =>
                    !MaterialDB.TryGet(o.MaterialId, out var spec) || spec.Organic <= 0.5f);

                if (nonFood != null)
                {
                    ManipulationSystem.Drop(this, nonFood, Position);
                }
            }

            // 2. Потребление еды из инвентаря
            if (Body.Hunger > 40f)
            {
                var foodItem = Body.Inventory.FirstOrDefault(o =>
                    MaterialDB.TryGet(o.MaterialId, out var spec) &&
                    spec.Organic > 0.5f &&
                    o.Quantity > 0f);

                if (foodItem != null)
                {
                    Body.Consume(foodItem, 1f);
                    ObservationSystem.RecordPattern(this, "Consume", foodItem, null, 50f);
                    CognitionSystem.Record("food.consume", 1f);

                    if (foodItem.Quantity <= 0f)
                        Body.Inventory.Remove(foodItem);

                    LastAction = "Consume";
                    return;
                }
            }

            // 3. Сбор еды
            bool hasFoodInInventory = Body.Inventory.Any(o =>
                MaterialDB.TryGet(o.MaterialId, out var spec) &&
                spec.Organic > 0.5f &&
                o.Quantity > 0f);

            if (Body.Hunger > 30f && !hasFoodInInventory)
            {
                float capacity = Body.MaxCarryWeight - Body.CurrentCarryWeight;

                var groundFood = currentTile.GroundObjects.FirstOrDefault(o =>
                    o.Quantity > 0.1f &&
                    MaterialDB.TryGet(o.MaterialId, out var spec) &&
                    spec.Organic > 0.5f);

                if (groundFood != null && capacity > 0.1f)
                {
                    float amount = Math.Min(5f, Math.Min(capacity, groundFood.Quantity));

                    if (ManipulationSystem.PickUp(this, groundFood, amount))
                    {
                        ObservationSystem.RecordPattern(this, "PickUp", groundFood, null, 10f);
                        LastAction = "PickUp";
                        return;
                    }
                }

                float tileFood = currentTile.Resources.GetValueOrDefault(ResourceType.Food, 0f);

                if (tileFood > 1f)
                {
                    currentTile.Resources[ResourceType.Food] = tileFood - 1f;
                    Body.Hunger = Math.Max(0f, Body.Hunger - 18f);
                    Body.Energy = Math.Min(100f, Body.Energy + 8f);
                    LastAction = "Forage";
                    return;
                }

                if (currentTile.GroundObjects.Count > 0 && rng.NextDouble() < 0.3f)
                {
                    var randomObj = currentTile.GroundObjects[rng.Next(currentTile.GroundObjects.Count)];

                    if (randomObj.Quantity > 0.1f && capacity > 0.1f)
                    {
                        float amount = Math.Min(1f, Math.Min(capacity, randomObj.Quantity));

                        if (ManipulationSystem.PickUp(this, randomObj, amount))
                        {
                            LastAction = "PickUp";
                            return;
                        }
                    }
                }
            }

            // 4. Крафт
            if (Body.Inventory.Count >= 2 && Curiosity > 0.3f && rng.NextDouble() < 0.1f)
            {
                var obj1 = Body.Inventory[0];
                var obj2 = Body.Inventory[1];

                if (obj1.Quantity >= 1f && obj2.Quantity >= 1f)
                {
                    var result = ManipulationSystem.Combine(this, obj1, obj2);

                    if (result != null)
                    {
                        ObservationSystem.RecordPattern(this, "Combine", obj1, obj2, 80f);
                        LastAction = "Combine";
                        return;
                    }
                }
            }

            // 5. Торговля
            if (Body.Inventory.Any(o => o.Quantity > 1f) &&
                rng.NextDouble() < 0.02f + Genome.Extraversion * 0.03f)
            {
                if (TradeSystem.AutoTrade(this))
                {
                    LastAction = "Trade";
                    return;
                }
            }

            // 6. Социальные действия: обучение, артефакты
            if (AgentEmergence.TrySocial(this, currentTile, rng))
                return;

            if (TeacherSystem.TryTeach(this, currentTile, rng))
                return;

            if (AgentCognition.TryLearnAndWrite(this, currentTile, rng))
                return;

            LogicExperimentSystem.TryExperiment(this, currentTile, rng);
            // 7. Охота
            if (AgentEmergence.TryHunt(this, currentTile, rng))
                return;

            // 8. Бой, если цивилизации в состоянии войны
            if (AgentEmergence.TryHostile(this, rng))
                return;

            // 9. Эмерджентное строительство
            if (AgentEmergence.TryBuild(this, currentTile, rng))
                return;

            // === ПАКЕТ 8: целевое движение по памяти ===
            if (AdvancedCognitivePrimitives.TryUseSpatialGoals(this, world, rng))
                return;

            // 10. Движение
            LastAction = "Move";

            int dx = rng.Next(-1, 2);
            int dy = rng.Next(-1, 2);

            if (dx == 0 && dy == 0)
                dx = 1;

            int nx = Position.X + dx;
            int ny = Position.Y + dy;

            if (nx >= 0 && nx < world.GetLength(0) && ny >= 0 && ny < world.GetLength(1))
            {
                Tile target = world[nx, ny];

                bool canEnter = target.IsPassable || CombinationEngine.CanCross(this, target.Terrain);
                if (!canEnter)
                    return;

                bool targetHasFood =
                    target.GroundObjects.Any(o =>
                        MaterialDB.TryGet(o.MaterialId, out var spec) &&
                        spec.Organic > 0.5f) ||
                    target.Resources.GetValueOrDefault(ResourceType.Food, 0f) > 10f;

                var tileAgents = SpatialGrid.GetNearby(new Vector2(nx, ny), 0);

                bool targetHasFriends = tileAgents.Any(a =>
                    a.Id != Id && Memory.GetTrust(a.Id) > 10);

                if (Body.Hunger > 20f && targetHasFood)
                {
                    Position = new Vector2(nx, ny);
                }
                else if (Loneliness > 30f && targetHasFriends)
                {
                    Position = new Vector2(nx, ny);
                    Loneliness = Math.Max(0f, Loneliness - 5f);
                }
                else if (rng.NextDouble() < 0.7f)
                {
                    Position = new Vector2(nx, ny);
                }
            }
        }

        private void DetermineRole()
        {
            if (Body.Inventory.Any(o =>
                MaterialDB.TryGet(o.MaterialId, out var spec) && spec.Hardness > 0.6f))
            {
                Role = AgentRole.Builder;
            }
            else if (Body.Inventory.Any(o =>
                MaterialDB.TryGet(o.MaterialId, out var spec) && spec.Organic > 0.8f))
            {
                Role = AgentRole.Farmer;
            }
            else
            {
                Role = AgentRole.None;
            }
        }
    }
}
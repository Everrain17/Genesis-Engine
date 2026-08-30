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
        public bool Infected;
        public float InfectionTimer;
        public string InfectedWith;                                // Тип текущего патогена (например, "marsh-fever")
        public Dictionary<string, float> PathogenImmunity = new(); // Приобретённый иммунитет (0.0 - 0.9)
        public Dictionary<string, int> PathogenSurvivals = new();  // Счётчик выживаний для генетической ассимиляции
        public string LastAction;
        public float Logic;
        public Dictionary<string, float> EnvironmentalCorrelations = new(); // Хеш окружения -> накопленный "урон"
        public float LastHealth = 100f;
        public float LastHunger = 0f;
        public float LastEnergy = 0f;
        public float LastDeductionTick = 0f;
        public int LastCorrelationTick = 0;
        public int LastEnvObservationTick = 0;
        // В классе Agent, после поля LastAction:
        public Dictionary<string, int> ActionHistory = new();  // Счётчик действий за последние N тиков
        public int ActionHistoryTick = 0;  // Когда последний раз очищали историю
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
                Body.Health -= 1.5f;
                LastAction = "Age";
            }



            var nearby = SpatialGrid.GetNearby(Position, 2);

            if (nearby.Any(a => a.Id != Id && Memory.GetTrust(a.Id) > 0f))
            {
                Loneliness = Math.Max(0f, Loneliness - 1.5f);
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


            if (Body.Health <= 0)
                return;

            var rng = RandomProvider.GetRandom();
            Tile currentTile = world[Position.X, Position.Y];
            // Эмерджентное наблюдение за влиянием среды на агента (раз в 50 тиков)
            if (Simulation.Instance.TotalTicks - LastEnvObservationTick > 50)
            {
                float healthDelta = Body.Health - LastHealth;
                float hungerDelta = Body.Hunger - LastHunger;

                CognitivePrimitives.ObserveEnvironment(this, currentTile, healthDelta, hungerDelta);

                LastHealth = Body.Health;
                LastHunger = Body.Hunger;
                LastEnvObservationTick = Simulation.Instance.TotalTicks;
            }
            // === v3: эпидемии ===
            EpidemicSystem.Update(this, currentTile, rng);
            if (Body.Health <= 0)
            {
                if (Infected) LastAction = "Plague";
                return;
            }
            // === НОВОЕ: Эмерджентное избегание болезней ===
            if (Simulation.Instance.TotalTicks % 50 == 0)
            {
                // Если агент здоров и замечал корреляции с болезнями
                if (!Infected && Genome.SelfAwareness > 0.5f)
                {
                    // Проверяем, есть ли больные рядом
                    int sickNearby = nearby.Count(a => a != this && a.Infected);

                    if (sickNearby > 0)
                    {
                        // Агент эмерджентно понимает опасность
                        CognitionSystem.Record("disease.avoidance_behavior", 1f);

                        // С шансом, зависящим от SelfAwareness, агент избегает больных
                        float avoidanceChance = Genome.SelfAwareness * 0.3f;
                        if (rng.NextDouble() < avoidanceChance)
                        {
                            // Двигаемся подальше от больных
                            Vector2 awayFromSick = CalculateDirectionAwayFromSick(nearby);
                            if (awayFromSick != Position)
                            {
                                Position = awayFromSick;
                                LastAction = "AvoidSick";
                            }
                        }
                    }
                }
            }
            CognitivePrimitives.UpdateCognitivePrimitives(this);
            AdvancedCognitivePrimitives.Update(this, currentTile, rng);
            HigherCognitivePrimitives.Update(this, currentTile, rng);
            if (Simulation.Instance.TotalTicks % 10 == 0)
                CognitionSystem.UpdateAgentLogic(this);

            AgentEmergence.EmitNeedsSignals(this, rng);
            LanguageSystem.TrySpeakContext(this, rng);
            GrammarSystem.TrySpeakGrammar(this, rng);

            if (AgentEmergence.HandleSignals(this, rng))
                return;

            // 1. Размножение
            // 1. Размножение
            // 1. Размножение
            if (Body.Hunger < 30 && Body.Energy > 50 && Genome.BondingDrive > 0.3f)
            {
                var partner = nearby.FirstOrDefault(a =>
                    a != null &&
                    a.Id != Id &&
                    a.Body.Health > 0f &&
                    a.BiologicalSex != BiologicalSex &&
                    a.Position.Distance(Position) <= 2 &&
                    a.Body.Hunger < 40 &&
                    a.Body.Energy > 40);

                if (partner != null)
                {
                    // === НОВОЕ: Реалистичная биологическая кривая фертильности ===
                    // Фертильность определяется по женскому агенту в паре, так как у мужчин нет резкого обрыва (менопаузы)
                    Agent female = this.BiologicalSex == Sex.Female ? this : partner;

                    // Нормализованный возраст: 0.0 = рождение, 1.0 = MaxAge

                    float normAge = female.Age / female.MaxAge;
                    float ageFertilityMultiplier = 0f;

                    if (normAge >= 0.15f && normAge < 0.25f)
                    {
                        // Ранняя фертильность (10% - 20% жизни): плавный разгон от 0 до 1.0
                        // (Например, при MaxAge=4000, это возраст 400-800 тиков)
                        ageFertilityMultiplier = (normAge - 0.10f) / 0.10f;
                    }
                    else if (normAge >= 0.25f && normAge < 0.45f)
                    {
                        // Пик фертильности (20% - 50% жизни): 100% базового шанса
                        ageFertilityMultiplier = 1.0f;
                    }
                    else if (normAge >= 0.45f && normAge < 0.70f)
                    {
                        // Постепенное снижение (50% - 70% жизни): падение с 1.0 до 0.3
                        // Дает хороший запас времени для размножения в зрелом возрасте
                        ageFertilityMultiplier = 1.0f - ((normAge - 0.50f) / 0.20f) * 0.7f;
                    }
                    else if (normAge >= 0.70f && normAge < 0.80f)
                    {
                        // "Длинный хвост" фертильности (70% - 85% жизни): падение с 0.3 до 0.0
                        // Критически важно для восстановления популяции после эпидемий!
                        ageFertilityMultiplier = 0.3f - ((normAge - 0.70f) / 0.15f) * 0.3f;
                    }
                    else
                    {
                        // Слишком юные (<10%) или глубокая старость (>85%)
                        ageFertilityMultiplier = 0f;
                    }

                    // Если биологический возраст не позволяет, просто пропускаем этот тик (не прерываем весь Update)
                    if (ageFertilityMultiplier > 0f)
                    {
                        // 2. Демографический переход (развитие цивилизации снижает рождаемость)
                        float civDevelopment = 1f;
                        if (!string.IsNullOrEmpty(CivilizationId))
                        {
                            var civ = Simulation.activeCivs?.FirstOrDefault(c => c.Id == CivilizationId);
                            if (civ != null)
                            {
                                float educationIndex = civ.EducationLevel;
                                float toolIndex = civ.AvgToolHardness;
                                float institutionIndex = Math.Min(1f, civ.EmergentStructuresCount / 20f);
                                civDevelopment = (educationIndex + toolIndex + institutionIndex) / 3f;
                            }
                        }

                        // 3. Итоговый шанс: базовая фертильность * биологический возраст * демографический переход
                        float chance = Genome.Fertility * Genome.BondingDrive * 0.02f * ageFertilityMultiplier / (1f + civDevelopment);

                        if (rng.NextDouble() < chance)
                        {
                            var mother = BiologicalSex == Sex.Female ? this : partner;
                            var father = BiologicalSex == Sex.Male ? this : partner;

                            string childCivId = "";
                            if (!string.IsNullOrEmpty(mother.CivilizationId) && mother.CivilizationId == partner.CivilizationId)
                                childCivId = mother.CivilizationId;
                            else if (!string.IsNullOrEmpty(mother.CivilizationId))
                                childCivId = mother.CivilizationId;
                            else if (!string.IsNullOrEmpty(partner.CivilizationId))
                                childCivId = partner.CivilizationId;

                            var child = new Agent(Position, rng, Simulation.Instance.TotalTicks, Math.Max(Generation, partner.Generation) + 1)
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
            }

            // Сброс лишнего груза, если агент голоден и тащит мусор
            if (Body.Hunger > 50f)
            {
                var nonFood = Body.Inventory.FirstOrDefault(o =>
                    !MaterialDB.TryGet(o.MaterialId, out var spec) || spec.Organic <= 0.5f);

                if (nonFood != null)
                {
                    ManipulationSystem.Drop(this, nonFood, Position);
                }
            }

            // 2. Потребление еды из инвентаря (если голод > 40)
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

                    ScatterSeed(currentTile, rng); // v3: поел — уронил семечко

                    LastAction = "Consume";
                    RecordAction("Consume");
                    return; // Завершаем тик, агент поел
                }
            }

            // 3. Сбор еды с земли или ресурсов тайла (если в инвентаре нет еды, но голод > 40)
            if (Body.Hunger > 40f)
            {
                float capacity = Body.MaxCarryWeight - Body.CurrentCarryWeight;

                // Сначала пытаемся подобрать объект с земли
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
                        currentTile.Exhaustion = Math.Min(0.9f, currentTile.Exhaustion + 0.01f);
                        currentTile.Fertility = Math.Max(0.05f, currentTile.Fertility * 0.995f);
                        LastAction = "PickUp";
                        RecordAction("PickUp");
                        return;
                    }
                }

                // Если подобрать не удалось или некуда, едим абстрактную еду тайла
                // ВАЖНО: Вернули порог > 1f, чтобы агент не ел по 0.1 единице каждый тик и не застревал в return
                float tileFood = currentTile.Resources.GetValueOrDefault(ResourceType.Food, 0f);
                if (tileFood > 1f)
                {
                    currentTile.Resources[ResourceType.Food] = tileFood - 1f;
                    Body.Hunger = Math.Max(0f, Body.Hunger - 18f);
                    Body.Energy = Math.Min(100f, Body.Energy + 8f);
                    ScatterSeed(currentTile, rng);
                    currentTile.Exhaustion = Math.Min(0.9f, currentTile.Exhaustion + 0.02f);
                    LastAction = "Forage";
                    RecordAction("Forage");
                    return;
                }
            }
            // === НОВОЕ: Добыча из шахт ===
            if (currentTile.IsMine && Body.Inventory.Count < 5)
            {
                WorldObject oreDeposit = null;

                // Используем sDx, sDy, sNx, sNy, чтобы не конфликтовать с dx, dy, nx, ny для движения внизу метода
                for (int sDx = -2; sDx <= 2; sDx++)
                {
                    for (int sDy = -2; sDy <= 2; sDy++)
                    {
                        int sNx = currentTile.X + sDx;
                        int sNy = currentTile.Y + sDy;

                        if (sNx >= 0 && sNx < world.GetLength(0) && sNy >= 0 && sNy < world.GetLength(1))
                        {
                            var neighbor = world[sNx, sNy];
                            oreDeposit = neighbor.GroundObjects.FirstOrDefault(o => o.IsOreDeposit && o.Quantity > 0.1f);
                            if (oreDeposit != null) break;
                        }
                    }
                    if (oreDeposit != null) break;
                }

                if (oreDeposit != null)
                {
                    float capacity = Body.MaxCarryWeight - Body.CurrentCarryWeight;
                    if (capacity > 0.5f)
                    {
                        float amount = Math.Min(2f, Math.Min(capacity, oreDeposit.Quantity));

                        // Создаём новый объект с тем же материалом
                        var minedOre = new WorldObject
                        {
                            MaterialId = oreDeposit.MaterialId,
                            Quantity = amount,
                            HolderId = Id,
                            Position = null
                        };

                        oreDeposit.Quantity -= amount;

                        // Если руда на тайле закончилась, удаляем её оттуда
                        if (oreDeposit.Quantity <= 0f && oreDeposit.Position.HasValue)
                        {
                            var tile = world[(int)oreDeposit.Position.Value.X, (int)oreDeposit.Position.Value.Y];
                            tile.GroundObjects.Remove(oreDeposit);
                        }

                        Body.Inventory.Add(minedOre);
                        LastAction = "Mine";
                        RecordAction("Mine");
                        return; // Прерываем Update, агент успешно добыл ресурс в этом тике
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
                        RecordAction("Combine");
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
                    RecordAction("Trade");
                    return;
                }
            }

            // 6. Социальные действия
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

            // 8. Бой + v3: рейды
            if (AgentEmergence.TryHostile(this, rng))
                return;

            if (AgentEmergence.TryRaid(this, rng))
                return;

            // 9. Эмерджентное строительство
            if (AgentEmergence.TryBuild(this, currentTile, rng))
                return;

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
        private Vector2 CalculateDirectionAwayFromSick(List<Agent> nearby)
        {
            float avgX = 0f, avgY = 0f;
            int sickCount = 0;

            foreach (var a in nearby)
            {
                if (a != this && a.Infected)
                {
                    avgX += a.Position.X;
                    avgY += a.Position.Y;
                    sickCount++;
                }
            }

            if (sickCount == 0) return Position;

            avgX /= sickCount;
            avgY /= sickCount;

            // Двигаемся в противоположном направлении
            int dx = Math.Sign(Position.X - avgX);
            int dy = Math.Sign(Position.Y - avgY);

            int newX = Math.Clamp(Position.X + dx, 0, Simulation.Instance.World.GetLength(0) - 1);
            int newY = Math.Clamp(Position.Y + dy, 0, Simulation.Instance.World.GetLength(1) - 1);

            return new Vector2(newX, newY);
        }
        private void RecordAction(string action)
        {
            if (string.IsNullOrEmpty(action) || action == "Move") return;

            // Очищаем историю каждые 500 тиков
            if (Simulation.Instance.TotalTicks - ActionHistoryTick > 500)
            {
                ActionHistory.Clear();
                ActionHistoryTick = Simulation.Instance.TotalTicks;
            }

            if (!ActionHistory.ContainsKey(action))
                ActionHistory[action] = 0;
            ActionHistory[action]++;
        }
        // v3: поел — уронил семечко (побочный продукт, не «изобретение»)
        private void ScatterSeed(Tile tile, Random rng)
        {
            if (tile == null) return;
            if (rng.NextDouble() < 0.12f)
            {
                tile.GroundObjects.Add(new WorldObject
                {
                    MaterialId = MaterialDB.GetFoodMaterialId(),
                    Quantity = 0.5f,
                    Position = new Vector2(tile.X, tile.Y),
                    IsSeed = true
                });
            }
        }


    }
}
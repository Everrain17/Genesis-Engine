using System;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.Systems.Physics;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class AgentEmergence
    {
        public static void EmitNeedsSignals(Agent a, Random rng)
        {
            if (a == null) return;

            float hunger = a.Body.Hunger / 100f;
            float fear = a.Fear / 100f;
            float loneliness = a.Loneliness / 100f;

            if (a.Body.Hunger > 80f && rng.NextDouble() < 0.03f + a.Genome.Extraversion * 0.05f)
                SignalSystem.EmitSignal(a, SignalType.Food, hunger, 8f);

            if (a.Body.Health < 35f && rng.NextDouble() < 0.02f + a.Genome.Neuroticism * 0.04f)
                SignalSystem.EmitSignal(a, SignalType.Help, 0.8f, 8f);

            if (a.Fear > 60f && rng.NextDouble() < 0.08f + a.Genome.Neuroticism * 0.08f)
                SignalSystem.EmitSignal(a, SignalType.Alarm, fear, 10f);

            if (a.Loneliness > 70f && rng.NextDouble() < 0.02f + a.Genome.BondingDrive * 0.04f)
                SignalSystem.EmitSignal(a, SignalType.Bond, loneliness, 12f);

            if (a.LastAction == "Predated" && rng.NextDouble() < 0.5f)
                SignalSystem.EmitSignal(a, SignalType.Danger, 0.9f, 10f);

            if (a.Body.Inventory.Count > 1 &&
                a.Genome.Extraversion > 0.55f &&
                rng.NextDouble() < 0.015f + a.Genome.Openness * 0.02f)
                SignalSystem.EmitSignal(a, SignalType.Trade, 0.5f, 8f);

            // v3: праздник — сытый агент в урожайный сезон празднует
            var season = SeasonSystem.GetCurrentSeason(Simulation.Instance.TotalTicks);
            if (a.Body.Hunger < 20f &&
                (season == SeasonSystem.Season.Summer || season == SeasonSystem.Season.Autumn) &&
                rng.NextDouble() < 0.02f + a.Genome.Extraversion * 0.02f)
                SignalSystem.EmitSignal(a, SignalType.Celebrate, 0.7f, 10f);
        }

        public static bool HandleSignals(Agent a, Random rng)
        {
            var heard = SignalSystem.Listen(a);
            if (heard.Count == 0) return false;

            bool acted = false;

            foreach (var entry in heard)
            {
                var signal = entry.signal;
                float clarity = entry.clarity;

                LanguageSystem.ObserveReception(a, signal, clarity);
                GrammarSystem.ObserveReception(a, signal, clarity);

                var response = SignalSystem.InterpretSignal(a, signal, clarity);

                GrammarSystem.ModifyResponse(a, signal, clarity, ref response);

                a.Fear = Math.Clamp(a.Fear + response.Alert * 10f, 0f, 100f);

                if (response.Flee > 0.45f && a.Fear > 25f)
                {
                    if (MoveAwayFrom(a, signal.Origin))
                    {
                        a.LastAction = "FleeSignal";
                        return true;
                    }
                }

                if (response.Approach > 0.45f)
                {
                    if (MoveToward(a, signal.Origin))
                    {
                        a.LastAction = "ApproachSignal";
                        acted = true;
                    }
                }

                if (response.Social > 0.35f)
                {
                    a.Loneliness = Math.Max(0f, a.Loneliness - response.Social * 4f);

                    float emotional = response.Social * 2f;
                    a.Memory.UpdateAgentMemory(signal.SenderId, "Signal", emotional);
                }
            }

            return acted;
        }

        public static bool TrySocial(Agent a, Tile tile, Random rng)
        {
            var nearby = SpatialGrid.GetNearby(a.Position, 7);

            foreach (var other in nearby)
            {
                if (other.Id == a.Id) continue;
                if (other.Body.Health <= 0) continue;

                float trust = a.Memory.GetTrust(other.Id);

                float teachChance =
                    0.02f +
                    a.Genome.Extraversion * 0.03f +
                    a.Genome.SelfAwareness * 0.03f;

                if (trust > 0f && rng.NextDouble() < teachChance)
                {
                    KnowledgeSystem.TryTeach(a, other);

                    EventBus.Publish(new SimEvent
                    {
                        Type = SimEventType.KnowledgeTaught,
                        Tick = Simulation.Instance.TotalTicks,
                        Actor = a,
                        Target = other,
                        Position = a.Position
                    });
                }
            }

            if (a.Genome.SelfAwareness > 0.55f && rng.NextDouble() < 0.008f)
            {
                var rareItem = a.Body.Inventory.FirstOrDefault(o =>
                    o.Quantity >= 1f &&
                    MaterialDB.TryGet(o.MaterialId, out var spec) &&
                    spec.Rarity > 0.6f);

                if (rareItem != null)
                {
                    rareItem.Quantity -= 1f;
                    if (rareItem.Quantity <= 0f)
                        a.Body.Inventory.Remove(rareItem);

                    var artifact = CultureSystem.CreateArtifact(a, tile, rareItem.MaterialId);

                    if (artifact != null)
                    {
                        a.LastAction = "CreateArtifact";

                        EventBus.Publish(new SimEvent
                        {
                            Type = SimEventType.ArtifactCreated,
                            Tick = Simulation.Instance.TotalTicks,
                            Actor = a,
                            Position = a.Position,
                            Data = artifact.Name,
                            Value = artifact.CulturalValue
                        });

                        return true;
                    }
                }
            }

            return false;
        }

        public static bool TryHunt(Agent a, Tile tile, Random rng)
        {
            if (a.Body.Hunger <= 55f) return false;
            if (a.Genome.Aggression < 0.2f && a.Genome.Courage < 0.3f) return false;

            var creature = Simulation.Instance.Creatures
                .FirstOrDefault(c =>
                    c.Energy > 0f &&
                    c.Position.Distance(a.Position) <= 1f &&
                    c.Behavior != CreatureBehavior.Predator);

            if (creature == null) return false;

            if (!CombatSystem.Hunt(a, creature))
            {
                a.Fear = Math.Clamp(a.Fear + 8f, 0f, 100f);
                a.LastAction = "HuntFail";
                return false;
            }

            creature.Energy = 0f;

            var meat = new WorldObject
            {
                MaterialId = MaterialDB.GetFoodMaterialId(),
                Quantity = Math.Max(2f, creature.Size * 3f),
                Position = a.Position
            };

            tile.GroundObjects.Add(meat);

            EventBus.Publish(new SimEvent
            {
                Type = SimEventType.Hunt,
                Tick = Simulation.Instance.TotalTicks,
                Actor = a,
                Position = a.Position,
                Data = creature.Species.ToString(),
                Value = meat.Quantity
            });

            a.LastAction = "Hunt";
            return true;
        }

        public static bool TryHostile(Agent a, Random rng)
        {
            if (string.IsNullOrEmpty(a.CivilizationId))
                return false;

            if (a.Genome.Aggression < 0.35f && a.Fear > 40f)
                return false;

            float warPressure = DiplomacySystem.WarPressure(a.CivilizationId);
            float peace = DiplomacySystem.PeaceStability(a.CivilizationId);

            // Если цивилизация долго жила в мире, она менее склонна к агрессии.
            if (peace > 0.85f && rng.NextDouble() < 0.50f)
                return false;

            if (string.IsNullOrEmpty(a.CivilizationId)) return false;
            if (!DiplomacySystem.IsAtWar(a.CivilizationId)) return false;

            var nearby = SpatialGrid.GetNearby(a.Position, 25);

            foreach (var other in nearby)
            {
                if (other.Id == a.Id)
                    continue;

                if (other.Body.Health <= 0)
                    continue;

                if (string.IsNullOrEmpty(other.CivilizationId))
                    continue;

                if (other.CivilizationId == a.CivilizationId)
                    continue;

                var state = DiplomacySystem.GetState(a.CivilizationId, other.CivilizationId);

                if (state != DiplomaticRelation.War)
                    continue;

                float attackChance =
                    0.10f +
                    a.Genome.Aggression * 0.20f +
                    a.Genome.Courage * 0.08f;

                // Долгая война истощает цивилизацию и снижает агрессию.
                attackChance *= 1f - Math.Clamp(warPressure * 0.30f, 0f, 0.50f);

                if (peace > 0.40f)
                    attackChance *= 0.50f;

                if (rng.NextDouble() < attackChance)
                {
                    other.LastAction = "Combat";

                    CombatSystem.Fight(a, other, Simulation.Instance.World);
                    // НОВОЕ: регистрируем потери
                    if (other.Body.Health <= 0)
                        DiplomacySystem.RecordLoss(other.CivilizationId);
                    EventBus.Publish(new SimEvent
                    {
                        Type = SimEventType.Combat,
                        Tick = Simulation.Instance.TotalTicks,
                        Actor = a,
                        Target = other,
                        Position = a.Position
                    });

                    a.LastAction = "Attack";

                    return true;
                }
            }

            return false;
        }

        public static bool TryBuild(Agent a, Tile tile, Random rng)
        {
            if (tile == null) return false;
            if (tile.Building != BuildingType.None) return false;
            if (!tile.IsPassable) return false;
            if (a.Body.Inventory.Count < 2) return false;

            bool builderRole = a.Role == AgentRole.Builder;
            bool highConscientiousness = a.Genome.Conscientiousness > 0.6f;

            if (!builderRole && !highConscientiousness)
                return false;

            float chance = 0.05f + a.Genome.Conscientiousness * 0.08f;
            float seasonMod = SeasonSystem.GetBuildingModifier(Simulation.Instance.CurrentSeason);
            chance *= seasonMod;
            if (a.Body.Hunger > 50f) chance += 0.24f;
            if (rng.NextDouble() > chance) return false;

            return ConstructionSystem.TryBuild(a, tile, rng);
        }

        private static bool MoveToward(Agent a, Vector2 target)
        {
            int dx = (int)Math.Sign((double)(target.X - a.Position.X)); 
            int dy = (int)Math.Sign((double)(target.Y - a.Position.Y));

            if (dx != 0 && TryStep(a, a.Position.X + dx, a.Position.Y))
                return true;

            if (dy != 0 && TryStep(a, a.Position.X, a.Position.Y + dy))
                return true;

            return false;
        }
        public static bool TryRaid(Agent a, Random rng)
        {
            if (string.IsNullOrEmpty(a.CivilizationId)) return false;
            if (!DiplomacySystem.IsAtWar(a.CivilizationId)) return false;
            if (a.Genome.Aggression < 0.5f || a.Fear > 60f) return false;
            var target = DiplomacySystem.GetWarTarget(a.CivilizationId);
            if (target == null) return false;
            if (a.Position.Distance(target.Value) <= 8f) return false; // уже на фронте — дерётся TryHostile
            if (rng.NextDouble() < 0.25f + a.Genome.Aggression * 0.25f)
            {
                if (MoveToward(a, target.Value))
                {
                    a.LastAction = "Raid";
                    if (rng.NextDouble() < 0.05f)   // прореживание, иначе спам на тысячи строк
                        EventBus.Publish(new SimEvent
                        {
                            Type = SimEventType.Raid,
                            Tick = Simulation.Instance.TotalTicks,
                            Actor = a,
                            Position = a.Position
                        });
                    return true;
                }
            }
            return false;
        }

        private static bool MoveAwayFrom(Agent a, Vector2 source)
        {
            int dx = Math.Sign(a.Position.X - source.X);
            int dy = Math.Sign(a.Position.Y - source.Y);

            if (dx == 0 && dy == 0)
                dx = 1;

            if (dx != 0 && TryStep(a, a.Position.X + dx, a.Position.Y))
                return true;

            if (dy != 0 && TryStep(a, a.Position.X, a.Position.Y + dy))
                return true;

            return false;
        }

        private static bool TryStep(Agent a, int x, int y)
        {
            var world = Simulation.Instance.World;
            if (world == null) return false;

            if (x < 0 || y < 0 || x >= world.GetLength(0) || y >= world.GetLength(1))
                return false;

            var tile = world[x, y];

            bool canEnter = tile.IsPassable || CombinationEngine.CanCross(a, tile.Terrain);
            if (!canEnter) return false;

            a.Position = new Vector2(x, y);
            return true;
        }
    }
}
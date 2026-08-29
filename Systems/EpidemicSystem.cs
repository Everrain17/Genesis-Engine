using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.UI;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public class Pathogen
    {
        public string Id;
        public string Type;        // тип болезни, привязан к биому
        public string Name;
        public float Virulence;
        public float Contagiousness;
        public float Drain;
        public float Duration;
        public TerrainType Affinity;
        public int BirthTick;
        public int TotalInfected;
        public int TotalDied;
    }

    public static class EpidemicSystem
    {
        private static readonly Dictionary<string, Pathogen> Pathogens = new();
        private static int _counter;

        public static float SparkChance = 0.02f;

        public static Pathogen GetPathogen(string id) =>
            string.IsNullOrEmpty(id) ? null : Pathogens.GetValueOrDefault(id);

        // ============================================================
        // СПАВН
        // ============================================================
        public static void TrySpark(Simulation sim, Random rng)
        {
            if (sim.Agents.Count < 50) return;
            if (rng.NextDouble() >= SparkChance) return;

            var victim = sim.Agents[rng.Next(sim.Agents.Count)];
            if (victim.Infected) return;

            var terrain = sim.World[victim.Position.X, victim.Position.Y].Terrain;
            var p = GeneratePathogen(terrain, rng);
            Pathogens[p.Id] = p;
            Infect(victim, p);

            FileLogger.Log(
                $"[TICK {sim.TotalTicks}] PATHOGEN '{p.Name}' ({p.Type}) emerged in {terrain}: " +
                $"vir={p.Virulence:F2}, cont={p.Contagiousness:F2}",
                FileLogger.LogLevel.Death);

            EventBus.Publish(new SimEvent
            {
                Type = SimEventType.PlagueStarted,
                Tick = sim.TotalTicks,
                Actor = victim,
                Position = victim.Position,
                Data = p.Name
            });
        }

        private static Pathogen GeneratePathogen(TerrainType terrain, Random rng)
        {
            string type = terrain switch
            {
                TerrainType.Swamp => "marsh-fever",
                TerrainType.Desert => "desert-pox",
                TerrainType.Tundra => "tundra-chill",
                TerrainType.IcePeak => "ice-chill",
                TerrainType.Forest => "forest-rot",
                TerrainType.Taiga => "taiga-rot",
                _ => "plains-pox"
            };

            int n = _counter++;
            var p = new Pathogen
            {
                Id = $"P_{n:000}",
                Type = type,
                Name = $"{type}-{n:000}",
                Affinity = terrain,
                BirthTick = Simulation.Instance.TotalTicks,
                Duration = 150f + rng.Next(150)
            };

            float contBonus = (terrain == TerrainType.Swamp || terrain == TerrainType.Forest) ? 0.05f : 0f;
            float virBonus = (terrain == TerrainType.Desert || terrain == TerrainType.Tundra || terrain == TerrainType.IcePeak) ? 0.10f : 0f;

            p.Virulence = Math.Clamp(0.10f + virBonus + (float)rng.NextDouble() * 0.20f, 0.05f, 0.50f);
            p.Contagiousness = Math.Clamp(0.08f + contBonus + (float)rng.NextDouble() * 0.06f, 0.02f, 0.20f);
            p.Drain = 0.02f + p.Virulence * 0.15f;
            return p;
        }

        public static void Infect(Agent agent, Pathogen p)
        {
            agent.Infected = true;
            agent.InfectedWith = p.Type;
            agent.InfectionTimer = 0f;
            p.TotalInfected++;
        }

        // ============================================================
        // ОСНОВНОЙ UPDATE
        // ============================================================
        public static void Update(Agent agent, Tile tile, Random rng)
        {
            if (agent == null || agent.Body.Health <= 0) return;

            // ---------- БОЛЕН ----------
            if (agent.Infected)
            {
                var p = GetPathogen(agent.InfectedWith);
                if (p == null) { agent.Infected = false; agent.InfectedWith = null; return; }

                agent.InfectionTimer += 1f;

                float biomeMul = (tile != null && tile.Terrain == p.Affinity) ? 1.5f : 1f;
                float medicine = MedicineLevel(agent, tile);
                float specificImmunity = agent.PathogenImmunity.GetValueOrDefault(p.Type, 0f);

                // НОВОЕ: Проверяем генетическую устойчивость (наследственную)
                bool hasGeneticResistance = agent.Genome.GeneticResistances.Contains(p.Type);
                float geneticFactor = hasGeneticResistance ? 0.5f : 0f;

                // Урон (Генетика снижает урон на 50%)
                float damage = p.Drain * biomeMul
                    * (1f - medicine * 0.6f)
                    * (1f - specificImmunity * 0.5f)
                    * (1f - geneticFactor * 0.5f);

                agent.Body.Health -= damage;
                agent.Body.Energy = Math.Max(0f, agent.Body.Energy - 0.6f);

                if (agent.Fear < 50f && rng.NextDouble() < 0.04f)
                    SignalSystem.EmitSignal(agent, SignalType.Help, 0.9f, 6f);

                if (agent.Body.Health <= 0f)
                {
                    agent.LastAction = "Plague";
                    p.TotalDied++;
                    return;
                }

                // Выздоровление или смерть
                if (agent.InfectionTimer >= p.Duration)
                {
                    // Летальность
                    float lethal = p.Virulence
                        * (1f - agent.Genome.ImmuneStrength * 0.4f)
                        * (1f - medicine * 0.5f)
                        * (1f - specificImmunity * 0.3f)
                        * (1f - geneticFactor * 0.5f);

                    if (rng.NextDouble() < lethal)
                    {
                        agent.Body.Health = 0f;
                        agent.LastAction = "Plague";
                        p.TotalDied++;
                    }
                    else
                    {
                        agent.Infected = false;
                        agent.InfectedWith = null;

                        // 1. Приобретённый иммунитет (антитела): +0.3, кап 0.9
                        float current = agent.PathogenImmunity.GetValueOrDefault(p.Type, 0f);
                        agent.PathogenImmunity[p.Type] = Math.Min(0.9f, current + 0.3f);

                        // 2. ГЕНЕТИЧЕСКАЯ АССИМИЛЯЦИЯ (Эффект Болдуина)
                        int survivals = agent.PathogenSurvivals.GetValueOrDefault(p.Type, 0) + 1;
                        agent.PathogenSurvivals[p.Type] = survivals;

                        if (!hasGeneticResistance)
                        {
                            // Шанс записи в геном: 1 - (0.5 ^ survivals)
                            // 1-е выживание: 50%, 2-е: 75%, 3-е: 87.5%
                            float assimilationChance = 1f - MathF.Pow(0.5f, survivals);

                            if (rng.NextDouble() < assimilationChance)
                            {
                                agent.Genome.GeneticResistances.Add(p.Type);
                                agent.Genome.GeneticResistances.Sort(); // Детерминизм!

                                FileLogger.Log(
                                    $"[TICK {Simulation.Instance.TotalTicks}] GENETIC ASSIMILATION: Agent {agent.Id} lineage adapted to '{p.Type}' (survivals: {survivals})",
                                    FileLogger.LogLevel.Info);
                            }
                        }

                        agent.Body.Health = Math.Min(100f, agent.Body.Health + 15f);
                        agent.Fear = Math.Max(0f, agent.Fear - 20f);
                    }
                }
                return;
            }

            if (!agent.Infected)
            {
                // ---------- ЗДОРОВ ----------
                var nearby = SpatialGrid.GetNearby(agent.Position, 2);
                Agent carrier = null;
                foreach (var other in nearby)
                {
                    if (other != agent && other.Infected) { carrier = other; break; }
                }
                if (carrier == null) return;

                var p = GetPathogen(carrier.InfectedWith);
                if (p == null) return;

                float biomeMul = (tile != null && tile.Terrain == p.Affinity) ? 1.5f : 0.8f;
                float medicine = MedicineLevel(agent, tile);

                float immuneStrength = agent.Genome.ImmuneStrength;
                float specificImmunity = agent.PathogenImmunity.GetValueOrDefault(p.Type, 0f);

                bool hasGeneticResistance = agent.Genome.GeneticResistances.Contains(p.Type);
                float geneticFactor = hasGeneticResistance ? 0.5f : 0f;

                // Шанс заражения (Генетика сильно снижает шанс подхватить вирус)
                float chance = p.Contagiousness * biomeMul
                    * (1f - medicine * 0.5f)
                    * (1f - immuneStrength * 0.5f)
                    * (1f - specificImmunity * 0.7f)
                    * (1f - geneticFactor);

                if (rng.NextDouble() < chance)
                    Infect(agent, p);
            }
            
        }

        // ============================================================
        // МЕДИЦИНА
        // ============================================================
        private static float MedicineLevel(Agent agent, Tile tile)
        {
            float medicine = 0f;
            if (tile != null && tile.BuildingFunctional)
            {
                // Исправлено: Hospice нет в Enums, используем Temple или ось healing
                if (tile.Building == BuildingType.Temple || tile.DominantAxis == "healing")
                    medicine += 0.3f + tile.BuildingQuality * 0.1f;
            }
            medicine += KnowledgeSystem.MethodBuff(agent, "healing") * 0.5f;
            return Math.Clamp(medicine, 0f, 0.8f);
        }

        public static float GetHerdImmunity(List<Agent> agents)
        {
            if (agents == null || agents.Count == 0) return 0f;
            // Учитываем и приобретенный, и генетический иммунитет
            int immuneCount = agents.Count(a => a.PathogenImmunity.Count > 0 || a.Genome.GeneticResistances.Count > 0);
            return (float)immuneCount / agents.Count;
        }

        public static void CleanupOutbreaks(int currentTick)
        {
            if (currentTick % 1000 != 0) return;
            var dead = Pathogens.Values
                .Where(p => currentTick - p.BirthTick > 6000)
                .Select(p => p.Id)
                .ToList();
            foreach (var id in dead) Pathogens.Remove(id);
        }
    }
}
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
        public string Type;
        public string Name;
        public float Virulence;
        public float Contagiousness;
        public float Duration;
        public TerrainType Affinity;
        public int BirthTick;
        public int TotalInfected;
        public int TotalDied;

        // === НОВОЕ: Отслеживаем, какие гены мутировал этот штамм ===
        public Dictionary<string, int> MutatedGenes = new();
    }

    public static class EpidemicSystem
    {
        private static readonly Dictionary<string, Pathogen> ActivePathogens = new();
        private static int _counter;

        public static float SparkChance = 0.04f;

        public static Pathogen GetPathogen(string id) =>
            string.IsNullOrEmpty(id) ? null : ActivePathogens.GetValueOrDefault(id);
        private static int _sparkAttempts;   // сколько раз бросали кубик шанса
        private static int _sparkSuccesses;  // сколько раз вирус реально родился

        /// <summary>Логгер читает и обнуляет раз в окно логирования.</summary>
        public static void ReadSparkStats(out int attempts, out int successes)
        {
            attempts = _sparkAttempts;
            successes = _sparkSuccesses;
            _sparkAttempts = 0;
            _sparkSuccesses = 0;
        }

        /// <summary>
        /// Рассчитывает множитель вирулентности в зависимости от температуры.
        /// 0.0 = экстремальный холод, 1.0 = экстремальная жара.
        /// Идеальное окно для вируса: от 0.3 до 0.7.
        /// </summary>
        private static float GetTemperatureVirulenceMultiplier(float normalizedTemperature)
        {
            if (normalizedTemperature < 0.3f)
            {
                // Плавное падение от 0.0 (при 0.0) до 1.0 (при 0.3)
                return normalizedTemperature / 0.3f;
            }
            else if (normalizedTemperature > 0.7f)
            {
                // Плавное падение от 1.0 (при 0.7) до 0.0 (при 1.0)
                return (1.0f - normalizedTemperature) / 0.3f;
            }
            else
            {
                // Идеальные условия (0.3 - 0.7): множитель 100%
                return 1.0f;
            }
        }

        private static Pathogen GeneratePathogen(TerrainType terrain, Random rng, float currentTemp)
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

            float baseVirulence = 0.10f + virBonus + (float)rng.NextDouble() * 0.20f;
            float baseContagiousness = 0.08f + contBonus + (float)rng.NextDouble() * 0.06f;

            // === ИСПРАВЛЕНИЕ: Применяем температурный множитель к характеристикам вируса ===
            float tempMultiplier = GetTemperatureVirulenceMultiplier(currentTemp);

            // Вирулентность и заразность падают, если температура выходит за пределы 0.3-0.7
            p.Virulence = Math.Clamp(baseVirulence * tempMultiplier, 0.02f, 0.50f);
            p.Contagiousness = Math.Clamp(baseContagiousness * tempMultiplier, 0.01f, 0.20f);

            return p;
        }
        // ============================================================
        // СПАВН
        // ============================================================
        public static void TrySpark(Simulation sim, Random rng)
        {
            if (sim.Agents.Count < 50) return;

            var victim = sim.Agents[rng.Next(sim.Agents.Count)];
            if (victim.Infected) return;

            var tile = sim.World[victim.Position.X, victim.Position.Y];
            var terrain = tile.Terrain;
            var currentSeason = SeasonSystem.GetCurrentSeason(sim.TotalTicks);

            // 1. ОПРЕДЕЛЯЕМ ТЕМПЕРАТУРУ (0.0 - 1.0)
            // ВАЖНО: Если у вас в Tile или SeasonSystem есть реальная переменная температуры, 
            // замените эту строку на: float currentTemp = tile.Temperature;
            float currentTemp = 0.5f;
            float seasonalVariance = (float)rng.NextDouble() * 0.4f - 0.2f; // Случайное отклонение ±0.2

            switch (currentSeason)
            {
                case SeasonSystem.Season.Winter:
                    currentTemp = Math.Clamp(0.1f + seasonalVariance, 0.0f, 1.0f); break; // Холодно (0.0 - 0.2)
                case SeasonSystem.Season.Spring:
                    currentTemp = Math.Clamp(0.4f + seasonalVariance, 0.0f, 1.0f); break; // Прохладно (0.2 - 0.6)
                case SeasonSystem.Season.Summer:
                    currentTemp = Math.Clamp(0.8f + (seasonalVariance * 0.5f), 0.0f, 1.0f); break; // Жарко (0.7 - 0.9)
                case SeasonSystem.Season.Autumn:
                    currentTemp = Math.Clamp(0.5f + seasonalVariance, 0.0f, 1.0f); break; // Может быть и 0.3, и 0.7
            }

            // 2. ГИБРИДНЫЙ РАСЧЕТ ШАНСА: Температура + Жесткий штраф Зимы
            float tempMultiplier = GetTemperatureVirulenceMultiplier(currentTemp);
            float actualSparkChance = SparkChance * tempMultiplier;

            if (currentSeason == SeasonSystem.Season.Winter)
            {
                // Зимой шанс зарождения новой эпидемии дополнительно падает в 5 раз
                actualSparkChance *= 0.2f;
            }
            _sparkAttempts++;
            if (rng.NextDouble() >= actualSparkChance) return;
            _sparkSuccesses++;
            // 3. Генерируем вирус с учетом текущей температуры
            var p = GeneratePathogen(terrain, rng, currentTemp);

            ActivePathogens[p.Id] = p;
            PathogenTracker.RegisterPathogen(p.Id, p.Name, null, p.Virulence, p.Contagiousness, sim.TotalTicks);

            Infect(victim, p);

            FileLogger.Log(
                $"[TICK {sim.TotalTicks}] PATHOGEN '{p.Name}' ({p.Type}) emerged in {terrain} (Season: {currentSeason}, Temp: {currentTemp:F2}): " +
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


        /// <summary>
        /// Вирус случайно мутирует один ген агента при выздоровлении.
        /// Эмерджентная адаптация: происходит РЕДКО, чтобы не вызывать деградацию популяции.
        /// </summary>
        private static void MutateRandomGene(Agent agent, Pathogen p, Random rng)
        {
            // === ИСПРАВЛЕНИЕ 1: Мутация происходит только в 10% случаев при выздоровлении ===
            // (Раньше было 100%, что быстро "ломало" генофонд)
            if (rng.NextDouble() > 0.10f) return;

            string[] genes = { "SelfAwareness", "Aggression", "Openness", "Agreeableness",
                       "Conscientiousness", "Extraversion", "Fertility", "ImmuneStrength" };

            string targetGene = genes[rng.Next(genes.Length)];

            // === ИСПРАВЛЕНИЕ 2: Сила мутации снижена с 0.1f до 0.05f ===
            // Это предотвращает резкое "тупение" или бесплодие агентов
            float change = rng.NextDouble() < 0.5f ? -0.05f : 0.05f;

            switch (targetGene)
            {
                case "SelfAwareness": agent.Genome.SelfAwareness = Math.Clamp(agent.Genome.SelfAwareness + change, 0f, 1f); break;
                case "Aggression": agent.Genome.Aggression = Math.Clamp(agent.Genome.Aggression + change, 0f, 1f); break;
                case "Openness": agent.Genome.Openness = Math.Clamp(agent.Genome.Openness + change, 0f, 1f); break;
                case "Agreeableness": agent.Genome.Agreeableness = Math.Clamp(agent.Genome.Agreeableness + change, 0f, 1f); break;
                case "Conscientiousness": agent.Genome.Conscientiousness = Math.Clamp(agent.Genome.Conscientiousness + change, 0f, 1f); break;
                case "Extraversion": agent.Genome.Extraversion = Math.Clamp(agent.Genome.Extraversion + change, 0f, 1f); break;
                case "Fertility": agent.Genome.Fertility = Math.Clamp(agent.Genome.Fertility + change, 0f, 1f); break;
                case "ImmuneStrength": agent.Genome.ImmuneStrength = Math.Clamp(agent.Genome.ImmuneStrength + change, 0f, 1f); break;
            }

            // === НОВОЕ: Записываем, какой ген мутировал этот конкретный штамм ===
            if (!p.MutatedGenes.ContainsKey(targetGene)) p.MutatedGenes[targetGene] = 0;
            p.MutatedGenes[targetGene]++;
            PathogenTracker.RecordGeneMutation(p.Id, targetGene);
            // Логируем только если это значимое событие (чтобы не спамить лог)
            if (p.MutatedGenes[targetGene] % 50 == 0)
            {
                FileLogger.Log($"[TICK {Simulation.Instance.TotalTicks}] STRAIN '{p.Name}' frequently mutates '{targetGene}' (Count: {p.MutatedGenes[targetGene]})", FileLogger.LogLevel.Info);
            }
        }
        public static void Infect(Agent agent, Pathogen p)
        {
            agent.Infected = true;
            agent.InfectedWith = p.Id; // ВАЖНО: используем Id (например, "P_000"), а не Type
            agent.InfectionTimer = 0f;
            p.TotalInfected++;

            // === ИСПРАВЛЕНИЕ: Сообщаем трекеру о заражении ===
            PathogenTracker.RecordInfection(p.Id);

            // Эмерджентное осознание заражения
            var nearby = SpatialGrid.GetNearby(agent.Position, 2);
            int sickNearby = nearby.Count(a => a != agent && a.Infected);
            if (sickNearby > 0) CognitionSystem.Record("disease.contact_with_sick", sickNearby);

            var tile = Simulation.Instance.GetTile(agent.Position);
            if (tile != null)
            {
                CognitionSystem.Record($"disease.terrain.{tile.Terrain}", 1f);
                if (tile.Fertility < 0.3f) CognitionSystem.Record("disease.low_fertility_terrain", 1f);
                if (tile.SanctityLevel > 10f) CognitionSystem.Record("disease.sacred_terrain", 1f);
            }
            if (!string.IsNullOrEmpty(agent.LastAction))
            {
                CognitionSystem.Record($"disease.last_action.{agent.LastAction}", 1f);
            }
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
                if (p == null)
                {
                    agent.Infected = false;
                    agent.InfectedWith = null;
                    return;
                }

                agent.InfectionTimer += 1f;

                float biomeMul = (tile != null && tile.Terrain == p.Affinity) ? 1.5f : 1f;

                // MedicineLevel теперь учитывает и локацию (хоспис под ногами), и общее кол-во хосписов у цивилизации
                float medicine = MedicineLevel(agent, tile);
                float specificImmunity = agent.PathogenImmunity.GetValueOrDefault(p.Type, 0f);
                bool hasGeneticResistance = agent.Genome.GeneticResistances.Contains(p.Type);
                float geneticFactor = hasGeneticResistance ? 0.5f : 0f;

                // 1. УРОН ЗДОРОВЬЮ: Медицина чуть-чуть помогает, но не спасает от фатального исхода
                float damage = p.Virulence * 0.1f * biomeMul
                    * (1f - medicine * 0.3f) // Медицина снижает урон максимум на 24% (при medicine=0.8)
                    * (1f - specificImmunity * 0.5f)
                    * (1f - geneticFactor * 0.5f);

                agent.Body.Health -= damage;

                // 2. ПОТЕРЯ ЭНЕРГИИ: ЗДЕСЬ ГЛАВНАЯ РАБОТА ХОСПИСОВ!
                // Базовая потеря энергии от болезни
                float baseEnergyDrain = 0.05f + (p.Virulence * 0.15f);

                // Хосписы снижают потерю энергии максимум на 50%. 
                // Даже при идеальной медицине агент потеряет половину энергии, но не упадет в ноль мгновенно.
                float energyDrain = baseEnergyDrain * (1f - medicine * 0.5f);
                agent.Body.Energy = Math.Max(0f, agent.Body.Energy - energyDrain);

                if (agent.Fear < 50f && rng.NextDouble() < 0.04f)
                    SignalSystem.EmitSignal(agent, SignalType.Help, 0.9f, 6f);

                // Смерть от потери здоровья (если вирус слишком сильно бьет)
                if (agent.Body.Health <= 0f)
                {
                    agent.LastAction = "Plague";
                    PathogenTracker.RecordDeath(p.Id);
                    return;
                }

                // 3. РОЛЛ НА СМЕРТЬ В КОНЦЕ БОЛЕЗНИ (Фатализм)
                if (agent.InfectionTimer >= p.Duration)
                {
                    // ВАЖНО: Медицина НЕ влияет на этот шанс! 
                    // Спасает только врожденный иммунитет (ImmuneStrength), специфический иммунитет и генетика.
                    float lethal = p.Virulence * 0.5f
                        * (1f - agent.Genome.ImmuneStrength * 0.5f)
                        * (1f - specificImmunity * 0.4f)
                        * (1f - geneticFactor * 0.5f);

                    if (rng.NextDouble() < lethal)
                    {
                        agent.Body.Health = 0f;
                        agent.LastAction = "Plague";
                        PathogenTracker.RecordDeath(p.Id);
                    }
                    else
                    {
                        // Выздоровление
                        agent.Infected = false;
                        agent.InfectedWith = null;
                        PathogenTracker.RecordRecovery(p.Id);

                        // Приобретенный иммунитет к этому типу вируса (и его штаммам)
                        float current = agent.PathogenImmunity.GetValueOrDefault(p.Type, 0f);
                        agent.PathogenImmunity[p.Type] = Math.Min(0.9f, current + 0.3f);

                        // Счетчик выживаний для генетической ассимиляции
                        int survivals = agent.PathogenSurvivals.GetValueOrDefault(p.Type, 0) + 1;
                        agent.PathogenSurvivals[p.Type] = survivals;

                        // Генетическая ассимиляция (резистентность)
                        if (!hasGeneticResistance)
                        {
                            float assimilationChance = 1f - MathF.Pow(0.5f, survivals);
                            if (rng.NextDouble() < assimilationChance)
                            {
                                agent.Genome.GeneticResistances.Add(p.Type);
                                agent.Genome.GeneticResistances.Sort();
                                FileLogger.Log($"[TICK {Simulation.Instance.TotalTicks}] GENETIC ASSIMILATION: Agent {agent.Id} lineage adapted to '{p.Type}'", FileLogger.LogLevel.Info);
                            }
                        }

                        // === НОВОЕ: Вирус мутирует случайный ген (с малым шансом 10%) ===
                        MutateRandomGene(agent, p, rng);

                        // Восстановление после болезни
                        agent.Body.Health = Math.Min(100f, agent.Body.Health + 15f);
                        agent.Fear = Math.Max(0f, agent.Fear - 20f);
                    }
                }
                return;
            }

            // ---------- ЗДОРОВ ----------
            var nearby = SpatialGrid.GetNearby(agent.Position, 2);
            Agent carrier = null;
            foreach (var other in nearby)
            {
                if (other != agent && other.Infected) { carrier = other; break; }
            }
            if (carrier == null) return;

            var pCarrier = GetPathogen(carrier.InfectedWith);
            if (pCarrier == null) return;

            float biomeMulHealth = (tile != null && tile.Terrain == pCarrier.Affinity) ? 1.5f : 0.8f;
            float medicineHealth = MedicineLevel(agent, tile);
            float immuneStrength = agent.Genome.ImmuneStrength;
            float specificImmunityHealth = agent.PathogenImmunity.GetValueOrDefault(pCarrier.Type, 0f);
            bool hasGeneticResistanceHealth = agent.Genome.GeneticResistances.Contains(pCarrier.Type);
            float geneticFactorHealth = hasGeneticResistanceHealth ? 0.5f : 0f;

            float chance = pCarrier.Contagiousness * biomeMulHealth
                * (1f - medicineHealth * 0.5f)
                * (1f - immuneStrength * 0.5f)
                * (1f - specificImmunityHealth * 0.7f)
                * (1f - geneticFactorHealth);

            if (rng.NextDouble() < chance)
            {
                // === ЛОГИКА МУТАЦИИ ПРИ ЗАРАЖЕНИИ ===
                string newStrainId = PathogenTracker.TryMutate(
                    pCarrier.Id,
                    pCarrier.Type,
                    pCarrier.Virulence,
                    pCarrier.Contagiousness,
                    Simulation.Instance.TotalTicks,
                    rng);

                // Если произошла мутация, обновляем ссылку на вирус в глобальном словаре (если его там еще нет)
                if (newStrainId != pCarrier.Id && !ActivePathogens.ContainsKey(newStrainId))
                {
                    var record = PathogenTracker.GetRecord(newStrainId);
                    ActivePathogens[newStrainId] = new Pathogen
                    {
                        Id = newStrainId,
                        Type = pCarrier.Type,
                        Name = record.Name,
                        Virulence = record.CurrentVirulence,
                        Contagiousness = record.CurrentContagiousness,
                        Duration = pCarrier.Duration,
                        Affinity = pCarrier.Affinity,
                        BirthTick = Simulation.Instance.TotalTicks
                    };
                }

                // Заражаем агента текущим (возможно, мутировавшим) штаммом
                var finalPathogen = GetPathogen(newStrainId) ?? pCarrier;
                Infect(agent, finalPathogen);
            }
        }

        private static float MedicineLevel(Agent agent, Tile tile)
        {
            float medicine = 0f;

            // 1. ЛОКАЛЬНЫЙ БАФФ: Агент физически находится в хосписе/храме
            if (tile != null && tile.BuildingFunctional && tile.DominantAxis == "healing")
            {
                medicine += 0.3f + tile.BuildingQuality * 0.15f;
            }

            // 2. ГЛОБАЛЬНЫЙ БАФФ: Сеть хосписов цивилизации
            if (!string.IsNullOrEmpty(agent.CivilizationId))
            {
                var civ = Simulation.activeCivs?.FirstOrDefault(c => c.Id == agent.CivilizationId);
                if (civ != null && civ.HealingBuildingsCount > 0)
                {
                    // 10 зданий = +0.05, 50 зданий = +0.25, 100+ зданий = +0.35 (максимум)
                    float globalHealthcareBuff = Math.Min(0.35f, civ.HealingBuildingsCount * 0.005f);
                    medicine += globalHealthcareBuff;
                }
            }

            // 3. БАФФ ОТ ЗНАНИЙ (Гигиена, методы лечения)
            medicine += KnowledgeSystem.MethodBuff(agent, "healing") * 0.4f;

            // Кап на уровне 0.8 (80% эффективности медицины)
            return Math.Clamp(medicine, 0f, 0.8f);
        }

        public static float GetHerdImmunity(List<Agent> agents)
        {
            if (agents == null || agents.Count == 0) return 0f;
            int immuneCount = agents.Count(a => a.PathogenImmunity.Count > 0 || a.Genome.GeneticResistances.Count > 0);
            return (float)immuneCount / agents.Count;
        }

        public static void CleanupOutbreaks(int currentTick)
        {
            if (currentTick % 500 == 0)
            {
                PathogenTracker.CheckExtinctions(currentTick);
            }

            if (currentTick % 1000 != 0) return;

            var dead = ActivePathogens.Values
                .Where(p => currentTick - p.BirthTick > 6000)
                .Select(p => p.Id)
                .ToList();
            foreach (var id in dead) ActivePathogens.Remove(id);
        }
    }
}
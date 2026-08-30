using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public class PathogenRecord
    {
        public string Id;
        public string Name;
        public string ParentStrainId; // Исправлено: было ParentStrain
        public int BirthTick;
        public int ExtinctionTick;    // 0, если вирус всё ещё активен
        public int TotalInfected;
        public int TotalDied;
        public int PeakActive;
        public int CurrentActive;
        public float BaseVirulence;
        public float BaseContagiousness;
        public float CurrentVirulence;
        public float CurrentContagiousness;
        public int LastActiveTick;    // Добавлено для корректного отслеживания угасания

        // Свойство только для чтения, вычисляемое на основе ExtinctionTick
        public bool IsExtinct => ExtinctionTick > 0;
    }

    public static class PathogenTracker
    {
        private static readonly Dictionary<string, PathogenRecord> Records = new();
        private static int _mutationCounter = 0;

        public static void RegisterPathogen(string id, string name, string parentId, float virulence, float contagion, int tick)
        {
            Records[id] = new PathogenRecord
            {
                Id = id,
                Name = name,
                ParentStrainId = parentId,
                BirthTick = tick,
                ExtinctionTick = 0,
                TotalInfected = 0,
                TotalDied = 0,
                PeakActive = 0,
                CurrentActive = 0,
                BaseVirulence = virulence,
                BaseContagiousness = contagion,
                CurrentVirulence = virulence,
                CurrentContagiousness = contagion,
                LastActiveTick = tick
            };
        }

        public static void RecordInfection(string pathogenId)
        {
            if (Records.TryGetValue(pathogenId, out var record))
            {
                record.TotalInfected++;
                record.CurrentActive++;
                record.LastActiveTick = Simulation.Instance.TotalTicks;
                if (record.CurrentActive > record.PeakActive)
                    record.PeakActive = record.CurrentActive;
            }
        }

        public static void RecordDeath(string pathogenId)
        {
            if (Records.TryGetValue(pathogenId, out var record))
            {
                record.TotalDied++;
                record.CurrentActive--;
                record.LastActiveTick = Simulation.Instance.TotalTicks;
            }
        }

        public static void RecordRecovery(string pathogenId)
        {
            if (Records.TryGetValue(pathogenId, out var record))
            {
                record.CurrentActive--;
                record.LastActiveTick = Simulation.Instance.TotalTicks;
            }
        }

        public static void CheckExtinctions(int currentTick)
        {
            foreach (var record in Records.Values)
            {
                // Исправлено: проверяем LastActiveTick и присваиваем ExtinctionTick, а не IsExtinct
                if (!record.IsExtinct && record.CurrentActive <= 0 && (currentTick - record.LastActiveTick > 1000))
                {
                    record.ExtinctionTick = currentTick;
                    FileLogger.Log(
                        $"[TICK {currentTick}] PATHOGEN EXTINCT: '{record.Name}' died out. " +
                        $"Total infected: {record.TotalInfected}, Total dead: {record.TotalDied}",
                        FileLogger.LogLevel.Info);
                }
            }
        }

        public static string TryMutate(string currentPathogenId, string baseType, float currentVirulence, float currentContagion, int tick, Random rng)
        {
            // Шанс мутации: 0.5% при каждом новом заражении
            if (rng.NextDouble() > 0.005f) return currentPathogenId;

            _mutationCounter++;
            string newId = $"P_{baseType}_M{_mutationCounter:D3}";
            string newName = $"{baseType}-Strain-{_mutationCounter:D3}";

            // Мутация: случайное изменение свойств (дрейф)
            float virulenceChange = (float)(rng.NextDouble() - 0.5) * 0.15f;
            float contagionChange = (float)(rng.NextDouble() - 0.5) * 0.15f;

            float newVirulence = Math.Clamp(currentVirulence + virulenceChange, 0.05f, 0.80f);
            float newContagion = Math.Clamp(currentContagion + contagionChange, 0.02f, 0.30f);

            RegisterPathogen(newId, newName, currentPathogenId, newVirulence, newContagion, tick);

            FileLogger.Log(
                $"[TICK {tick}] PATHOGEN MUTATION: '{currentPathogenId}' mutated into '{newName}' " +
                $"(Vir: {newVirulence:F2}, Cont: {newContagion:F2})",
                FileLogger.LogLevel.Warning);

            return newId;
        }
        /// <summary>
        /// Экспортирует финальные данные всех штаммов в CSV после завершения симуляции.
        /// </summary>
        public static void ExportFinalData(string csvPath, string runId)
        {
            try
            {
                var header = "RunId,PathogenId,PathogenName,ParentStrain,TotalInfected,TotalDied,PeakActive,CurrentActive,Virulence,Contagiousness,IsExtinct,BirthTick,ExtinctionTick";

                using var writer = new StreamWriter(csvPath, false);
                writer.WriteLine(header);

                foreach (var rec in Records.Values)
                {
                    // Пишем только штаммы, которые хоть кого-то заразили
                    if (rec.TotalInfected <= 0) continue;

                    string line = $"{runId},{rec.Id},\"{rec.Name}\",{rec.ParentStrainId ?? "None"}," +
                                 $"{rec.TotalInfected},{rec.TotalDied},{rec.PeakActive},{rec.CurrentActive}," +
                                 $"{rec.CurrentVirulence:F3},{rec.CurrentContagiousness:F3},{rec.IsExtinct}," +
                                 $"{rec.BirthTick},{rec.ExtinctionTick}";
                    writer.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PathogenTracker] Export error: {ex.Message}");
            }
        }
        public static PathogenRecord GetRecord(string id) => Records.GetValueOrDefault(id);
        public static List<PathogenRecord> GetAllRecords() => Records.Values.ToList();
    }
}
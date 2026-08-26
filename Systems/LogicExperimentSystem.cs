using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.UI;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class LogicExperimentSystem
    {
        public class ExperimentRecord
        {
            public Guid AgentId;
            public Guid DeviceId;
            public float[] Inputs;
            public float[] Outputs;
            public int Tick;
        }

        private static readonly SortedDictionary<Guid, List<ExperimentRecord>> AgentExperiments = new();
        private static readonly SortedDictionary<Guid, int> LastExperimentTick = new();
        private static readonly SortedDictionary<Guid, int> DeviceExperimentCount = new();

        private static int _lastAggregateLogTick = 0;

        private const int ExperimentCooldown = 50; // Агент может экспериментировать раз в 50 тиков

        public static void TryExperiment(Agent agent, Tile tile, Random rng)
        {
            if (agent == null || tile == null || rng == null)
                return;

            // Нужен достаточный уровень логики
            if (agent.Logic < 0.4f)
                return;

            // Нужен институт знания
            if (tile.InstitutionAxis != "knowledge" || tile.InstitutionLevel < 1.5f)
                return;

            // НОВОЕ: Кулдаун
            int currentTick = Simulation.Instance.TotalTicks;
            if (LastExperimentTick.TryGetValue(agent.Id, out int lastTick))
            {
                if (currentTick - lastTick < ExperimentCooldown)
                    return;
            }

            // Ищем логическое устройство на тайле
            var device = tile.Artifacts.FirstOrDefault(a =>
                a.Name != null && a.Name.StartsWith("logic-node"));

            if (device == null)
                return;

            // СНИЖЕННАЯ вероятность эксперимента
            float chance =
                0.03f +
                agent.Genome.SelfAwareness * 0.05f +
                agent.Logic * 0.03f +
                tile.InstitutionLevel * 0.005f;

            if (rng.NextDouble() > chance)
                return;

            // Обновляем кулдаун
            LastExperimentTick[agent.Id] = currentTick;

            // Генерируем случайный вход
            var inputs = new float[]
            {
                rng.NextDouble() < 0.5f ? 0f : 1f,
                rng.NextDouble() < 0.5f ? 0f : 1f
            };

            // Симулируем работу устройства
            var outputs = SimulateDevice(device, inputs, rng);

            // Записываем эксперимент
            var record = new ExperimentRecord
            {
                AgentId = agent.Id,
                DeviceId = device.Id,
                Inputs = inputs,
                Outputs = outputs,
                Tick = currentTick
            };

            if (!AgentExperiments.TryGetValue(agent.Id, out var records))
            {
                records = new List<ExperimentRecord>();
                AgentExperiments[agent.Id] = records;
            }

            records.Add(record);

            // Ограничиваем историю экспериментов
            if (records.Count > 200)
                records.RemoveRange(0, records.Count - 200);

            // НОВОЕ: Считаем эксперименты для агрегированного лога
            if (!DeviceExperimentCount.TryGetValue(device.Id, out int count))
                count = 0;
            DeviceExperimentCount[device.Id] = count + 1;

            // НОВОЕ: НЕ пишем каждый эксперимент в лог
            // Логируем только агрегированно раз в 500 тиков
            LogAggregateIfNeeded(currentTick);

            agent.LastAction = "Experiment";
        }

        private static void LogAggregateIfNeeded(int currentTick)
        {
            if (currentTick - _lastAggregateLogTick < 500)
                return;

            _lastAggregateLogTick = currentTick;

            int totalExperiments = 0;
            int activeDevices = 0;

            foreach (var kv in DeviceExperimentCount)
            {
                if (kv.Value >= 5)
                {
                    totalExperiments += kv.Value;
                    activeDevices++;
                }
            }

            if (totalExperiments > 0)
            {
                FileLogger.Log(
                    $"[TICK {currentTick}] LOGIC RESEARCH: {totalExperiments} experiments " +
                    $"on {activeDevices} devices",
                    FileLogger.LogLevel.Info);
            }

            DeviceExperimentCount.Clear();
        }

        private static float[] SimulateDevice(Artifact device, float[] inputs, Random rng)
        {
            if (!MaterialDB.TryGet(device.MaterialId, out var spec))
                return new float[] { 0f };

            float logic = spec.Logic;
            float conductivity = spec.Conductivity;

            float predictability = logic * conductivity;

            float output;

            if (predictability > 0.6f)
            {
                float hash = (spec.Hardness + spec.Conductivity + spec.Logic) % 1f;

                if (hash < 0.25f)
                {
                    output = 1f - inputs[0];
                }
                else if (hash < 0.50f)
                {
                    output = (inputs[0] > 0.5f && inputs[1] > 0.5f) ? 1f : 0f;
                }
                else if (hash < 0.75f)
                {
                    output = (inputs[0] > 0.5f || inputs[1] > 0.5f) ? 1f : 0f;
                }
                else
                {
                    output = (inputs[0] > 0.5f) != (inputs[1] > 0.5f) ? 1f : 0f;
                }
            }
            else
            {
                output = rng.NextDouble() < 0.5f ? 0f : 1f;
            }

            return new float[] { output };
        }

        public static List<ExperimentRecord> GetExperiments(Guid agentId)
        {
            return AgentExperiments.GetValueOrDefault(agentId, new List<ExperimentRecord>());
        }
    }
}
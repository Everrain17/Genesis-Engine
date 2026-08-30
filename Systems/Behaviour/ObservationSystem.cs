using System.Collections.Generic;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems.Behaviour
{
    public class ActionPattern
    {
        public string Key;
        public string ActionType;
        public string Object1;
        public string Object2;
        public string Environment;
        public float SuccessScore;
        public int Occurrences;

        public float GetConfidence()
        {
            return Occurrences > 0
                ? SuccessScore / Occurrences
                : 0f;
        }
    }

    public static class ObservationSystem
    {
        public static void RecordPattern(
            Agent agent,
            string action,
            WorldObject obj1,
            WorldObject obj2,
            float benefit)
        {
            if (agent == null)
                return;

            string env = Simulation.Instance.World[agent.Position.X, agent.Position.Y].Terrain.ToString();
            string obj1Id = obj1?.MaterialId ?? "None";
            string obj2Id = obj2?.MaterialId ?? "None";

            string key = action + "|" + obj1Id + "|" + obj2Id + "|" + env;

            if (!agent.Memory.PatternIndex.TryGetValue(key, out var pattern))
            {
                pattern = new ActionPattern
                {
                    Key = key,
                    ActionType = action,
                    Object1 = obj1Id,
                    Object2 = obj2Id,
                    Environment = env
                };

                agent.Memory.PatternIndex[key] = pattern;
                agent.Memory.Patterns.Add(pattern);

                if (OptimizationSettings.EnableSoftMemoryCaps &&
                    agent.Memory.Patterns.Count > OptimizationSettings.MaxPatternsPerAgent)
                {
                    RemoveWorstPattern(agent.Memory);
                }
            }

            pattern.SuccessScore += benefit;
            pattern.Occurrences++;
        }



        private static void RemoveWorstPattern(MemorySystem memory)
        {
            if (memory.Patterns.Count == 0)
                return;

            int worstIndex = 0;
            float worstScore = float.MaxValue;

            for (int i = 0; i < memory.Patterns.Count; i++)
            {
                float score = memory.Patterns[i].GetConfidence();

                if (score < worstScore)
                {
                    worstScore = score;
                    worstIndex = i;
                }
            }

            var removed = memory.Patterns[worstIndex];

            memory.Patterns.RemoveAt(worstIndex);

            if (!string.IsNullOrEmpty(removed.Key))
                memory.PatternIndex.Remove(removed.Key);
        }
    }
}
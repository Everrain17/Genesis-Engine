using System;
using System.Collections.Generic;
using GenesisEngine.Core;
using GenesisEngine.Systems;
using GenesisEngine.Systems.Behaviour;

namespace GenesisEngine.Entities
{
    public class AgentMemory
    {
        public Guid AgentId;
        public float TrustScore;
        public int InteractionCount;
        public string LastAction;
        public int LastInteractionTick;
        public Vector2 Location;
        public float FoodValue;
        public float DangerValue;
        public int VisitCount;
    }

    public class MemorySystem
    {
        public List<AgentMemory> AgentMemories = new();
        public List<AgentMemory> PlaceMemories = new();

        public List<ActionPattern> Patterns = new();

        // Быстрый индекс паттернов.
        // Используется ObservationSystem.
        public SortedDictionary<string, ActionPattern> PatternIndex = new();
        private readonly SortedDictionary<Guid, AgentMemory> _agentMemoryIndex = new();

        public void UpdateAgentMemory(Guid otherId, string action, float emotionalImpact)
        {
            if (!_agentMemoryIndex.TryGetValue(otherId, out var mem))
            {
                if (OptimizationSettings.EnableSoftMemoryCaps &&
                    AgentMemories.Count >= OptimizationSettings.MaxAgentMemories)
                {
                    EvictWeakestMemory();
                }

                mem = new AgentMemory
                {
                    AgentId = otherId
                };

                _agentMemoryIndex[otherId] = mem;
                AgentMemories.Add(mem);
            }

            mem.LastAction = action;
            mem.InteractionCount++;
            mem.TrustScore = Math.Clamp(mem.TrustScore + emotionalImpact, -100f, 100f);
            mem.LastInteractionTick = Simulation.Instance?.TotalTicks ?? 0;
        }

        public float GetTrust(Guid otherId)
        {
            return _agentMemoryIndex.TryGetValue(otherId, out var mem)
                ? mem.TrustScore
                : 0f;
        }

        private void EvictWeakestMemory()
        {
            if (AgentMemories.Count == 0)
                return;

            int worstIndex = 0;
            int worstScore = int.MaxValue;

            for (int i = 0; i < AgentMemories.Count; i++)
            {
                var m = AgentMemories[i];

                int score = m.InteractionCount * 1000 + m.LastInteractionTick;

                if (score < worstScore)
                {
                    worstScore = score;
                    worstIndex = i;
                }
            }

            var removed = AgentMemories[worstIndex];
            AgentMemories.RemoveAt(worstIndex);
            _agentMemoryIndex.Remove(removed.AgentId);
        }
    }
}
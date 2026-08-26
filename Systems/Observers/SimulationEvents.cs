using System;
using System.Collections.Concurrent;
using GenesisEngine.Core;
using GenesisEngine.Entities;

namespace GenesisEngine.Systems.Observers
{
    public enum SimEventType
    {
        AgentBorn,
        AgentDied,
        Trade,
        Combat,
        Hunt,
        Discovery,
        BuildingCreated,
        MaterialMixed,
        SignalEmitted,
        KnowledgeTaught,
        ArtifactCreated
    }

    public class SimEvent
    {
        public SimEventType Type;
        public int Tick;
        public Agent Actor;
        public Agent Target;
        public Vector2 Position;
        public string Data;
        public float Value;
        public object Payload;
    }

    public static class EventBus
    {
        private static readonly ConcurrentQueue<SimEvent> _events = new();

        public static void Publish(SimEvent e)
        {
            if (e == null) return;
            _events.Enqueue(e);
        }

        public static bool TryDequeue(out SimEvent e)
        {
            return _events.TryDequeue(out e);
        }
    }
}
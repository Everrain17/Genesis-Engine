using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;

namespace GenesisEngine.Systems
{
    public class SignalInstance
    {
        public SignalType Type;
        public float Intensity;
        public float Duration;
        public Vector2 Origin;
        public Guid SenderId;
        public int TickCreated;
        public string Data;

        public List<SignalType> Sequence = new();
        public List<string> SequenceData = new();

        public bool IsActive(int tick) => tick - TickCreated < Duration;

        public float CurrentStrength(int tick)
        {
            float age = tick - TickCreated;
            return Math.Max(0f, Intensity * (1f - age / Duration));
        }
    }

    public struct SignalResponse
    {
        public float Approach;
        public float Flee;
        public float Social;
        public float Alert;
    }
    public class SignalSequencePayload
    {
        public List<SignalType> Types = new();
        public List<string> Referents = new();
    }
    public static class SignalSystem
    {
        public static readonly List<SignalInstance> ActiveSignals = new();

        public static SignalInstance EmitSignal(
            Agent sender,
            SignalType type,
            float intensity,
            float duration = 10f,
            string data = null)
        {
            var signal = new SignalInstance
            {
                Type = type,
                Intensity = Math.Clamp(intensity, 0f, 1f),
                Duration = duration,
                Origin = sender.Position,
                SenderId = sender.Id,
                TickCreated = Simulation.Instance.TotalTicks,
                Data = data
            };

            lock (ActiveSignals)
            {
                ActiveSignals.Add(signal);
            }

            Observers.EventBus.Publish(new Observers.SimEvent
            {
                Type = Observers.SimEventType.SignalEmitted,
                Tick = Simulation.Instance.TotalTicks,
                Actor = sender,
                Position = sender.Position,
                Data = type.ToString(),
                Value = intensity,
                Payload = data
            });

            return signal;
        }
        public static SignalInstance EmitSequence(
    Agent sender,
    List<SignalType> types,
    List<string> referents,
    float intensity,
    float duration = 12f)
        {
            if (sender == null || types == null || types.Count == 0)
                return null;

            var signal = new SignalInstance
            {
                Type = types[0],
                Intensity = Math.Clamp(intensity, 0f, 1f),
                Duration = duration,
                Origin = sender.Position,
                SenderId = sender.Id,
                TickCreated = Simulation.Instance.TotalTicks,
                Data = referents != null && referents.Count > 0 ? referents[0] : null,
                Sequence = new List<SignalType>(types),
                SequenceData = referents != null
                    ? new List<string>(referents)
                    : new List<string>()
            };

            lock (ActiveSignals)
            {
                ActiveSignals.Add(signal);
            }

            Observers.EventBus.Publish(new Observers.SimEvent
            {
                Type = Observers.SimEventType.SignalEmitted,
                Tick = Simulation.Instance.TotalTicks,
                Actor = sender,
                Position = sender.Position,
                Data = "SEQ:" + string.Join(">", types),
                Value = intensity,
                Payload = new SignalSequencePayload
                {
                    Types = new List<SignalType>(types),
                    Referents = referents != null
                        ? new List<string>(referents)
                        : new List<string>()
                }
            });

            return signal;
        }
        public static List<(SignalInstance signal, float clarity, float distance)> Listen(Agent listener)
        {
            var heard = new List<(SignalInstance, float, float)>();

            int currentTick = Simulation.Instance.TotalTicks;

            lock (ActiveSignals)
            {
                int start = Math.Max(0, ActiveSignals.Count - 80);

                for (int i = start; i < ActiveSignals.Count; i++)
                {
                    var s = ActiveSignals[i];

                    if (!s.IsActive(currentTick)) continue;
                    if (s.SenderId == listener.Id) continue;

                    float dist = listener.Position.Distance(s.Origin);
                    if (dist > listener.EffectiveHearing) continue;

                    float clarity =
                        (1f - dist / Math.Max(1f, listener.EffectiveHearing)) *
                        s.CurrentStrength(currentTick);

                    if (clarity > 0.05f)
                        heard.Add((s, clarity, dist));
                }
            }

            return heard;
        }

        public static SignalResponse InterpretSignal(Agent listener, SignalInstance signal, float clarity)
        {
            float trust = Math.Clamp(listener.Memory.GetTrust(signal.SenderId) / 100f, -1f, 1f);

            float hunger = listener.Body.Hunger / 100f;
            float loneliness = listener.Loneliness / 100f;
            float fear = listener.Fear / 100f;

            float openness = listener.Genome.Openness;
            float agreeableness = listener.Genome.Agreeableness;

            var response = new SignalResponse();

            switch (signal.Type)
            {
                case SignalType.Alarm:
                case SignalType.Danger:
                    response.Alert = clarity;

                    if (trust > 0.1f)
                    {
                        response.Flee = 0.75f * clarity;
                    }
                    else
                    {
                        response.Flee = clarity * (0.35f + 0.45f * fear);
                    }
                    break;

                case SignalType.Food:
                    if (hunger > 0.3f && trust > -0.2f)
                    {
                        response.Approach = clarity * (0.35f + 0.65f * hunger);
                    }
                    break;

                case SignalType.Come:
                case SignalType.Bond:
                    if (loneliness > 0.3f && trust > 0f)
                    {
                        response.Social = clarity * Math.Max(0.1f, trust);
                        response.Approach = clarity * 0.35f;
                    }
                    break;

                case SignalType.Help:
                    if (trust > 0f && agreeableness > 0.45f)
                    {
                        response.Approach = clarity * agreeableness;
                        response.Alert = clarity * 0.35f;
                    }
                    break;

                case SignalType.Trade:
                    if (trust > 0f && openness > 0.4f)
                    {
                        response.Approach = clarity * openness;
                        response.Social = clarity * 0.2f;
                    }
                    break;

                case SignalType.Mourn:
                    if (listener.Genome.Spirituality > 0.5f || listener.Genome.Agreeableness > 0.6f)
                    {
                        response.Social = clarity * 0.3f;
                        response.Alert = clarity * 0.15f;
                    }
                    break;

                case SignalType.Celebrate:
                    if (trust > 0f)
                    {
                        response.Social = clarity * 0.4f;
                    }
                    break;
            }

            response.Approach = Math.Clamp(response.Approach, 0f, 1f);
            response.Flee = Math.Clamp(response.Flee, 0f, 1f);
            response.Social = Math.Clamp(response.Social, 0f, 1f);
            response.Alert = Math.Clamp(response.Alert, 0f, 1f);

            return response;
        }

        public static void CleanupSignals()
        {
            int currentTick = Simulation.Instance.TotalTicks;

            lock (ActiveSignals)
            {
                ActiveSignals.RemoveAll(s => !s.IsActive(currentTick));
            }
        }
    }
}
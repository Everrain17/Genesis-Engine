using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.UI;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems.Observers
{
    public static class ObserverCoordinator
    {
        private static readonly HashSet<string> _seenComposites = new();

        public static int LastProcessedEvents;

        private static int _materialLogCount;
        public static int MaterialBreakthroughs;
        public static int MaterialAnalogs;

        public static void ProcessEvents(Simulation sim)
        {
            int processed = 0;

            while (EventBus.TryDequeue(out var e))
            {
                processed++;

                if (OptimizationSettings.SafeObservers)
                {
                    try
                    {
                        ProcessOne(sim, e);
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Log(
                            $"OBSERVER ERROR: type={e?.Type}, tick={e?.Tick}, error={ex.Message}",
                            FileLogger.LogLevel.Error);
                    }
                }
                else
                {
                    ProcessOne(sim, e);
                }
            }

            LastProcessedEvents = processed;

            if (processed > 20000)
            {
                FileLogger.Log(
                    $"EVENT BACKLOG: processed {processed} events in one tick",
                    FileLogger.LogLevel.Warning);
            }
        }

        private static void ProcessOne(Simulation sim, SimEvent e)
        {
            if (e == null)
                return;

            CognitionSystem.Observe(e);
            switch (e.Type)
            {
                case SimEventType.AgentDied:
                    Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "AgentDied", e.Actor?.CivilizationId, e.Data ?? "");
                    break;
                case SimEventType.Trade:
                    Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "Trade", e.Actor?.CivilizationId, "");
                    break;
                case SimEventType.Combat:
                    Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "Combat", e.Actor?.CivilizationId, "");
                    break;
                case SimEventType.BuildingCreated:
                    Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "BuildingCreated", e.Actor?.CivilizationId, e.Data ?? "");
                    break;
                case SimEventType.Discovery:
                    Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "Discovery", e.Actor?.CivilizationId, e.Data ?? "");
                    break;
                case SimEventType.ArtifactCreated:
                    Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "ArtifactCreated", e.Actor?.CivilizationId, e.Data ?? "");
                    break;
                case SimEventType.MaterialMixed:
                    Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "MaterialMixed", e.Actor?.CivilizationId, e.Data ?? "");
                    break;
            }
            switch (e.Type)
            {
                case SimEventType.Trade:
                    OnTrade(e);
                    break;

                case SimEventType.Combat:
                    OnCombat(e);
                    break;

                case SimEventType.MaterialMixed:
                    OnMaterialMixed(e);
                    break;

                case SimEventType.Discovery:
                    OnDiscovery(e);
                    break;

                case SimEventType.BuildingCreated:
                    OnBuildingCreated(sim, e);
                    break;

                case SimEventType.ArtifactCreated:
                    OnArtifactCreated(e);
                    break;

                case SimEventType.SignalEmitted:
                    LanguageSystem.ObserveEmission(e);
                    GrammarSystem.ObserveEmission(e);

                    // НОВОЕ: Анализ фонем из последовательностей сигналов
                    if (e.Payload is SignalSequencePayload seqPayload)
                    {
                        PhonemeSystem.AnalyzeSignalSequence(e.Actor, seqPayload);
                    }
                    break;
            }
        }

        private static void OnTrade(SimEvent e)
        {
            if (e.Actor == null || e.Target == null)
                return;

            var civA = e.Actor.CivilizationId;
            var civB = e.Target.CivilizationId;

            if (string.IsNullOrEmpty(civA) ||
                string.IsNullOrEmpty(civB) ||
                civA == civB)
            {
                return;
            }

            DiplomacySystem.ShiftRelation(civA, civB, +2f);
        }

        private static void OnCombat(SimEvent e)
        {
            if (e.Actor == null || e.Target == null)
                return;

            var civA = e.Actor.CivilizationId;
            var civB = e.Target.CivilizationId;

            if (string.IsNullOrEmpty(civA) ||
                string.IsNullOrEmpty(civB) ||
                civA == civB)
            {
                return;
            }

            DiplomacySystem.ShiftRelation(civA, civB, -6f);
            DiplomacySystem.RecordLoss(civB);
        }

        private static void OnMaterialMixed(SimEvent e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            if (!_seenComposites.Add(e.Data))
                return;

            if (!MaterialDB.TryGet(e.Data, out var spec))
                return;

            if (spec.Observed == null)
                return;

            var (analog, match) = MaterialAnalyzer.FindAnalog(spec.Observed);

            if (OptimizationSettings.ThrottleMaterialLogs &&
                _materialLogCount >= OptimizationSettings.MaxMaterialLogs)
            {
                return;
            }

            if (match < 70f)
            {
                _materialLogCount++;
                MaterialBreakthroughs++;
                FileLogger.Log(
                    $"[TICK {e.Tick}] MATERIAL BREAKTHROUGH: '{spec.Id}' has no close real-world analog. " +
                    $"Hardness={spec.Hardness:F2}, Conductivity={spec.Conductivity:F2}, Organic={spec.Organic:F2}, " +
                    $"Flexibility={spec.Flexibility:F2}, Logic={spec.Logic:F2}",
                    FileLogger.LogLevel.Info);
                Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "MaterialBreakthrough", e.Actor?.CivilizationId, e.Data);
            }
            else if (match > 85f)
            {
                _materialLogCount++;
                MaterialAnalogs++;
                FileLogger.Log(
                    $"[TICK {e.Tick}] MATERIAL ANALOG: '{spec.Id}' ≈ {analog} ({match:F1}%)",
                    FileLogger.LogLevel.Info);
            }
        }

        private static void OnDiscovery(SimEvent e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            FileLogger.Log(
                $"[TICK {e.Tick}] DISCOVERY EVENT: {e.Data} by {e.Actor?.Id.ToString() ?? "unknown"}",
                FileLogger.LogLevel.Info);
            Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "Discovery", e.Actor?.CivilizationId, e.Data);
        }

        private static void OnBuildingCreated(Simulation sim, SimEvent e)
        {
            var tile = sim.GetTile(e.Position);

            if (tile == null)
                return;

            if (string.IsNullOrEmpty(tile.OwnerCivId))
                return;

            var civ = Simulation.activeCivs?.FirstOrDefault(c => c.Id == tile.OwnerCivId);

            if (civ == null)
                return;

            civ.EmergentStructuresCount++;

            if (!string.IsNullOrEmpty(tile.DominantAxis))
            {
                civ.Capabilities[tile.DominantAxis] =
                    Math.Max(civ.GetCap(tile.DominantAxis), e.Value);
            }

            FileLogger.Log(
                $"[TICK {e.Tick}] {civ.Name}: emergent structure '{tile.BuildingName}' [{tile.DominantAxis}] quality={tile.BuildingQuality:F2}",
                FileLogger.LogLevel.Info);
            Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "BuildingCreated", e.Actor?.CivilizationId, e.Data);
        }

        private static void OnArtifactCreated(SimEvent e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            FileLogger.Log(
                $"[TICK {e.Tick}] CULTURE: artifact '{e.Data}' created by {e.Actor?.Id.ToString() ?? "unknown"}",
                FileLogger.LogLevel.Info);
            Analytics.ExtendedMetricsLogger.LogEvent(e.Tick, "ArtifactCreated", e.Actor?.CivilizationId, e.Data);

        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.UI;

namespace GenesisEngine.Systems
{
    public class Phoneme
    {
        public string Id;
        public string CivId;
        public List<SignalType> Pattern; // Последовательность сигналов, составляющая фонему
        public int Occurrences;
        public float Stability; // Насколько устойчива эта фонема
        public int LastUsedTick;
    }
    public static class PhonemeSystem
    {


        // Глобальный словарь фонем по цивилизациям
        private static readonly SortedDictionary<string, List<Phoneme>> CivPhonemes = new();

        public static void AnalyzeSignalSequence(Agent speaker, SignalSequencePayload payload)
        {
            if (speaker == null || payload == null || payload.Types == null || payload.Types.Count < 3)
                return;

            string civId = speaker.CivilizationId;
            if (string.IsNullOrEmpty(civId))
                return;

            // Ищем повторяющиеся паттерны в последовательности
            var phonemes = ExtractPhonemes(payload.Types);

            if (!CivPhonemes.TryGetValue(civId, out var civPhonemeList))
            {
                civPhonemeList = new List<Phoneme>();
                CivPhonemes[civId] = civPhonemeList;
            }

            foreach (var phonemePattern in phonemes)
            {
                var existing = civPhonemeList.FirstOrDefault(p =>
                    p.Pattern.Count == phonemePattern.Count &&
                    p.Pattern.SequenceEqual(phonemePattern));

                if (existing != null)
                {
                    existing.Occurrences++;
                    existing.Stability = Math.Min(1f, existing.Stability + 0.05f);
                    existing.LastUsedTick = Simulation.Instance.TotalTicks;
                }
                else
                {
                    var newPhoneme = new Phoneme
                    {
                        Id = $"PH_{civPhonemeList.Count:D3}",
                        CivId = civId,
                        Pattern = new List<SignalType>(phonemePattern),
                        Occurrences = 1,
                        Stability = 0.1f,
                        LastUsedTick = Simulation.Instance.TotalTicks
                    };

                    civPhonemeList.Add(newPhoneme);

                    if (newPhoneme.Occurrences >= 5)
                    {
                        FileLogger.Log(
                            $"[TICK {Simulation.Instance.TotalTicks}] PHONEME: civ {civId} stabilized phoneme " +
                            $"'{newPhoneme.Id}' pattern=[{string.Join(">", newPhoneme.Pattern)}] " +
                            $"occurrences={newPhoneme.Occurrences}",
                            FileLogger.LogLevel.Info);
                    }
                }
            }
        }

        private static List<List<SignalType>> ExtractPhonemes(List<SignalType> sequence)
        {
            var phonemes = new List<List<SignalType>>();

            // Ищем повторяющиеся биграммы и триграммы
            for (int length = 2; length <= Math.Min(3, sequence.Count); length++)
            {
                for (int i = 0; i <= sequence.Count - length; i++)
                {
                    var subsequence = sequence.GetRange(i, length);

                    // Проверяем, встречается ли этот паттерн ещё где-то
                    int occurrences = 0;
                    for (int j = 0; j <= sequence.Count - length; j++)
                    {
                        if (j == i) continue;

                        bool match = true;
                        for (int k = 0; k < length; k++)
                        {
                            if (sequence[j + k] != subsequence[k])
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            occurrences++;
                            break;
                        }
                    }

                    // Если паттерн повторяется, считаем его фонемой
                    if (occurrences > 0)
                    {
                        phonemes.Add(subsequence);
                    }
                }
            }

            return phonemes;
        }

        public static List<Phoneme> GetPhonemes(string civId)
        {
            if (string.IsNullOrEmpty(civId))
                return new List<Phoneme>();

            return CivPhonemes.GetValueOrDefault(civId, new List<Phoneme>())
                .Where(p => p.Occurrences >= 3)
                .ToList();
        }

        public static int PhonemeCount(string civId)
        {
            return GetPhonemes(civId).Count;
        }

        public static void CleanupOldPhonemes(int maxAge = 5000)
        {
            int currentTick = Simulation.Instance.TotalTicks;

            foreach (var civPhonemes in CivPhonemes.Values)
            {
                civPhonemes.RemoveAll(p => currentTick - p.LastUsedTick > maxAge && p.Occurrences < 5);
            }
        }
    }
}
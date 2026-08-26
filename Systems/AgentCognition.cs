using System;
using System.Linq;
using GenesisEngine.Entities;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class AgentCognition
    {
        public static bool TryLearnAndWrite(Agent a, Tile tile, Random rng)
        {
            if (a == null || tile == null)
                return false;

            // Чтение текстов
            if (tile.Texts.Count > 0)
            {
                float readChance =
                    0.05f +
                    a.Genome.Openness * 0.08f +
                    a.Genome.SelfAwareness * 0.05f;

                if (rng.NextDouble() < readChance)
                {
                    if (KnowledgeSystem.TryReadFromText(a, tile, rng))
                    {
                        a.LastAction = "Read";
                        return true;
                    }
                }
            }

            // Запись знания
            if (tile.Texts.Count < 8 && a.Genome.SelfAwareness > 0.45f)
            {
                float writeChance =
                    0.010f +
                    a.Genome.Conscientiousness * 0.020f +
                    a.Genome.SelfAwareness * 0.020f +
                    tile.InstitutionLevel * 0.004f;

                if (rng.NextDouble() < writeChance)
                {
                    var knowledge = KnowledgeSystem.All
                        .FirstOrDefault(k =>
                            k.Knowers.Contains(a.Id) &&
                            !k.RecordedInText);

                    if (knowledge != null)
                    {
                        var text = KnowledgeSystem.WriteKnowledge(a, tile, knowledge);

                        if (text != null)
                        {
                            a.LastAction = "Write";
                            return true;
                        }
                    }
                }
            }


            // Сборка логического устройства
            if (LogicSystem.TryAssembleLogicDevice(a, tile, rng))
            {
                a.LastAction = "AssembleLogic";
                return true;
            }

            // === НОВОЕ: Попытка создать графему для устойчивой фонемы ===
            if (tile.InstitutionAxis == "knowledge" && tile.InstitutionLevel >= 1.5f)
            {
                var phonemes = PhonemeSystem.GetPhonemes(a.CivilizationId);
                if (phonemes.Count > 0)
                {
                    // Берем самую устойчивую (часто используемую) фонему
                    var bestPhoneme = phonemes.OrderByDescending(p => p.Occurrences).First();

                    if (GraphemeSystem.TryCreateGrapheme(a, bestPhoneme, tile, rng))
                    {
                        a.LastAction = "DrawGrapheme";
                        return true;
                    }
                }
            }

            // === НОВОЕ (ПАКЕТ 10): Символическая манипуляция и поиск инвариантов ===
            if (tile.InstitutionAxis == "knowledge" && tile.InstitutionLevel >= 2f && a.Logic > 0.55f)
            {
                if (SymbolicManipulationSystem.TryManipulateSymbols(a, tile, rng))
                {
                    a.LastAction = "SymbolicManipulation";
                    return true;
                }
            }

            return false;
        }

        
    }
}
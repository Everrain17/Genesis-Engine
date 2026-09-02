using System;
using System.Collections.Generic;
using System.Linq;

namespace GenesisEngine.Entities
{
    public class AgentGenome
    {
        public float BaseVision = 5f;
        public float BaseHearing = 10f;
        public float BaseInfluence = 1f;
        public float MaxAge = 2000;
        public float Speed = 0.7f;
        public float Fertility = 0.5f;
        public float ImmuneStrength = 0.7f;

        // НОВОЕ: Генетические устойчивости к конкретным типам патогенов (наследуются от предков)
        public List<string> GeneticResistances = new();

        public float Openness = 0.5f;
        public float Conscientiousness = 0.5f;
        public float Extraversion = 0.5f;
        public float Agreeableness = 0.5f;
        public float Neuroticism = 0.5f;
        public float Aggression = 0.5f;
        public float Courage = 0.5f;
        public float KinshipBias = 0.5f;
        public float OutgroupGenerosity = 0.2f;
        public float Forgiveness = 0.5f;
        public float Vengefulness = 0.5f;
        public float SameSexAffinity = 0.2f;
        public float BondingDrive = 0.5f;
        public float SelfAwareness = 0.05f;
        public float MasculineBehavior = 0.5f;
        public float FeminineBehavior = 0.5f;
        public float Spirituality = 0.3f;

        public static AgentGenome Random(Random rng)
        {
            return new AgentGenome
            {
                BaseVision = 5f + (float)rng.NextDouble() * 2 - 1,
                BaseHearing = 10f + (float)rng.NextDouble() * 4 - 2,
                BaseInfluence = 1f + (float)rng.NextDouble(),
                MaxAge = 1500 + rng.Next(1000),
                Speed = 0.5f + (float)rng.NextDouble() * 0.5f,
                Fertility = 0.3f + (float)rng.NextDouble() * 0.4f,
                ImmuneStrength = 0.5f + (float)rng.NextDouble() * 0.5f,
                Openness = (float)rng.NextDouble(),
                Conscientiousness = (float)rng.NextDouble(),
                Extraversion = (float)rng.NextDouble(),
                Agreeableness = (float)rng.NextDouble(),
                Neuroticism = (float)rng.NextDouble(),
                Aggression = 0.4f + (float)rng.NextDouble() * 0.6f,
                Courage = (float)rng.NextDouble(),
                KinshipBias = (float)rng.NextDouble(),
                OutgroupGenerosity = (float)rng.NextDouble() * 0.3f,
                Forgiveness = (float)rng.NextDouble(),
                Vengefulness = (float)rng.NextDouble(),
                SameSexAffinity = (float)rng.NextDouble() * 0.3f,
                BondingDrive = (float)rng.NextDouble(),
                SelfAwareness = 0.05f + (float)rng.NextDouble() * 0.80f,
                MasculineBehavior = (float)rng.NextDouble(),
                FeminineBehavior = (float)rng.NextDouble(),
                Spirituality = (float)rng.NextDouble(),
                GeneticResistances = new List<string>() // Изначально чистый геном
            };
        }

        

        public static AgentGenome Combine(AgentGenome a, AgentGenome b, Random rng)
        {
            float Avg01(float x, float y, float noise = 0.08f)
            {
                float v = (x + y) * 0.5f;
                v += ((float)rng.NextDouble() - 0.5f) * noise;
                return Math.Clamp(v, 0f, 1f);
            }

            float AvgRange(float x, float y, float min, float max, float noise = 0.1f)
            {
                float v = (x + y) * 0.5f;
                v += ((float)rng.NextDouble() - 0.5f) * noise;
                return Math.Clamp(v, min, max);
            }

            // ДЕТЕРМИНИЗМ: Объединяем списки устойчивостей от обоих родителей и сортируем
            var mergedResistances = a.GeneticResistances
                .Union(b.GeneticResistances)
                .OrderBy(x => x)
                .ToList();

            return new AgentGenome
            {
                BaseVision = AvgRange(a.BaseVision, b.BaseVision, 2f, 12f, 1f),
                BaseHearing = AvgRange(a.BaseHearing, b.BaseHearing, 5f, 25f, 2f),
                BaseInfluence = AvgRange(a.BaseInfluence, b.BaseInfluence, 0f, 10f, 0.5f),
                MaxAge = Math.Max(500, (int)((a.MaxAge + b.MaxAge) * 0.5f + rng.Next(-400, 400))),
                Speed = AvgRange(a.Speed, b.Speed, 0.2f, 1.5f, 0.1f),
                Fertility = Avg01(a.Fertility, b.Fertility, 0.1f),
                ImmuneStrength = Avg01(a.ImmuneStrength, b.ImmuneStrength, 0.1f),
                Openness = Avg01(a.Openness, b.Openness),
                Conscientiousness = Avg01(a.Conscientiousness, b.Conscientiousness),
                Extraversion = Avg01(a.Extraversion, b.Extraversion),
                Agreeableness = Avg01(a.Agreeableness, b.Agreeableness),
                Neuroticism = Avg01(a.Neuroticism, b.Neuroticism),
                Aggression = Avg01(a.Aggression, b.Aggression, 0.1f),
                Courage = Avg01(a.Courage, b.Courage),
                KinshipBias = Avg01(a.KinshipBias, b.KinshipBias),
                OutgroupGenerosity = Avg01(a.OutgroupGenerosity, b.OutgroupGenerosity, 0.05f),
                Forgiveness = Avg01(a.Forgiveness, b.Forgiveness),
                Vengefulness = Avg01(a.Vengefulness, b.Vengefulness),
                SameSexAffinity = Avg01(a.SameSexAffinity, b.SameSexAffinity, 0.05f),
                BondingDrive = Avg01(a.BondingDrive, b.BondingDrive),
                SelfAwareness = Avg01(a.SelfAwareness, b.SelfAwareness, 0.15f),
                MasculineBehavior = Avg01(a.MasculineBehavior, b.MasculineBehavior),
                FeminineBehavior = Avg01(a.FeminineBehavior, b.FeminineBehavior),
                Spirituality = Avg01(a.Spirituality, b.Spirituality),
                GeneticResistances = mergedResistances // Передаём детям
            };
        }
    }
}
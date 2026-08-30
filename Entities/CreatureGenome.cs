using System;
using System.Linq;
using GenesisEngine.Core;

namespace GenesisEngine.Entities
{
    public class CreatureGenome
    {
        // Физические параметры
        public float Size;          // 0.5f (мышь) до 5.0f (слон/медведь)
        public float Speed;         // 0.5f (черепаха) до 2.5f (гепард)
        public float Defense;       // 0.0f (голая кожа) до 1.0f (толстая шкура/панцирь)

        // Поведенческие параметры
        public float CarnivoreDrive; // 0.0f (строгий травоядный) до 1.0f (строгий хищник)
        public float Aggression;     // 0.0f (пугливый) до 1.0f (безумно агрессивный)
        public float HerdInstinct;   // 0.0f (одиночка) до 1.0f (стадный)

        // Биологические параметры
        public float Fertility;      // 0.1f (медленно размножается) до 1.0f (как кролики)
        public float MaxAge;         // 500 до 6000 тиков

        public static CreatureGenome Random(Random rng)
        {
            return new CreatureGenome
            {
                Size = 1f + (float)rng.NextDouble() * 3f, // 1.0 - 4.0
                Speed = 0.8f + (float)rng.NextDouble() * 1.2f,
                Defense = (float)rng.NextDouble(),
                CarnivoreDrive = (float)rng.NextDouble(), // Решает, хищник это или травоядное
                Aggression = (float)rng.NextDouble(),
                HerdInstinct = (float)rng.NextDouble(),
                Fertility = 0.2f + (float)rng.NextDouble() * 0.8f,
                MaxAge = 1000 + rng.Next(4000)
            };
        }

        public static CreatureGenome Combine(CreatureGenome a, CreatureGenome b, Random rng)
        {
            return new CreatureGenome
            {
                Size = Avg(a.Size, b.Size, rng),
                Speed = Avg(a.Speed, b.Speed, rng),
                Defense = Avg(a.Defense, b.Defense, rng),
                CarnivoreDrive = Avg(a.CarnivoreDrive, b.CarnivoreDrive, rng),
                Aggression = Avg(a.Aggression, b.Aggression, rng),
                HerdInstinct = Avg(a.HerdInstinct, b.HerdInstinct, rng),
                Fertility = Avg(a.Fertility, b.Fertility, rng),
                MaxAge = (a.MaxAge + b.MaxAge) / 2f + rng.Next(-500, 500)
            };
        }

        public CreatureGenome Mutate(Random rng)
        {
            float M(float v, float rate) => Math.Clamp(v + ((float)rng.NextDouble() - 0.5f) * rate, 0f, v == nameof(MaxAge) ? 10000f : (v == nameof(Size) ? 6f : 1f));

            return new CreatureGenome
            {
                Size = Math.Clamp(Size + ((float)rng.NextDouble() - 0.5f) * 0.5f, 0.5f, 6f),
                Speed = Math.Clamp(Speed + ((float)rng.NextDouble() - 0.5f) * 0.3f, 0.5f, 3f),
                Defense = Math.Clamp(Defense + ((float)rng.NextDouble() - 0.5f) * 0.2f, 0f, 1f),
                CarnivoreDrive = Math.Clamp(CarnivoreDrive + ((float)rng.NextDouble() - 0.5f) * 0.1f, 0f, 1f),
                Aggression = Math.Clamp(Aggression + ((float)rng.NextDouble() - 0.5f) * 0.2f, 0f, 1f),
                HerdInstinct = Math.Clamp(HerdInstinct + ((float)rng.NextDouble() - 0.5f) * 0.2f, 0f, 1f),
                Fertility = Math.Clamp(Fertility + ((float)rng.NextDouble() - 0.5f) * 0.1f, 0.1f, 1f),
                MaxAge = Math.Max(500, MaxAge + rng.Next(-300, 300))
            };
        }

        private static float Avg(float a, float b, Random rng)
        {
            float mid = (a + b) / 2f;
            return Math.Clamp(mid + ((float)rng.NextDouble() - 0.5f) * 0.2f, 0f, 1f);
        }
    }
}
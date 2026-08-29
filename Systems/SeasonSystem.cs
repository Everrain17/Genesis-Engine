using System;

namespace GenesisEngine.Systems
{
    /// <summary>
    /// Система времён года: влияет на урожай, метаболизм, здоровье.
    /// Год = 4000 тиков (1000 тиков на сезон).
    /// </summary>
    public static class SeasonSystem
    {
        public enum Season { Spring, Summer, Autumn, Winter }

        public const int TicksPerSeason = 1000;
        public const int TicksPerYear = TicksPerSeason * 4;

        public static Season GetCurrentSeason(int tick)
        {
            int seasonIndex = (tick / TicksPerSeason) % 4;
            return (Season)seasonIndex;
        }

        public static string GetSeasonName(Season season) => season switch
        {
            Season.Spring => "Spring",
            Season.Summer => "Summer",
            Season.Autumn => "Autumn",
            Season.Winter => "Winter",
            _ => "Unknown"
        };

        /// <summary>
        /// Модификатор урожайности (Fertility multiplier)
        /// </summary>
        public static float GetFertilityModifier(Season season) => season switch
        {
            Season.Spring => 1.2f,   // Весна: рост +20%
            Season.Summer => 1.5f,   // Лето: пик урожая +50%
            Season.Autumn => 0.8f,   // Осень: спад -20%
            Season.Winter => 0.2f,   // Зима: почти нет урожая -80%
            _ => 1.0f
        };

        /// <summary>
        /// Модификатор голода (Hunger rate multiplier)
        /// </summary>
        public static float GetHungerModifier(Season season) => season switch
        {
            Season.Spring => 1.0f,   // Нормально
            Season.Summer => 0.9f,   // Легче (тепло)
            Season.Autumn => 1.1f,   // Чуть тяжелее
            Season.Winter => 1.5f,   // Тяжело (холод + нет еды)
            _ => 1.0f
        };

        /// <summary>
        /// Урон от холода зимой (Health drain per tick)
        /// </summary>
        public static float GetColdDamage(Season season, float clothingProtection)
        {
            if (season != Season.Winter)
                return 0f;

            // Базовый урон 0.1, снижается одеждой/зданиями
            float baseDamage = 0.08f;
            return Math.Max(0f, baseDamage * (1f - clothingProtection));
        }

        /// <summary>
        /// Модификатор скорости строительства
        /// </summary>
        public static float GetBuildingModifier(Season season) => season switch
        {
            Season.Spring => 1.2f,   // Легко строить
            Season.Summer => 1.0f,   // Нормально
            Season.Autumn => 0.8f,   // Сложнее
            Season.Winter => 0.4f,   // Очень сложно
            _ => 1.0f
        };

        /// <summary>
        /// Модификатор торговли (зимой меньше)
        /// </summary>
        public static float GetTradeModifier(Season season) => season switch
        {
            Season.Spring => 1.0f,
            Season.Summer => 1.2f,   // Лето: активная торговля
            Season.Autumn => 1.1f,   // Осень: подготовка к зиме
            Season.Winter => 0.5f,   // Зима: мало торговли
            _ => 1.0f
        };
        public static float GetImmunityModifier(Season season)
        {
            return season switch
            {
                Season.Spring => 1.1f,   // Весна: иммунитет чуть выше
                Season.Summer => 1.2f,   // Лето: иммунитет выше
                Season.Autumn => 0.9f,   // Осень: иммунитет чуть ниже
                Season.Winter => 0.7f,   // Зима: иммунитет ниже
                _ => 1.0f
            };
        }
    }
}
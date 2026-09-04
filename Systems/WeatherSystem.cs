using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.UI;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems
{
    /// <summary>
    /// Эмерджентная погодная система.
    /// Нет типов "дождь/снег/ураган" — есть только параметры, из которых возникают явления.
    /// </summary>
    public static class WeatherSystem
    {
        // Глобальные погодные параметры (меняются медленно, влияют на весь мир)
        private static float GlobalTemperature = 0.5f;  // 0 = арктика, 1 = пустыня
        private static float GlobalHumidity = 0.5f;     // 0 = засуха, 1 = тропики
        private static float GlobalWindSpeed = 0.3f;    // 0 = штиль, 1+ = ураган
        private static float GlobalPrecipitation = 0.2f; // 0 = ясно, 1+ = ливень
        private static float GlobalPressure = 0.5f;     // 0 = низкое, 1 = высокое

        // Скорость изменения глобальных параметров
        private static float TemperatureDrift = 0f;
        private static float HumidityDrift = 0f;
        private static float WindDrift = 0f;
        private static float PrecipitationDrift = 0f;

        private static int _lastUpdateTick = 0;
        private const int UpdateInterval = 50; // Обновление каждые 50 тиков

        /// <summary>
        /// Локальные погодные параметры тайла
        /// </summary>
        public struct WeatherState
        {
            public float Temperature;
            public float Humidity;
            public float WindSpeed;
            public float Precipitation;
            public float Pressure;

            // Производные эффекты (вычисляются из параметров)
            public float Visibility => Math.Clamp(1f - WindSpeed * 0.3f - Precipitation * 0.4f, 0f, 1f);
            public float Comfort => Math.Clamp(1f - Math.Abs(Temperature - 0.5f) * 2f - WindSpeed * 0.3f, 0f, 1f);
            public float DangerLevel => Math.Max(
                WindSpeed > 0.8f ? (WindSpeed - 0.8f) * 5f : 0f,
                Math.Max(
                    Temperature > 0.9f ? (Temperature - 0.9f) * 10f : 0f,
                    Temperature < 0.1f ? (0.1f - Temperature) * 10f : 0f
                )
            );
        }

        /// <summary>
        /// Обновление погоды. Вызывается из Simulation.Tick()
        /// </summary>
        public static void Update(Tile[,] world, List<Agent> agents, int tick)
        {
            if (world == null) return;

            // Обновляем глобальные параметры каждые UpdateInterval тиков
            if (tick - _lastUpdateTick >= UpdateInterval)
            {
                UpdateGlobalParameters(tick);
                _lastUpdateTick = tick;
            }

            // Применяем погоду к каждому тайлу
            ApplyWeatherToWorld(world, tick);

            // Влияние на агентов
            ApplyWeatherEffects(agents, world, tick);
        }

        /// <summary>
        /// Медленное изменение глобальных параметров с привязкой к сезонам.
        /// </summary>
        private static void UpdateGlobalParameters(int tick)
        {
            var rng = RandomProvider.GetRandom();
            var season = SeasonSystem.GetCurrentSeason(tick);

            // 1. ПРИВЯЗКА К СЕЗОНАМ: Глобальная температура стремится к сезонной норме
            float seasonalTarget = season switch
            {
                SeasonSystem.Season.Spring => 0.50f,  // Прохладно, но комфортно
                SeasonSystem.Season.Summer => 0.80f,  // Тепло
                SeasonSystem.Season.Autumn => 0.40f,  // Прохладная осень (но НЕ ниже порога холода 0.2f!)
                SeasonSystem.Season.Winter => 0.15f,  // Холодная зима (здесь сработает логика холода)
                _ => 0.50f
            };


            // Плавная, но более быстрая интерполяция к целевому значению + небольшой шум
            // (0.7f * старое + 0.3f * новое = быстрый прогрев за ~150-200 тиков)
            float noise = (float)(rng.NextDouble() - 0.5f) * 0.1f; // Уменьшили шум с 0.15f до 0.1f
            GlobalTemperature = Math.Clamp(GlobalTemperature * 0.7f + seasonalTarget * 0.3f + noise, 0f, 1f);

            // 2. Влажность, ветер и осадки дрейфуют, но с мягкими ограничениями (не даём им уйти в абсолютный 0 или 1.5 навсегда)
            HumidityDrift += (float)(rng.NextDouble() - 0.5f) * 0.03f;
            WindDrift += (float)(rng.NextDouble() - 0.5f) * 0.04f;
            PrecipitationDrift += (float)(rng.NextDouble() - 0.5f) * 0.05f;

            // Затухание дрейфа (возврат к норме)
            HumidityDrift *= 0.95f;
            WindDrift *= 0.90f;
            PrecipitationDrift *= 0.90f;

            // Ограничиваем диапазоны, чтобы погода не сходила с ума
            GlobalHumidity = Math.Clamp(GlobalHumidity + HumidityDrift, 0.2f, 0.9f);
            GlobalWindSpeed = Math.Clamp(GlobalWindSpeed + WindDrift, 0.1f, 1.2f);
            GlobalPrecipitation = Math.Clamp(GlobalPrecipitation + PrecipitationDrift, 0.0f, 1.2f);

            // Давление зависит от температуры и влажности
            GlobalPressure = Math.Clamp(0.5f + (GlobalTemperature - 0.5f) * 0.3f - GlobalHumidity * 0.2f, 0f, 1f);

            // Логируем только настоящие аномалии (например, аномально холодная зима или аномально ветреная осень)
            if (GlobalWindSpeed > 0.9f || GlobalPrecipitation > 1.0f || GlobalTemperature < 0.1f)
            {
                LogExtremeWeather(tick);
            }
        }

        /// <summary>
        /// Применяем глобальную погоду к каждому тайлу с локальными вариациями
        /// </summary>
        private static void ApplyWeatherToWorld(Tile[,] world, int tick)
        {
            int width = world.GetLength(0);
            int height = world.GetLength(1);
            var rng = RandomProvider.GetRandom();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tile = world[x, y];
                    if (!tile.IsPassable) continue;

                    // Локальные вариации на основе биома и шума
                    float localTempMod = GetTerrainTemperatureModifier(tile.Terrain);
                    float localHumMod = GetTerrainHumidityModifier(tile.Terrain);
                    float noise = (float)(rng.NextDouble() - 0.5f) * 0.1f;

                    tile.Weather = new WeatherState
                    {
                        Temperature = Math.Clamp(GlobalTemperature + localTempMod + noise, 0f, 1f),
                        Humidity = Math.Clamp(GlobalHumidity + localHumMod + noise, 0f, 1f),
                        WindSpeed = Math.Clamp(GlobalWindSpeed + noise * 0.5f, 0f, 1.5f),
                        Precipitation = Math.Clamp(GlobalPrecipitation + noise, 0f, 1.5f),
                        Pressure = GlobalPressure
                    };

                    // Применяем эффекты погоды к тайлу
                    ApplyWeatherEffectsToTile(tile, tick);
                }
            }
        }

        /// <summary>
        /// Влияние погоды на тайл (эмерджентные эффекты)
        /// </summary>
        private static void ApplyWeatherEffectsToTile(Tile tile, int tick)
        {


            // Высокая влажность + низкая температура = конденсация (рост органики)
            if (tile.Weather.Humidity > 0.7f && tile.Weather.Temperature < 0.4f)
            {
                tile.Fertility = Math.Min(1f, tile.Fertility + 0.001f);
            }

            // Сильный ветер + низкая влажность = эрозия (потеря плодородия)
            if (tile.Weather.WindSpeed > 0.6f && tile.Weather.Humidity < 0.3f)
            {
                tile.Fertility = Math.Max(0.05f, tile.Fertility - 0.002f);
            }

            // Экстремальная жара = высушивание
            if (tile.Weather.Temperature > 0.85f)
            {
                tile.Fertility = Math.Max(0.05f, tile.Fertility - 0.003f);
                tile.Exhaustion = Math.Min(0.9f, tile.Exhaustion + 0.001f);
            }

            // Экстремальный холод = заморозка
            if (tile.Weather.Temperature < 0.15f)
            {
                tile.Fertility = Math.Max(0.05f, tile.Fertility - 0.002f);
            }

            // Сильные осадки = возможное наводнение (если низина)
            if (tile.Weather.Precipitation > 0.9f && tile.Elevation < 0.3f)
            {
                tile.Exhaustion = Math.Min(0.9f, tile.Exhaustion + 0.005f);
            }
        }

        /// <summary>
        /// Влияние погоды на агентов
        /// </summary>
        private static void ApplyWeatherEffects(List<Agent> agents, Tile[,] world, int tick)
        {
            if (agents == null || world == null) return;

            foreach (var agent in agents)
            {
                if (agent.Body.Health <= 0) continue;

                var tile = world[agent.Position.X, agent.Position.Y];

                var weather = tile.Weather;

                if (weather.Temperature < 0.15f)
                {
                    float protection = 0f;
                    if (tile.IsHouse || tile.IsTemple) protection += 0.7f;
                    if (agent.Body.Inventory.Any(o => MaterialDB.TryGet(o.MaterialId, out var spec) && spec.Organic > 0.5f))
                        protection += 0.3f;
                    float damage = (0.15f - weather.Temperature) * 2f * (1f - protection);
                    agent.Body.Health -= damage;
                    agent.Body.Energy -= damage * 1.3f;
                    if (damage > 0.3f)  // ← понизил с 0.5 до 0.3 — больше фиксаций
                    {
                        agent.LastDamageType = "cold";     // ← НОВОЕ
                        agent.LastAction = "Cold";
                        CognitionSystem.Record("weather.cold_damage", damage);
                    }
                }

                // Жара: обезвоживание (голод растёт быстрее)
                if (weather.Temperature > 0.85f)
                {
                    float protection = 0f;
                    if (tile.IsHouse || tile.IsTemple) protection += 0.5f;

                    float stress = (weather.Temperature - 0.8f) * 5f * (1f - protection);
                    agent.Body.Hunger = Math.Min(100f, agent.Body.Hunger + stress);
                    agent.Body.Energy -= stress * 0.5f;

                    if (stress > 0.5f)
                    {
                        CognitionSystem.Record("weather.heat_stress", stress);
                    }
                }

                // Сильный ветер: замедление, потеря энергии
                if (weather.WindSpeed > 0.7f)
                {
                    float resistance = 0.5f + agent.Genome.Conscientiousness * 0.3f;
                    float penalty = (weather.WindSpeed - 0.7f) * 3f / resistance;
                    agent.Body.Energy -= penalty;

                    if (penalty > 0.3f)
                    {
                        CognitionSystem.Record("weather.wind_resistance", penalty);
                    }
                }

                // Сильные осадки: дискомфорт, возможная болезнь
                if (weather.Precipitation > 0.8f)
                {
                    agent.Loneliness = Math.Min(100f, agent.Loneliness + 0.1f);
                    agent.Fear = Math.Min(100f, agent.Fear + 0.05f);

                    // Шанс простуды (если нет укрытия)
                    if (!tile.IsHouse && !tile.IsTemple && RandomProvider.GetFloat() < 0.01f)
                    {
                        CognitionSystem.Record("weather.exposure", 1f);
                    }
                }

                // Комфортная погода: бонус к энергии
                if (weather.Comfort > 0.7f)
                {
                    agent.Body.Energy = Math.Min(100f, agent.Body.Energy + 0.1f);
                }
            }
        }

        /// <summary>
        /// Логи экстремальных погодных событий
        /// </summary>
        private static void LogExtremeWeather(int tick)
        {
            var events = new List<string>();

            if (GlobalWindSpeed > 0.9f)
                events.Add($"EXTREME WIND: {GlobalWindSpeed:F2}");
            if (GlobalPrecipitation > 1.0f)
                events.Add($"EXTREME PRECIPITATION: {GlobalPrecipitation:F2}");
            if (GlobalTemperature > 0.9f)
                events.Add($"EXTREME HEAT: {GlobalTemperature:F2}");
            if (GlobalTemperature < 0.1f)
                events.Add($"EXTREME COLD: {GlobalTemperature:F2}");

            if (events.Count > 0)
            {
                FileLogger.Log(
                    $"[TICK {tick}] WEATHER ANOMALY: {string.Join(", ", events)}",
                    FileLogger.LogLevel.Warning);
            }
        }

        /// <summary>
        /// Модификатор температуры для биома
        /// </summary>
        private static float GetTerrainTemperatureModifier(TerrainType terrain) => terrain switch
        {
            TerrainType.Desert => 0.3f,
            TerrainType.Tundra => -0.3f,
            TerrainType.IcePeak => -0.4f,
            TerrainType.Swamp => 0.1f,
            TerrainType.Mountain => -0.2f,
            TerrainType.Forest => -0.05f,
            TerrainType.Taiga => -0.15f,
            _ => 0f
        };

        /// <summary>
        /// Модификатор влажности для биома
        /// </summary>
        private static float GetTerrainHumidityModifier(TerrainType terrain) => terrain switch
        {
            TerrainType.Swamp => 0.3f,
            TerrainType.Forest => 0.2f,
            TerrainType.Desert => -0.3f,
            TerrainType.Tundra => -0.1f,
            TerrainType.Beach => 0.1f,
            _ => 0f
        };

        /// <summary>
        /// Получить текущие глобальные параметры (для UI/логгера)
        /// </summary>
        public static (float temp, float humidity, float wind, float precipitation) GetGlobalState()
        {
            return (GlobalTemperature, GlobalHumidity, GlobalWindSpeed, GlobalPrecipitation);
        }
    }
}
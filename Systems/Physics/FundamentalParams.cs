using System;

namespace GenesisEngine.Systems.Physics
{
    // Фундаментальные параметры (аналог атомарных свойств)
    public struct FundamentalParams
    {
        public float BondEnergy;        // Энергия связи (0-1) - влияет на твердость, температуру плавления
        public float ElectronDensity;   // Плотность электронов (0-1) - проводимость, ковкость
        public float LatticeSymmetry;   // Симметрия решетки (0-1) - хрупкость/гибкость
        public float AtomicMass;        // Атомная масса (0-1) - плотность, вес
        public float ThermalVibration;  // Тепловые колебания (0-1) - теплоемкость
        public float QuantumCoherence;  // Квантовая когерентность (0-1) - для квантовых эффектов
    }

    // Наблюдаемые свойства (то, что видят агенты)
    public class ObservableProperties
    {
        public float Hardness;          // Твердость
        public float Flexibility;       // Гибкость
        public float Conductivity;      // Электропроводность
        public float ThermalConductivity; // Теплопроводность
        public float HeatCapacity;      // Теплоемкость
        public float MeltingPoint;      // Температура плавления
        public float Density;           // Плотность
        public float Brittleness;       // Хрупкость
        public float Malleability;      // Ковкость
        public float Logic;             // Способность хранить информацию (для компьютеров)
        public float Organic;           // Органика
        public float HeatOutput;        // Тепловыделение
        public float Durability;        // Износостойкость
        public float Rarity;            // Редкость
        public float Buoyancy;          // Плавучесть
        public float Salt;              // Соленость
    }

    public static class MaterialPhysics
    {
        // Вывод наблюдаемых свойств из фундаментальных параметров
        public static ObservableProperties DeriveProperties(FundamentalParams p, Random rng = null)
        {
            var props = new ObservableProperties();

            // Твердость: высокая энергия связи + низкая симметрия = твердый материал
            props.Hardness = Math.Clamp((p.BondEnergy * 1.5f) - (p.LatticeSymmetry * 0.5f) + (p.AtomicMass * 0.2f), 0, 1);

            // Гибкость: высокая симметрия + низкая энергия связи = гибкий
            props.Flexibility = Math.Clamp((p.LatticeSymmetry * 1.2f) - (p.BondEnergy * 0.8f), 0, 1);

            // Электропроводность: прямо от плотности электронов
            props.Conductivity = Math.Clamp(p.ElectronDensity * 1.3f, 0, 1);

            // Теплопроводность: электроны + тепловые колебания
            props.ThermalConductivity = Math.Clamp((p.ElectronDensity * 0.7f) + (p.ThermalVibration * 0.5f), 0, 1);

            // Теплоемкость: атомная масса + тепловые колебания
            props.HeatCapacity = Math.Clamp((p.AtomicMass * 0.6f) + (p.ThermalVibration * 0.8f), 0, 1);

            // Температура плавления: энергия связи
            props.MeltingPoint = Math.Clamp(p.BondEnergy * 1.5f, 0, 1);

            // Плотность: атомная масса
            props.Density = Math.Clamp(p.AtomicMass * 1.2f, 0, 1);

            // Хрупкость: высокая твердость + низкая гибкость
            props.Brittleness = Math.Clamp(props.Hardness * 0.7f - props.Flexibility * 0.5f, 0, 1);

            // Ковкость: гибкость + проводимость
            props.Malleability = Math.Clamp((props.Flexibility * 0.6f) + (props.Conductivity * 0.4f), 0, 1);

            // Логика (для компьютеров): квантовая когерентность + проводимость
            props.Logic = Math.Clamp((p.QuantumCoherence * 1.5f) + (props.Conductivity * 0.3f), 0, 1);

            // Органика: низкая энергия связи + низкая плотность
            props.Organic = Math.Clamp(1.0f - (p.BondEnergy * 0.8f) - (p.AtomicMass * 0.4f), 0, 1);

            // Тепловыделение: тепловые колебания
            props.HeatOutput = Math.Clamp(p.ThermalVibration * 1.2f, 0, 1);

            // Износостойкость: твердость + ковкость
            props.Durability = Math.Clamp((props.Hardness * 0.5f) + (props.Malleability * 0.5f), 0, 1);

            // Редкость: случайная (генерируется отдельно)
            props.Rarity = rng != null ? (float)rng.NextDouble() : 0.5f;

            // Плавучесть: низкая плотность
            props.Buoyancy = Math.Clamp(1.0f - props.Density, 0, 1);

            // Соленость: случайная
            props.Salt = rng != null ? (float)rng.NextDouble() * 0.3f : 0.1f;

            return props;
        }

        // Генерация базового материала (случайные фундаментальные параметры)
        public static (FundamentalParams, ObservableProperties) GenerateBaseMaterial(Random rng)
        {
            var fp = new FundamentalParams
            {
                BondEnergy = (float)rng.NextDouble(),
                ElectronDensity = (float)rng.NextDouble(),
                LatticeSymmetry = (float)rng.NextDouble(),
                AtomicMass = (float)rng.NextDouble(),
                ThermalVibration = (float)rng.NextDouble(),
                QuantumCoherence = (float)rng.NextDouble() * 0.3f // Редко высокое значение
            };

            var props = DeriveProperties(fp, rng);
            return (fp, props);
        }

        // Комбинирование двух материалов (сплавы, композиты)
        public static (FundamentalParams, ObservableProperties) Mix(FundamentalParams a, FundamentalParams b, float ratioA = 0.5f, Random rng = null)
        {
            float ratioB = 1f - ratioA;

            // Нелинейное взаимодействие! (Эффект искажения решетки)
            float distortion = Math.Abs(a.LatticeSymmetry - b.LatticeSymmetry);
            float electronMismatch = Math.Abs(a.ElectronDensity - b.ElectronDensity);

            var mixed = new FundamentalParams
            {
                // Энергия связи: среднее + бонус за искажение (упрочнение сплава)
                BondEnergy = Math.Clamp((a.BondEnergy * ratioA) + (b.BondEnergy * ratioB) + (distortion * 0.2f), 0, 1),

                // Плотность электронов: среднее - штраф за несоответствие
                ElectronDensity = Math.Clamp((a.ElectronDensity * ratioA) + (b.ElectronDensity * ratioB) - (electronMismatch * 0.1f), 0, 1),

                // Симметрия решетки: среднее - искажение (хрупкость сплава)
                LatticeSymmetry = Math.Clamp((a.LatticeSymmetry * ratioA) + (b.LatticeSymmetry * ratioB) - (distortion * 0.15f), 0, 1),

                // Атомная масса: простое среднее
                AtomicMass = (a.AtomicMass * ratioA) + (b.AtomicMass * ratioB),

                // Тепловые колебания: среднее + небольшой бонус
                ThermalVibration = Math.Clamp((a.ThermalVibration * ratioA) + (b.ThermalVibration * ratioB) + 0.05f, 0, 1),

                // Квантовая когерентность: редко сохраняется при смешивании
                QuantumCoherence = Math.Clamp((a.QuantumCoherence * ratioA) + (b.QuantumCoherence * ratioB) - 0.1f, 0, 1)
            };

            var props = DeriveProperties(mixed, rng);
            return (mixed, props);
        }

        // Специальные эффекты при определенных комбинациях
        public static ObservableProperties ApplySpecialEffects(FundamentalParams fp, ObservableProperties props, Random rng)
        {
            // Эффект "бронзы": медь + олово = резкий скачок твердости
            if (fp.BondEnergy > 0.7f && fp.ElectronDensity > 0.6f && rng.NextDouble() < 0.3f)
            {
                props.Hardness = Math.Min(1f, props.Hardness + 0.3f);
                props.Durability = Math.Min(1f, props.Durability + 0.2f);
            }

            // Эффект "стали": высокая энергия связи + средняя гибкость
            if (fp.BondEnergy > 0.8f && props.Flexibility > 0.3f && props.Flexibility < 0.6f)
            {
                props.Hardness = Math.Min(1f, props.Hardness + 0.2f);
                props.Malleability = Math.Min(1f, props.Malleability + 0.3f);
            }

            // Эффект "полупроводника": средняя проводимость + высокая когерентность
            if (props.Conductivity > 0.3f && props.Conductivity < 0.7f && fp.QuantumCoherence > 0.5f)
            {
                props.Logic = Math.Min(1f, props.Logic + 0.5f);
            }

            // Эффект "сверхпроводника": высокая проводимость + низкие тепловые колебания
            if (props.Conductivity > 0.8f && fp.ThermalVibration < 0.2f && rng.NextDouble() < 0.1f)
            {
                props.Conductivity = 1.0f;
                props.ThermalConductivity = 0.1f; // Низкое тепловыделение
            }

            return props;
        }
    }
}
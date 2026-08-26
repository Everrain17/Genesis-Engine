using System;
using System.Collections.Generic;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems.Physics
{
    public static class MaterialAnalyzer
    {
        // Эталонные векторы реальных материалов (нормализованы 0.0 - 1.0)
        public class RealMaterial
        {
            public string Name;
            public float Hardness;
            public float Conductivity;
            public float Density;
            public float Flexibility;
            public float Organic;
            public float HeatOutput;
        }

        public static readonly List<RealMaterial> RealWorldDatabase = new()
        {
            new RealMaterial { Name = "Дерево (Wood)", Hardness = 0.3f, Conductivity = 0.1f, Density = 0.4f, Flexibility = 0.6f, Organic = 0.9f, HeatOutput = 0.5f },
            new RealMaterial { Name = "Камень (Stone)", Hardness = 0.7f, Conductivity = 0.2f, Density = 0.8f, Flexibility = 0.1f, Organic = 0.0f, HeatOutput = 0.1f },
            new RealMaterial { Name = "Медь (Copper)", Hardness = 0.5f, Conductivity = 0.9f, Density = 0.8f, Flexibility = 0.7f, Organic = 0.0f, HeatOutput = 0.2f },
            new RealMaterial { Name = "Железо/Сталь (Iron/Steel)", Hardness = 0.8f, Conductivity = 0.6f, Density = 0.9f, Flexibility = 0.5f, Organic = 0.0f, HeatOutput = 0.2f },
            new RealMaterial { Name = "Золото (Gold)", Hardness = 0.3f, Conductivity = 1.0f, Density = 1.0f, Flexibility = 0.9f, Organic = 0.0f, HeatOutput = 0.1f },
            new RealMaterial { Name = "Кремний (Silicon)", Hardness = 0.7f, Conductivity = 0.5f, Density = 0.6f, Flexibility = 0.2f, Organic = 0.0f, HeatOutput = 0.1f },
            new RealMaterial { Name = "Уголь (Coal)", Hardness = 0.4f, Conductivity = 0.3f, Density = 0.5f, Flexibility = 0.2f, Organic = 0.8f, HeatOutput = 0.9f },
            new RealMaterial { Name = "Резина/Смола (Rubber/Resin)", Hardness = 0.2f, Conductivity = 0.1f, Density = 0.5f, Flexibility = 0.9f, Organic = 0.8f, HeatOutput = 0.6f },
            new RealMaterial { Name = "Стекло/Кристалл (Glass)", Hardness = 0.8f, Conductivity = 0.1f, Density = 0.7f, Flexibility = 0.0f, Organic = 0.0f, HeatOutput = 0.1f },
            new RealMaterial { Name = "Кость/Кожа (Bone/Hide)", Hardness = 0.4f, Conductivity = 0.2f, Density = 0.6f, Flexibility = 0.7f, Organic = 0.9f, HeatOutput = 0.3f },
            new RealMaterial { Name = "Свинец (Lead)", Hardness = 0.2f, Conductivity = 0.5f, Density = 0.95f, Flexibility = 0.8f, Organic = 0.0f, HeatOutput = 0.1f },
            new RealMaterial { Name = "Алюминий (Aluminum)", Hardness = 0.4f, Conductivity = 0.8f, Density = 0.5f, Flexibility = 0.6f, Organic = 0.0f, HeatOutput = 0.2f }
        };

        public static (string AnalogName, float MatchPercentage) FindAnalog(ObservableProperties props)
        {
            RealMaterial bestMatch = null;
            float minDistance = float.MaxValue;

            foreach (var realMat in RealWorldDatabase)
            {
                // Евклидово расстояние в 6-мерном пространстве свойств
                float dist = MathF.Sqrt(
                    MathF.Pow(props.Hardness - realMat.Hardness, 2) +
                    MathF.Pow(props.Conductivity - realMat.Conductivity, 2) +
                    MathF.Pow(props.Density - realMat.Density, 2) +
                    MathF.Pow(props.Flexibility - realMat.Flexibility, 2) +
                    MathF.Pow(props.Organic - realMat.Organic, 2) +
                    MathF.Pow(props.HeatOutput - realMat.HeatOutput, 2)
                );

                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestMatch = realMat;
                }
            }

            // Максимально возможное расстояние в нормализованном пространстве (0-1) для 6 параметров = sqrt(6) ≈ 2.45
            float maxPossibleDistance = 2.45f;
            float matchPercentage = MathF.Max(0, 100f - (minDistance / maxPossibleDistance * 100f));

            return (bestMatch?.Name ?? "Неизвестный материал", matchPercentage);
        }
    }
}
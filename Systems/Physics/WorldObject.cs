using System.Collections.Generic;
using GenesisEngine.Core;

namespace GenesisEngine.Systems.Physics
{
    public class WorldObject
    {
        public Guid Id = Guid.Empty;
        public string MaterialId;
        public float Quantity = 1f;
        public Guid HolderId = Guid.Empty;
        public Vector2? Position = null;
        public bool IsSeed = false;   // v3: эмерджентные огороды
        private static readonly Dictionary<string, float> EmptyProperties = new();
        private static readonly Dictionary<string, Dictionary<string, float>> PropertiesCache = new();
        private static readonly object CacheLock = new();
        public bool IsOreDeposit = false;   // Рудная жила — добывается только через шахту
        // Важно: возвращаемый словарь теперь кэшируется.
        // Его нельзя изменять вручную.
        public Dictionary<string, float> GetProperties()
        {
            if (string.IsNullOrEmpty(MaterialId))
                return EmptyProperties;

            if (PropertiesCache.TryGetValue(MaterialId, out var cached))
                return cached;

            if (!MaterialDB.TryGet(MaterialId, out var spec))
                return EmptyProperties;

            var dict = new Dictionary<string, float>
            {
                { "Hardness", spec.Observed.Hardness },
                { "Flexibility", spec.Observed.Flexibility },
                { "Conductivity", spec.Observed.Conductivity },
                { "ThermalConductivity", spec.Observed.ThermalConductivity },
                { "HeatCapacity", spec.Observed.HeatCapacity },
                { "MeltingPoint", spec.Observed.MeltingPoint },
                { "Density", spec.Observed.Density },
                { "Brittleness", spec.Observed.Brittleness },
                { "Malleability", spec.Observed.Malleability },
                { "Logic", spec.Observed.Logic },
                { "Organic", spec.Observed.Organic },
                { "HeatOutput", spec.Observed.HeatOutput },
                { "Durability", spec.Observed.Durability },
                { "Rarity", spec.Observed.Rarity },
                { "Buoyancy", spec.Observed.Buoyancy },
                { "Salt", spec.Observed.Salt }
            };

            lock (CacheLock)
            {
                PropertiesCache[MaterialId] = dict;
            }

            return dict;
        }
    }
}
namespace GenesisEngine.Systems
{
    public static class OptimizationSettings
    {
        // ============================================================
        // Мягкие лимиты памяти.
        // Если false — память агентов и паттерны не обрезаются.
        // ============================================================
        public static bool EnableSoftMemoryCaps = false;

        public static int MaxAgentMemories = 4096;
        public static int MaxPatternsPerAgent = 512;

        // ============================================================
        // Очистка лексики.
        // Если false — слова не удаляются.
        // ============================================================
        public static bool EnableLexiconPrune = false;

        public static int MaxWords = 50000;
        public static int PruneTargetWords = 40000;

        // ============================================================
        // Лимит композитных материалов.
        // Если false — новые композиты не ограничиваются.
        // ============================================================
        public static bool EnableCompositeCap = false;

        public static int MaxCompositeMaterials = 100000;

        // ============================================================
        // Кэш потребностей цивилизаций.
        // Это служебный кэш, его очистка не режет логику.
        // ============================================================
        public static bool EnableNeedsCachePrune = true;

        public static int MaxNeedsCacheEntries = 512;

        // ============================================================
        // Безопасность наблюдателей.
        // Если true — ошибка в наблюдателе не убивает тик.
        // ============================================================
        public static bool SafeObservers = true;

        // ============================================================
        // Ограничение логов материалов.
        // Если false — логи материалов не ограничиваются.
        // ============================================================
        public static bool ThrottleMaterialLogs = true;

        public static int MaxMaterialLogs = 20000;
    }
}
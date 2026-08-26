using System.Collections.Generic;
using System.Linq;

namespace GenesisEngine.Systems
{
    public static class ScienceEpochAnalyzer
    {
        public static string Analyze(List<CivilizationSnapshot> civs)
        {
            int words = LanguageSystem.StableWordCount();
            int grammar = GrammarSystem.RuleCount();
            int texts = CultureSystem.AllTexts.Count;
            int symbols = SymbolSystem.TotalKnownSymbols();
            int institutions = InstitutionSystem.ActiveInstitutions;

            // Разделяем простые наблюдения и сложные теории
            int mathObservations = KnowledgeSystem.All.Count(k => k.Kind == "math");
            int solidTheories = KnowledgeSystem.All.Count(k => k.Kind == "theory");
            int advancedLogic = KnowledgeSystem.All.Count(k => k.Kind == "algorithm" || k.Kind == "logic_gate" || k.Kind == "symbolic_rule");

            int gates = KnowledgeSystem.All.Count(k => k.Kind == "logic_gate");
            float computation = LogicSystem.GlobalComputationCapacity();
            float automata = LogicAutomataSystem.GetTotalComputation();

            float score =
                words * 0.4f +
                grammar * 1.2f +
                texts * 0.8f +
                symbols * 0.15f +
                institutions * 2f +
                solidTheories * 5f +      // Увеличили вес теорий
                advancedLogic * 10f +     // Большой вес за алгоритмы/вентили/символические правила
                computation * 0.5f +
                automata * 0.75f;

            // 1. Дописьменный период
            if (score < 10f || words < 3)
                return "Pre-Symbolic";

            // 2. Появление устойчивых слов и простой грамматики
            if (score < 30f || grammar < 2)
                return "Proto-Symbolic";

            // 3. Появление институтов и записей
            if (institutions >= 1 && texts >= 1 && grammar >= 3)
                return "Institutional Symbolic";

            // 4. Прото-вычисления: есть серьезные теории и заметная вычислительная база
            if (solidTheories >= 2 && computation >= 10f)
                return "Proto-Computational";

            // 5. Абстрактные вычисления: требуются реальные алгоритмы, логические вентили или символические правила, а не просто наблюдения
            if (advancedLogic >= 2 && solidTheories >= 1 && computation >= 15f)
                return "Abstract Computational";

            // 6. Если есть продвинутая логика, но теорий мало (узкая специализация)
            if (advancedLogic >= 3)
                return "Logical Computational";

            // Дефолт для промежуточных состояний
            return "Symbolic";
        }
    }
}
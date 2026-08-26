using System;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;

namespace GenesisEngine.Systems
{
    public static class TeacherSystem
    {
        public static bool TryTeach(Agent teacher, Tile tile, Random rng)
        {
            if (teacher == null || tile == null || rng == null)
                return false;

            bool knowledgePlace =
                tile.InstitutionAxis == "knowledge" ||
                tile.Building == BuildingType.Library;

            if (!knowledgePlace)
                return false;

            if (teacher.Body.Energy < 35f)
                return false;

            if (teacher.Body.Hunger > 60f || teacher.Fear > 60f)
                return false;

            if (teacher.Logic < 0.35f)
                return false;

            if (teacher.Memory.Patterns.Count < 3 && !KnowledgeSystem.AgentKnowsAnything(teacher))
                return false;

            float chance =
                0.03f +
                teacher.Genome.Extraversion * 0.03f +
                teacher.Logic * 0.04f +
                tile.InstitutionLevel * 0.004f;

            if (rng.NextDouble() > chance)
                return false;

            var students = SpatialGrid.GetNearby(teacher.Position, 1)
                .Where(a =>
                    a.Id != teacher.Id &&
                    a.Body.Health > 0f &&
                    a.Body.Energy > 20f &&
                    a.Logic < teacher.Logic)
                .Take(3)
                .ToList();

            if (students.Count == 0)
                return false;

            bool taughtAnything = false;

            foreach (var student in students)
            {
                teacher.Body.Energy = Math.Max(0f, teacher.Body.Energy - 2f);
                student.Body.Energy = Math.Max(0f, student.Body.Energy - 1f);

                if (KnowledgeSystem.TryTeachFromTeacher(teacher, student, tile, rng))
                {
                    student.LastAction = "Learn";
                    taughtAnything = true;
                }
            }

            if (taughtAnything)
            {
                teacher.LastAction = "Teach";
                return true;
            }

            return false;
        }
    }
}
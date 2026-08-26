using System;
using System.Collections.Generic;
using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.World;
using GenesisEngine.Systems.Physics;
using GenesisEngine.Systems;

namespace GenesisEngine.Systems
{
    public class Artifact
    {
        public Guid Id = Guid.Empty;
        public Guid CreatorId;
        public int CreationTick;
        public string MaterialId;
        public float Craftsmanship;
        public float Durability = 100f;
        public float CulturalValue;
        public bool IsSacred;
        public string Name;
    }

    public class WrittenText
    {
        public Guid Id = Guid.Empty;
        public Guid AuthorId;
        public int CreationTick;
        public string Content;
        public List<string> Symbols = new();
        public float Readability = 1.0f;
        public int Copies;
        public bool IsSacred;

        // НОВОЕ: текст может хранить знание
        public List<string> KnowledgeIds = new();
        public List<string> EncodedSymbols = new();
    }

    public static class CultureSystem
    {
        public static readonly List<Artifact> AllArtifacts = new();
        public static readonly List<WrittenText> AllTexts = new();

        public static Artifact CreateArtifact(Agent creator, Tile tile, string materialId)
        {
            if (creator == null || tile == null) return null;
            if (!MaterialDB.TryGet(materialId, out var spec)) return null;

            float craftsmanship =
                creator.Genome.Conscientiousness * 0.6f +
                creator.Genome.Openness * 0.4f;

            var artifact = new Artifact
            {
                CreatorId = creator.Id,
                CreationTick = Simulation.Instance.TotalTicks,
                MaterialId = materialId,
                Craftsmanship = craftsmanship,
                CulturalValue = craftsmanship * 10f * (1f + spec.Rarity),
                Name = $"Artifact_{AllArtifacts.Count}"
            };

            if (creator.Genome.SelfAwareness > 0.5f)
            {
                artifact.CulturalValue *= 1.8f;
            }

            tile.Artifacts.Add(artifact);
            AllArtifacts.Add(artifact);

            return artifact;
        }

        public static WrittenText CreateText(Agent author, Tile tile, string content)
        {
            if (author == null || tile == null) return null;
            if (author.Genome.SelfAwareness < 0.4f) return null;

            var text = new WrittenText
            {
                AuthorId = author.Id,
                CreationTick = Simulation.Instance.TotalTicks,
                Content = content
            };

            text.Symbols.Add("circle");
            text.Symbols.Add(author.Genome.Aggression > 0.7f ? "mark" : "bond");

            tile.Texts.Add(text);
            AllTexts.Add(text);

            return text;
        }
        public static WrittenText CreateKnowledgeText(Agent author, Tile tile, Knowledge knowledge)
        {
            if (author == null || tile == null || knowledge == null)
                return null;

            if (author.Genome.SelfAwareness < 0.35f)
                return null;

            var text = new WrittenText
            {
                AuthorId = author.Id,
                CreationTick = Simulation.Instance.TotalTicks,
                Content = knowledge.Name,
                Readability = 0.55f + author.Genome.SelfAwareness * 0.45f
            };

            text.Symbols.Add("circle");
            text.Symbols.Add(knowledge.DominantAxis ?? "knowledge");

            text.KnowledgeIds.Add(knowledge.Id);

            text.EncodedSymbols = SymbolSystem.EncodeKnowledge(knowledge);

            SymbolSystem.RegisterWriting(author.CivilizationId ?? "wild", text.EncodedSymbols);

            if (knowledge.DominantAxis == "faith")
                text.IsSacred = true;

            tile.Texts.Add(text);
            AllTexts.Add(text);

            return text;
        }
        public static void OnDeath(Agent dead, List<Agent> nearbyAgents)
        {
            if (dead == null) return;

            var world = Simulation.Instance.World;
            if (world == null) return;

            var tile = world[dead.Position.X, dead.Position.Y];
            tile.SanctityLevel = Math.Clamp(tile.SanctityLevel + 0.5f, 0f, 100f);

            if (nearbyAgents == null) return;

            foreach (var observer in nearbyAgents)
            {
                if (observer.Id == dead.Id) continue;

                float empathy =
                    observer.Genome.Agreeableness * 0.5f +
                    observer.Genome.Spirituality * 0.5f;

                if (empathy > 0.5f)
                {
                    observer.LastAction = "Mourn";
                    observer.Fear = Math.Max(0f, observer.Fear - 5f);
                    observer.Loneliness = Math.Max(0f, observer.Loneliness - 5f);

                    tile.SanctityLevel = Math.Clamp(tile.SanctityLevel + 0.1f, 0f, 100f);

                    observer.Memory.UpdateAgentMemory(dead.Id, "Mourn", 5f);
                }
            }
        }

        public static void UpdateWorld(Tile[,] world)
        {
            if (world == null) return;

            foreach (var tile in world)
            {
                if (tile.SanctityLevel <= 0f &&
                    tile.Artifacts.Count == 0 &&
                    tile.Texts.Count == 0)
                {
                    continue;
                }

                var agentsHere = SpatialGrid.GetNearby(new Vector2(tile.X, tile.Y), 0);

                UpdateSanctity(tile, agentsHere);

                tile.SanctityLevel = Math.Max(0f, tile.SanctityLevel - 0.01f);
            }
        }

        private static void UpdateSanctity(Tile tile, List<Agent> agentsHere)
        {
            if (agentsHere == null) return;

            tile.SanctityLevel += agentsHere.Count(a => a.LastAction == "Mourn") * 0.1f;

            foreach (var artifact in tile.Artifacts)
            {
                if (Simulation.Instance.TotalTicks - artifact.CreationTick > 10000)
                {
                    artifact.CulturalValue += 0.01f;
                    artifact.IsSacred = artifact.CulturalValue > 500f;
                    tile.SanctityLevel += 0.001f;
                }
            }

            foreach (var text in tile.Texts)
            {
                if (text.IsSacred)
                    tile.SanctityLevel += 0.05f;
            }

            tile.SanctityLevel = Math.Clamp(tile.SanctityLevel, 0f, 100f);

            if (tile.SanctityLevel > 10f)
            {
                foreach (var a in agentsHere)
                {
                    a.Curiosity += tile.SanctityLevel * 0.005f;

                    if (a.Genome.Spirituality > 0.5f)
                    {
                        a.Fear = Math.Max(0f, a.Fear - tile.SanctityLevel * 0.01f);
                    }
                }
            }
        }
    }
}
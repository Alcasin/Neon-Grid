using System;
using System.Collections.Generic;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Session;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Tests
{
    internal sealed class CampaignTestFixture : IDisposable
    {
        private readonly List<ScriptableObject> objects = new List<ScriptableObject>();
        private readonly Dictionary<string, LevelDefinition> levels =
            new Dictionary<string, LevelDefinition>(StringComparer.Ordinal);

        public CampaignDefinition Campaign { get; }

        public CampaignTestFixture()
        {
            foreach (string id in new[] { "power_01", "power_02", "power_03", "metro_01", "metro_02", "metro_03" })
                levels.Add(id, CreateLevel());
            Campaign = CreateDefaultCampaign();
        }

        public LevelDefinition Level(string levelId) => levels[levelId];

        public CampaignDefinition CreateDefaultCampaign()
        {
            return CreateCampaign("test_campaign",
                Chapter("power_station", "Power Station", "power_01", "power_02", "power_03"),
                Chapter("metro", "Metro", "metro_01", "metro_02", "metro_03"));
        }

        public CampaignDefinition CreateReorderedCampaign()
        {
            return CreateCampaign("test_campaign",
                Chapter("power_station", "Power Station", "power_03", "power_01", "power_02"),
                Chapter("metro", "Metro", "metro_01", "metro_02", "metro_03"));
        }

        public CampaignDefinition CreateCampaign(string id, params CampaignChapterDefinition[] chapters)
        {
            CampaignDefinition definition = ScriptableObject.CreateInstance<CampaignDefinition>();
            definition.SetData(id, chapters);
            objects.Add(definition);
            return definition;
        }

        public CampaignChapterDefinition Chapter(string id, string name, params string[] levelIds)
        {
            var entries = new List<CampaignLevelEntry>();
            foreach (string levelId in levelIds)
                entries.Add(new CampaignLevelEntry(levelId, levelId, levels[levelId]));
            return new CampaignChapterDefinition(id, name, entries);
        }

        public static SessionCompletionResult Result(LevelDefinition level, int moves, float seconds,
            int? stars)
        {
            StarEvaluationResult rating = stars.HasValue
                ? new StarEvaluationResult(StarEvaluationStatus.Rated, stars.Value)
                : new StarEvaluationResult(StarEvaluationStatus.OptimalMovesUnknown, 0);
            return new SessionCompletionResult(level, moves, seconds, null, false, rating);
        }

        public static void Solve(GameplaySession session)
        {
            if (!session.IsCompleted)
                session.InteractWithTile(new GridPosition(1, 0));
        }

        public void Dispose()
        {
            foreach (ScriptableObject item in objects)
                UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
        }

        private LevelDefinition CreateLevel()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.SetData(3, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false)
            });
            objects.Add(level);
            return level;
        }
    }
}

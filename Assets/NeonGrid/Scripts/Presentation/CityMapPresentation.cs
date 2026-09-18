using System;
using System.Collections.Generic;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public enum ChapterMapVisualState
    {
        Locked,
        ProgressStage1,
        ProgressStage2,
        ProgressStage3,
        Restored
    }

    public enum CityEnergyPathState
    {
        Locked,
        Frontier,
        Restored
    }

    public enum CityBuildingSilhouette
    {
        Generator,
        Substation,
        ControlTower,
        Factory,
        CentralCore
    }

    public sealed class CityMapLayoutEntry
    {
        public string ChapterId { get; }
        public Vector2 Position { get; }
        public Vector2 VisualSize { get; }
        public Vector2 HitSize { get; }
        public float Scale { get; }
        public CityBuildingSilhouette Silhouette { get; }
        public IReadOnlyList<Vector2> RouteToNext { get; }

        public CityMapLayoutEntry(string chapterId, Vector2 position, Vector2 visualSize,
            Vector2 hitSize, float scale, CityBuildingSilhouette silhouette,
            params Vector2[] routeToNext)
        {
            ChapterId = chapterId;
            Position = position;
            VisualSize = visualSize;
            HitSize = hitSize;
            Scale = scale;
            Silhouette = silhouette;
            RouteToNext = routeToNext ?? Array.Empty<Vector2>();
        }
    }

    public sealed class CityMapLayoutDefinition
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public static readonly Vector2 CompositionOffset = new Vector2(0f, -60f);
        public IReadOnlyList<CityMapLayoutEntry> Entries { get; }

        public CityMapLayoutDefinition(IReadOnlyList<CityMapLayoutEntry> entries)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public bool Matches(CampaignDefinition campaign)
        {
            if (campaign?.Chapters == null || campaign.Chapters.Count != Entries.Count) return false;
            for (int index = 0; index < Entries.Count; index++)
                if (!string.Equals(campaign.Chapters[index].ChapterId,
                        Entries[index].ChapterId, StringComparison.Ordinal))
                    return false;
            return true;
        }

        public Rect CalculateHitRect(CityMapLayoutEntry entry, float screenWidth, float screenHeight)
        {
            float scale = Mathf.Min(screenWidth / ReferenceWidth, screenHeight / ReferenceHeight);
            Vector2 center = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f) +
                             (CompositionOffset + entry.Position) * scale;
            Vector2 size = entry.HitSize * scale;
            return new Rect(center - size * 0.5f, size);
        }
    }

    public static class CityMapLayoutCatalog
    {
        public static CityMapLayoutDefinition Production { get; } =
            new CityMapLayoutDefinition(new[]
            {
                new CityMapLayoutEntry("power_station", new Vector2(-310f, -510f),
                    new Vector2(210f, 150f), new Vector2(280f, 280f), 1f,
                    CityBuildingSilhouette.Generator,
                    new Vector2(-420f, -330f), new Vector2(-390f, -170f)),
                new CityMapLayoutEntry("substation", new Vector2(-340f, 0f),
                    new Vector2(210f, 150f), new Vector2(280f, 280f), 1f,
                    CityBuildingSilhouette.Substation,
                    new Vector2(-230f, 190f), new Vector2(-90f, 260f)),
                new CityMapLayoutEntry("control_center", new Vector2(60f, 420f),
                    new Vector2(190f, 190f), new Vector2(280f, 300f), 1f,
                    CityBuildingSilhouette.ControlTower,
                    new Vector2(210f, 330f), new Vector2(300f, 200f)),
                new CityMapLayoutEntry("automation_plant", new Vector2(370f, 0f),
                    new Vector2(230f, 155f), new Vector2(280f, 280f), 1f,
                    CityBuildingSilhouette.Factory,
                    new Vector2(310f, -160f), new Vector2(190f, -250f)),
                new CityMapLayoutEntry("central_grid", new Vector2(40f, -300f),
                    new Vector2(260f, 205f), new Vector2(350f, 330f), 1.25f,
                    CityBuildingSilhouette.CentralCore)
            });

        public static bool TryGet(CampaignDefinition campaign, out CityMapLayoutDefinition layout)
        {
            layout = Production;
            if (layout.Matches(campaign)) return true;
            layout = null;
            return false;
        }
    }

    public static class CityMapPresentationModel
    {
        public static ChapterMapVisualState GetChapterVisualState(CampaignChapterState state,
            int completedLevels, int totalLevels)
        {
            if (state == CampaignChapterState.Locked) return ChapterMapVisualState.Locked;
            if (state == CampaignChapterState.Restored ||
                totalLevels > 0 && completedLevels >= totalLevels)
                return ChapterMapVisualState.Restored;
            if (completedLevels <= 3) return ChapterMapVisualState.ProgressStage1;
            if (completedLevels <= 6) return ChapterMapVisualState.ProgressStage2;
            return ChapterMapVisualState.ProgressStage3;
        }

        public static CityEnergyPathState GetPathState(CampaignChapterState from,
            CampaignChapterState to)
        {
            if (from != CampaignChapterState.Restored) return CityEnergyPathState.Locked;
            return to == CampaignChapterState.Restored
                ? CityEnergyPathState.Restored
                : to == CampaignChapterState.Available
                    ? CityEnergyPathState.Frontier
                    : CityEnergyPathState.Locked;
        }

        public static int GetChapterStars(CampaignProgressService progress,
            CampaignChapterDefinition chapter)
        {
            int total = 0;
            foreach (CampaignLevelEntry entry in chapter.Levels)
                total += progress.GetLevelProgress(entry.LevelId)?.BestStars ?? 0;
            return total;
        }
    }
}

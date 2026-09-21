using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Editor;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class NarrativeIntegrationQaTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private string temporaryDirectory;
        private CampaignDefinition campaign;
        private CampaignNarrativeDefinition narrative;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NeonGridM14E",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
            narrative = Resources.Load<CampaignNarrativeDefinition>(
                "Narratives/NeonGrid_Main_Narrative");
            Assert.That(campaign, Is.Not.Null);
            Assert.That(narrative, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object item in cleanup)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            cleanup.Clear();
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
        }

        [Test]
        public void ProductionNarrativeDefinition_IsOneCompleteCoherentSource()
        {
            IReadOnlyList<string> issues = NarrativeQaValidator.Validate(campaign, narrative);

            Assert.That(issues, Is.Empty);
            Assert.That(CampaignNarrativeCatalog.LoadForCampaign(campaign.CampaignId),
                Is.SameAs(narrative));
            Assert.That(narrative.IntroPages, Has.Count.EqualTo(4));
            Assert.That(narrative.ChapterNarratives, Has.Count.EqualTo(5));
            Assert.That(narrative.HasUniqueChapterIds(), Is.True);
            Assert.That(narrative.ChapterNarratives.Count(entry => entry.HasRestorationStatus),
                Is.EqualTo(4));
            Assert.That(narrative.EndingNarrative.IsConfigured, Is.True);
        }

        [TestCase(NarrativeQaPreset.FreshIntro, 0, false, false, false)]
        [TestCase(NarrativeQaPreset.FreshMap, 0, true, false, false)]
        [TestCase(NarrativeQaPreset.FirstRestorationPending, 10, true, true, false)]
        [TestCase(NarrativeQaPreset.FinalRestorationPending, 50, true, true, false)]
        [TestCase(NarrativeQaPreset.EndingPending, 50, true, false, false)]
        [TestCase(NarrativeQaPreset.PostEndingComplete, 50, true, false, true)]
        public void EveryQaPreset_SavesAndReloadsThroughNormalPersistence(
            NarrativeQaPreset preset, int completedLevels, bool introComplete,
            bool hasPendingRestoration, bool endingComplete)
        {
            string path = SavePath($"{preset}.json");
            CampaignSaveResult saved = NarrativeQaStateBuilder.PrepareAndSave(campaign, preset,
                path);
            CampaignLoadResult loaded = new CampaignSaveStore(path).Load(campaign);

            Assert.That(saved.Succeeded, Is.True);
            Assert.That(loaded.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(CountCompleted(loaded.Progress), Is.EqualTo(completedLevels));
            Assert.That(loaded.Progress.IntroCompleted, Is.EqualTo(introComplete));
            Assert.That(loaded.Progress.PendingRestorationCount,
                Is.EqualTo(hasPendingRestoration ? 1 : 0));
            Assert.That(loaded.Progress.EndingCompleted, Is.EqualTo(endingComplete));
            Assert.That(loaded.Progress.TotalStars, Is.EqualTo(completedLevels));

            CampaignSaveData raw = JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText(path));
            Assert.That(raw.hasIntroCompletionState, Is.True);
            Assert.That(raw.hasEndingCompletionState, Is.True);
            if (preset == NarrativeQaPreset.FirstRestorationPending)
                Assert.That(loaded.Progress.PendingRestoration.RestoredChapterId,
                    Is.EqualTo(campaign.Chapters[0].ChapterId));
            if (preset == NarrativeQaPreset.FinalRestorationPending)
                Assert.That(loaded.Progress.PendingRestoration.RestoredChapterId,
                    Is.EqualTo(campaign.Chapters[campaign.Chapters.Count - 1].ChapterId));
        }

        [Test]
        public void QaStateGeneration_DerivesFirstAndFinalChaptersFromAuthoredOrdering()
        {
            CampaignLevelEntry firstLevel = campaign.Chapters[0].Levels[0];
            CampaignLevelEntry finalLevel = campaign.Chapters[0].Levels[1];
            var custom = ScriptableObject.CreateInstance<CampaignDefinition>();
            cleanup.Add(custom);
            custom.SetData("qa_order_test", new[]
            {
                new CampaignChapterDefinition("alpha_system", "Alpha",
                    new[] { firstLevel }),
                new CampaignChapterDefinition("omega_system", "Omega",
                    new[] { finalLevel })
            });

            CampaignProgressService first = NarrativeQaStateBuilder.Build(custom,
                NarrativeQaPreset.FirstRestorationPending);
            CampaignProgressService final = NarrativeQaStateBuilder.Build(custom,
                NarrativeQaPreset.FinalRestorationPending);

            Assert.That(first.PendingRestoration.RestoredChapterId, Is.EqualTo("alpha_system"));
            Assert.That(first.GetLevelProgress(firstLevel.LevelId).Completed, Is.True);
            Assert.That(first.GetLevelProgress(finalLevel.LevelId).Completed, Is.False);
            Assert.That(final.PendingRestoration.RestoredChapterId, Is.EqualTo("omega_system"));
            Assert.That(final.IsCampaignComplete, Is.True);
        }

        [Test]
        public void Snapshot_ReportsLifecycleStateAndAuthoredChapterIdentity()
        {
            string path = SavePath("snapshot.json");
            NarrativeQaStateBuilder.PrepareAndSave(campaign,
                NarrativeQaPreset.FirstRestorationPending, path);

            NarrativeQaSnapshot snapshot = NarrativeQaSnapshot.Inspect(campaign, path);

            Assert.That(snapshot.LoadStatus, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(snapshot.IntroStatus, Is.EqualTo("Complete"));
            Assert.That(snapshot.CompletedLevels, Is.EqualTo(campaign.Chapters[0].Levels.Count));
            Assert.That(snapshot.TotalLevels, Is.EqualTo(50));
            Assert.That(snapshot.PendingRestoration,
                Does.Contain(campaign.Chapters[0].ChapterId));
            Assert.That(snapshot.EndingStatus, Is.EqualTo("Not applicable"));
            Assert.That(NarrativeQaValidator.Validate(campaign, narrative, snapshot), Is.Empty);
        }

        [Test]
        public void LegacySaveMatrix_DerivesIntroAndEndingWithoutSurprises()
        {
            string path = SavePath("legacy.json");
            WriteLegacy(path, new List<LevelProgressSaveEntry>());
            CampaignProgressService empty = new CampaignSaveStore(path).Load(campaign).Progress;
            Assert.That(empty.IntroCompleted, Is.False);
            Assert.That(empty.IsEndingRequired, Is.False);

            var partial = new List<LevelProgressSaveEntry>
            {
                Entry(campaign.Chapters[0].Levels[0], 1)
            };
            WriteLegacy(path, partial);
            CampaignProgressService progressed = new CampaignSaveStore(path).Load(campaign).Progress;
            Assert.That(progressed.IntroCompleted, Is.True);
            Assert.That(progressed.IsCampaignComplete, Is.False);
            Assert.That(progressed.EndingCompleted, Is.False);

            CampaignProgressService complete = CompleteCampaignWithStars(1);
            WriteLegacy(path, complete.ExportProgress().ToList());
            CampaignProgressService legacyComplete = new CampaignSaveStore(path).Load(campaign)
                .Progress;
            Assert.That(legacyComplete.IntroCompleted, Is.True);
            Assert.That(legacyComplete.EndingCompleted, Is.True);
            Assert.That(legacyComplete.IsEndingRequired, Is.False);
        }

        [TestCase(50)]
        [TestCase(100)]
        [TestCase(143)]
        [TestCase(150)]
        public void EndingEligibility_IsIndependentOfRepresentativeStarTotals(int targetStars)
        {
            CampaignProgressService progress = CompleteCampaignWithTotalStars(targetStars);

            Assert.That(progress.IsCampaignComplete, Is.True);
            Assert.That(progress.TotalStars, Is.EqualTo(targetStars));
            Assert.That(progress.IsEndingRequired, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void NormalRestorationStatus_BindsToRestoredChapterId(int chapterIndex)
        {
            CampaignProgressService progress = CompleteThroughChapter(chapterIndex);
            CampaignChapterDefinition restored = campaign.Chapters[chapterIndex];
            progress.QueuePendingRestoration(restored.ChapterId);
            var flow = new CampaignFlowCoordinator(campaign, progress,
                new MemoryStore(progress));
            var root = new GameObject($"Narrative binding {chapterIndex}");
            cleanup.Add(root);
            var view = root.AddComponent<CampaignRuntimeView>();
            view.Build(campaign, progress, _ => { }, _ => { }, () => { });
            var sequence = root.AddComponent<CityRestorationSequenceController>();
            sequence.Initialize(view, flow);

            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);

            Assert.That(view.RestorationStatus.CurrentChapterId,
                Is.EqualTo(restored.ChapterId));
            Assert.That(narrative.TryGetChapterNarrative(restored.ChapterId, out var expected),
                Is.True);
            Assert.That(view.RestorationStatus.TitleText.text, Is.EqualTo(expected.RestoredTitle));
            Assert.That(view.RestorationStatus.BodyText.text, Is.EqualTo(expected.RestoredBody));
        }

        [Test]
        public void ReplayAndStatImprovement_DoNotResetNarrativeOrQueueEvents()
        {
            CampaignProgressService progress = CompleteCampaignWithStars(1);
            progress.SetIntroCompleted(true);
            progress.SetEndingCompleted(true);
            CampaignLevelEntry final = campaign.Chapters.Last().Levels.Last();
            LevelProgress before = progress.GetLevelProgress(final.LevelId);
            int oldMoves = before.BestMoves;
            float oldTime = before.BestTimeSeconds;

            CampaignProgressUpdate replay = progress.RecordCompletion(final.LevelId,
                Result(final, 3, 1, 0.25f));

            Assert.That(replay.Accepted, Is.True);
            Assert.That(replay.ChapterJustRestored, Is.False);
            Assert.That(progress.PendingRestorationCount, Is.Zero);
            Assert.That(progress.IntroCompleted, Is.True);
            Assert.That(progress.EndingCompleted, Is.True);
            Assert.That(progress.GetLevelProgress(final.LevelId).BestStars, Is.EqualTo(3));
            Assert.That(progress.GetLevelProgress(final.LevelId).BestMoves, Is.LessThan(oldMoves));
            Assert.That(progress.GetLevelProgress(final.LevelId).BestTimeSeconds,
                Is.LessThan(oldTime));
        }

        [Test]
        public void BackupAndRestore_RoundTripsExistingSaveOutsideAssets()
        {
            string savePath = SavePath("production.json");
            string backupDirectory = Path.Combine(temporaryDirectory, "Library", "NarrativeQA");
            File.WriteAllText(savePath, "original-save");
            var backups = new NarrativeQaBackupService(savePath, backupDirectory,
                campaign.CampaignId);

            backups.BackupCurrentSave();
            File.WriteAllText(savePath, "qa-save");
            backups.RestoreLastBackup();

            Assert.That(File.ReadAllText(savePath), Is.EqualTo("original-save"));
            Assert.That(backups.BackupPath, Does.StartWith(Path.GetFullPath(backupDirectory)));
            Assert.That(backups.BackupPath, Does.Not.Contain($"{Path.DirectorySeparatorChar}Assets"));
        }

        [Test]
        public void BackupAndRestore_PreservesOriginalNoSaveState()
        {
            string savePath = SavePath("missing.json");
            string backupDirectory = Path.Combine(temporaryDirectory, "Library", "NarrativeQA");
            var backups = new NarrativeQaBackupService(savePath, backupDirectory,
                campaign.CampaignId);

            backups.BackupCurrentSave();
            Assert.That(backups.HasBackup, Is.True);
            NarrativeQaStateBuilder.PrepareAndSave(campaign, NarrativeQaPreset.FreshMap,
                savePath);
            Assert.That(File.Exists(savePath), Is.True);
            backups.RestoreLastBackup();

            Assert.That(File.Exists(savePath), Is.False);
        }

        [TestCase("PowerStation_VerticalSlice")]
        [TestCase("Substation_VerticalSlice")]
        [TestCase("ControlCenter_VerticalSlice")]
        [TestCase("AutomationPlant_VerticalSlice")]
        [TestCase("CentralGrid_VerticalSlice")]
        public void NarrativeQaAndProductionNarrative_RemainIsolatedFromVerticalSlices(
            string resourceName)
        {
            CampaignDefinition slice = Resources.Load<CampaignDefinition>(
                $"Campaigns/{resourceName}");
            string root = Path.Combine(temporaryDirectory, "saves");
            string productionPath = CampaignSaveStore.BuildSavePath(root, campaign.CampaignId);
            string slicePath = CampaignSaveStore.BuildSavePath(root, slice.CampaignId);

            Assert.That(CampaignNarrativeCatalog.LoadForCampaign(slice.CampaignId), Is.Null);
            Assert.That(productionPath, Is.Not.EqualTo(slicePath));
            NarrativeQaStateBuilder.PrepareAndSave(campaign, NarrativeQaPreset.FreshMap,
                productionPath);
            Assert.That(File.Exists(productionPath), Is.True);
            Assert.That(File.Exists(slicePath), Is.False);
        }

        private CampaignProgressService CompleteThroughChapter(int chapterIndex)
        {
            var progress = new CampaignProgressService(campaign);
            for (int index = 0; index <= chapterIndex; index++)
                foreach (CampaignLevelEntry level in campaign.Chapters[index].Levels)
                    Assert.That(progress.RecordCompletion(level.LevelId,
                        Result(level, 1, 4, 1f)).Accepted, Is.True);
            return progress;
        }

        private CampaignProgressService CompleteCampaignWithStars(int stars)
        {
            var progress = new CampaignProgressService(campaign);
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
            foreach (CampaignLevelEntry level in chapter.Levels)
                Assert.That(progress.RecordCompletion(level.LevelId,
                    Result(level, stars, 4, 1f)).Accepted, Is.True);
            return progress;
        }

        private CampaignProgressService CompleteCampaignWithTotalStars(int targetStars)
        {
            var progress = new CampaignProgressService(campaign);
            int remainingBonus = targetStars - 50;
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
            foreach (CampaignLevelEntry level in chapter.Levels)
            {
                int stars = 1 + Math.Min(2, remainingBonus);
                remainingBonus -= stars - 1;
                Assert.That(progress.RecordCompletion(level.LevelId,
                    Result(level, stars, 4, 1f)).Accepted, Is.True);
            }
            Assert.That(remainingBonus, Is.Zero);
            return progress;
        }

        private static SessionCompletionResult Result(CampaignLevelEntry level, int stars,
            int moves, float time)
        {
            return new SessionCompletionResult(level.LevelDefinition, moves, time,
                level.AuthoredOptimalMoves, false,
                new StarEvaluationResult(StarEvaluationStatus.Rated, stars));
        }

        private static LevelProgressSaveEntry Entry(CampaignLevelEntry level, int stars)
        {
            return new LevelProgressSaveEntry(level.LevelId, true, stars, 4, 1f);
        }

        private void WriteLegacy(string path, List<LevelProgressSaveEntry> entries)
        {
            var data = new CampaignSaveData
            {
                version = CampaignSaveStore.CurrentVersion,
                campaignId = campaign.CampaignId,
                levelProgressEntries = entries
            };
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        private int CountCompleted(CampaignProgressService progress)
        {
            return campaign.Chapters.Sum(chapter => chapter.Levels.Count(level =>
                progress.GetLevelProgress(level.LevelId).Completed));
        }

        private string SavePath(string name) => Path.Combine(temporaryDirectory, name);

        private sealed class MemoryStore : ICampaignProgressStore
        {
            private readonly CampaignProgressService progress;
            public string SavePath => "memory://m14-e";

            public MemoryStore(CampaignProgressService progress)
            {
                this.progress = progress;
            }

            public CampaignLoadResult Load(CampaignDefinition definition) =>
                new CampaignLoadResult(CampaignLoadStatus.Loaded, progress,
                    Array.Empty<string>());

            public CampaignSaveResult Save(CampaignProgressService value) =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);

            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

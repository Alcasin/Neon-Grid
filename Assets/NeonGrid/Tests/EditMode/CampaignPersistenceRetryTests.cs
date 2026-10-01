using System;
using System.Collections.Generic;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NeonGrid.Tests
{
    public sealed class CampaignPersistenceRetryTests
    {
        private CampaignTestFixture campaignFixture;
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            campaignFixture = new CampaignTestFixture();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
            campaignFixture.Dispose();
        }

        [Test]
        public void PauseFalseDoesNotRetryDirtyCampaign()
        {
            RuntimeFixture fixture = CreateRuntime();
            MakeCampaignDirty(fixture);

            fixture.Controller.HandleApplicationPause(false);

            Assert.That(fixture.CampaignStore.SaveCount, Is.EqualTo(1));
            Assert.That(fixture.Controller.Flow.HasUnpersistedProgress, Is.True);
        }

        [Test]
        public void PauseTrueRetriesDirtyCampaignAndSuccessfulSaveClearsDirtyState()
        {
            RuntimeFixture fixture = CreateRuntime();
            MakeCampaignDirty(fixture);
            fixture.CampaignStore.FailSaves = false;

            fixture.Controller.HandleApplicationPause(true);

            Assert.That(fixture.CampaignStore.SaveCount, Is.EqualTo(2));
            Assert.That(fixture.Controller.Flow.HasUnpersistedProgress, Is.False);
            Assert.That(fixture.Progress.GetLevelProgress("power_01").Completed, Is.True);
        }

        [Test]
        public void FailedPauseRetryKeepsDirtyState()
        {
            RuntimeFixture fixture = CreateRuntime();
            MakeCampaignDirty(fixture);
            LogAssert.Expect(LogType.Warning, "Simulated campaign save failure");

            fixture.Controller.HandleApplicationPause(true);

            Assert.That(fixture.CampaignStore.SaveCount, Is.EqualTo(2));
            Assert.That(fixture.Controller.Flow.HasUnpersistedProgress, Is.True);
        }

        [Test]
        public void QuitRetriesDirtyCampaignExactlyOnce()
        {
            RuntimeFixture fixture = CreateRuntime();
            MakeCampaignDirty(fixture);
            fixture.CampaignStore.FailSaves = false;

            fixture.Controller.HandleApplicationQuit();

            Assert.That(fixture.CampaignStore.SaveCount, Is.EqualTo(2));
            Assert.That(fixture.Controller.Flow.HasUnpersistedProgress, Is.False);
        }

        [Test]
        public void CleanPauseAndQuitPerformNoCampaignSave()
        {
            RuntimeFixture fixture = CreateRuntime();

            fixture.Controller.HandleApplicationPause(false);
            fixture.Controller.HandleApplicationPause(true);
            fixture.Controller.HandleApplicationQuit();

            Assert.That(fixture.CampaignStore.SaveCount, Is.Zero);
            Assert.That(fixture.Controller.Flow.HasUnpersistedProgress, Is.False);
        }

        [Test]
        public void CampaignRetryDoesNotPersistIndependentlyDirtySettings()
        {
            RuntimeFixture fixture = CreateRuntime();
            MakeCampaignDirty(fixture);
            fixture.CampaignStore.FailSaves = false;
            fixture.Controller.RuntimeSettings.SetMasterVolume(.5f);
            Assert.That(fixture.Controller.RuntimeSettings.HasUnpersistedChanges, Is.True);

            fixture.Controller.HandleApplicationPause(true);

            Assert.That(fixture.CampaignStore.SaveCount, Is.EqualTo(2));
            Assert.That(fixture.Controller.Flow.HasUnpersistedProgress, Is.False);
            Assert.That(fixture.SettingsStore.SaveCount, Is.Zero);
            Assert.That(fixture.Controller.RuntimeSettings.HasUnpersistedChanges, Is.True);
        }

        private RuntimeFixture CreateRuntime()
        {
            var progress = new CampaignProgressService(campaignFixture.Campaign);
            var campaignStore = new MemoryCampaignStore(progress);
            var settingsStore = new MemorySettingsStore();
            var root = new GameObject("M18-A3 Campaign Persistence Retry");
            root.SetActive(false);
            cleanup.Add(root);
            Camera existingCamera = Camera.main;
            var controller = root.AddComponent<CampaignRuntimeController>();
            controller.Initialize(campaignFixture.Campaign, campaignStore, settingsStore);
            Camera initializedCamera = Camera.main;
            if (existingCamera == null && initializedCamera != null)
                cleanup.Add(initializedCamera.gameObject);
            return new RuntimeFixture(controller, progress, campaignStore, settingsStore);
        }

        private static void MakeCampaignDirty(RuntimeFixture fixture)
        {
            fixture.CampaignStore.FailSaves = true;
            LogAssert.Expect(LogType.Warning, "Simulated campaign save failure");
            Assert.That(fixture.Controller.Flow.OpenChapter("power_station"), Is.True);
            Assert.That(fixture.Controller.Flow.StartLevel("power_01"), Is.True);
            CampaignTestFixture.Solve(fixture.Controller.Flow.ActiveSession);
            Assert.That(fixture.Controller.Flow.HasUnpersistedProgress, Is.True);
            Assert.That(fixture.CampaignStore.SaveCount, Is.EqualTo(1));
        }

        private sealed class RuntimeFixture
        {
            public CampaignRuntimeController Controller { get; }
            public CampaignProgressService Progress { get; }
            public MemoryCampaignStore CampaignStore { get; }
            public MemorySettingsStore SettingsStore { get; }

            public RuntimeFixture(CampaignRuntimeController controller,
                CampaignProgressService progress, MemoryCampaignStore campaignStore,
                MemorySettingsStore settingsStore)
            {
                Controller = controller;
                Progress = progress;
                CampaignStore = campaignStore;
                SettingsStore = settingsStore;
            }
        }

        private sealed class MemoryCampaignStore : ICampaignProgressStore
        {
            private readonly CampaignProgressService progress;

            public string SavePath => "memory://m18-a3-campaign";
            public int SaveCount { get; private set; }
            public bool FailSaves { get; set; }

            public MemoryCampaignStore(CampaignProgressService progress)
            {
                this.progress = progress;
            }

            public CampaignLoadResult Load(CampaignDefinition campaign) =>
                new CampaignLoadResult(CampaignLoadStatus.Loaded, progress,
                    Array.Empty<string>());

            public CampaignSaveResult Save(CampaignProgressService current)
            {
                SaveCount++;
                return FailSaves
                    ? new CampaignSaveResult(CampaignSaveStatus.Failed,
                        "Simulated campaign save failure")
                    : new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }

            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }

        private sealed class MemorySettingsStore : IUserSettingsStore
        {
            public string SettingsPath => "memory://m18-a3-settings";
            public int SaveCount { get; private set; }

            public UserSettingsLoadResult Load() =>
                new UserSettingsLoadResult(UserSettingsLoadStatus.NoFileFound,
                    UserSettings.CreateDefault(), string.Empty);

            public UserSettingsSaveResult Save(UserSettings settings)
            {
                SaveCount++;
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Saved, SettingsPath);
            }

            public UserSettingsSaveResult Delete() =>
                new UserSettingsSaveResult(UserSettingsSaveStatus.Saved, SettingsPath);
        }
    }
}

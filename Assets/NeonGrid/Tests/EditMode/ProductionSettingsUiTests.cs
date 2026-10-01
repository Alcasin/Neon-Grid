using System.Collections.Generic;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NeonGrid.Tests
{
    public sealed class ProductionSettingsUiTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
            NeonGridHapticsSettings.HapticsEnabled = true;
        }

        [Test]
        public void ProductionCityMap_HasSafeAreaAwareMapOnlySettingsEntry()
        {
            Fixture fixture = Build(new UserSettings(.8f, .7f, .6f, true));
            fixture.View.ShowMap();

            Assert.That(fixture.View.UsesCityMap, Is.True);
            Assert.That(fixture.SettingsView.SettingsButton, Is.Not.Null);
            Assert.That(fixture.SettingsView.SettingsButton.GetComponent<RectTransform>().sizeDelta,
                Is.EqualTo(new Vector2(ProgrammerUiMetrics.SettingsEntryWidth,
                    ProgrammerUiMetrics.SettingsEntryHeight)));
            Assert.That(fixture.SettingsView.SettingsButton
                .GetComponent<CityMapCornerSafeAreaLayout>(), Is.Not.Null);
            Assert.That(fixture.SettingsView.IsEntryVisible, Is.True);
            var entryRect = new Rect(
                new Vector2(1080f - ProgrammerUiMetrics.SettingsEntryRightInset -
                            ProgrammerUiMetrics.SettingsEntryWidth * .5f,
                    1920f - ProgrammerUiMetrics.SettingsEntryTopInset -
                    ProgrammerUiMetrics.SettingsEntryHeight * .5f),
                new Vector2(ProgrammerUiMetrics.SettingsEntryWidth,
                    ProgrammerUiMetrics.SettingsEntryHeight));
            foreach (CityMapLayoutEntry node in CityMapLayoutCatalog.Production.Entries)
                Assert.That(entryRect.Overlaps(CityMapLayoutCatalog.Production.CalculateHitRect(
                    node, 1080f, 1920f)), Is.False, node.ChapterId);

            fixture.View.ShowChapter(fixture.Campaign.Chapters[0]);
            Assert.That(fixture.SettingsView.IsEntryVisible, Is.False);
            fixture.View.SetVisible(false);
            Assert.That(fixture.SettingsView.IsEntryVisible, Is.False);
        }

        [Test]
        public void OpeningPanel_ReflectsAuthoritativeValuesWithoutMutationOrPersistence()
        {
            Fixture fixture = Build(new UserSettings(.83f, .37f, .04f, false));
            fixture.View.ShowMap();
            UserSettings before = fixture.Settings.CurrentSettings;

            fixture.SettingsView.SettingsButton.onClick.Invoke();

            Assert.That(fixture.SettingsView.IsOpen, Is.True);
            Assert.That(fixture.SettingsView.MasterSlider.value, Is.EqualTo(.83f));
            Assert.That(fixture.SettingsView.SfxSlider.value, Is.EqualTo(.37f));
            Assert.That(fixture.SettingsView.AmbienceSlider.value, Is.EqualTo(.04f));
            Assert.That(fixture.SettingsView.MasterPercentage.text, Is.EqualTo("83%"));
            Assert.That(fixture.SettingsView.SfxPercentage.text, Is.EqualTo("37%"));
            Assert.That(fixture.SettingsView.AmbiencePercentage.text, Is.EqualTo("4%"));
            Assert.That(fixture.SettingsView.HapticsToggle.isOn, Is.False);
            Assert.That(fixture.SettingsView.HapticsState.text, Is.EqualTo("OFF"));
            Assert.That(fixture.Settings.CurrentSettings.MasterVolume,
                Is.EqualTo(before.MasterVolume));
            Assert.That(fixture.Store.SaveCount, Is.Zero);
            Assert.That(fixture.View.IsMapInteractionEnabled, Is.False);
        }

        [Test]
        public void LiveControls_UpdateRuntimeAndPercentagesWithoutSavingPerTick()
        {
            Fixture fixture = Build(UserSettings.CreateDefault());
            fixture.View.ShowMap();
            fixture.SettingsView.Open();

            fixture.SettingsView.MasterSlider.value = .504f;
            fixture.SettingsView.SfxSlider.value = .371f;
            fixture.SettingsView.AmbienceSlider.value = 0f;
            fixture.SettingsView.HapticsToggle.isOn = false;

            UserSettings current = fixture.Settings.CurrentSettings;
            Assert.That(current.MasterVolume, Is.EqualTo(.504f));
            Assert.That(current.SfxVolume, Is.EqualTo(.371f));
            Assert.That(current.AmbienceVolume, Is.Zero);
            Assert.That(current.HapticsEnabled, Is.False);
            Assert.That(fixture.SettingsView.MasterPercentage.text, Is.EqualTo("50%"));
            Assert.That(fixture.SettingsView.SfxPercentage.text, Is.EqualTo("37%"));
            Assert.That(fixture.SettingsView.AmbiencePercentage.text, Is.EqualTo("0%"));
            Assert.That(fixture.SettingsView.HapticsState.text, Is.EqualTo("OFF"));
            Assert.That(fixture.Store.SaveCount, Is.Zero);
            Assert.That(fixture.Settings.HasUnpersistedChanges, Is.True);
        }

        [Test]
        public void ClosingPersistsDirtyStateOnceAndUnchangedCloseDoesNotSave()
        {
            Fixture fixture = Build(UserSettings.CreateDefault());
            fixture.View.ShowMap();
            fixture.SettingsView.Open();
            fixture.SettingsView.MasterSlider.value = .42f;

            Assert.That(fixture.SettingsView.Close(), Is.True);
            Assert.That(fixture.Store.SaveCount, Is.EqualTo(1));
            Assert.That(fixture.Settings.HasUnpersistedChanges, Is.False);
            Assert.That(fixture.View.IsMapInteractionEnabled, Is.True);

            Assert.That(fixture.SettingsView.Open(), Is.True);
            Assert.That(fixture.SettingsView.MasterSlider.value, Is.EqualTo(.42f));
            Assert.That(fixture.SettingsView.Close(), Is.True);
            Assert.That(fixture.Store.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedOpenClose_ReusesPanelControlsAndListeners()
        {
            Fixture fixture = Build(UserSettings.CreateDefault());
            fixture.View.ShowMap();
            int viewId = fixture.SettingsView.GetInstanceID();
            int modalId = fixture.SettingsView.ModalRoot.GetInstanceID();
            int sliderId = fixture.SettingsView.MasterSlider.GetInstanceID();
            RectTransform masterLabel = fixture.SettingsView.ModalRoot.transform
                .Find("Settings Panel/Master Label").GetComponent<RectTransform>();
            RectTransform hapticsToggle = fixture.SettingsView.ModalRoot.transform
                .Find("Settings Panel/Haptics Toggle").GetComponent<RectTransform>();
            Vector2 masterLabelPosition = masterLabel.anchoredPosition;
            Vector2 hapticsTogglePosition = hapticsToggle.anchoredPosition;

            for (int index = 0; index < 3; index++)
            {
                fixture.SettingsView.Open();
                fixture.SettingsView.MasterSlider.value = .9f - index * .1f;
                fixture.SettingsView.Close();
            }
            fixture.View.ConfigureSettings(fixture.Settings);

            Assert.That(fixture.Root.GetComponents<ProductionSettingsView>(), Has.Length.EqualTo(1));
            Assert.That(fixture.SettingsView.GetInstanceID(), Is.EqualTo(viewId));
            Assert.That(fixture.SettingsView.ModalRoot.GetInstanceID(), Is.EqualTo(modalId));
            Assert.That(fixture.SettingsView.MasterSlider.GetInstanceID(), Is.EqualTo(sliderId));
            Assert.That(masterLabel.anchoredPosition, Is.EqualTo(masterLabelPosition));
            Assert.That(hapticsToggle.anchoredPosition, Is.EqualTo(hapticsTogglePosition));
            Assert.That(fixture.Root.GetComponentsInChildren<Slider>(true), Has.Length.EqualTo(3));
            Assert.That(fixture.Root.GetComponentsInChildren<Toggle>(true), Has.Length.EqualTo(1));
            Assert.That(fixture.Store.SaveCount, Is.EqualTo(3));
        }

        [Test]
        public void SettingsPanel_UsesAlignedStackedRowsInsideSharedContentBounds()
        {
            Fixture fixture = Build(UserSettings.CreateDefault());
            Transform panel = fixture.SettingsView.ModalRoot.transform.Find("Settings Panel");
            float contentLeft = -ProgrammerUiMetrics.SettingsContentWidth * .5f;
            float contentRight = ProgrammerUiMetrics.SettingsContentWidth * .5f;

            foreach (string row in new[] { "Master", "SFX", "Ambience" })
            {
                RectTransform label = panel.Find(row + " Label").GetComponent<RectTransform>();
                RectTransform percentage = panel.Find(row + " Percentage")
                    .GetComponent<RectTransform>();
                RectTransform slider = panel.Find(row + " Slider").GetComponent<RectTransform>();
                RectTransform track = slider.Find("Background").GetComponent<RectTransform>();
                RectTransform thumb = slider.Find("Handle Slide Area/Handle")
                    .GetComponent<RectTransform>();

                Assert.That(Left(label), Is.EqualTo(contentLeft).Within(.001f), row);
                Assert.That(Right(percentage), Is.EqualTo(contentRight).Within(.001f), row);
                Assert.That(Left(slider), Is.EqualTo(contentLeft).Within(.001f), row);
                Assert.That(Right(slider), Is.EqualTo(contentRight).Within(.001f), row);
                Assert.That(label.anchoredPosition.y, Is.GreaterThan(slider.anchoredPosition.y), row);
                Assert.That(slider.sizeDelta.y,
                    Is.EqualTo(ProgrammerUiMetrics.SettingsSliderTouchHeight), row);
                Assert.That(track.offsetMax.y - track.offsetMin.y,
                    Is.EqualTo(ProgrammerUiMetrics.SettingsSliderTrackHeight), row);
                Assert.That(thumb.sizeDelta, Is.EqualTo(new Vector2(
                    ProgrammerUiMetrics.SettingsSliderThumbSize,
                    ProgrammerUiMetrics.SettingsSliderThumbHeight)), row);
                AssertInsideSettingsPanel(label, row + " label");
                AssertInsideSettingsPanel(percentage, row + " percentage");
                AssertInsideSettingsPanel(slider, row + " slider");
            }

            RectTransform audioKeyline = panel.Find("Audio Keyline").GetComponent<RectTransform>();
            RectTransform feedbackKeyline = panel.Find("Feedback Keyline")
                .GetComponent<RectTransform>();
            RectTransform hapticsLabel = panel.Find("Haptics Label").GetComponent<RectTransform>();
            RectTransform hapticsToggle = panel.Find("Haptics Toggle").GetComponent<RectTransform>();
            Assert.That(audioKeyline.sizeDelta.x,
                Is.EqualTo(ProgrammerUiMetrics.SettingsContentWidth));
            Assert.That(feedbackKeyline.sizeDelta.x,
                Is.EqualTo(ProgrammerUiMetrics.SettingsContentWidth));
            Assert.That(Left(hapticsLabel), Is.EqualTo(contentLeft).Within(.001f));
            Assert.That(Right(hapticsToggle), Is.EqualTo(contentRight).Within(.001f));
            AssertInsideSettingsPanel(hapticsLabel, "Haptics label");
            AssertInsideSettingsPanel(hapticsToggle, "Haptics toggle");
            Assert.That(contentLeft,
                Is.GreaterThan(-ProgrammerUiMetrics.SettingsPanelWidth * .5f));
            Assert.That(contentRight,
                Is.LessThan(ProgrammerUiMetrics.SettingsPanelWidth * .5f));
        }

        [Test]
        public void ModalBlocksMapWithoutChangingCampaignProgress()
        {
            Fixture fixture = Build(UserSettings.CreateDefault());
            fixture.View.ShowMap();
            int stars = fixture.Progress.TotalStars;
            int completed = fixture.Progress.GetCompletedLevelCount(
                fixture.Campaign.Chapters[0].ChapterId);

            fixture.SettingsView.Open();

            Assert.That(fixture.SettingsView.ModalRoot.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(fixture.View.IsMapInteractionEnabled, Is.False);
            Assert.That(fixture.Progress.TotalStars, Is.EqualTo(stars));
            Assert.That(fixture.Progress.GetCompletedLevelCount(
                fixture.Campaign.Chapters[0].ChapterId), Is.EqualTo(completed));
        }

        [Test]
        public void PersistenceFailure_LeavesLiveSettingsDirtyAndDoesNotCrash()
        {
            Fixture fixture = Build(UserSettings.CreateDefault(), failSave: true);
            fixture.View.ShowMap();
            fixture.SettingsView.Open();
            fixture.SettingsView.AmbienceSlider.value = .31f;
            LogAssert.Expect(LogType.Warning,
                "Could not save user settings: Simulated save failure.");

            Assert.That(fixture.SettingsView.Close(), Is.True);

            Assert.That(fixture.Settings.CurrentSettings.AmbienceVolume, Is.EqualTo(.31f));
            Assert.That(fixture.Settings.HasUnpersistedChanges, Is.True);
            Assert.That(fixture.Settings.LastSaveResult.Succeeded, Is.False);
            Assert.That(fixture.Store.SaveCount, Is.EqualTo(1));
            Assert.That(fixture.View.IsMapInteractionEnabled, Is.True);
        }

        [Test]
        public void SettingsEntrySafeArea_DoesNotMoveMapOrAccumulateOffsets()
        {
            Fixture fixture = Build(UserSettings.CreateDefault());
            fixture.View.ShowMap();
            RectTransform entry = fixture.SettingsView.SettingsButton.GetComponent<RectTransform>();
            CityMapCornerSafeAreaLayout layout =
                fixture.SettingsView.SettingsButton.GetComponent<CityMapCornerSafeAreaLayout>();
            RectTransform composition = fixture.Root.transform
                .Find("Campaign Canvas/Campaign Map/City Composition")
                .GetComponent<RectTransform>();
            Vector2 mapBaseline = composition.anchoredPosition;
            layout.ApplyLayout(new Rect(0f, 0f, 1080f, 1920f), 1080f, 1920f, 1f);
            Vector2 entryBaseline = entry.anchoredPosition;

            layout.ApplyLayout(new Rect(0f, 0f, 1040f, 1840f), 1080f, 1920f, 2f);
            Vector2 expected = entryBaseline + new Vector2(-20f, -40f);
            Assert.That(entry.anchoredPosition, Is.EqualTo(expected));
            layout.ApplyLayout(new Rect(0f, 0f, 1040f, 1840f), 1080f, 1920f, 2f);
            Assert.That(entry.anchoredPosition, Is.EqualTo(expected));
            Assert.That(composition.anchoredPosition, Is.EqualTo(mapBaseline));
        }

        [TestCase(0f, "0%")]
        [TestCase(.37f, "37%")]
        [TestCase(1f, "100%")]
        public void PercentageFormatting_IsNormalizedAndRounded(float value, string expected)
        {
            Assert.That(ProductionSettingsView.FormatPercentage(value), Is.EqualTo(expected));
        }

        private static float Left(RectTransform rect)
        {
            return rect.anchoredPosition.x - rect.sizeDelta.x * rect.pivot.x;
        }

        private static float Right(RectTransform rect)
        {
            return rect.anchoredPosition.x + rect.sizeDelta.x * (1f - rect.pivot.x);
        }

        private static float Bottom(RectTransform rect)
        {
            return rect.anchoredPosition.y - rect.sizeDelta.y * rect.pivot.y;
        }

        private static float Top(RectTransform rect)
        {
            return rect.anchoredPosition.y + rect.sizeDelta.y * (1f - rect.pivot.y);
        }

        private static void AssertInsideSettingsPanel(RectTransform rect, string context)
        {
            Assert.That(Left(rect),
                Is.GreaterThanOrEqualTo(-ProgrammerUiMetrics.SettingsPanelWidth * .5f), context);
            Assert.That(Right(rect),
                Is.LessThanOrEqualTo(ProgrammerUiMetrics.SettingsPanelWidth * .5f), context);
            Assert.That(Bottom(rect),
                Is.GreaterThanOrEqualTo(-ProgrammerUiMetrics.SettingsPanelHeight * .5f), context);
            Assert.That(Top(rect),
                Is.LessThanOrEqualTo(ProgrammerUiMetrics.SettingsPanelHeight * .5f), context);
        }

        private Fixture Build(UserSettings initial, bool failSave = false)
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            Assert.That(campaign, Is.Not.Null);
            var root = new GameObject("Production Settings UI Fixture");
            cleanup.Add(root);
            var store = new MemorySettingsStore(initial, failSave);
            var settings = new RuntimeUserSettingsController(store);
            var progress = new CampaignProgressService(campaign);
            var view = root.AddComponent<CampaignRuntimeView>();
            view.Build(campaign, progress, _ => { }, _ => { }, () => { },
                campaign.CampaignUiTheme);
            view.ConfigureSettings(settings);
            Assert.That(view.SettingsView, Is.Not.Null);
            return new Fixture(root, campaign, progress, settings, store, view);
        }

        private readonly struct Fixture
        {
            public GameObject Root { get; }
            public CampaignDefinition Campaign { get; }
            public CampaignProgressService Progress { get; }
            public RuntimeUserSettingsController Settings { get; }
            public MemorySettingsStore Store { get; }
            public CampaignRuntimeView View { get; }
            public ProductionSettingsView SettingsView => View.SettingsView;

            public Fixture(GameObject root, CampaignDefinition campaign,
                CampaignProgressService progress, RuntimeUserSettingsController settings,
                MemorySettingsStore store, CampaignRuntimeView view)
            {
                Root = root;
                Campaign = campaign;
                Progress = progress;
                Settings = settings;
                Store = store;
                View = view;
            }
        }

        private sealed class MemorySettingsStore : IUserSettingsStore
        {
            private UserSettings current;
            private readonly bool failSave;

            public string SettingsPath => "memory://m17-b1-settings";
            public int SaveCount { get; private set; }

            public MemorySettingsStore(UserSettings initial, bool failSave)
            {
                current = initial.CopySanitized();
                this.failSave = failSave;
            }

            public UserSettingsLoadResult Load()
            {
                return new UserSettingsLoadResult(UserSettingsLoadStatus.Loaded,
                    current.CopySanitized(), string.Empty);
            }

            public UserSettingsSaveResult Save(UserSettings settings)
            {
                SaveCount++;
                if (failSave)
                    return new UserSettingsSaveResult(UserSettingsSaveStatus.Failed,
                        "Simulated save failure.");
                current = settings.CopySanitized();
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Saved, SettingsPath);
            }

            public UserSettingsSaveResult Delete()
            {
                current = UserSettings.CreateDefault();
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Saved, SettingsPath);
            }
        }
    }
}

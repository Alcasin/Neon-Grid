using System;
using System.IO;
using NeonGrid.Settings;
using NUnit.Framework;

namespace NeonGrid.Tests
{
    public sealed class UserSettingsPersistenceTests
    {
        private string temporaryDirectory;
        private string settingsPath;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NeonGridM17A1Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            settingsPath = UserSettingsStore.BuildSettingsPath(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }

        [Test]
        public void Defaults_AreDeterministic()
        {
            UserSettings settings = UserSettings.CreateDefault();

            Assert.That(settings.MasterVolume, Is.EqualTo(1f));
            Assert.That(settings.SfxVolume, Is.EqualTo(1f));
            Assert.That(settings.AmbienceVolume, Is.EqualTo(1f));
            Assert.That(settings.HapticsEnabled, Is.True);
        }

        [Test]
        public void MissingFile_ReturnsDefaultsWithoutError()
        {
            UserSettingsLoadResult result = new UserSettingsStore(settingsPath).Load();

            Assert.That(result.Status, Is.EqualTo(UserSettingsLoadStatus.NoFileFound));
            AssertDefaults(result.Settings);
            Assert.That(result.Diagnostic, Is.Empty);
        }

        [Test]
        public void SaveLoadRoundTrip_PreservesAllSettings()
        {
            var store = new UserSettingsStore(settingsPath);
            var settings = new UserSettings(0.72f, 0.41f, 0.63f, false);

            Assert.That(store.Save(settings).Succeeded, Is.True);
            UserSettingsLoadResult result = store.Load();

            Assert.That(result.Status, Is.EqualTo(UserSettingsLoadStatus.Loaded));
            Assert.That(result.Settings.MasterVolume, Is.EqualTo(0.72f));
            Assert.That(result.Settings.SfxVolume, Is.EqualTo(0.41f));
            Assert.That(result.Settings.AmbienceVolume, Is.EqualTo(0.63f));
            Assert.That(result.Settings.HapticsEnabled, Is.False);
            Assert.That(File.Exists(settingsPath + ".tmp"), Is.False);
        }

        [TestCase(-0.25f, 0f)]
        [TestCase(1.25f, 1f)]
        public void MasterVolume_ClampsToNormalizedRange(float input, float expected)
        {
            var settings = new UserSettings { MasterVolume = input };

            Assert.That(settings.MasterVolume, Is.EqualTo(expected));
        }

        [TestCase(-0.25f, 0f)]
        [TestCase(1.25f, 1f)]
        public void SfxVolume_ClampsToNormalizedRange(float input, float expected)
        {
            var settings = new UserSettings { SfxVolume = input };

            Assert.That(settings.SfxVolume, Is.EqualTo(expected));
        }

        [TestCase(-0.25f, 0f)]
        [TestCase(1.25f, 1f)]
        public void AmbienceVolume_ClampsToNormalizedRange(float input, float expected)
        {
            var settings = new UserSettings { AmbienceVolume = input };

            Assert.That(settings.AmbienceVolume, Is.EqualTo(expected));
        }

        [Test]
        public void InvalidNumericValues_FallBackToChannelDefaults()
        {
            var settings = new UserSettings(float.NaN, float.PositiveInfinity,
                float.NegativeInfinity, false);

            Assert.That(settings.MasterVolume, Is.EqualTo(1f));
            Assert.That(settings.SfxVolume, Is.EqualTo(1f));
            Assert.That(settings.AmbienceVolume, Is.EqualTo(1f));
            Assert.That(settings.HapticsEnabled, Is.False);
        }

        [Test]
        public void OutOfRangePersistedValues_AreSanitizedOnLoad()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath,
                $"{{\"version\":{UserSettingsStore.CurrentVersion},\"masterVolume\":-3," +
                "\"sfxVolume\":4,\"ambienceVolume\":0.35," +
                "\"hapticsEnabled\":false}");

            UserSettingsLoadResult result = new UserSettingsStore(settingsPath).Load();

            Assert.That(result.Status, Is.EqualTo(UserSettingsLoadStatus.Loaded));
            Assert.That(result.Settings.MasterVolume, Is.Zero);
            Assert.That(result.Settings.SfxVolume, Is.EqualTo(1f));
            Assert.That(result.Settings.AmbienceVolume, Is.EqualTo(0.35f));
            Assert.That(result.Settings.HapticsEnabled, Is.False);
        }

        [Test]
        public void CorruptJson_ReturnsDefaultsWithDiagnostic()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath, "{ definitely not valid json");

            UserSettingsLoadResult result = new UserSettingsStore(settingsPath).Load();

            Assert.That(result.Status, Is.EqualTo(UserSettingsLoadStatus.Corrupt));
            AssertDefaults(result.Settings);
            Assert.That(result.Diagnostic, Is.Not.Empty);
        }

        [Test]
        public void UnsupportedVersion_ReturnsDefaultsWithDiagnostic()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            int unsupportedVersion = UserSettingsStore.CurrentVersion + 1;
            File.WriteAllText(settingsPath,
                $"{{\"version\":{unsupportedVersion},\"masterVolume\":0.2," +
                "\"sfxVolume\":0.3,\"ambienceVolume\":0.4," +
                "\"hapticsEnabled\":false}");

            UserSettingsLoadResult result = new UserSettingsStore(settingsPath).Load();

            Assert.That(result.Status, Is.EqualTo(UserSettingsLoadStatus.UnsupportedVersion));
            AssertDefaults(result.Settings);
            Assert.That(result.Diagnostic, Is.Not.Empty);
        }

        [Test]
        public void SaveFailure_ReturnsFailureWithoutThrowing()
        {
            var store = new UserSettingsStore(temporaryDirectory);

            UserSettingsSaveResult result = store.Save(new UserSettings(0.2f, 0.3f, 0.4f,
                false));

            Assert.That(result.Status, Is.EqualTo(UserSettingsSaveStatus.Failed));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
            Assert.That(File.Exists(temporaryDirectory + ".tmp"), Is.False);
        }

        [Test]
        public void Delete_RestoresDefaultLoadBehavior()
        {
            var store = new UserSettingsStore(settingsPath);
            Assert.That(store.Save(new UserSettings(0.5f, 0.4f, 0.3f, false)).Succeeded,
                Is.True);

            UserSettingsSaveResult deleted = store.Delete();
            UserSettingsLoadResult reloaded = store.Load();

            Assert.That(deleted.Succeeded, Is.True);
            Assert.That(reloaded.Status, Is.EqualTo(UserSettingsLoadStatus.NoFileFound));
            AssertDefaults(reloaded.Settings);
            Assert.That(File.Exists(settingsPath), Is.False);
            Assert.That(File.Exists(settingsPath + ".tmp"), Is.False);
        }

        [Test]
        public void SettingsStore_DoesNotUseOrMutateCampaignProgressStorage()
        {
            string campaignPath = Path.Combine(temporaryDirectory, "NeonGrid", "main_campaign",
                "campaign_progress.json");
            Directory.CreateDirectory(Path.GetDirectoryName(campaignPath));
            const string campaignContents = "campaign-progress-marker";
            File.WriteAllText(campaignPath, campaignContents);
            var store = new UserSettingsStore(settingsPath);

            Assert.That(store.Save(new UserSettings(0.2f, 0.3f, 0.4f, false)).Succeeded,
                Is.True);
            Assert.That(store.Delete().Succeeded, Is.True);

            Assert.That(settingsPath, Is.Not.EqualTo(campaignPath));
            Assert.That(File.ReadAllText(campaignPath), Is.EqualTo(campaignContents));
        }

        [Test]
        public void IndependentStores_DoNotInterfere()
        {
            string secondPath = Path.Combine(temporaryDirectory, "Second",
                UserSettingsStore.SettingsFileName);
            var first = new UserSettingsStore(settingsPath);
            var second = new UserSettingsStore(secondPath);
            first.Save(new UserSettings(0.1f, 0.2f, 0.3f, false));
            second.Save(new UserSettings(0.9f, 0.8f, 0.7f, true));

            UserSettingsLoadResult firstLoad = first.Load();
            UserSettingsLoadResult secondLoad = second.Load();

            Assert.That(firstLoad.Settings.MasterVolume, Is.EqualTo(0.1f));
            Assert.That(firstLoad.Settings.HapticsEnabled, Is.False);
            Assert.That(secondLoad.Settings.MasterVolume, Is.EqualTo(0.9f));
            Assert.That(secondLoad.Settings.HapticsEnabled, Is.True);
            Assert.That(first.SettingsPath, Is.Not.EqualTo(second.SettingsPath));
        }

        private static void AssertDefaults(UserSettings settings)
        {
            Assert.That(settings.MasterVolume, Is.EqualTo(1f));
            Assert.That(settings.SfxVolume, Is.EqualTo(1f));
            Assert.That(settings.AmbienceVolume, Is.EqualTo(1f));
            Assert.That(settings.HapticsEnabled, Is.True);
        }
    }
}

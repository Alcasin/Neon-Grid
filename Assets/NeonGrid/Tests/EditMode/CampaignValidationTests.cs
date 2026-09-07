using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NUnit.Framework;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class CampaignValidationTests
    {
        private CampaignTestFixture fixture;

        [SetUp]
        public void SetUp() => fixture = new CampaignTestFixture();

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void EmptyCampaignId_IsRejected()
        {
            CampaignDefinition campaign = fixture.CreateCampaign(" ",
                fixture.Chapter("chapter", "Chapter", "power_01"));
            AssertError(campaign, CampaignValidationCode.EmptyCampaignId);
        }

        [TestCase("../campaign")]
        [TestCase("campaign\\other")]
        [TestCase("campaign:other")]
        [TestCase("Campaign")]
        public void UnsafeCampaignId_IsRejected(string campaignId)
        {
            CampaignDefinition campaign = fixture.CreateCampaign(campaignId,
                fixture.Chapter("chapter", "Chapter", "power_01"));
            AssertError(campaign, CampaignValidationCode.InvalidCampaignId);
        }

        [Test]
        public void MissingCampaignAndMissingChapterList_AreRejected()
        {
            AssertError(null, CampaignValidationCode.MissingCampaign);
            CampaignDefinition empty = fixture.CreateCampaign("campaign");
            AssertError(empty, CampaignValidationCode.MissingChapters);
        }

        [Test]
        public void EmptyChapterAndLevelIds_AreRejected()
        {
            CampaignDefinition campaign = fixture.CreateCampaign("campaign",
                new CampaignChapterDefinition(" ", "Chapter", new[]
                {
                    new CampaignLevelEntry(" ", "Level", fixture.Level("power_01"))
                }));
            CampaignValidationReport report = new CampaignValidator().Validate(campaign);

            Assert.That(report.Issues.Any(issue => issue.Code == CampaignValidationCode.EmptyChapterId), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == CampaignValidationCode.EmptyLevelId), Is.True);
            Assert.That(report.IsValid, Is.False);
        }

        [Test]
        public void DuplicateChapterId_IsRejected()
        {
            CampaignDefinition campaign = fixture.CreateCampaign("campaign",
                fixture.Chapter("same", "One", "power_01"),
                fixture.Chapter("same", "Two", "power_02"));
            AssertError(campaign, CampaignValidationCode.DuplicateChapterId);
        }

        [Test]
        public void EmptyChapter_IsRejected()
        {
            CampaignDefinition campaign = fixture.CreateCampaign("campaign",
                new CampaignChapterDefinition("empty", "Empty", new CampaignLevelEntry[0]));
            AssertError(campaign, CampaignValidationCode.EmptyChapter);
        }

        [Test]
        public void DuplicateLevelIdAcrossChapters_IsRejected()
        {
            CampaignDefinition campaign = fixture.CreateCampaign("campaign",
                fixture.Chapter("one", "One", "power_01"),
                new CampaignChapterDefinition("two", "Two", new[]
                {
                    new CampaignLevelEntry("power_01", "Duplicate", fixture.Level("power_02"))
                }));
            AssertError(campaign, CampaignValidationCode.DuplicateLevelId);
        }

        [Test]
        public void MissingLevelDefinition_IsRejected()
        {
            CampaignDefinition campaign = fixture.CreateCampaign("campaign",
                new CampaignChapterDefinition("one", "One", new[]
                {
                    new CampaignLevelEntry("missing", "Missing", null)
                }));
            AssertError(campaign, CampaignValidationCode.MissingLevelDefinition);
        }

        [Test]
        public void NullChapterAndLevelRecords_AreRejected()
        {
            CampaignDefinition campaign = fixture.CreateCampaign("campaign", null,
                new CampaignChapterDefinition("one", "One", new CampaignLevelEntry[] { null }));
            CampaignValidationReport report = new CampaignValidator().Validate(campaign);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == CampaignValidationCode.NullChapter), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == CampaignValidationCode.NullLevelEntry), Is.True);
        }

        [Test]
        public void DuplicateLevelAsset_IsAWarningRatherThanError()
        {
            LevelDefinition shared = fixture.Level("power_01");
            CampaignDefinition campaign = fixture.CreateCampaign("campaign",
                new CampaignChapterDefinition("one", "One", new[]
                {
                    new CampaignLevelEntry("one_01", "One", shared),
                    new CampaignLevelEntry("one_02", "Two", shared)
                }));
            CampaignValidationReport report = new CampaignValidator().Validate(campaign);

            Assert.That(report.IsValid, Is.True);
            CampaignValidationIssue warning = report.Issues.Single();
            Assert.That(warning.Code, Is.EqualTo(CampaignValidationCode.DuplicateLevelDefinitionReference));
            Assert.That(warning.Severity, Is.EqualTo(CampaignValidationSeverity.Warning));
        }

        [Test]
        public void GeneratedM6TestCampaign_PassesValidation()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>("Campaigns/M6_Test_Campaign");
            Assert.That(campaign, Is.Not.Null);
            CampaignValidationReport report = new CampaignValidator().Validate(campaign);
            Assert.That(report.IsValid, Is.True,
                string.Join("\n", report.Issues.Select(issue => issue.Message)));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Tutorial_NullStepAndEmptyMessageAreRejected()
        {
            CampaignDefinition nullStep = CampaignWithTutorial(fixture.Level("power_01"),
                new LevelTutorialDefinition(new TutorialStepDefinition[] { null }));
            AssertError(nullStep, CampaignValidationCode.NullTutorialStep);

            CampaignDefinition emptyMessage = CampaignWithTutorial(fixture.Level("power_01"),
                Tutorial(" ", new GridPosition(1, 0)));
            AssertError(emptyMessage, CampaignValidationCode.EmptyTutorialMessage);
        }

        [Test]
        public void Tutorial_OutOfBoundsAndEmptyTargetsAreRejected()
        {
            CampaignDefinition outOfBounds = CampaignWithTutorial(fixture.Level("power_01"),
                Tutorial("Rotate", new GridPosition(3, 0)));
            AssertError(outOfBounds, CampaignValidationCode.TutorialTargetOutOfBounds);

            LevelDefinition sparse = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                sparse.SetData(2, 1, new[]
                {
                    new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false)
                });
                CampaignDefinition empty = CampaignWithTutorial(sparse,
                    Tutorial("Rotate", new GridPosition(1, 0)));
                AssertError(empty, CampaignValidationCode.TutorialTargetEmpty);
            }
            finally
            {
                Object.DestroyImmediate(sparse);
            }
        }

        [Test]
        public void Tutorial_RotateConditionRequiresRotatableTarget()
        {
            CampaignDefinition campaign = CampaignWithTutorial(fixture.Level("power_01"),
                Tutorial("Rotate", new GridPosition(0, 0)));
            AssertError(campaign, CampaignValidationCode.TutorialCompletionIncompatible);
        }

        private static void AssertError(CampaignDefinition campaign, CampaignValidationCode code)
        {
            CampaignValidationReport report = new CampaignValidator().Validate(campaign);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == code &&
                                                  issue.Severity == CampaignValidationSeverity.Error), Is.True);
        }

        private CampaignDefinition CampaignWithTutorial(LevelDefinition level,
            LevelTutorialDefinition tutorial)
        {
            return fixture.CreateCampaign("tutorial_campaign",
                new CampaignChapterDefinition("chapter", "Chapter", new[]
                {
                    new CampaignLevelEntry("tutorial_level", "Tutorial Level", level, tutorial)
                }));
        }

        private static LevelTutorialDefinition Tutorial(string message, GridPosition target)
        {
            return new LevelTutorialDefinition(new[]
            {
                new TutorialStepDefinition(message, target,
                    TutorialCompletionCondition.RotateClockwise)
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NeonGrid.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class ProductionMainCampaignTests
    {
        private static readonly string[] SourceCampaignNames =
        {
            "PowerStation_VerticalSlice", "Substation_VerticalSlice",
            "ControlCenter_VerticalSlice", "AutomationPlant_VerticalSlice",
            "CentralGrid_VerticalSlice"
        };

        private static readonly int[][] AcceptedMinima =
        {
            new[] { 1, 1, 4, 5, 4, 3, 4, 4, 2, 5 },
            new[] { 3, 7, 3, 3, 3, 3, 5, 6, 7, 6 },
            new[] { 4, 6, 1, 3, 4, 3, 4, 7, 6, 8 },
            new[] { 5, 6, 8, 5, 6, 7, 7, 6, 6, 9 },
            new[] { 5, 6, 6, 6, 5, 7, 7, 9, 9, 10 }
        };

        private static readonly string[][] ProductionHashes =
        {
            new[] { "AutomationPlant/AP_01", "6A6DC3D19C7262B4A8B43EAF6A5651645AD3FC2EB64A8666A2BC08A5714D66C3", "BF0C01B84750621D348C5A10624C62841118E9E113C35E8521D20BD022E966EE" },
            new[] { "AutomationPlant/AP_02", "42D55D92B0709F0BEF43BB4F4E2EBD62F612945A909E778323B9E47F9F0BF8A8", "163548F8425790A08E34D859FBB858540E9070103372DC652808A8100441FDF9" },
            new[] { "AutomationPlant/AP_03", "28D85E69372AF5F4C39608765D113C68307C5B1F8A793741C75ED6A269294D37", "3EF0A88B1F1B1DFA79EC0FE70CA4225F37D9D69DF694E489598D6BD50B1D1C0F" },
            new[] { "AutomationPlant/AP_04", "E62DE3DC474DE76020D8B943B1953F0A78D567380618FC3BB0EBF03F75D14FD7", "83F5F43F08A2B3A890168C45BD5A18C9C02123897D1F2F2AED87890B2AEC753D" },
            new[] { "AutomationPlant/AP_05", "F157D6F27713779841483D2CC9E4DED7EF9640307DF70A5FC9931F880DA99190", "F4492F5C373BDFDB2AF6967E0CB97B6981815635709C3436485BD11BDE3FC3C8" },
            new[] { "AutomationPlant/AP_06", "AE51221C5A961A732C3EF71146AD565324F8A74A8D9960AFB6633B077A23CA25", "6BDD97745EF48283ABDBF9CB93E2ACB1B7C2422B905EF2729E6BEFAB824C4668" },
            new[] { "AutomationPlant/AP_07", "6400912C6C38AEF10ABB999119C31D36EC0041E98F77CE897FD2D07044293FEE", "6C5BD95120C55AFDE18EB612E8E25A35A080B04CCCE77D705F7813509030BB67" },
            new[] { "AutomationPlant/AP_08", "E922F5BF5752DFF7EEDB177485FFB1E1CAFAD4513FC6644A5FB96980DF903C41", "017022E8F17026D3D9E2240121B7A3C2BCEFB37EDAB21E1FA571967EF43EB346" },
            new[] { "AutomationPlant/AP_09", "205C26ED7A1456EC247A263E332A2D3AF5BECA649CD83139E5A1605775953DE8", "D445A606BF9E63E49C5CF33BACC8357D9F2DEB2BF453D41B9C67FD43DAC0019C" },
            new[] { "AutomationPlant/AP_10", "4A3AE56502AF3A678AA24CD4CBA88AE98EC20BC9E2010C77B1E4874D1EA5559C", "0B1FCAFA81C7798A7F89E7CE2C0C5D2A64BBE2B2AC4BB09A18868E3FFC906109" },
            new[] { "CentralGrid/CG_01", "29C8EB6EFACEF8253FFF111B51B3863580E17D9652CEB8869A93721257FE95C8", "A8D9FD9D0157A11BD66B62DAB8498BDFDA0B33C077F098E347F488770C3BB806" },
            new[] { "CentralGrid/CG_02", "A486AE430E5BBE5D20214B491FE680E45BF434192D086F7BF2BBA1889CF8C73C", "0A77EC9494D3BF485E757D31DE2455ECCF033552B3B981601157101D2D666335" },
            new[] { "CentralGrid/CG_03", "BD707B1F1EE3D19EBA3B0AEF7645E0C587C582774854563E404EF29473C6A595", "2F3206F3B16CC7145F6A6010B5B018F9F0BA64ECD2A959804270C69388114C46" },
            new[] { "CentralGrid/CG_04", "5BB9AA143EE45740965B1A681E183D29F539EBB7DBE8FCA6BA8EBF852712A20A", "06CA561768CE39D03657C252782E11A8736883939DBA98209549D7FCCABA855B" },
            new[] { "CentralGrid/CG_05", "1EB706EC00B118215D765AB8E02CC80BE679FF83D180265AB4AA64F335098FE3", "BE902013EE2BEB6D3DEA89943119489A32D2A8CF1D5FF50D3F351352D855F75E" },
            new[] { "CentralGrid/CG_06", "A8F5B72F332BA1268D6CCC6FA1F5E4809A075C47CA1BC051C4B29040E30D5439", "83D340CE7816B59AFB0F97DD10546F4A0917CCB84FC5781E29572CAB6A27A050" },
            new[] { "CentralGrid/CG_07", "D264971F3A4A025B1B0EC422CF9CE95A57DE27E355B7F3FCEAE9B009009CD576", "80F2C4DA5E1AA101981B1B65AB620F29E783D82BD1BFA1611DADADCD00DF8897" },
            new[] { "CentralGrid/CG_08", "F6B4F2D823D46B1F1A5ECE032578F783C394CDEA458FE4ABA52301383277DFF6", "44349C89305EA2B95AAEB1D6AEEEFEA8A94FAD3400FEA958E93ECAC640C52C00" },
            new[] { "CentralGrid/CG_09", "43007DCAA962409EEE638D9E58EBF02580EDFF6D2E3EFFDF3EBDAA7B5655E96D", "4D43FA7FF804F832C7C5E9F96BE2CD14CA7392AE23B09A59695F83763AC85E53" },
            new[] { "CentralGrid/CG_10", "525FC7F9C02FE3940A1EB88DDC2F79141F9883E21D55FF34FF175119F7005CC6", "653408B6763F9F683A35E38D988A08F72E22DF0F1732DF52951189F638DC5C32" },
            new[] { "ControlCenter/CC_01", "B92F17189119180B7E5C9302DB1E259A4CAE9920C40D99961FBA1362A079FB93", "A50A36C8E6AAB59399E879F2E0FA263F5A1BE298CD6AA49C99974DC99A2EC8C0" },
            new[] { "ControlCenter/CC_02", "FDA283A1DF76C04BF61A60E819D1058140D3667B4AC60AD7431178B5D92EB83D", "BBBB8330032E9B15AD2631A3F9B3F3D1C2BD13A7CEF33BDECD37CB21042AAF07" },
            new[] { "ControlCenter/CC_03", "A0B23FD6F56E3BCDE0C8AE1FA0FAA4064AFE92B1C29AEB2639A9F422F7BF80B7", "692D7510EFF3C90E3BC55E4CDE36B67D57EB1A0EAECE673A5F6A553892C45D64" },
            new[] { "ControlCenter/CC_04", "6C1A438D14A6E691E03F28F1600D225FE27294928CD212694172B23C7F11C2AE", "D47E7B9BCF54555DB4B58359800BD4F786CADE39B68AB142ADC90B888DD26025" },
            new[] { "ControlCenter/CC_05", "F74BBA0AF601AA6820F8887FCA1772D1DBD7A84D749277CAE0C84C4182C14471", "AC9BC1418A6C48623D4BB49727510FED5A8D0152AE117910F3C302609BFAA931" },
            new[] { "ControlCenter/CC_06", "726F1980AE8E802892EFED816081575DF6FF15802EC5CB383365ED91B2DCF0A6", "A96BAF0263A9C6C8C114787823055D0E3E77489A6BF2B5E1028EFF9CD8422B5D" },
            new[] { "ControlCenter/CC_07", "DB7CAEE5E9C554A69EBE6D9C25D32EF79C6DF2381CFF3CF3DDBF0B3276EC7B5A", "78998132DB33055D1520E30D2B5ADEA9D80B4ABDC195E31E37175FDE1A4042B8" },
            new[] { "ControlCenter/CC_08", "57A112B76D477F92778EA32FB43287E600DAF0B3B394CA9691C207D475920ABE", "90F4A0EE2CFEEF54413F582E76F281F1FDF4989DC285DA2EB9DAB4B3A6215D2B" },
            new[] { "ControlCenter/CC_09", "5C3634E9A0C9FFAC54739681AA4BBB415036BCB0A32A9EEA4113578F4D309271", "9A6C8C7A11DB74A58F57230133D910E065854968691051D28272D6289F45E6FC" },
            new[] { "ControlCenter/CC_10", "ADDD45125014693DFBD4B982740729A28C20226C70B68447EDFF781676000F2B", "1A35BC61A760F74B7FB93D6D148EBDC231F7B6F5EFD6E4A418A92F254DEE2F74" },
            new[] { "PowerStation/PS_01", "C033423F49C11F60581E6EBB287617249AAA9F11944133354D9386DC53D652F2", "C0E1889D392B3B06C228FBF8213909ECFF5DF123053E43BF323DB0721E55D533" },
            new[] { "PowerStation/PS_02", "52B6E05A094F087092EDF05452F1CEFB127B4B926754312EF1A57875A499985C", "884F4F10FF302EA6E873334B6E835EDA27C9077AF990EA591225C74680CEBC9C" },
            new[] { "PowerStation/PS_03", "5B6FFC82B4048484775BECD65C34A0CFC3CC52D5EDE851DB1C9D95EF434165D6", "6928C5371651A156B47C34929FE4CE9C821BEC99917702C33A6D39959C322563" },
            new[] { "PowerStation/PS_04", "EB3A5AD67102FE297057EC0FF56E2875E142072F53FF379B3355E6FB503F920D", "FADB1E3E20E254AE6654CC987A1651486BD2F53B4371506B7745F1B4DDE3F4CB" },
            new[] { "PowerStation/PS_05", "EB9348EE98B31209CEBD29F40DCCF942C4B6A3FBD154EA1AA84A1D0CC747EA36", "B807DBE5418BFEAD531767A3B852C85CBD7B21BC20C209B2255E62AEEB185E68" },
            new[] { "PowerStation/PS_06", "2EF30C1B5974DDB6E9F45F209E70789F6E88D438C8AF0FFC332DB078454EBFCE", "FF20B392834E3213C2DADE8F51C3CD94B9A3343099B2E4DF28DDCF3E7FF0B778" },
            new[] { "PowerStation/PS_07", "FBA8F4966C078BB2F32D16889EA277291A4E1A22F6F25B9A3666A2A4EAC9752C", "B952E16BA9B4E9AAFBF4898748958082C0DCE8F1D8C72A22A57581EACAA7B010" },
            new[] { "PowerStation/PS_08", "4E302462044378734FB717A10E5071DCF19EB890A7B880A07D6E7458FB32B56E", "741D357C24F5F69E518E22C7E1E161B7D03F359549A41028C7FDD5B3FB2E679E" },
            new[] { "PowerStation/PS_09", "2607D3FBDDAF222EC4CA5223DC2BE1329A3504EF6CAEC66983219E851FC84CD5", "38481D5D2D34159C03588161733366D17D2E13DA183B3A32DD95C54C80C17D77" },
            new[] { "PowerStation/PS_10", "93786A9A89CB01BBF6B440AB178C21C0C7A04ED3FFC3DC8CFE7EC47AE3314C48", "BBB567CF2143F2AEC098F2E6E5D831471A076FE1B92D8397759B122B96FBD212" },
            new[] { "Substation/S_01", "AC62A981D072576E081A9F28B948CABACAD2B734569BA256A53ECAD1863301CD", "29DB3A1A19805B90C8D6FA4B35860363416F587ECA17332AB4227E0DAEDCF1D1" },
            new[] { "Substation/S_02", "BA5B2A015404D24782A221E41847E2F74C0A52F6A3343AF6AA25C015F5679642", "32889F4D55F72E899C257B8B8A76C7E4F815FD16DDE25B5D6E4154A186851682" },
            new[] { "Substation/S_03", "6FA4E85600075FC5065FB09C2203985421ED8E9CCB80E0D272B8DF8BF248FA8D", "D64EB70DAF3CC87302E092B3295A15E6D71BB44BE797113084729DC5D43B91D6" },
            new[] { "Substation/S_04", "82EAE1DED6B3A1E4AB99BBB9D7D74CC301F74ECCF95C618CAF1B3EA83393E929", "2BC55930DA5F4659799C90E322A3D9362E3ECE1F6095780644D4A4B043D10BB4" },
            new[] { "Substation/S_05", "23B6A453134000A8B998C5C965B42C03326464E1087843413DD1237D8F0C98EE", "20DE03008F7F04BDFB8697988846104C5A9F6B091D0A8DABE218A0535B07E89B" },
            new[] { "Substation/S_06", "53D848BC4FDA43BDE3F7D847DF271A7E96AE3E1C0AF63AF6FCEE1A6756930BEA", "8A93147A87C64D9B887046A941BCF90792E72C27F6DA5CEC2E83C40C552036C9" },
            new[] { "Substation/S_07", "6639DAD7F78A7BBE280B9C23445AE33A15FAB58FAA426F4A1E076EB3B2E97152", "E4DC3B58CA185C49AB3B6C66D39F5F70EBC7F66EE1EF8CEA598F1143EC34850A" },
            new[] { "Substation/S_08", "0DA30E83412EF133806D16B3B32A2AD3A5E0578D5B2F100DAD9F08758DE73723", "BAF11FAD52F4792562CD8D9F21C911BDC19D35587569BEFD6493F50F5403E556" },
            new[] { "Substation/S_09", "95350BF8E44D46DD58674A6E623835ACD1C8A9CE2FD21E8C3CA75F06C3BAE09D", "9AC296CAB31D1651066D721ADE699318CD0F67C357C01C0D3D279F122F7B1649" },
            new[] { "Substation/S_10", "D29478C84899B6FDA17C2B917C2A55C2D6E5D1E14CBFD7DAEEEEAEBF2DDC0064", "60C5EDF85FB0D9162A600B3DD4D86B51C90EE33AC86366090FDDC31060079E02" }
        };

        [Test]
        public void AllProductionLevelAssetAndMetaHashesRemainImmutable()
        {
            const string root = "Assets/NeonGrid/Resources/Levels";
            Assert.That(ProductionHashes, Has.Length.EqualTo(50));
            foreach (string[] expected in ProductionHashes)
            {
                string assetPath = Path.GetFullPath(Path.Combine(root,
                    expected[0].Replace('/', Path.DirectorySeparatorChar) + ".asset"));
                Assert.That(Hash(assetPath), Is.EqualTo(expected[1]), expected[0] + ".asset");
                Assert.That(Hash(assetPath + ".meta"), Is.EqualTo(expected[2]),
                    expected[0] + ".asset.meta");
            }
        }

        [Test]
        public void StructureAndAllMetadataExactlyMatchAcceptedSourceCampaigns()
        {
            CampaignDefinition main = LoadMain();
            Assert.That(main.CampaignId, Is.EqualTo("neon_grid_main"));
            Assert.That(main.DisplayName, Is.EqualTo("Neon Grid"));
            Assert.That(main.Chapters, Has.Count.EqualTo(5));
            Assert.That(main.Chapters.Select(chapter => chapter.ChapterId), Is.EqualTo(new[]
                { "power_station", "substation", "control_center", "automation_plant", "central_grid" }));
            Assert.That(main.Chapters.Select(chapter => chapter.DisplayName), Is.EqualTo(new[]
                { "Power Station", "Substation", "Control Center", "Automation Plant", "Central Grid" }));

            var chapterIds = new HashSet<string>(StringComparer.Ordinal);
            var levelIds = new HashSet<string>(StringComparer.Ordinal);
            int levelCount = 0;
            for (int chapterIndex = 0; chapterIndex < main.Chapters.Count; chapterIndex++)
            {
                CampaignDefinition source = LoadSource(chapterIndex);
                CampaignChapterDefinition expected = source.Chapters.Single();
                CampaignChapterDefinition actual = main.Chapters[chapterIndex];
                Assert.That(chapterIds.Add(actual.ChapterId), Is.True);
                Assert.That(actual.ChapterId, Is.EqualTo(expected.ChapterId));
                Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName));
                Assert.That(actual.Levels, Has.Count.EqualTo(10));
                Assert.That(new CampaignProgressService(source).MaximumCampaignStars, Is.EqualTo(30));
                for (int levelIndex = 0; levelIndex < actual.Levels.Count; levelIndex++)
                {
                    CampaignLevelEntry expectedEntry = expected.Levels[levelIndex];
                    CampaignLevelEntry actualEntry = actual.Levels[levelIndex];
                    Assert.That(levelIds.Add(actualEntry.LevelId), Is.True,
                        $"Duplicate LevelId {actualEntry.LevelId}");
                    Assert.That(actualEntry.LevelId, Is.EqualTo(expectedEntry.LevelId));
                    Assert.That(actualEntry.DisplayName, Is.EqualTo(expectedEntry.DisplayName));
                    Assert.That(actualEntry.LevelDefinition, Is.SameAs(expectedEntry.LevelDefinition));
                    Assert.That(actualEntry.AuthoredOptimalMoves,
                        Is.EqualTo(expectedEntry.AuthoredOptimalMoves));
                    AssertTutorialEqual(expectedEntry.Tutorial, actualEntry.Tutorial);
                    levelCount++;
                }
            }
            Assert.That(levelCount, Is.EqualTo(50));
            Assert.That(levelIds.Count, Is.EqualTo(50));
            Assert.That(new CampaignProgressService(main).MaximumCampaignStars, Is.EqualTo(150));
        }

        [Test]
        public void AllFiftyLevelsRemainValidAndMatchAcceptedExactMinima()
        {
            CampaignDefinition main = LoadMain();
            for (int chapterIndex = 0; chapterIndex < main.Chapters.Count; chapterIndex++)
            for (int levelIndex = 0; levelIndex < 10; levelIndex++)
            {
                CampaignLevelEntry entry = main.Chapters[chapterIndex].Levels[levelIndex];
                LevelValidationResult validation = new LevelValidator().Validate(entry.LevelDefinition);
                Assert.That(validation.IsValid, Is.True,
                    $"{entry.LevelId}: {string.Join(" | ", validation.Errors.Select(error => error.Message))}");
                Assert.That(validation.Warnings, Is.Empty);
                var timer = Stopwatch.StartNew();
                PuzzleSolverResult result = new PuzzleSolver().Solve(
                    entry.LevelDefinition.CreateBoardState(), PuzzleSolverProfiles.AuthoringExact);
                timer.Stop();
                int expected = AcceptedMinima[chapterIndex][levelIndex];
                Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved), entry.LevelId);
                Assert.That(result.MinimumMoveCount, Is.EqualTo(expected), entry.LevelId);
                Assert.That(entry.AuthoredOptimalMoves, Is.EqualTo(expected), entry.LevelId);
                TestContext.WriteLine($"{entry.LevelId}: min={result.MinimumMoveCount}, " +
                    $"states={result.ExploredStateCount}, depth={result.DeepestSearchDepth}, " +
                    $"ms={timer.Elapsed.TotalMilliseconds:F2}");
            }
        }

        [Test]
        public void ContinuousProgressionNavigationAndCampaignCompletionAreGeneric()
        {
            CampaignDefinition campaign = LoadMain();
            var progress = new CampaignProgressService(campaign);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore());
            Assert.That(progress.IsCampaignComplete, Is.False);
            Assert.That(progress.GetChapterState("power_station"),
                Is.EqualTo(CampaignChapterState.Available));
            for (int index = 1; index < 5; index++)
                Assert.That(progress.GetChapterState(campaign.Chapters[index].ChapterId),
                    Is.EqualTo(CampaignChapterState.Locked));

            for (int chapterIndex = 0; chapterIndex < campaign.Chapters.Count; chapterIndex++)
            {
                CampaignChapterDefinition chapter = campaign.Chapters[chapterIndex];
                Assert.That(flow.OpenChapter(chapter.ChapterId), Is.True);
                Assert.That(flow.StartLevel(chapter.Levels[0].LevelId), Is.True);
                for (int levelIndex = 0; levelIndex < chapter.Levels.Count; levelIndex++)
                {
                    Solve(flow.ActiveSession);
                    if (levelIndex < 9)
                    {
                        AssertNormalNavigation(flow.ResultNavigation, true);
                        Assert.That(flow.StartNextLevel(), Is.True);
                    }
                    else
                    {
                        Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.True);
                        Assert.That(flow.ResultNavigation.ShowRetry, Is.True);
                        Assert.That(flow.ResultNavigation.ShowLevels, Is.False);
                        Assert.That(flow.ResultNavigation.ShowMap, Is.True);
                        Assert.That(flow.ResultNavigation.ShowNext, Is.False);
                        Assert.That(flow.Retry(), Is.True);
                        Solve(flow.ActiveSession);
                        AssertNormalNavigation(flow.ResultNavigation, false);
                    }
                }
                Assert.That(progress.GetChapterState(chapter.ChapterId),
                    Is.EqualTo(CampaignChapterState.Restored));
                if (chapterIndex + 1 < campaign.Chapters.Count)
                    Assert.That(progress.GetChapterState(campaign.Chapters[chapterIndex + 1].ChapterId),
                        Is.EqualTo(CampaignChapterState.Available));
                flow.ReturnToMap();
            }
            Assert.That(progress.IsCampaignComplete, Is.True);
            Assert.That(progress.TotalStars, Is.LessThanOrEqualTo(150));
        }

        [Test]
        public void CrossChapterSaveLoadPreservesProgressStatsAndIsolation()
        {
            CampaignDefinition campaign = LoadMain();
            string root = Path.Combine(Path.GetTempPath(), "NeonGridM12Tests",
                Guid.NewGuid().ToString("N"));
            string path = CampaignSaveStore.BuildSavePath(root, campaign.CampaignId);
            try
            {
                Assert.That(path, Does.EndWith(Path.Combine("NeonGrid", "neon_grid_main",
                    CampaignSaveStore.SaveFileName)));
                foreach (string sourceName in SourceCampaignNames)
                {
                    CampaignDefinition source = Resources.Load<CampaignDefinition>(
                        $"Campaigns/{sourceName}");
                    Assert.That(path, Is.Not.EqualTo(CampaignSaveStore.BuildSavePath(root,
                        source.CampaignId)));
                }

                var progress = new CampaignProgressService(campaign);
                CompleteChapter(progress, campaign.Chapters[0], 2);
                for (int index = 0; index < 4; index++)
                    Assert.That(Record(progress, campaign.Chapters[1].Levels[index],
                        index % 3 + 1, index + 4, 10f + index).Accepted, Is.True);
                Assert.That(new CampaignSaveStore(path).Save(progress).Succeeded, Is.True);
                CampaignLoadResult loaded = new CampaignSaveStore(path).Load(campaign);
                Assert.That(loaded.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
                Assert.That(loaded.Progress.GetChapterState("power_station"),
                    Is.EqualTo(CampaignChapterState.Restored));
                Assert.That(loaded.Progress.GetChapterState("substation"),
                    Is.EqualTo(CampaignChapterState.Available));
                Assert.That(loaded.Progress.GetChapterState("control_center"),
                    Is.EqualTo(CampaignChapterState.Locked));
                LevelProgress s04 = loaded.Progress.GetLevelProgress("substation_04");
                Assert.That(s04.Completed, Is.True);
                Assert.That(s04.BestStars, Is.EqualTo(1));
                Assert.That(s04.BestMoves, Is.EqualTo(7));
                Assert.That(s04.BestTimeSeconds, Is.EqualTo(13f));

                var later = new CampaignProgressService(campaign);
                CompleteChapter(later, campaign.Chapters[0], 1);
                CompleteChapter(later, campaign.Chapters[1], 2);
                CompleteChapter(later, campaign.Chapters[2], 3);
                for (int index = 0; index < 4; index++)
                    Record(later, campaign.Chapters[3].Levels[index], 2, index + 5, 20f + index);
                Assert.That(new CampaignSaveStore(path).Save(later).Succeeded, Is.True);
                CampaignLoadResult laterLoad = new CampaignSaveStore(path).Load(campaign);
                Assert.That(laterLoad.Progress.GetChapterState("control_center"),
                    Is.EqualTo(CampaignChapterState.Restored));
                Assert.That(laterLoad.Progress.GetChapterState("automation_plant"),
                    Is.EqualTo(CampaignChapterState.Available));
                Assert.That(laterLoad.Progress.GetChapterState("central_grid"),
                    Is.EqualTo(CampaignChapterState.Locked));
                Assert.That(new CampaignSaveStore(path).Load(LoadSource(0)).Status,
                    Is.EqualTo(CampaignLoadStatus.CampaignMismatch));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void GenericMapSelectorsAndHudSupportAllFiveChapters()
        {
            CampaignDefinition campaign = LoadMain();
            var root = new GameObject("Main Campaign Presentation Test");
            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { }, () => { });
                Transform map = root.transform.Find("Campaign Canvas/Campaign Map");
                Assert.That(map, Is.Not.Null);
                foreach (CampaignChapterDefinition chapter in campaign.Chapters)
                {
                    view.ShowChapter(chapter);
                    Transform selection = root.transform.Find("Campaign Canvas/Level Selection");
                    Transform grid = selection.Find("Generated Level Grid");
                    Assert.That(grid.childCount, Is.EqualTo(4));
                    for (int row = 0; row < 4; row++)
                        Assert.That(grid.GetChild(row).childCount, Is.EqualTo(row == 3 ? 1 : 3));
                    Assert.That(grid.GetChild(3).GetComponent<HorizontalLayoutGroup>().childAlignment,
                        Is.EqualTo(TextAnchor.MiddleCenter));
                    Assert.That(selection.GetComponentInChildren<ScrollRect>(true), Is.Null);
                    Assert.That(selection.Find("Back To Map"), Is.Not.Null);

                    for (int index = 0; index < chapter.Levels.Count; index++)
                        Assert.That(CampaignRuntimeController.FindLevelOrdinal(chapter,
                            chapter.Levels[index]), Is.EqualTo(index + 1));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(0, "PS_10")]
        [TestCase(1, "S_10")]
        [TestCase(2, "CC_10")]
        [TestCase(3, "AP_10")]
        [TestCase(4, "CG_10")]
        public void RuntimeHint_FinalLevelsStayWithinAcceptedBudget(int chapterIndex, string label)
        {
            CampaignLevelEntry entry = LoadMain().Chapters[chapterIndex].Levels[9];
            var timer = Stopwatch.StartNew();
            PuzzleSolverResult result = new PuzzleSolver().Solve(
                entry.LevelDefinition.CreateBoardState(), PuzzleSolverProfiles.RuntimeHint);
            timer.Stop();
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.ExploredStateCount,
                Is.LessThan(PuzzleSolverProfiles.RuntimeHint.MaximumExploredStates));
            TestContext.WriteLine($"{label} RuntimeHint: status={result.Status}, " +
                $"states={result.ExploredStateCount}, depth={result.DeepestSearchDepth}, " +
                $"ms={timer.Elapsed.TotalMilliseconds:F2}, limitHit=False");
        }

        [Test]
        public void CG10ScrambleIsInMemoryAndRemainsWithinHintBudget()
        {
            CampaignLevelEntry entry = LoadMain().Chapters[4].Levels[9];
            BoardState board = entry.LevelDefinition.CreateBoardState();
            var action = new PuzzleAction(new GridPosition(1, 0), PuzzleActionType.RotateClockwise);
            Assert.That(board.TryApplyAction(action), Is.True);
            var timer = Stopwatch.StartNew();
            PuzzleSolverResult result = new PuzzleSolver().Solve(board, PuzzleSolverProfiles.RuntimeHint);
            timer.Stop();
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.ExploredStateCount,
                Is.LessThan(PuzzleSolverProfiles.RuntimeHint.MaximumExploredStates));
            TestContext.WriteLine($"CG_10 scrambled RuntimeHint: status={result.Status}, " +
                $"states={result.ExploredStateCount}, depth={result.DeepestSearchDepth}, " +
                $"ms={timer.Elapsed.TotalMilliseconds:F2}, limitHit=False");
        }

        [Test]
        public void AllSessionsUseAuthoredBaselinesWithoutStartupSearch()
        {
            foreach (CampaignChapterDefinition chapter in LoadMain().Chapters)
            foreach (CampaignLevelEntry entry in chapter.Levels)
            {
                var runner = new RejectingRunner();
                using (var session = new GameplaySession(entry.LevelDefinition,
                    entry.AuthoredOptimalMoves.Value, runner,
                    new PuzzleSolverOptions { MaximumExploredStates = 1, MaximumDepth = 0 }))
                {
                    Assert.That(session.OptimalMoves, Is.EqualTo(entry.AuthoredOptimalMoves.Value));
                    Assert.That(session.OptimalSolverStatus, Is.EqualTo(PuzzleSolverStatus.Solved));
                    Assert.That(runner.StartCount, Is.Zero);
                }
            }
        }

        [Test]
        public void RuntimeSceneUsesMainCampaignAndIsEnabledInBuildSettings()
        {
            const string path = "Assets/NeonGrid/Scenes/M12_NeonGrid_Main.unity";
            CampaignDefinition campaign = LoadMain();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path),
                Is.True);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                CampaignRuntimeController controller = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<CampaignRuntimeController>())
                    .Single(component => component != null);
                var serialized = new SerializedObject(controller);
                Assert.That(serialized.FindProperty("campaign").objectReferenceValue,
                    Is.SameAs(campaign));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static CampaignDefinition LoadMain() =>
            Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
        private static CampaignDefinition LoadSource(int index) =>
            Resources.Load<CampaignDefinition>($"Campaigns/{SourceCampaignNames[index]}");

        private static string Hash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void AssertTutorialEqual(LevelTutorialDefinition expected,
            LevelTutorialDefinition actual)
        {
            if (expected == null) { Assert.That(actual, Is.Null); return; }
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Steps, Has.Count.EqualTo(expected.Steps.Count));
            for (int index = 0; index < expected.Steps.Count; index++)
            {
                Assert.That(actual.Steps[index].Message, Is.EqualTo(expected.Steps[index].Message));
                Assert.That(actual.Steps[index].TargetPosition,
                    Is.EqualTo(expected.Steps[index].TargetPosition));
                Assert.That(actual.Steps[index].CompletionCondition,
                    Is.EqualTo(expected.Steps[index].CompletionCondition));
            }
        }

        private static void CompleteChapter(CampaignProgressService progress,
            CampaignChapterDefinition chapter, int stars)
        {
            foreach (CampaignLevelEntry entry in chapter.Levels)
                Assert.That(Record(progress, entry, stars, stars, 1f).Accepted, Is.True);
        }

        private static CampaignProgressUpdate Record(CampaignProgressService progress,
            CampaignLevelEntry entry, int stars, int moves, float time) =>
            progress.RecordCompletion(entry.LevelId,
                CampaignTestFixture.Result(entry.LevelDefinition, moves, time, stars));

        private static void Solve(GameplaySession session)
        {
            PuzzleSolverResult result = new PuzzleSolver().Solve(session.Board,
                PuzzleSolverProfiles.AuthoringExact);
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            foreach (PuzzleAction action in result.Solution)
                Assert.That(session.PerformAction(action), Is.True);
            Assert.That(session.IsCompleted, Is.True);
        }

        private static void AssertNormalNavigation(CampaignResultNavigationState navigation,
            bool showNext)
        {
            Assert.That(navigation.ShowRetry, Is.True);
            Assert.That(navigation.ShowLevels, Is.True);
            Assert.That(navigation.ShowMap, Is.False);
            Assert.That(navigation.ShowNext, Is.EqualTo(showNext));
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public string SavePath => "memory://neon-grid-main";
            public CampaignLoadResult Load(CampaignDefinition campaign) =>
                new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(campaign), Array.Empty<string>());
            public CampaignSaveResult Save(CampaignProgressService progress) =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }

        private sealed class RejectingRunner : IHintSolverRunner
        {
            public int StartCount { get; private set; }
            public void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
                Action<PuzzleSolverResult> completed, Action<Exception> failed)
            {
                StartCount++;
                failed(new InvalidOperationException("Search was not expected."));
            }
        }
    }
}

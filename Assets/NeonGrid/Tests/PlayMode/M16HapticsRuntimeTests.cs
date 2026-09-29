using System.Collections;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NeonGrid.Tests
{
    public sealed class M16HapticsRuntimeTests
    {
        [UnityTest]
        public IEnumerator RepeatedRequestsEnableDisableAndCompletionRemainBounded()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            NeonGridHapticsDefinition definition = Resources.Load<NeonGridHapticsDefinition>(
                "VisualPrototypes/M16_HapticsPrototype");
            Assert.That(definition, Is.Not.Null);
            var backend = new FakeBackend();
            var root = new GameObject("M16 C1 Runtime Haptics");
            NeonGridHapticsService service = root.AddComponent<NeonGridHapticsService>();
            service.Initialize(definition, null, backend);
            service.Initialize(definition, null, backend);
            int hierarchySize = root.GetComponentsInChildren<Transform>(true).Length;
            int componentCount = root.GetComponentsInChildren<Component>(true).Length;
            try
            {
                for (int index = 0; index < 30; index++)
                    service.TryPlay(NeonGridHapticEvent.TileRotate, index * .1f);
                service.SetHapticsEnabled(false);
                int beforeDisabled = backend.Pulses.Count;
                Assert.That(service.TryPlay(NeonGridHapticEvent.Hint, 10f), Is.False);
                Assert.That(backend.Pulses, Has.Count.EqualTo(beforeDisabled));
                service.SetHapticsEnabled(true);
                Assert.That(service.TryPlay(NeonGridHapticEvent.Hint, 10f), Is.True);
                Assert.That(service.TryPlay(NeonGridHapticEvent.Completion, 11f), Is.True);
                Assert.That(service.TryPlay(NeonGridHapticEvent.Completion, 20f), Is.False);
                service.ResetCompletionLifecycle();
                Assert.That(service.TryPlay(NeonGridHapticEvent.Completion, 20f), Is.True);
                Assert.That(service.enabled, Is.True);
                Assert.That(service.gameObject.activeInHierarchy, Is.True);
                Assert.That(root.GetComponentsInChildren<Transform>(true),
                    Has.Length.EqualTo(hierarchySize));
                Assert.That(root.GetComponentsInChildren<Component>(true),
                    Has.Length.EqualTo(componentCount));
            }
            finally
            {
                NeonGridHapticsSettings.HapticsEnabled = true;
                Object.Destroy(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrototypeCreatesHapticsWhileProductionDefaultRemainsUnbound()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            Assert.That(prototype, Is.Not.Null);
            Assert.That(prototype.HapticsDefinition, Is.Not.Null);
            var prototypeRoot = new GameObject("M16 C1 Prototype Binding");
            var productionRoot = new GameObject("M16 C1 Production Isolation");
            try
            {
                BoardController prototypeBoard = prototypeRoot.AddComponent<BoardController>();
                prototypeBoard.Initialize(prototype.SimpleLevel, prototype.ProductionVisualTheme,
                    prototype.CircuitJuice, prototype.AudioDefinition,
                    prototype.HapticsDefinition);
                Assert.That(prototypeBoard.HapticsService, Is.Not.Null);
                Assert.That(prototypeBoard.HapticsService.HapticsEnabled, Is.True);

                BoardController productionBoard = productionRoot.AddComponent<BoardController>();
                productionBoard.Initialize(prototype.SimpleLevel);
                Assert.That(productionBoard.HapticsService, Is.Null);
            }
            finally
            {
                NeonGridHapticsSettings.HapticsEnabled = true;
                Object.Destroy(prototypeRoot);
                Object.Destroy(productionRoot);
            }
            yield return null;
        }

        private sealed class FakeBackend : INeonGridHapticsBackend
        {
            public readonly List<(int Duration, float Intensity)> Pulses =
                new List<(int Duration, float Intensity)>();
            public bool IsSupported => true;

            public bool TryPulse(int durationMilliseconds, float intensity)
            {
                Pulses.Add((durationMilliseconds, intensity));
                return true;
            }
        }
    }
}

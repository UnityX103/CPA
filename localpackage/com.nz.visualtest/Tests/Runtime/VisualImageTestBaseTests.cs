using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NZ.VisualTest.Tests
{
    [TestFixture]
    public sealed class VisualImageTestBaseTests : VisualImageTestBase
    {
        private GameObject _cameraObject;
        private GameObject _uiDocumentObject;
        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;

        [UnitySetUp]
        public IEnumerator SetUpCamera()
        {
            _cameraObject = new GameObject("VisualImageTestBaseTests_Camera");
            _cameraObject.AddComponent<Camera>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownCamera()
        {
            if (_uiDocumentObject != null)
            {
                UnityEngine.Object.Destroy(_uiDocumentObject);
                _uiDocumentObject = null;
                _uiDocument = null;
            }

            if (_panelSettings != null)
            {
                UnityEngine.Object.Destroy(_panelSettings);
                _panelSettings = null;
            }

            if (_cameraObject != null)
            {
                UnityEngine.Object.Destroy(_cameraObject);
                _cameraObject = null;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureScreenStep_WritesArtifactAndManifest()
        {
            yield return CaptureScreenStep("full-screen", "Baselines/full-screen.png", "smoke");

            string actualPath = Path.Combine(CurrentRunDirectory, "01-full-screen-actual.png");
            Assert.That(File.Exists(actualPath), Is.True);

            VisualImageTestRunManifest manifest = LoadManifest(CurrentRunDirectory);

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.outputDirectory, Is.Not.Empty);
            Assert.That(manifest.steps, Is.Not.Null);
            Assert.That(manifest.steps.Count, Is.EqualTo(1));
            Assert.That(manifest.steps[0].name, Is.EqualTo("full-screen"));
            Assert.That(manifest.steps[0].baselineImagePath, Is.EqualTo("Baselines/full-screen.png"));
            Assert.That(manifest.steps[0].notes, Is.EqualTo("smoke"));

            VisualImageTestRunManifest snapshot = CurrentManifest;
            snapshot.outputDirectory = "mutated-output";
            snapshot.steps[0].name = "mutated-step";
            snapshot.steps.Add(new VisualImageTestStepManifest { index = 99, name = "extra" });

            VisualImageTestRunManifest latest = CurrentManifest;
            Assert.That(latest.outputDirectory, Is.EqualTo(manifest.outputDirectory));
            Assert.That(latest.steps.Count, Is.EqualTo(1));
            Assert.That(latest.steps[0].name, Is.EqualTo("full-screen"));

            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureScreenStep_WritesSequentialArtifactsAcrossMultipleSteps()
        {
            yield return CaptureScreenStep("full-screen", "Baselines/full-screen.png", "first");
            yield return CaptureScreenStep("detail-screen", "Baselines/detail-screen.png", "second");

            string firstActualPath = Path.Combine(CurrentRunDirectory, "01-full-screen-actual.png");
            string secondActualPath = Path.Combine(CurrentRunDirectory, "02-detail-screen-actual.png");
            Assert.That(File.Exists(firstActualPath), Is.True);
            Assert.That(File.Exists(secondActualPath), Is.True);

            VisualImageTestRunManifest manifest = LoadManifest(CurrentRunDirectory);
            Assert.That(manifest.outputDirectory, Is.Not.Empty);
            Assert.That(manifest.steps.Count, Is.EqualTo(2));

            Assert.That(manifest.steps[0].index, Is.EqualTo(1));
            Assert.That(manifest.steps[0].actualImagePath, Is.EqualTo("01-full-screen-actual.png"));
            Assert.That(manifest.steps[1].index, Is.EqualTo(2));
            Assert.That(manifest.steps[1].actualImagePath, Is.EqualTo("02-detail-screen-actual.png"));

            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureStep_WritesElementArtifactAndManifest()
        {
            VisualElement targetElement = null;
            yield return CreateCapturableElement(element => targetElement = element);

            Assert.That(targetElement, Is.Not.Null);
            Assert.That(targetElement.worldBound.width, Is.GreaterThan(0f));
            Assert.That(targetElement.worldBound.height, Is.GreaterThan(0f));

            yield return CaptureStep("element", targetElement, "Baselines/element.png", "element-smoke");

            string actualPath = Path.Combine(CurrentRunDirectory, "01-element-actual.png");
            Assert.That(File.Exists(actualPath), Is.True);

            VisualImageTestRunManifest manifest = LoadManifest(CurrentRunDirectory);
            Assert.That(manifest.steps.Count, Is.EqualTo(1));
            Assert.That(manifest.steps[0].name, Is.EqualTo("element"));
            Assert.That(manifest.steps[0].actualImagePath, Is.EqualTo("01-element-actual.png"));
            Assert.That(manifest.steps[0].baselineImagePath, Is.EqualTo("Baselines/element.png"));
            Assert.That(manifest.steps[0].notes, Is.EqualTo("element-smoke"));
        }

        [Test]
        public void CaptureScreenStep_ThrowsInvalidOperationException_WhenRunIsNotInitialized()
        {
            var probe = new VisualImageTestBaseProbe();

            IEnumerator capture = probe.InvokeCaptureScreenStep("full-screen");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => capture.MoveNext());
            Assert.That(exception.Message, Does.Contain("SetUpVisualImageRun"));
        }

        [Test]
        public void CaptureStep_ThrowsInvalidOperationException_WhenRunIsNotInitialized()
        {
            var probe = new VisualImageTestBaseProbe();

            IEnumerator capture = probe.InvokeCaptureStep("target", new VisualElement());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => capture.MoveNext());
            Assert.That(exception.Message, Does.Contain("SetUpVisualImageRun"));
        }

        [Test]
        public void CaptureStep_ThrowsAssertionException_WhenTargetHasNoSize()
        {
            var probe = new VisualImageTestBaseProbe();
            RunEnumeratorToCompletion(probe.Initialize());

            try
            {
                IEnumerator capture = probe.InvokeCaptureStep("target", new VisualElement());

                Assert.Throws<AssertionException>(() => capture.MoveNext());
            }
            finally
            {
                CleanupProbe(probe);
            }
        }

        private static VisualImageTestRunManifest LoadManifest(string runDirectory)
        {
            string manifestPath = Path.Combine(runDirectory, "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True);
            string json = File.ReadAllText(manifestPath);
            return JsonUtility.FromJson<VisualImageTestRunManifest>(json);
        }

        private IEnumerator CreateCapturableElement(Action<VisualElement> onReady)
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _uiDocumentObject = new GameObject("VisualImageTestBaseTests_UIDocument");
            _uiDocument = _uiDocumentObject.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;

            yield return null;

            VisualElement root = _uiDocument.rootVisualElement;
            Assert.That(root, Is.Not.Null, "UIDocument rootVisualElement 必须可用。");
            root.style.flexGrow = 1f;

            var target = new VisualElement
            {
                name = "capture-target"
            };
            target.style.position = Position.Absolute;
            target.style.left = 24f;
            target.style.top = 16f;
            target.style.width = 128f;
            target.style.height = 72f;
            target.style.backgroundColor = new Color(0.2f, 0.7f, 0.35f, 1f);
            root.Add(target);

            yield return WaitUntilElementHasWorldBound(target, 30);

            onReady?.Invoke(target);
        }

        private static IEnumerator WaitUntilElementHasWorldBound(VisualElement target, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (target.panel != null
                    && target.worldBound.width > 0f
                    && target.worldBound.height > 0f)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("等待 VisualElement 完成布局超时。");
        }

        private static void RunEnumeratorToCompletion(IEnumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
            }
        }

        private static void CleanupProbe(VisualImageTestBaseProbe probe)
        {
            string runDirectory = probe.ReadCurrentRunDirectory();

            if (!string.IsNullOrWhiteSpace(runDirectory))
            {
                RunEnumeratorToCompletion(probe.Shutdown());

                if (Directory.Exists(runDirectory))
                {
                    Directory.Delete(runDirectory, true);
                }
            }
        }

        private sealed class VisualImageTestBaseProbe : VisualImageTestBase
        {
            public IEnumerator InvokeCaptureScreenStep(string stepName, string baselinePath = null, string notes = null)
            {
                return CaptureScreenStep(stepName, baselinePath, notes);
            }

            public IEnumerator InvokeCaptureStep(
                string stepName,
                VisualElement target,
                string baselinePath = null,
                string notes = null,
                int padding = 0)
            {
                return CaptureStep(stepName, target, baselinePath, notes, padding);
            }

            public IEnumerator Initialize()
            {
                return SetUpVisualImageRun();
            }

            public IEnumerator Shutdown()
            {
                return TearDownVisualImageRun();
            }

            public string ReadCurrentRunDirectory()
            {
                return CurrentRunDirectory;
            }
        }
    }
}

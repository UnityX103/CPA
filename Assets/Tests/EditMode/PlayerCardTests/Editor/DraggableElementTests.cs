using APP.Pomodoro.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    public sealed class DraggableElementTests
    {
        [Test]
        public void DraggingInsideParent_UpdatesElementPosition()
        {
            var parent = new VisualElement();
            parent.style.width = 500f;
            parent.style.height = 400f;

            var target = new VisualElement();
            target.style.width = 220f;
            target.style.height = 120f;
            target.style.left = 10f;
            target.style.top = 20f;
            parent.Add(target);

            var controller = DraggableElement.MakeDraggable(target);

            controller.ProcessPointerDown(new Vector2(100f, 100f), 0);
            controller.ProcessPointerMove(new Vector2(140f, 155f));

            Assert.That(target.style.left.value.value, Is.EqualTo(50f));
            Assert.That(target.style.top.value.value, Is.EqualTo(75f));
        }

        [Test]
        public void DraggingBeyondParentBounds_ClampsElementPosition()
        {
            var parent = new VisualElement();
            parent.style.width = 300f;
            parent.style.height = 220f;

            var target = new VisualElement();
            target.style.width = 120f;
            target.style.height = 80f;
            parent.Add(target);

            var controller = DraggableElement.MakeDraggable(target);

            controller.ProcessPointerDown(new Vector2(20f, 20f), 0);
            controller.ProcessPointerMove(new Vector2(500f, 400f));

            Assert.That(target.style.left.value.value, Is.EqualTo(180f));
            Assert.That(target.style.top.value.value, Is.EqualTo(140f));
        }

        [Test]
        public void PointerCaptureOut_PreventsLaterMoveFromChangingPosition()
        {
            var parent = new VisualElement();
            parent.style.width = 500f;
            parent.style.height = 400f;

            var target = new VisualElement();
            target.style.width = 120f;
            target.style.height = 80f;
            parent.Add(target);

            var controller = DraggableElement.MakeDraggable(target);

            controller.ProcessPointerDown(new Vector2(10f, 10f), 0);
            controller.ProcessPointerCaptureOut();
            controller.ProcessPointerMove(new Vector2(100f, 100f));

            Assert.That(target.style.left.value.value, Is.EqualTo(0f));
            Assert.That(target.style.top.value.value, Is.EqualTo(0f));
        }

        [Test]
        public void NullParentMove_DoesNotThrow()
        {
            var target = new VisualElement();
            target.style.width = 120f;
            target.style.height = 80f;

            var controller = DraggableElement.MakeDraggable(target);

            controller.ProcessPointerDown(new Vector2(10f, 10f), 0);

            Assert.DoesNotThrow(() => controller.ProcessPointerMove(new Vector2(40f, 40f)));
        }
    }
}

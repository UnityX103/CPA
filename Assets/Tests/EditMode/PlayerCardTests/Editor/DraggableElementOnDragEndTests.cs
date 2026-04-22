using APP.Pomodoro.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    public sealed class DraggableElementOnDragEndTests
    {
        [Test]
        public void OnDragEnd_InvokedWithFinalPosition_AfterPointerUp()
        {
            var parent = new VisualElement { style = { width = 400, height = 300 } };
            var target = new VisualElement { style = { width = 100, height = 50, position = Position.Absolute } };
            var handle = new VisualElement();
            parent.Add(target);
            target.Add(handle);

            var ctrl = new DraggableElement.DragController(target, handle);

            Vector2? result = null;
            ctrl.OnDragEnd += pos => result = pos;

            ctrl.ProcessPointerDown(new Vector2(10, 10), pointerId: 0);
            ctrl.ProcessPointerMove(new Vector2(60, 40));
            ctrl.ProcessPointerUp(pointerId: 0);

            Assert.That(result, Is.Not.Null, "拖拽结束应触发 OnDragEnd");
            Assert.That(result.Value, Is.EqualTo(new Vector2(50, 30)),
                "OnDragEnd 传入的位置应等于 target 最终 left/top");
        }

        [Test]
        public void OnDragEnd_NotInvoked_WhenNeverDragged()
        {
            var target = new VisualElement();
            var handle = new VisualElement();
            target.Add(handle);
            var ctrl = new DraggableElement.DragController(target, handle);

            bool called = false;
            ctrl.OnDragEnd += _ => called = true;

            ctrl.ProcessPointerUp(pointerId: 0); // 无对应的 down
            Assert.That(called, Is.False);
        }
    }
}

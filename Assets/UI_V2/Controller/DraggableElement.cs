using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 为 UI Toolkit 元素提供拖拽能力。
    /// </summary>
    public static class DraggableElement
    {
        public static DragController MakeDraggable(VisualElement target, VisualElement dragHandle = null)
        {
            var controller = new DragController(target, dragHandle ?? target);
            controller.RegisterCallbacks();
            return controller;
        }

        public sealed class DragController
        {
            private readonly VisualElement _target;
            private readonly VisualElement _handle;

            private Vector2 _pointerStart;
            private Vector2 _elementStart;
            private int _activePointerId = -1;
            private bool _dragging;
            private bool _callbacksRegistered;

            public DragController(VisualElement target, VisualElement handle)
            {
                _target = target ?? throw new ArgumentNullException(nameof(target));
                _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            }

            public void RegisterCallbacks()
            {
                if (_callbacksRegistered)
                {
                    return;
                }

                _handle.RegisterCallback<PointerDownEvent>(OnPointerDown);
                _handle.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                _handle.RegisterCallback<PointerUpEvent>(OnPointerUp);
                _handle.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
                _callbacksRegistered = true;
            }

            public void ProcessPointerDown(Vector2 pointerPosition, int pointerId)
            {
                _pointerStart = pointerPosition;
                _elementStart = new Vector2(GetCurrentLeft(_target), GetCurrentTop(_target));
                _activePointerId = pointerId;
                _dragging = true;
            }

            public void ProcessPointerMove(Vector2 pointerPosition)
            {
                if (!_dragging)
                {
                    return;
                }

                try
                {
                    var delta = pointerPosition - _pointerStart;
                    var parent = _target.parent;

                    var parentWidth = GetElementWidth(parent);
                    var parentHeight = GetElementHeight(parent);
                    var targetWidth = GetElementWidth(_target);
                    var targetHeight = GetElementHeight(_target);

                    var maxLeft = Mathf.Max(0f, parentWidth - targetWidth);
                    var maxTop = Mathf.Max(0f, parentHeight - targetHeight);

                    var newLeft = Mathf.Clamp(_elementStart.x + delta.x, 0f, maxLeft);
                    var newTop = Mathf.Clamp(_elementStart.y + delta.y, 0f, maxTop);

                    _target.style.left = newLeft;
                    _target.style.top = newTop;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DraggableElement] 拖拽更新失败：{ex}");
                }
            }

            public void ProcessPointerUp(int pointerId)
            {
                if (_activePointerId != pointerId)
                {
                    return;
                }

                _dragging = false;
                _activePointerId = -1;
            }

            public void ProcessPointerCaptureOut()
            {
                _dragging = false;
                _activePointerId = -1;
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                try
                {
                    ProcessPointerDown(evt.position, evt.pointerId);
                    _handle.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                    evt.PreventDefault();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DraggableElement] PointerDown 处理失败：{ex}");
                }
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                try
                {
                    ProcessPointerMove(evt.position);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DraggableElement] PointerMove 处理失败：{ex}");
                }
                finally
                {
                    evt.StopPropagation();
                }
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                try
                {
                    ProcessPointerUp(evt.pointerId);
                    if (_handle.HasPointerCapture(evt.pointerId))
                    {
                        _handle.ReleasePointer(evt.pointerId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DraggableElement] PointerUp 处理失败：{ex}");
                }
                finally
                {
                    evt.StopPropagation();
                }
            }

            private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                try
                {
                    ProcessPointerCaptureOut();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DraggableElement] PointerCaptureOut 处理失败：{ex}");
                }
            }

            private static float GetCurrentLeft(VisualElement element)
            {
                if (element == null)
                {
                    return 0f;
                }

                return GetResolvedOrStyleValue(element.resolvedStyle.left, element.style.left);
            }

            private static float GetCurrentTop(VisualElement element)
            {
                if (element == null)
                {
                    return 0f;
                }

                return GetResolvedOrStyleValue(element.resolvedStyle.top, element.style.top);
            }

            private static float GetElementWidth(VisualElement element)
            {
                if (element == null)
                {
                    return Screen.width;
                }

                return GetResolvedOrStyleValue(element.resolvedStyle.width, element.style.width, Screen.width);
            }

            private static float GetElementHeight(VisualElement element)
            {
                if (element == null)
                {
                    return Screen.height;
                }

                return GetResolvedOrStyleValue(element.resolvedStyle.height, element.style.height, Screen.height);
            }

            private static float GetResolvedOrStyleValue(float resolvedValue, StyleLength styleValue, float fallbackValue = 0f)
            {
                if (!float.IsNaN(resolvedValue) && resolvedValue > 0f)
                {
                    return resolvedValue;
                }

                if (styleValue.keyword == StyleKeyword.Auto || styleValue.keyword == StyleKeyword.Null)
                {
                    return fallbackValue;
                }

                if (styleValue.value.unit == LengthUnit.Pixel)
                {
                    return styleValue.value.value;
                }

                return fallbackValue;
            }
        }
    }
}

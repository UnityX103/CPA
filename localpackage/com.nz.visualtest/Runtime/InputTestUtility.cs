using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace NZ.VisualTest.Runtime
{
    /// <summary>
    /// 输入系统测试工具
    /// </summary>
    public sealed class InputTestUtility : IDisposable
    {
        private readonly List<InputDevice> _devices = new List<InputDevice>();
        private Collider2D _currentHovered;

        public TDevice AddDevice<TDevice>() where TDevice : InputDevice
        {
            var device = InputSystem.AddDevice<TDevice>();
            if (device != null)
            {
                _devices.Add(device);
            }
            return device;
        }

        public void Dispose()
        {
            Cleanup();
        }

        public void Cleanup()
        {
            for (int i = _devices.Count - 1; i >= 0; i--)
            {
                if (_devices[i] != null)
                {
                    InputSystem.RemoveDevice(_devices[i]);
                }
            }
            _devices.Clear();
        }

        public static void SetControl<TValue>(InputControl<TValue> control, TValue value) where TValue : struct
        {
            if (control == null) return;
            InputSystem.QueueDeltaStateEvent(control, value);
            InputSystem.Update();
        }

        public static void SetGamepadStick(Gamepad gamepad, Vector2 value)
        {
            if (gamepad == null) return;
            var state = new GamepadState
            {
                leftStick = value
            };
            InputSystem.QueueStateEvent(gamepad, state);
            InputSystem.Update();
        }

        public static void SetGamepadButton(Gamepad gamepad, GamepadButton button, bool pressed)
        {
            if (gamepad == null) return;
            var state = new GamepadState().WithButton(button, pressed);
            InputSystem.QueueStateEvent(gamepad, state);
            InputSystem.Update();
        }

        public void MoveMouseAndSimulateHover(Camera camera, Mouse mouse, Vector2 screenPosition)
        {
            if (camera == null || mouse == null) return;

            SetControl(mouse.position, screenPosition);

            var ray = camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            var hit2D = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
            var hit = hit2D.collider;
            if (hit == _currentHovered) return;

            if (_currentHovered != null)
            {
                _currentHovered.gameObject.SendMessage("OnMouseExit", SendMessageOptions.DontRequireReceiver);
            }

            if (hit != null)
            {
                hit.gameObject.SendMessage("OnMouseEnter", SendMessageOptions.DontRequireReceiver);
            }

            _currentHovered = hit;
        }

        public void ForceMouseExitCurrentHover()
        {
            if (_currentHovered == null) return;
            _currentHovered.gameObject.SendMessage("OnMouseExit", SendMessageOptions.DontRequireReceiver);
            _currentHovered = null;
        }
    }
}

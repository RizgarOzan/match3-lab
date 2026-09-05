using System;
using Match3Lab.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Match3Lab.Unity
{
    /// <summary>
    /// Turns pointer gestures into <see cref="Move"/>s: press-and-drag one cell to swap, press-and-
    /// release on a special to tap it. Works with either input backend; mouse and touch alike.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        public event Action<Move> MoveRequested;

        public float DragThreshold = 0.3f;

        private BoardView _board;
        private Camera _camera;
        private bool _pressed;
        private bool _consumed;
        private GridPos _pressCell;
        private Vector3 _pressWorld;

        public bool Enabled = true;

        public void Bind(BoardView board, Camera camera)
        {
            _board = board;
            _camera = camera;
        }

        private void Update()
        {
            if (!Enabled || _board == null) return;

            if (!ReadPointer(out var screen, out bool down, out bool held, out bool up)) return;
            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            world.z = 0f;

            if (down)
            {
                _pressed = _board.TryCellAt(world, out _pressCell);
                _consumed = false;
                _pressWorld = world;
                return;
            }

            if (!_pressed) return;

            if (held && !_consumed)
            {
                var delta = world - _pressWorld;
                if (delta.magnitude >= DragThreshold)
                {
                    _consumed = true;
                    GridPos target = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                        ? _pressCell.Offset(delta.x > 0 ? 1 : -1, 0)
                        : _pressCell.Offset(0, delta.y > 0 ? -1 : 1); // screen up = smaller grid Y
                    MoveRequested?.Invoke(Move.Swap(_pressCell, target));
                }
            }

            if (up)
            {
                if (!_consumed) MoveRequested?.Invoke(Move.Tap(_pressCell));
                _pressed = false;
            }
        }

        private static bool ReadPointer(out Vector2 position, out bool down, out bool held, out bool up)
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer == null)
            {
                position = default; down = held = up = false;
                return false;
            }
            position = pointer.position.ReadValue();
            down = pointer.press.wasPressedThisFrame;
            held = pointer.press.isPressed;
            up = pointer.press.wasReleasedThisFrame;
            return true;
#else
            position = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            up = Input.GetMouseButtonUp(0);
            return true;
#endif
        }
    }
}

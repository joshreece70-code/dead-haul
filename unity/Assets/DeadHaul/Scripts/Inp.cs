using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeadHaul
{
    // One input wrapper that works whether the project uses the new Input System, the old Input Manager, or both.
    public static class Inp
    {
#if ENABLE_INPUT_SYSTEM
        static Key Map(KeyCode k)
        {
            if (k >= KeyCode.A && k <= KeyCode.Z) return (Key)((int)Key.A + (k - KeyCode.A));
            if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha9) return (Key)((int)Key.Digit1 + (k - KeyCode.Alpha1));
            switch (k)
            {
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Space: return Key.Space;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.F1: return Key.F1;
            }
            return Key.None;
        }

        static UnityEngine.InputSystem.Controls.KeyControl K(KeyCode k)
        {
            var kb = Keyboard.current; if (kb == null) return null;
            var key = Map(k); if (key == Key.None) return null;
            return kb[key];
        }

        public static bool Held(KeyCode k) { var c = K(k); return c != null && c.isPressed; }
        public static bool Down(KeyCode k) { var c = K(k); return c != null && c.wasPressedThisFrame; }

        static UnityEngine.InputSystem.Controls.ButtonControl MB(int b)
        {
            var m = Mouse.current; if (m == null) return null;
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
        }
        public static bool MouseHeld(int b) { var c = MB(b); return c != null && c.isPressed; }
        public static bool MouseDown(int b) { var c = MB(b); return c != null && c.wasPressedThisFrame; }
        public static Vector2 MouseDelta { get { var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue(); } }
        public static float Scroll { get { var m = Mouse.current; return m == null ? 0f : Mathf.Sign(m.scroll.ReadValue().y) * (Mathf.Abs(m.scroll.ReadValue().y) > 0.01f ? 1f : 0f); } }
#else
        public static bool Held(KeyCode k) => Input.GetKey(k);
        public static bool Down(KeyCode k) => Input.GetKeyDown(k);
        public static bool MouseHeld(int b) => Input.GetMouseButton(b);
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b);
        public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        public static float Scroll { get { float s = Input.mouseScrollDelta.y; return Mathf.Abs(s) > 0.01f ? Mathf.Sign(s) : 0f; } }
#endif
    }
}

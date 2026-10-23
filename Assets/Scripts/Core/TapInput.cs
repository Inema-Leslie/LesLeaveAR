using UnityEngine;
using UnityEngine.InputSystem;

namespace LesLeave
{
    /// <summary>One tap (touch on a phone, left click on a PC) per call, with its screen position.</summary>
    public static class TapInput
    {
        public static bool TryGet(out Vector2 position)
        {
            position = Vector2.zero;

            Touchscreen ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
            {
                position = ts.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                position = m.position.ReadValue();
                return true;
            }
            return false;
        }
    }
}

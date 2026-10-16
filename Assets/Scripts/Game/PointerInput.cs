using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Mouse and touch input through the built-in Input Manager (the project's active input handling).
    /// On phones and tablets Unity maps the first touch to the left mouse button, so one code path covers both.
    /// </summary>
    public static class PointerInput
    {
        public static bool WasPressedThisFrame => Input.GetMouseButtonDown(0);
        public static bool IsHeld => Input.GetMouseButton(0);
        public static bool WasReleasedThisFrame => Input.GetMouseButtonUp(0);

        /// <summary>Pointer position in screen pixels.</summary>
        public static Vector2 Position => Input.mousePosition;
    }
}

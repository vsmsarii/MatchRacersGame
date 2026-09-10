using UnityEngine.InputSystem;

namespace MatchRacers
{
    public sealed class RaceInputReader
    {
        public int ReadPressedKey()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return 0;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                return 1;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                return 2;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                return 3;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
                return 4;
            if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame)
                return 5;

            return 0;
        }
    }
}

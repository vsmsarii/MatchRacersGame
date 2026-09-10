using UnityEngine;

namespace CasualKit.Core
{
    public sealed class DeviceHaptics : Service, IHaptics
    {
        public void Play(EHapticStrength strength)
        {
            if (strength == EHapticStrength.Light)
                return;

            Handheld.Vibrate();
        }
    }
}

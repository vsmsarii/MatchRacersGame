namespace CasualKit.Core
{
    public enum EHapticStrength
    {
        Light = 0,
        Medium = 1,
        Heavy = 2,
    }

    public interface IHaptics : IService
    {
        void Play(EHapticStrength strength);
    }
}

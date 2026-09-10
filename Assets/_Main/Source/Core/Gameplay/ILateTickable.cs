namespace CasualKit.Core
{
    public interface ILateTickable 
    {
        void LateTick(float deltaTime);
    }
}
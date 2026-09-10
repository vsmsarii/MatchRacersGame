namespace CasualKit.Core
{
    public readonly struct ApplicationPauseChanged
    {
        public readonly bool Paused;

        public ApplicationPauseChanged(bool paused)
        {
            Paused = paused;
        }
    }
}

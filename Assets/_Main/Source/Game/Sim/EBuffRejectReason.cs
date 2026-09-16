namespace MatchRacers
{
    public enum EBuffRejectReason
    {
        None = 0,
        InvalidKey = 1,
        NotRacing = 2,
        BuffActive = 3,
        Cooldown = 4,
        InsufficientEnergy = 5,
        AlreadyFinished = 6,
        KeyCooldown = 7,
    }
}

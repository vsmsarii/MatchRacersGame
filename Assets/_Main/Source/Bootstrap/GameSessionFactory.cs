using CasualKit.Core;
using MatchRacers;

namespace CasualKit.Bootstrap
{
    public static class GameSessionFactory
    {
        public static IGameSession Create(GameContext context)
        {
            return new RaceSession(context);
        }
    }
}

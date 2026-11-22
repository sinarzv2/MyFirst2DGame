using System;

namespace Player
{
    // What happened to the hero after a hit or a fall.
    public enum HealthResult
    {
        Hurt,       // a heart less, still standing
        LifeLost,   // a life less: knocked out, then back at the start with full hearts
        GameOver,   // no lives left: the level starts again
    }

    // The rules of hearts and lives, with no Unity in them (a plain class, not a MonoBehaviour), so
    // Edit Mode tests can try every case directly. The hero has `lives` lives of `heartsPerLife`
    // hearts each: an enemy costs a heart, a pit costs a whole life, and the last heart is a life too.
    // PlayerHealth puts it in the game.
    public class Health
    {
        public int HeartsPerLife { get; }
        public int Hearts { get; private set; }
        public int Lives { get; private set; }
        public bool IsGameOver => Lives == 0;

        public Health(int heartsPerLife, int lives)
        {
            if (heartsPerLife < 1 || lives < 1)
            {
                throw new ArgumentOutOfRangeException(heartsPerLife < 1 ? nameof(heartsPerLife) : nameof(lives), "at least one");
            }

            HeartsPerLife = heartsPerLife;
            Hearts = heartsPerLife;
            Lives = lives;
        }

        // Hit by an enemy: a heart less; the last heart takes a life with it.
        public HealthResult TakeHit()
        {
            CheckNotOver();
            if (Hearts == 0)
            {
                throw new InvalidOperationException("no hearts until the next life starts (Refill)");
            }

            Hearts--;
            return Hearts > 0 ? HealthResult.Hurt : LoseLife();
        }

        // Fallen into a pit: the whole life at once, however many hearts were left.
        public HealthResult LoseLife()
        {
            CheckNotOver();
            Hearts = 0;
            Lives--;
            return Lives > 0 ? HealthResult.LifeLost : HealthResult.GameOver;
        }

        // Back at the start with the next life: all its hearts.
        public void Refill()
        {
            if (!IsGameOver)
            {
                Hearts = HeartsPerLife;
            }
        }

        private void CheckNotOver()
        {
            if (IsGameOver)
            {
                throw new InvalidOperationException("the game is over: no lives left to lose");
            }
        }
    }
}

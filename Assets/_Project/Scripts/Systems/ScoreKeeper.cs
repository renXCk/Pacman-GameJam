namespace Game.Systems
{
    // Score rules in plain C#: add points, and award the extra life exactly once per game when
    // the score reaches the threshold. ScoreSystem wires it to the event channels.
    public sealed class ScoreKeeper
    {
        readonly int extraLifeScore;

        public int Score { get; private set; }
        public bool ExtraLifeAwarded { get; private set; }

        // extraLifeScore of 0 or less turns the extra life off.
        public ScoreKeeper(int extraLifeScore)
        {
            this.extraLifeScore = extraLifeScore;
        }

        public void Reset()
        {
            Score = 0;
            ExtraLifeAwarded = false;
        }

        // Adds points. Returns true when this addition earns the extra life.
        public bool Add(int points)
        {
            if (points <= 0)
                return false;

            Score += points;

            if (ExtraLifeAwarded || extraLifeScore <= 0 || Score < extraLifeScore)
                return false;

            ExtraLifeAwarded = true;
            return true;
        }
    }
}

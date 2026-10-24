namespace Game.Systems
{
    // Lives rules in plain C#. LivesSystem wires it to the event channels.
    public sealed class LivesKeeper
    {
        public int Lives { get; private set; }
        public bool IsOut => Lives <= 0;

        public void Reset(int startingLives) => Lives = startingLives < 0 ? 0 : startingLives;

        // Never goes below zero, so a late second catch cannot make lives negative.
        public void Lose()
        {
            if (Lives > 0)
                Lives--;
        }

        public void Gain() => Lives++;
    }
}

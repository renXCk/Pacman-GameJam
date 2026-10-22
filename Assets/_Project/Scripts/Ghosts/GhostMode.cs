namespace Game.Ghosts
{
    // The global mode every ghost out in the maze follows.
    public enum GhostMode
    {
        Scatter,
        Chase,
        Frightened
    }

    // Payload of the ModeChanged channel.
    public readonly struct GhostModeChange
    {
        public readonly GhostMode Mode;

        // How long this mode lasts. Zero means until something else changes it.
        public readonly float Seconds;

        public GhostModeChange(GhostMode mode, float seconds)
        {
            Mode = mode;
            Seconds = seconds;
        }

        public override string ToString() => Seconds > 0f ? $"{Mode} for {Seconds:0.#}s" : $"{Mode} until changed";
    }
}

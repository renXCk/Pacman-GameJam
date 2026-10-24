namespace Game.Core
{
    // GameManager states, from section 7.3 of the spec.
    public enum GameState
    {
        Boot,
        Menu,
        Ready,
        Playing,
        PlayerCaught,
        LevelComplete,
        GameOver
    }
}

namespace Game.Ghosts
{
    // What one ghost is doing right now. Scatter, Chase and Frightened follow the global mode;
    // the house states and Eaten are the ghost's own.
    public enum GhostState
    {
        InHouse,      // waiting in the ghost house for its release time
        LeavingHouse, // walking out through the house door
        Scatter,      // heading for its corner
        Chase,        // hunting with its own targeting rule
        Frightened,   // blue, slow, fleeing; touching it banishes it
        Eaten         // eyes only, hurrying back to the house to revive
    }
}

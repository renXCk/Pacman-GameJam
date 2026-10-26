using UnityEngine;

namespace Game.Maze
{
    // One end of a warp. A trigger on a 'W' tile: when the Player walks in, MazeWarps carries them
    // to the other end. MazeWarps creates these and calls Initialise; nothing is set in the Inspector.
    public sealed class WarpDoor : MonoBehaviour
    {
        MazeWarps owner;

        public WarpDoor Partner { get; private set; }
        public TileCoord Tile { get; private set; }

        public void Initialise(MazeWarps warps, TileCoord tile)
        {
            owner = warps;
            Tile = tile;
        }

        public void SetPartner(WarpDoor other) => Partner = other;

        void OnTriggerEnter(Collider other)
        {
            if (owner != null)
                owner.Touch(this, other);
        }
    }
}

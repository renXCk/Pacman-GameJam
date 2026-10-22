using UnityEngine;

namespace Game.Core
{
    // Which Chase-mode targeting rule a ghost uses, named after the original ghosts.
    public enum ChaseRule
    {
        Blinky, // the player's position
        Pinky,  // a point ahead of where the player is facing
        Inky,   // its partner's position mirrored through a point ahead of the player
        Clyde   // the player when far away, otherwise his own scatter corner
    }

    public enum MazeCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    // Per-ghost data. One asset per ghost; each ghost prefab points at its own.
    [CreateAssetMenu(fileName = "GhostConfig", menuName = "Game/Configs/Ghost Config")]
    public sealed class GhostConfig : ScriptableObject
    {
        [SerializeField] string displayName = "Ghost";
        [SerializeField] Color color = Color.red;
        [SerializeField] ChaseRule chaseRule = ChaseRule.Blinky;

        [Tooltip("The maze corner this ghost heads for in Scatter mode.")]
        [SerializeField] MazeCorner scatterCorner = MazeCorner.TopRight;

        [Header("Ghost house")]
        [Tooltip("Off for a ghost that starts outside the house, like Blinky.")]
        [SerializeField] bool startsInHouse = true;

        [Tooltip("Seconds after the round starts before this ghost leaves the house.")]
        [Min(0f)] [SerializeField] float releaseDelay;

        [Header("Targeting")]
        [Tooltip("Pinky and Inky: how many tiles ahead of the player to aim.")]
        [Min(0)] [SerializeField] int aheadTiles = 4;

        [Tooltip("Clyde: when he is closer to the player than this many tiles, he heads for his corner instead.")]
        [Min(0f)] [SerializeField] float shyDistanceTiles = 8f;

        [Tooltip("Inky: the ghost whose position gets mirrored (Blinky).")]
        [SerializeField] GhostConfig partner;

        public string DisplayName => displayName;
        public Color Color => color;
        public ChaseRule ChaseRule => chaseRule;
        public MazeCorner ScatterCorner => scatterCorner;
        public bool StartsInHouse => startsInHouse;
        public float ReleaseDelay => releaseDelay;
        public int AheadTiles => aheadTiles;
        public float ShyDistanceTiles => shyDistanceTiles;
        public GhostConfig Partner => partner;
    }
}

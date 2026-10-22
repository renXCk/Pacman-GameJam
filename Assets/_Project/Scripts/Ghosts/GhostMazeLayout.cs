using Game.Core;
using UnityEngine;

namespace Game.Ghosts
{
    // The maze points the ghosts need. Dev A's maze builder fills them in from the map with
    // Configure, or they can be placed by hand in a scene.
    public sealed class GhostMazeLayout : MonoBehaviour
    {
        [Tooltip("Width of one maze tile in meters. Targeting rules measure distances in tiles.")]
        [Min(0.1f)] [SerializeField] float tileSize = 2f;

        [Header("Ghost house")]
        [Tooltip("Just outside the house door. Ghosts leaving the house walk here first.")]
        [SerializeField] Transform houseExit;

        [Tooltip("Inside the house. Banished ghosts come back here to revive.")]
        [SerializeField] Transform houseInside;

        [Header("Scatter corners")]
        [SerializeField] Transform topLeft;
        [SerializeField] Transform topRight;
        [SerializeField] Transform bottomLeft;
        [SerializeField] Transform bottomRight;

        public float TileSize => tileSize;
        public Vector3 HouseExit => houseExit.position;
        public Vector3 HouseInside => houseInside.position;

        public bool IsComplete =>
            houseExit != null && houseInside != null &&
            topLeft != null && topRight != null && bottomLeft != null && bottomRight != null;

        public Vector3 Corner(MazeCorner corner) => CornerMarker(corner).position;

        // For a maze builder that creates the marker objects at runtime.
        public void Configure(float tileSize, Transform houseExit, Transform houseInside,
            Transform topLeft, Transform topRight, Transform bottomLeft, Transform bottomRight)
        {
            this.tileSize = tileSize;
            this.houseExit = houseExit;
            this.houseInside = houseInside;
            this.topLeft = topLeft;
            this.topRight = topRight;
            this.bottomLeft = bottomLeft;
            this.bottomRight = bottomRight;
        }

        Transform CornerMarker(MazeCorner corner) => corner switch
        {
            MazeCorner.TopLeft => topLeft,
            MazeCorner.TopRight => topRight,
            MazeCorner.BottomLeft => bottomLeft,
            _ => bottomRight
        };

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.8f);
            DrawMarker(houseExit);
            DrawMarker(houseInside);

            Gizmos.color = Color.yellow;
            DrawMarker(topLeft);
            DrawMarker(topRight);
            DrawMarker(bottomLeft);
            DrawMarker(bottomRight);
        }

        static void DrawMarker(Transform marker)
        {
            if (marker != null)
                Gizmos.DrawWireCube(marker.position + Vector3.up * 0.5f, Vector3.one);
        }
    }
}

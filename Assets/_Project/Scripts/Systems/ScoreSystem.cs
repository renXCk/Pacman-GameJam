using Game.Core;
using Game.Events;
using Game.Ghosts;
using UnityEngine;

namespace Game.Systems
{
    // Turns pickups and banishes into points. Every point value comes from an asset: cells and
    // flares from their PickupConfig, batteries from the current LevelConfig, banishes from the
    // GhostBanished payload (whose combo values live in GhostAIConfig).
    public sealed class ScoreSystem : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameRulesConfig rules;
        [Tooltip("Flares raise a payload-free channel the ghosts share, so their points come from here.")]
        [SerializeField] PickupConfig flare;

        [Header("Listens to")]
        [SerializeField] VoidEventChannel newGameStarted;
        [SerializeField] LevelConfigEventChannel levelStarted;
        [SerializeField] PickupEventChannel pickupCollected;
        [SerializeField] VoidEventChannel flareCollected;
        [SerializeField] GhostBanishedEventChannel ghostBanished;

        [Header("Raises")]
        [SerializeField] IntEventChannel scoreChanged;
        [SerializeField] VoidEventChannel extraLifeAwarded;

        ScoreKeeper keeper;
        LevelConfig level;

        public int Score => keeper != null ? keeper.Score : 0;

        void Awake()
        {
            keeper = new ScoreKeeper(rules != null ? rules.ExtraLifeScore : 0);
        }

        void OnEnable()
        {
            if (newGameStarted != null) newGameStarted.Raised += OnNewGame;
            if (levelStarted != null) levelStarted.Raised += OnLevelStarted;
            if (pickupCollected != null) pickupCollected.Raised += OnPickup;
            if (flareCollected != null) flareCollected.Raised += OnFlare;
            if (ghostBanished != null) ghostBanished.Raised += OnBanished;
        }

        void OnDisable()
        {
            if (newGameStarted != null) newGameStarted.Raised -= OnNewGame;
            if (levelStarted != null) levelStarted.Raised -= OnLevelStarted;
            if (pickupCollected != null) pickupCollected.Raised -= OnPickup;
            if (flareCollected != null) flareCollected.Raised -= OnFlare;
            if (ghostBanished != null) ghostBanished.Raised -= OnBanished;
        }

        void OnNewGame()
        {
            keeper.Reset();
            if (scoreChanged != null)
                scoreChanged.Raise(keeper.Score);
        }

        void OnLevelStarted(LevelConfig config) => level = config;

        void OnPickup(PickupConfig pickup)
        {
            if (pickup != null)
                Add(pickup.PointsFor(level));
        }

        void OnFlare()
        {
            if (flare != null)
                Add(flare.PointsFor(level));
        }

        void OnBanished(GhostBanishedInfo info) => Add(info.Points);

        void Add(int points)
        {
            if (points <= 0)
                return;

            bool earnedLife = keeper.Add(points);

            if (scoreChanged != null)
                scoreChanged.Raise(keeper.Score);
            if (earnedLife && extraLifeAwarded != null)
                extraLifeAwarded.Raise();
        }
    }
}

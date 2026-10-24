using Game.Core;
using Game.Events;
using UnityEngine;

namespace Game.Systems
{
    // Owns the life count. GameManager asks for a life to be taken (LifeLost), ScoreSystem
    // announces the extra life (ExtraLifeAwarded), and everyone hears the result on LivesChanged.
    // Channels raise synchronously, so GameManager knows the new count as soon as LifeLost returns.
    public sealed class LivesSystem : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameRulesConfig rules;

        [Header("Listens to")]
        [SerializeField] VoidEventChannel newGameStarted;
        [SerializeField] VoidEventChannel lifeLost;
        [SerializeField] VoidEventChannel extraLifeAwarded;

        [Header("Raises")]
        [SerializeField] IntEventChannel livesChanged;

        readonly LivesKeeper keeper = new();

        public int Lives => keeper.Lives;

        void OnEnable()
        {
            if (newGameStarted != null) newGameStarted.Raised += OnNewGame;
            if (lifeLost != null) lifeLost.Raised += OnLifeLost;
            if (extraLifeAwarded != null) extraLifeAwarded.Raised += OnExtraLife;
        }

        void OnDisable()
        {
            if (newGameStarted != null) newGameStarted.Raised -= OnNewGame;
            if (lifeLost != null) lifeLost.Raised -= OnLifeLost;
            if (extraLifeAwarded != null) extraLifeAwarded.Raised -= OnExtraLife;
        }

        void OnNewGame()
        {
            keeper.Reset(rules != null ? rules.StartingLives : 0);
            Announce();
        }

        void OnLifeLost()
        {
            keeper.Lose();
            Announce();
        }

        void OnExtraLife()
        {
            keeper.Gain();
            Announce();
        }

        void Announce()
        {
            if (livesChanged != null)
                livesChanged.Raise(keeper.Lives);
        }
    }
}

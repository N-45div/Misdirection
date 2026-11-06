using System;
using UnityEngine;

namespace Misdirection.Tricks
{
    /// <summary>A trick that can take its turn in a show.</summary>
    public interface ISequencedTrick
    {
        /// <summary>Fires when the trick has finished (after the bow), not when it busts.</summary>
        event Action Completed;
    }

    /// <summary>
    /// Runs tricks one after another: only the current trick is enabled (they share the coin, hands,
    /// table and crowd); when it completes, the next one starts, wrapping round. First slice of the
    /// Day 8 show flow (three-trick set, tips, walk-offs come then).
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class ShowSequence : MonoBehaviour
    {
        [Tooltip("Trick directors in show order (each must implement ISequencedTrick).")]
        [SerializeField] private MonoBehaviour[] tricks = new MonoBehaviour[0];

        private int current;

        public int Current { get { return current; } }
        public int TricksPerformed { get; private set; }

        public void SetTricks(MonoBehaviour[] order)
        {
            tricks = order ?? new MonoBehaviour[0];
        }

        private void Start()
        {
            for (int i = 0; i < tricks.Length; i++)
            {
                ISequencedTrick t = tricks[i] as ISequencedTrick;
                if (t == null)
                {
                    Debug.LogWarning("[Misdirection] ShowSequence: " + (tricks[i] != null ? tricks[i].GetType().Name : "empty slot") +
                                     " can't take part in a show (no ISequencedTrick).");
                    continue;
                }
                t.Completed += OnCompleted;
            }
            Activate(0);
        }

        private void OnDestroy()
        {
            foreach (MonoBehaviour m in tricks)
            {
                ISequencedTrick t = m as ISequencedTrick;
                if (t != null)
                {
                    t.Completed -= OnCompleted;
                }
            }
        }

        private void OnCompleted()
        {
            TricksPerformed++;
            if (tricks.Length > 1)
            {
                Activate((current + 1) % tricks.Length);
            }
        }

        private void Activate(int index)
        {
            current = Mathf.Clamp(index, 0, Mathf.Max(0, tricks.Length - 1));
            // Disable first so only one trick ever owns the coin.
            for (int i = 0; i < tricks.Length; i++)
            {
                if (tricks[i] != null && i != current)
                {
                    tricks[i].enabled = false;
                }
            }
            if (tricks.Length > 0 && tricks[current] != null)
            {
                tricks[current].enabled = true;
                Debug.Log("[Misdirection] Show: now performing " + tricks[current].GetType().Name);
            }
        }
    }
}

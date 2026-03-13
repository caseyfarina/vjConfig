using UnityEngine;
using System;
using System.Collections;

namespace VJSystem
{
    public enum DeckIdentity { A, B }

    public class DualDeckManager : MonoBehaviour
    {
        public static DualDeckManager Instance { get; private set; }

        public StageController stageA;
        public StageController stageB;

        public DeckIdentity liveDeck = DeckIdentity.A;

        public StageController LiveStage => liveDeck == DeckIdentity.A ? stageA : stageB;
        public StageController StandbyStage => liveDeck == DeckIdentity.A ? stageB : stageA;

        public event Action OnTakeCompleted;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Take()
        {
            StartCoroutine(TakeSequence());
        }

        IEnumerator TakeSequence()
        {
            liveDeck = liveDeck == DeckIdentity.A ? DeckIdentity.B : DeckIdentity.A;
            yield return new WaitForEndOfFrame();
            OnTakeCompleted?.Invoke();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}

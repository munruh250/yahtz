using System;
using System.Collections;
using UnityEngine;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>The five 2D dice as a row. Tumble animation just cycles random faces on the
    /// rolling dice before settling on the engine values — pure theater, per architecture.</summary>
    public sealed class DiceView2D : MonoBehaviour, IDiceView
    {
        private const float RollDuration = 0.45f;
        private const float FaceFlickerInterval = 0.06f;

        private DieView2D[] _dice;
        private Coroutine _rollRoutine;
        private int[] _pendingValues;
        private bool[] _pendingKept;
        private Action _pendingSettled;

        public void Init(DieView2D[] dice)
        {
            _dice = dice;
        }

        public void SetDice(int[] values, bool[] kept)
        {
            for (int i = 0; i < _dice.Length; i++)
            {
                _dice[i].SetValue(values[i]);
                _dice[i].SetKept(kept[i]);
            }
        }

        public void PlayRoll(int[] values, bool[] kept, Action onSettled)
        {
            SkipAnimation(); // never two rolls in flight
            if (!GameController.AnimationsEnabled)
            {
                SetDice(values, kept);
                onSettled();
                return;
            }
            _pendingValues = values;
            _pendingKept = kept;
            _pendingSettled = onSettled;
            _rollRoutine = StartCoroutine(RollRoutine(values, kept));
        }

        public void SetInteractable(bool interactable)
        {
            foreach (var die in _dice)
                die.SetInteractable(interactable);
        }

        public void SkipAnimation()
        {
            if (_rollRoutine == null)
                return;
            StopCoroutine(_rollRoutine);
            _rollRoutine = null;
            Settle();
        }

        private void Settle()
        {
            SetDice(_pendingValues, _pendingKept);
            var settled = _pendingSettled;
            _pendingValues = null;
            _pendingKept = null;
            _pendingSettled = null;
            settled?.Invoke();
        }

        private IEnumerator RollRoutine(int[] values, bool[] kept)
        {
            float elapsed = 0f;
            while (elapsed < RollDuration)
            {
                for (int i = 0; i < _dice.Length; i++)
                    if (!kept[i])
                        _dice[i].SetValue(UnityEngine.Random.Range(1, 7));
                yield return new WaitForSeconds(FaceFlickerInterval);
                elapsed += FaceFlickerInterval;
            }
            _rollRoutine = null;
            Settle();
        }
    }
}

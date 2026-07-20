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
            if (_rollRoutine != null)
                StopCoroutine(_rollRoutine);
            if (!GameController.AnimationsEnabled)
            {
                SetDice(values, kept);
                onSettled();
                return;
            }
            _rollRoutine = StartCoroutine(RollRoutine(values, kept, onSettled));
        }

        public void SetInteractable(bool interactable)
        {
            foreach (var die in _dice)
                die.SetInteractable(interactable);
        }

        private IEnumerator RollRoutine(int[] values, bool[] kept, Action onSettled)
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
            SetDice(values, kept);
            _rollRoutine = null;
            onSettled();
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>The five physical dice behind the same IDiceView contract as the 2D layer.
    /// Unkept dice pour from the cup position with randomized impulses and settle (guided)
    /// on the engine values; kept dice sit kinematic in the keep row near the player's edge.
    /// Value 0 (not yet rolled this turn) hides a die.</summary>
    public sealed class DiceView3D : MonoBehaviour, IDiceView
    {
        private Die3D[] _dice;
        private Camera _camera;
        private GameController _controller;
        private Vector3 _cupPosition;
        private Vector3[] _restSlots;
        private Vector3[] _keepSlots;
        private Transform[] _keepMarkers;
        private bool _interactable;
        private Action _pendingSettled;
        private readonly bool[] _placedKept = new bool[5];
        private System.Random _throwRng = new System.Random();

        public Die3D[] Dice => _dice;

        public void Init(Die3D[] dice, Camera camera, GameController controller,
            Vector3 cupPosition, Vector3[] restSlots, Vector3[] keepSlots, Transform[] keepMarkers)
        {
            _dice = dice;
            _camera = camera;
            _controller = controller;
            _cupPosition = cupPosition;
            _restSlots = restSlots;
            _keepSlots = keepSlots;
            _keepMarkers = keepMarkers;
        }

        // ---- IDiceView -------------------------------------------------------

        public void SetDice(int[] values, bool[] kept)
        {
            for (int i = 0; i < _dice.Length; i++)
            {
                var die = _dice[i];
                bool visible = values[i] >= 1 && values[i] <= 6;
                ShowKeepMarker(i, visible && kept[i]);
                if (!visible)
                {
                    die.gameObject.SetActive(false);
                    continue;
                }
                bool wasHidden = !die.gameObject.activeSelf;
                die.gameObject.SetActive(true);

                // Never disturb an in-flight die (PlayRoll owns it), and leave a physics-
                // settled die in its natural scatter spot unless its value/keep state changed.
                if (!wasHidden && !die.Settled)
                    continue;
                if (!wasHidden && die.Settled && die.UpFace() == values[i] && _placedKept[i] == kept[i])
                    continue;

                die.PlaceAt(kept[i] ? _keepSlots[i] : _restSlots[i], values[i], DeterministicYaw(i, values[i]));
                _placedKept[i] = kept[i];
            }
        }

        public void PlayRoll(int[] values, bool[] kept, Action onSettled)
        {
            if (!GameController.AnimationsEnabled)
            {
                SetDice(values, kept);
                onSettled();
                return;
            }

            _pendingSettled = onSettled;
            for (int i = 0; i < _dice.Length; i++)
            {
                _dice[i].gameObject.SetActive(true);
                ShowKeepMarker(i, kept[i]);
                if (kept[i])
                {
                    _dice[i].PlaceAt(_keepSlots[i], values[i], DeterministicYaw(i, values[i]));
                    _placedKept[i] = true;
                    continue;
                }
                _placedKept[i] = false;
                // Randomized pour toward the table center; guidance handles the rest. Kept
                // deliberately gentle: in the tight roll zone a harder throw just pinballs off
                // the walls, and values that skitter around are hard to read.
                var from = _cupPosition + new Vector3(Rnd(-0.03f, 0.03f), Rnd(0f, 0.05f), Rnd(-0.03f, 0.03f));
                var target = _restSlots[i];
                var flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
                var velocity = flat * Rnd(1.4f, 1.9f) + Vector3.up * Rnd(0.20f, 0.40f);
                var spin = new Vector3(Rnd(-12f, 12f), Rnd(-12f, 12f), Rnd(-12f, 12f));
                _dice[i].LaunchRoll(values[i], from, velocity, spin);
            }
        }

        public void SetInteractable(bool interactable) => _interactable = interactable;

        /// <summary>The gold pad under a kept die — the visible half of "this one is staying".</summary>
        private void ShowKeepMarker(int index, bool visible) => _keepMarkers[index].gameObject.SetActive(visible);

        public void SkipAnimation()
        {
            if (_pendingSettled == null)
                return;
            foreach (var die in _dice)
                die.SnapNow();
            // FireSettledIfDone runs in Update; force the check now for instant skips.
            FireSettledIfDone();
        }

        // ---- Settle + tap input ----------------------------------------------

        private void Update()
        {
            FireSettledIfDone();

            if (_interactable && Input.GetMouseButtonDown(0) && !IsPointerOverUi())
            {
                var ray = _camera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 5f))
                {
                    var die = hit.collider.GetComponent<Die3D>();
                    if (die != null)
                        _controller.OnDieTapped(die.Index);
                }
            }
        }

        private void FireSettledIfDone()
        {
            if (_pendingSettled == null)
                return;
            foreach (var die in _dice)
                if (die.gameObject.activeSelf && !die.Settled)
                    return;
            var settled = _pendingSettled;
            _pendingSettled = null;
            settled();
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private float Rnd(float min, float max) => (float)(_throwRng.NextDouble() * (max - min) + min);

        /// <summary>Stable decorative yaw so instant placement doesn't jitter between refreshes.</summary>
        private static float DeterministicYaw(int index, int value) => (index * 53 + value * 31) % 360;
    }
}

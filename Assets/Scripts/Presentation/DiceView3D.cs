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
        private Material _faceMaterial, _pipMaterial;
        private bool _interactable;

        /// <summary>Ray length for tap picking — the seated framings sit ~2.5 m from the table.</summary>
        private const float PickDistance = 8f;
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
            bool placed = false;
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
                placed = true;
            }

            // Releasing a kept die drops it back on its rest slot, which another die may have
            // drifted onto during the throw — so re-separate whenever anything was placed.
            if (placed)
                CommitPositions();
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
                // Tossed underarm from the player's edge of the table, away from the camera.
                // Speed is set explicitly rather than scaled by the distance to the target slot:
                // the throw now starts close to those slots, so a distance-scaled impulse would
                // barely move the dice. Kept gentle — in a tight roll zone a harder throw just
                // pinballs off the walls and the values skitter about.
                var from = _cupPosition + new Vector3(Rnd(-0.10f, 0.10f), Rnd(0f, 0.05f), Rnd(-0.02f, 0.02f));
                var target = _restSlots[i];
                var toTarget = new Vector3(target.x - from.x, 0f, target.z - from.z);
                var heading = toTarget.sqrMagnitude > 1e-4f ? toTarget.normalized : Vector3.forward;
                var velocity = heading * Rnd(0.60f, 0.95f) + Vector3.up * Rnd(0.55f, 0.85f);
                var spin = new Vector3(Rnd(-12f, 12f), Rnd(-12f, 12f), Rnd(-12f, 12f));
                _dice[i].LaunchRoll(values[i], from, velocity, spin);
            }
        }

        public void SetInteractable(bool interactable) => _interactable = interactable;

        /// <summary>The face and pip materials are shared across all five dice, so re-tinting them
        /// is the whole of a skin change. Cosmetic only — values come from the engine.</summary>
        public void InitMaterials(Material face, Material pip)
        {
            _faceMaterial = face;
            _pipMaterial = pip;
        }

        public void ApplySkin(DiceSkins.Skin skin)
        {
            if (_faceMaterial != null) _faceMaterial.color = skin.Face;
            if (_pipMaterial != null) _pipMaterial.color = skin.Pip;
        }

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
        }

        /// <summary>The die under a screen point, or null if the tap missed one.
        ///
        /// Must consider every hit along the ray, not just the first: the invisible fence stands
        /// between the camera and the table, so a first-hit raycast reports a wall and the tap is
        /// silently swallowed. That bug made unkept dice untappable while kept ones — parked in
        /// front of the near wall — still worked.
        ///
        /// Public so tests can exercise the pick path without synthesising input events.</summary>
        public Die3D DieAtScreenPoint(Vector3 screenPoint)
        {
            var hits = Physics.RaycastAll(_camera.ScreenPointToRay(screenPoint), PickDistance);
            Die3D nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var hit in hits)
            {
                var die = hit.collider.GetComponent<Die3D>();
                if (die == null || hit.distance >= nearestDistance)
                    continue;
                nearestDistance = hit.distance;
                nearest = die;
            }
            return nearest;
        }

        private void FireSettledIfDone()
        {
            if (_pendingSettled == null)
                return;
            foreach (var die in _dice)
                if (die.gameObject.activeSelf && !die.Settled)
                    return;
            CommitPositions();
            var settled = _pendingSettled;
            _pendingSettled = null;
            settled();
        }

        /// <summary>Separate any overlapping dice, then push the new transforms into the physics
        /// scene.
        ///
        /// The sync is not optional: this project has Auto Sync Transforms **off**
        /// (`m_AutoSyncTransforms: 0`), so moving a die's Transform leaves its collider at the
        /// old position until the next FixedUpdate. Tap picking raycasts the physics scene, so
        /// without this a tap immediately after keeping or releasing a die would be tested
        /// against where the dice used to be — hitting the wrong die, or nothing.</summary>
        private void CommitPositions()
        {
            Unstack();
            Physics.SyncTransforms();
        }

        /// <summary>Push apart any loose dice that ended up overlapping.
        ///
        /// A die can come to rest on top of another; settling forces it down to table height, so
        /// without this it would end up intersecting the die it was resting on. Relaxation in XZ
        /// only — faces and yaw are already correct and must not change — clamped to the roll
        /// zone so unstacking can never push a die out from under the fence.</summary>
        private void Unstack()
        {
            const float minGap = 0.095f;  // a hair over the 0.09 die width
            const float halfDie = 0.045f;
            float maxX = KitchenBuilder.RollZoneHalfX - halfDie;
            float minZ = KitchenBuilder.RollZoneMinZ + halfDie;
            float maxZ = KitchenBuilder.RollZoneMaxZ - halfDie;

            for (int pass = 0; pass < 12; pass++)
            {
                bool moved = false;
                for (int i = 0; i < _dice.Length; i++)
                {
                    if (!IsLoose(i))
                        continue;
                    for (int j = i + 1; j < _dice.Length; j++)
                    {
                        if (!IsLoose(j))
                            continue;
                        var a = _dice[i].transform.position;
                        var b = _dice[j].transform.position;
                        float dx = b.x - a.x, dz = b.z - a.z;
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance >= minGap)
                            continue;

                        // Exactly coincident: pick a deterministic direction from the indices so
                        // the soak stays reproducible.
                        if (distance < 1e-4f)
                        {
                            float angle = (i * 5 + j) * Mathf.PI / 4f;
                            dx = Mathf.Cos(angle);
                            dz = Mathf.Sin(angle);
                            distance = 1f;
                        }
                        float push = (minGap - distance) / 2f;
                        float ux = dx / distance, uz = dz / distance;
                        _dice[i].SlideTo(
                            Mathf.Clamp(a.x - ux * push, -maxX, maxX),
                            Mathf.Clamp(a.z - uz * push, minZ, maxZ));
                        _dice[j].SlideTo(
                            Mathf.Clamp(b.x + ux * push, -maxX, maxX),
                            Mathf.Clamp(b.z + uz * push, minZ, maxZ));
                        moved = true;
                    }
                }
                if (!moved)
                    return;
            }
        }

        /// <summary>A die on the table from the throw — kept dice sit in their own row and are
        /// placed exactly, so they neither move nor collide.</summary>
        private bool IsLoose(int index) => _dice[index].gameObject.activeSelf && !_placedKept[index];

        private float Rnd(float min, float max) => (float)(_throwRng.NextDouble() * (max - min) + min);

        /// <summary>Stable decorative yaw so instant placement doesn't jitter between refreshes.</summary>
        private static float DeterministicYaw(int index, int value) => (index * 53 + value * 31) % 360;
    }
}

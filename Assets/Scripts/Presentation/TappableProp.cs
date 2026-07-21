using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Yahtzee.Presentation
{
    /// <summary>Marks a physical object in the kitchen as tappable, so interaction stays diegetic
    /// — you reach out and touch the thing rather than hunting for a button. Currently Oma's
    /// scorecard, which is how you peek at her scores.</summary>
    public sealed class TappableProp : MonoBehaviour
    {
        private Action _onTapped;

        public void Init(Action onTapped) => _onTapped = onTapped;

        public void Tap() => _onTapped?.Invoke();
    }

    /// <summary>Routes taps that land on the table to whichever <see cref="TappableProp"/> was
    /// hit. Separate from the dice picking in <see cref="DiceView3D"/> because props stay
    /// tappable when the dice are not — you can peek at Oma's card before rolling, or after your
    /// rolls are spent.</summary>
    public sealed class WorldTapInput : MonoBehaviour
    {
        private const float PickDistance = 8f;

        private Camera _camera;
        private Func<bool> _enabled;

        public void Init(Camera camera, Func<bool> enabled)
        {
            _camera = camera;
            _enabled = enabled;
        }

        private void Update()
        {
            if (_camera == null || !Input.GetMouseButtonDown(0) || !_enabled())
                return;
            if (IsPointerOverUi())
                return;

            var prop = PropAtScreenPoint(Input.mousePosition);
            if (prop != null)
                prop.Tap();
        }

        /// <summary>Nearest tappable prop under a screen point. Considers every hit along the ray
        /// for the same reason the dice pick does: the invisible fence sits between the camera
        /// and the table, so a first-hit raycast would just report a wall.</summary>
        public TappableProp PropAtScreenPoint(Vector3 screenPoint)
        {
            var hits = Physics.RaycastAll(_camera.ScreenPointToRay(screenPoint), PickDistance);
            TappableProp nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var hit in hits)
            {
                var prop = hit.collider.GetComponent<TappableProp>();
                if (prop == null || hit.distance >= nearestDistance)
                    continue;
                nearestDistance = hit.distance;
                nearest = prop;
            }
            return nearest;
        }

        private static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
                return false;
            return Input.touchCount > 0
                ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                : EventSystem.current.IsPointerOverGameObject();
        }
    }
}

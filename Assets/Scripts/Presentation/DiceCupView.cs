using System.Collections;
using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>The dice cup: lifts off the table, tips toward the play area, and releases the
    /// dice from its mouth (design §5.4 — "dice pour from the black cup").
    ///
    /// The cup is theatre on top of theatre. The engine has already decided the values and the
    /// physics is only there to look right, so this animation cannot affect anything; it exists
    /// to make the throw feel like the player's own.</summary>
    public sealed class DiceCupView : MonoBehaviour
    {
        /// <summary>Kept short on purpose: design §5.4 wants tap-to-readable-rest within about
        /// 1.2 s, and the dice still have to tumble and settle after this.</summary>
        private const float LiftSeconds = 0.26f;
        private const float ReturnSeconds = 0.34f;

        /// <summary>Half the cup's height, so the mouth sits at the rim rather than the centre.</summary>
        private const float MouthOffset = 0.085f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _pourPosition;
        private Quaternion _pourRotation;

        public void Init(Vector3 restPosition, Vector3 pourPosition, Quaternion pourRotation)
        {
            _restPosition = restPosition;
            _restRotation = transform.localRotation;
            _pourPosition = pourPosition;
            _pourRotation = pourRotation;
            SnapToRest();
        }

        /// <summary>Where a die leaves the cup: out through the mouth, wherever it is pointing.</summary>
        public Vector3 Mouth => transform.position + transform.up * MouthOffset;

        /// <summary>Which way the dice spill. Down the mouth, flattened so they travel across the
        /// table rather than being fired at it.</summary>
        public Vector3 SpillDirection
        {
            get
            {
                var direction = transform.up;
                direction.y = Mathf.Min(direction.y, -0.15f);
                return direction.normalized;
            }
        }

        public void SnapToRest()
        {
            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
            StopAllCoroutines();
        }

        /// <summary>Lift and tip. Yields until the mouth is aimed at the table.</summary>
        public IEnumerator Tip()
        {
            yield return Move(_restPosition, _restRotation, _pourPosition, _pourRotation, LiftSeconds);
        }

        /// <summary>Set the cup back down. Runs alongside the tumble, so it costs no extra time.</summary>
        public void Return() => StartCoroutine(ReturnRoutine());

        private IEnumerator ReturnRoutine() =>
            Move(transform.localPosition, transform.localRotation, _restPosition, _restRotation, ReturnSeconds);

        private IEnumerator Move(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition,
            Quaternion toRotation, float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / seconds;
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.localPosition = Vector3.Lerp(fromPosition, toPosition, eased);
                transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, eased);
                yield return null;
            }
        }
    }
}

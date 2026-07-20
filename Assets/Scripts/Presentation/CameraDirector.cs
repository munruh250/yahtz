using System.Collections;
using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>Per-phase camera framings with short eased blends (design §5.2: all moves
    /// ≤0.6 s, subtle, mostly translational). M4-start decision: a small custom rig instead
    /// of Cinemachine — four fixed framings with one blend coroutine doesn't warrant the
    /// package; swap later if framing needs grow (flagged in HANDOFF).</summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        public enum Framing { Default, DiceFocus, ScorecardFocus, OmaFocus }

        private const float BlendSeconds = 0.5f;

        private Camera _camera;
        private Vector3[] _positions;
        private Quaternion[] _rotations;
        private float[] _fovs;
        private Coroutine _blend;
        private Framing _current = Framing.Default;

        public Framing Current => _current;

        public void Init(Camera camera,
            (Vector3 pos, Vector3 lookAt, float fov)[] framings)
        {
            _camera = camera;
            _positions = new Vector3[framings.Length];
            _rotations = new Quaternion[framings.Length];
            _fovs = new float[framings.Length];
            for (int i = 0; i < framings.Length; i++)
            {
                _positions[i] = framings[i].pos;
                _rotations[i] = Quaternion.LookRotation(framings[i].lookAt - framings[i].pos, Vector3.up);
                _fovs[i] = framings[i].fov;
            }
            Set(Framing.Default, instant: true);
        }

        public void Set(Framing framing, bool instant = false)
        {
            _current = framing;
            if (_blend != null)
            {
                StopCoroutine(_blend);
                _blend = null;
            }
            int i = (int)framing;
            if (instant || !GameController.AnimationsEnabled)
            {
                Apply(_positions[i], _rotations[i], _fovs[i]);
                return;
            }
            _blend = StartCoroutine(BlendTo(i));
        }

        private IEnumerator BlendTo(int i)
        {
            var fromPos = _camera.transform.position;
            var fromRot = _camera.transform.rotation;
            float fromFov = _camera.fieldOfView;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / BlendSeconds;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)); // eased, no overshoot
                Apply(Vector3.Lerp(fromPos, _positions[i], e),
                    Quaternion.Slerp(fromRot, _rotations[i], e),
                    Mathf.Lerp(fromFov, _fovs[i], e));
                yield return null;
            }
            _blend = null;
        }

        private void Apply(Vector3 pos, Quaternion rot, float fov)
        {
            _camera.transform.SetPositionAndRotation(pos, rot);
            _camera.fieldOfView = fov;
        }
    }
}

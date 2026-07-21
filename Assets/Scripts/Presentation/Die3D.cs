using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>One physical die. The engine decides the value BEFORE the throw; physics is
    /// theater (TECH_PLAN §5.4). The die tumbles freely, and once it loses energy the last
    /// tumble is blended (~0.25 s slerp along the shortest arc) so the resting face matches
    /// the engine value — imperceptible because it happens while the die is still moving.
    /// A watchdog hard-snaps anything still unsettled (cocked, off-oval) after 2.5 s.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class Die3D : MonoBehaviour
    {
        public const float WatchdogSeconds = 2.5f;
        private const float SettleLinearSpeed = 0.30f;
        private const float SettleAngularSpeed = 2.0f;
        private const float CorrectionSeconds = 0.25f;
        /// <summary>How long a die must stay slow before we call it settled even though it is
        /// above table height — i.e. it has come to rest on top of another die. Long enough that
        /// the momentary stillness at the apex of a bounce doesn't count.</summary>
        private const float RestingHighSeconds = 0.30f;

        /// <summary>Local direction that points up when the face shows: 1=+Y 6=-Y 2=+Z 5=-Z
        /// 3=+X 4=-X (opposite faces sum to 7, like a real die).</summary>
        private static readonly Vector3[] FaceDirs =
        {
            Vector3.zero,      // unused (faces are 1-based)
            Vector3.up,        // 1
            Vector3.forward,   // 2
            Vector3.right,     // 3
            Vector3.left,      // 4
            Vector3.back,      // 5
            Vector3.down,      // 6
        };

        private enum Phase { Idle, Tumbling, Correcting, Settled }

        private Rigidbody _rb;
        private Phase _phase = Phase.Idle;
        private int _targetValue;
        private float _rollStartTime;
        private float _correctT;
        private Quaternion _correctFrom, _correctTo;
        private float _restY;
        private float _slowSeconds;

        public int Index { get; private set; }
        public bool Settled => _phase == Phase.Settled || _phase == Phase.Idle;

        public void Init(int index, float restY)
        {
            Index = index;
            _rb = GetComponent<Rigidbody>();
            _restY = restY;
        }

        /// <summary>The face currently pointing up (for tests and sanity checks).</summary>
        public int UpFace()
        {
            int best = 1;
            float bestDot = float.MinValue;
            for (int face = 1; face <= 6; face++)
            {
                float dot = Vector3.Dot(transform.rotation * FaceDirs[face], Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = face;
                }
            }
            return best;
        }

        /// <summary>Rotation with <paramref name="value"/> up, at a random yaw.</summary>
        public static Quaternion RotationFor(int value, float yawDegrees) =>
            Quaternion.AngleAxis(yawDegrees, Vector3.up) * Quaternion.FromToRotation(FaceDirs[value], Vector3.up);

        /// <summary>Kinematic placement at an exact pose (kept dice, instant mode, snaps).</summary>
        public void PlaceAt(Vector3 position, int value, float yawDegrees)
        {
            _rb.isKinematic = true;
            transform.SetPositionAndRotation(
                new Vector3(position.x, _restY, position.z),
                RotationFor(value, yawDegrees));
            _phase = Phase.Idle;
            _targetValue = value;
        }

        /// <summary>Physics throw toward the engine-chosen value.</summary>
        public void LaunchRoll(int targetValue, Vector3 from, Vector3 velocity, Vector3 angularVelocity)
        {
            _targetValue = targetValue;
            _rb.isKinematic = false;
            transform.SetPositionAndRotation(from, Random.rotationUniform);
            _rb.velocity = velocity;
            _rb.angularVelocity = angularVelocity;
            _rollStartTime = Time.time;
            _slowSeconds = 0f;
            _phase = Phase.Tumbling;
        }

        /// <summary>Slide a settled die across the table, keeping its resting face and yaw. Used
        /// to unstack dice that came to rest on top of each other.</summary>
        public void SlideTo(float x, float z)
        {
            transform.position = new Vector3(x, _restY, z);
        }

        /// <summary>Skip: freeze in place with the correct face up immediately.</summary>
        public void SnapNow()
        {
            if (_phase != Phase.Tumbling && _phase != Phase.Correcting)
                return;
            _rb.isKinematic = true;
            var p = transform.position;
            transform.SetPositionAndRotation(
                new Vector3(p.x, _restY, p.z),
                ClosestTargetRotation());
            _phase = Phase.Settled;
        }

        private void FixedUpdate()
        {
            switch (_phase)
            {
                case Phase.Tumbling:
                    if (Time.time - _rollStartTime > WatchdogSeconds)
                    {
                        SnapNow(); // cocked against something / never calmed down
                        return;
                    }
                    bool slow = _rb.velocity.magnitude < SettleLinearSpeed
                                && _rb.angularVelocity.magnitude < SettleAngularSpeed;
                    _slowSeconds = slow ? _slowSeconds + Time.fixedDeltaTime : 0f;

                    // Normally we wait until the die is down on the table. A die that comes to
                    // rest ON another die never gets there, so it used to sit stacked until the
                    // 2.5 s watchdog dropped it — straight into the die beneath it. Treat
                    // sustained stillness as settled too; DiceView3D then unstacks them.
                    if ((slow && transform.position.y < _restY + 0.05f) || _slowSeconds >= RestingHighSeconds)
                        BeginCorrection();
                    break;

                case Phase.Correcting:
                    _correctT += Time.fixedDeltaTime / CorrectionSeconds;
                    if (_correctT >= 1f)
                    {
                        var p = transform.position;
                        transform.SetPositionAndRotation(new Vector3(p.x, _restY, p.z), _correctTo);
                        _phase = Phase.Settled;
                    }
                    else
                    {
                        transform.rotation = Quaternion.Slerp(_correctFrom, _correctTo, _correctT);
                    }
                    break;
            }
        }

        private void BeginCorrection()
        {
            _rb.isKinematic = true; // hand the last tumble to the blend
            _correctFrom = transform.rotation;
            _correctTo = ClosestTargetRotation();
            _correctT = 0f;
            _phase = Phase.Correcting;
        }

        /// <summary>The target-face-up rotation nearest the die's current orientation: rotate
        /// the target face's current world direction up along the shortest arc, preserving
        /// yaw — so the correction reads as the natural end of the tumble.</summary>
        private Quaternion ClosestTargetRotation()
        {
            Vector3 targetFaceWorld = transform.rotation * FaceDirs[_targetValue];
            return Quaternion.FromToRotation(targetFaceWorld, Vector3.up) * transform.rotation;
        }
    }
}

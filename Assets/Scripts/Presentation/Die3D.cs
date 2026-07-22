using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>One physical die. The engine decides the value BEFORE the throw; physics is
    /// theater (TECH_PLAN §5.4). The die tumbles freely, and once it loses energy the last
    /// tumble is blended (~0.25 s) onto the engine value — landing by *continuing* the roll
    /// forward (<see cref="ForwardTargetRotation"/>) rather than snapping the short way, so it
    /// never flips backward as it settles. A watchdog hard-snaps anything still unsettled
    /// (cocked, off-oval) after 2.5 s.</summary>
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
        private Vector3 _correctAxis;
        private float _correctDegrees;
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
                        // Drive the rotation FORWARD along the tumble axis rather than slerping the
                        // short way — a forward roll past 180° would wrap and slerp backward. The
                        // exact target (with its tiny yaw snap) eases in as the roll finishes.
                        float e = Mathf.SmoothStep(0f, 1f, _correctT);
                        Quaternion rolled = Quaternion.AngleAxis(_correctDegrees * e, _correctAxis) * _correctFrom;
                        transform.rotation = Quaternion.Slerp(rolled, _correctTo, e);
                    }
                    break;
            }
        }

        private void BeginCorrection()
        {
            // Capture the spin BEFORE going kinematic (which zeroes it) — the correction follows
            // its direction so the die keeps tumbling the way it was, instead of flipping back.
            Vector3 spin = _rb.angularVelocity;
            _rb.isKinematic = true; // hand the last tumble to the blend
            _correctFrom = transform.rotation;
            ComputeSettle(transform.rotation, _targetValue, spin, out _correctTo, out _correctAxis, out _correctDegrees);
            _correctT = 0f;
            _phase = Phase.Correcting;
        }

        /// <summary>Instant snap to the engine face (skip, watchdog) — the short way, since an
        /// instant jump has no direction to read.</summary>
        private Quaternion ClosestTargetRotation() => SnapUp(transform.rotation, _targetValue);

        /// <summary>How to land the engine value by *continuing the roll forward*, not flipping the
        /// short way. Returns the exact resting rotation plus the forward roll (<paramref
        /// name="axis"/>, <paramref name="degrees"/>) to get there, so the correction can drive the
        /// die along that arc rather than slerping backward. Pure and static so the
        /// "never flips backward" property is unit-testable without a physics scene.
        ///
        /// Sweeps forward about the spin axis (from 0, so an already-landed die barely moves) for
        /// the first orientation with the target face up — the natural end of the tumble. Falls
        /// back to the shortest snap only when the spin is negligible or near-vertical, where there
        /// is no forward face-up to roll into and no tumble direction to preserve anyway.</summary>
        public static void ComputeSettle(Quaternion current, int targetValue, Vector3 spinWorld,
            out Quaternion target, out Vector3 axis, out float degrees)
        {
            if (spinWorld.sqrMagnitude >= 0.04f) // ~0.2 rad/s: worth continuing the tumble
            {
                // Tip the target face up by rolling about a HORIZONTAL axis perpendicular to that
                // face's normal — the axis a real die pivots on as it tips onto a face. Being ⊥ the
                // normal *and* ⊥ up guarantees the face sweeps through straight-up (so the engine
                // value is always reachable this way), and being horizontal means the sweep
                // actually passes through up rather than tracing a tilted cone that misses it.
                Vector3 targetNormal = current * FaceDirs[targetValue];
                Vector3 pivot = Vector3.Cross(targetNormal, Vector3.up); // ⊥ normal, ⊥ up
                if (pivot.sqrMagnitude < 0.02f)                          // face already vertical…
                    pivot = new Vector3(spinWorld.x, 0f, spinWorld.z);   // …tip about the spin's ground track
                if (pivot.sqrMagnitude < 0.02f)                          // spin is vertical too
                    pivot = Vector3.forward;                             // any horizontal axis will do

                axis = pivot.normalized;
                // Sign it to the tumble so the roll goes the way the die is already turning.
                if (Vector3.Dot(axis, spinWorld) < 0f)
                    axis = -axis;

                for (float deg = 0f; deg <= 358f; deg += 2f)
                {
                    Quaternion rolled = Quaternion.AngleAxis(deg, axis) * current;
                    // Target face within ~11° of straight up: the resting point of this tip.
                    if (Vector3.Dot(rolled * FaceDirs[targetValue], Vector3.up) > 0.98f)
                    {
                        target = SnapUp(rolled, targetValue);
                        degrees = deg;
                        return;
                    }
                }
            }

            // Negligible spin: die is essentially stopped, so direction doesn't read — shortest snap.
            target = SnapUp(current, targetValue);
            (target * Quaternion.Inverse(current)).ToAngleAxis(out degrees, out axis);
        }

        /// <summary>Rotate <paramref name="from"/> the short way so <paramref name="value"/>'s face
        /// is exactly up, preserving yaw — removes the residual tilt left after a forward roll.</summary>
        private static Quaternion SnapUp(Quaternion from, int value) =>
            Quaternion.FromToRotation(from * FaceDirs[value], Vector3.up) * from;
    }
}

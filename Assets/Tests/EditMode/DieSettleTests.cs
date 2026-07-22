using NUnit.Framework;
using UnityEngine;
using Yahtzee.Presentation;

namespace Yahtzee.Tests
{
    /// <summary>The guided-settle geometry (Die3D.ForwardTargetRotation). The user-visible bug was
    /// a die flipping *backwards* as it landed, because the old correction took the shortest arc to
    /// the engine face regardless of which way the die was tumbling. The fix continues the roll
    /// forward; these lock that in without needing a physics scene.</summary>
    public class DieSettleTests
    {
        /// <summary>Local up-direction of each face at identity (mirrors Die3D.FaceDirs).</summary>
        private static readonly Vector3[] FaceDirs =
        {
            Vector3.zero, Vector3.up, Vector3.forward, Vector3.right, Vector3.left, Vector3.back, Vector3.down,
        };

        private static int UpFace(Quaternion rotation)
        {
            int best = 1;
            float bestDot = float.MinValue;
            for (int face = 1; face <= 6; face++)
            {
                float dot = Vector3.Dot(rotation * FaceDirs[face], Vector3.up);
                if (dot > bestDot) { bestDot = dot; best = face; }
            }
            return best;
        }

        [Test]
        public void Result_AlwaysRestsOnTheEngineValue([Values(1, 2, 3, 4, 5, 6)] int target)
        {
            var rng = new System.Random(target * 17 + 3);
            for (int i = 0; i < 200; i++)
            {
                Die3D.ComputeSettle(RandomRotation(rng), target, RandomSpin(rng),
                    out Quaternion settled, out _, out _);
                Assert.AreEqual(target, UpFace(settled),
                    $"target {target}, iter {i}: die settled on {UpFace(settled)}, not the engine value");
            }
        }

        /// <summary>The roll that lands the die must be applied to it exactly: rolling
        /// <c>current</c> forward by <c>degrees</c> about <c>axis</c> lands the target face up.
        /// This is what the correction phase drives, so it has to be self-consistent.</summary>
        [Test]
        public void RollByAxisAndDegrees_LandsOnTarget([Values(1, 2, 3, 4, 5, 6)] int target)
        {
            var rng = new System.Random(target * 23 + 5);
            for (int i = 0; i < 200; i++)
            {
                var current = RandomRotation(rng);
                Die3D.ComputeSettle(current, target, RandomSpin(rng),
                    out Quaternion settled, out Vector3 axis, out float degrees);
                var rolled = Quaternion.AngleAxis(degrees, axis) * current;
                Assert.Less(Quaternion.Angle(rolled, settled), 12f,
                    $"target {target}, iter {i}: the driven roll ({degrees:F0}° about {axis}) misses the settle pose");
            }
        }

        /// <summary>The whole point: for any real tumble, the settle rolls with the spin, never
        /// against it. Projecting the spin onto the plane ⊥ the target normal guarantees the roll
        /// axis stays in the forward half — so no reachability filter is needed, only excluding the
        /// degenerate "spinning like a top on the target face" case that can't roll at all.</summary>
        [Test]
        public void Settle_ContinuesTheRoll_NeverFlipsBackward([Values(1, 2, 3, 4, 5, 6)] int target)
        {
            var rng = new System.Random(target * 31 + 7);
            int decisive = 0;
            for (int i = 0; i < 600; i++)
            {
                var current = RandomRotation(rng);
                var spin = RandomSpin(rng);
                if (spin.magnitude < 1.5f)
                    continue; // a clear tumble

                // Skip only the degenerate case: spin essentially along the target normal, where
                // there is nothing to project and a snap is unavoidable.
                Vector3 targetNormal = current * FaceDirs[target];
                if (Mathf.Abs(Vector3.Dot(targetNormal.normalized, spin.normalized)) > 0.95f)
                    continue;

                Die3D.ComputeSettle(current, target, spin, out _, out Vector3 axis, out float degrees);
                if (degrees < 3f)
                    continue; // already there: the tiny snap has no meaningful direction

                decisive++;
                float alignment = Vector3.Dot(axis.normalized, spin.normalized);
                Assert.GreaterOrEqual(alignment, -0.01f,
                    $"target {target}, iter {i}: settle rolls about {axis} against the spin {spin} — a backward flip");
            }
            Assert.Greater(decisive, 200, "not enough decisive cases exercised");
        }

        [Test]
        public void NegligibleSpin_FallsBackToNearestFace()
        {
            Die3D.ComputeSettle(Quaternion.identity, 3, Vector3.zero, out Quaternion settled, out _, out _);
            Assert.AreEqual(3, UpFace(settled));
        }

        private static Quaternion RandomRotation(System.Random rng) =>
            Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);

        private static Vector3 RandomSpin(System.Random rng) => new Vector3(
            (float)rng.NextDouble() * 8f - 4f, (float)rng.NextDouble() * 8f - 4f, (float)rng.NextDouble() * 8f - 4f);
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Yahtzee.Presentation;

namespace Yahtzee.Tests
{
    /// <summary>TECH_PLAN §5.4 / risk table: the worst possible bug is a die whose rest face
    /// disagrees with GameState. This soak throws the physics dice 1,000 times (time-scaled)
    /// and asserts every die settles on exactly the engine-chosen face, inside the fence.</summary>
    public class DiceSoakTests
    {
        private GameObject _cameraGo;
        private KitchenBuilder.Refs _kitchen;

        [SetUp]
        public void SetUp()
        {
            GameController.AnimationsEnabled = true; // real physics path
            Time.timeScale = 10f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameController.AnimationsEnabled = true;
            if (_cameraGo != null)
                Object.Destroy(_cameraGo);
            if (_kitchen != null)
                Object.Destroy(_kitchen.Dice.gameObject); // the kitchen root
            _kitchen = null;
        }

        private KitchenBuilder.Refs BuildKitchen()
        {
            _cameraGo = new GameObject("SoakCamera", typeof(Camera));
            _kitchen = KitchenBuilder.Build(null, null, _cameraGo.GetComponent<Camera>());
            return _kitchen;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator GuidedDice_1000Rolls_AlwaysRestOnEngineValues()
        {
            var kitchen = BuildKitchen();
            var rng = new System.Random(20260720);

            for (int roll = 0; roll < 1000; roll++)
            {
                var values = new int[5];
                for (int i = 0; i < 5; i++)
                    values[i] = rng.Next(1, 7);

                bool settled = false;
                kitchen.Dice.PlayRoll(values, new bool[5], () => settled = true);

                float deadline = Time.realtimeSinceStartup + 15f;
                while (!settled)
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline, $"roll {roll}: dice never settled");
                    yield return null;
                }

                for (int i = 0; i < 5; i++)
                {
                    var die = kitchen.Dice.Dice[i];
                    Assert.AreEqual(values[i], die.UpFace(),
                        $"roll {roll} die {i}: rest face disagrees with engine value");
                    var p = die.transform.position;
                    Assert.IsTrue(
                        Mathf.Abs(p.x) < 0.60f && p.z > -0.45f && p.z < 0.55f && p.y > -0.05f && p.y < 0.25f,
                        $"roll {roll} die {i}: escaped the fence at {p}");
                }
            }
        }

        [UnityTest]
        public IEnumerator Skip_MidTumble_SnapsToEngineValues()
        {
            var kitchen = BuildKitchen();
            var rng = new System.Random(99);

            for (int roll = 0; roll < 25; roll++)
            {
                var values = new int[5];
                for (int i = 0; i < 5; i++)
                    values[i] = rng.Next(1, 7);

                bool settled = false;
                kitchen.Dice.PlayRoll(values, new bool[5], () => settled = true);
                // A couple of frames of real tumbling, then skip mid-flight.
                yield return null;
                yield return null;
                kitchen.Dice.SkipAnimation();
                Assert.IsTrue(settled, $"roll {roll}: skip did not settle immediately");
                for (int i = 0; i < 5; i++)
                    Assert.AreEqual(values[i], kitchen.Dice.Dice[i].UpFace(), $"roll {roll} die {i} after skip");
            }
        }
    }
}

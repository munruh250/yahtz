using UnityEngine;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>Gray-box kitchen for M4 groundwork: table plane, invisible collider fence
    /// around the roll oval, warm lamp light, five physics dice, and the camera framings.
    /// Real low-poly art replaces the visuals later; all gameplay-relevant transforms
    /// (slots, cup, framings) live here so the art swap doesn't touch logic.</summary>
    public static class KitchenBuilder
    {
        public sealed class Refs
        {
            public DiceView3D Dice;
            public CameraDirector CameraDirector;
            /// <summary>The diegetic card on the table — the interactive scorecard in 3D mode.</summary>
            public ScorecardView Scorecard;
            /// <summary>Null until the Oma FBX assets have been imported (setup tool).</summary>
            public OmaView Oma;
        }

        private const float DieSize = 0.09f;
        private const float TableY = 0f;

        // ---- Play zones -------------------------------------------------------
        // Rolled dice are penned into a compact patch in the middle of the table: values stay
        // easy to track, and nothing ever wanders behind the scorecard. Kept dice line up in a
        // row in front of it. Both zones sit clear of the card, which is propped 24 degrees and
        // hides anything on the table behind roughly z = -0.245 from the seated framings.

        /// <summary>Inner faces of the fence — the patch rolled dice must come to rest in.</summary>
        public const float RollZoneHalfX = 0.31f;
        public const float RollZoneMinZ = 0.10f;
        public const float RollZoneMaxZ = 0.38f;
        /// <summary>Kept dice line up here, between the roll zone and the card. The card's raised
        /// top edge hides table level behind about z = -0.19 at the tightest framing, so the row
        /// sits well forward of that — the gold pads are wider than the dice and clip first.</summary>
        public const float KeepRowZ = -0.05f;
        private const float SlotSpacingX = 0.12f;
        private static float RollZoneCenterZ => (RollZoneMinZ + RollZoneMaxZ) / 2f;

        // Dice pour in from beside the (visual) cup, but inside the fence — outside it they
        // would land on the wrong side of a wall and never reach the table.
        private static readonly Vector3 CupPosition = new Vector3(0.26f, 0.30f, 0.27f);

        public static Refs Build(Transform parent, GameController controller, Camera camera)
        {
            var root = new GameObject("Kitchen3D").transform;
            root.SetParent(parent, false);

            BuildTable(root);
            BuildFence(root);
            BuildLamp(root);
            BuildProps(root);
            var oma = BuildOma(root);
            var dice = BuildDice(root, controller, camera);
            var scorecard = ScorecardBuilder.BuildWorld(root, controller, camera);

            var director = root.gameObject.AddComponent<CameraDirector>();
            director.Init(camera, new[]
            {
                // pos, lookAt, fov — portrait framings tuned against the concept mockup via the
                // FramingCapture renders. Default and ScorecardFocus are the two the player
                // scores from, so both frame the whole card (see WorldScorecardTests); DiceFocus
                // is the transient push-in during a roll and is free to crop it.
                // Default is the scoring-capable framing (see GameController.OnDiceSettled), so
                // it sits back far enough to hold Oma, the dice and the whole card at once.
                (new Vector3(0f, 1.45f, -1.60f), new Vector3(0f, -0.02f, 0.25f), 58f), // Default
                (new Vector3(0f, 1.00f, -0.95f), new Vector3(0f, -0.12f, 0.06f), 56f), // DiceFocus
                // ScorecardFocus sits back and aims high so the diegetic card lands in the
                // bottom third, fully inside the frustum and clear of the action bar.
                (new Vector3(0f, 1.22f, -1.42f), new Vector3(0f, 0.02f, -0.20f), 52f), // ScorecardFocus
                (new Vector3(0f, 0.92f, -0.75f), new Vector3(0f, 0.40f, 0.85f), 50f),  // OmaFocus
            });

            return new Refs { Dice = dice, CameraDirector = director, Scorecard = scorecard, Oma = oma };
        }

        /// <summary>Set dressing per the concept mockup: black dice cup (right), game box
        /// (left, outside the fence), Oma's mug and face-down scorecard on her side.</summary>
        private static void BuildProps(Transform root)
        {
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cup.name = "DiceCup";
            cup.transform.SetParent(root, false);
            cup.transform.localPosition = new Vector3(0.46f, TableY + 0.075f, 0.30f);
            cup.transform.localScale = new Vector3(0.14f, 0.075f, 0.14f);
            cup.GetComponent<Renderer>().material = Mat(new Color(0.12f, 0.11f, 0.11f));
            RemoveCollider(cup); // sits outside the fence at x=0.46; dice can never reach it

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "GameBox";
            box.transform.SetParent(root, false);
            box.transform.localPosition = new Vector3(-0.66f, TableY + 0.05f, 0.32f);
            box.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);
            box.transform.localScale = new Vector3(0.34f, 0.10f, 0.24f);
            box.GetComponent<Renderer>().material = Mat(new Color(0.55f, 0.14f, 0.12f));
            RemoveCollider(box); // outside the fence, decoration

            var mug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mug.name = "OmaMug";
            mug.transform.SetParent(root, false);
            mug.transform.localPosition = new Vector3(-0.30f, TableY + 0.055f, 0.62f);
            mug.transform.localScale = new Vector3(0.10f, 0.055f, 0.10f);
            mug.GetComponent<Renderer>().material = Mat(new Color(0.92f, 0.90f, 0.85f));
            RemoveCollider(mug);

            var omaCard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            omaCard.name = "OmaScorecardProp";
            omaCard.transform.SetParent(root, false);
            omaCard.transform.localPosition = new Vector3(0.02f, TableY + 0.003f, 0.60f);
            omaCard.transform.localRotation = Quaternion.Euler(0f, -6f, 0f);
            omaCard.transform.localScale = new Vector3(0.16f, 0.004f, 0.24f);
            omaCard.GetComponent<Renderer>().material = Mat(new Color(0.93f, 0.89f, 0.78f));
            RemoveCollider(omaCard);

            var pencil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pencil.name = "OmaPencil";
            pencil.transform.SetParent(root, false);
            pencil.transform.localPosition = new Vector3(0.14f, TableY + 0.008f, 0.58f);
            pencil.transform.localRotation = Quaternion.Euler(90f, 15f, 0f);
            pencil.transform.localScale = new Vector3(0.012f, 0.07f, 0.012f);
            pencil.GetComponent<Renderer>().material = Mat(new Color(0.85f, 0.65f, 0.15f));
            RemoveCollider(pencil);
        }

        /// <summary>Instantiates the animated Oma character seated across the table (concept
        /// mockup: grey bun, glasses, cardigan — for now the raw imported FBX character).
        /// Returns null gracefully if the assets haven't been imported/setup yet.</summary>
        private static OmaView BuildOma(Transform root)
        {
            var prefab = Resources.Load<GameObject>("Oma/Sitting Idle");
            if (prefab == null)
                return null;

            var oma = Object.Instantiate(prefab, root);
            oma.name = "Oma";
            // Seated across the table, facing the player, under the lamp pool.
            oma.transform.localPosition = new Vector3(0f, TableY - 0.75f, 1.05f); // feet on floor, table height ~0.75
            oma.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Placeholder look: the Mixamo body ships untextured; a soft cardigan-purple
            // tint keeps her reading as "Oma" until the real low-poly model lands in M5.
            var cardigan = Mat(new Color(0.52f, 0.40f, 0.58f));
            foreach (var renderer in oma.GetComponentsInChildren<Renderer>())
                renderer.material = cardigan;

            var animator = oma.GetComponent<Animator>();
            if (animator == null)
                animator = oma.AddComponent<Animator>();
            var controller = Resources.Load<RuntimeAnimatorController>("Oma/OmaAnimator");
            if (controller != null)
                animator.runtimeAnimatorController = controller;

            var view = oma.AddComponent<OmaView>();
            view.Init(animator);
            return view;
        }

        private static void BuildTable(Transform root)
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.SetParent(root, false);
            // Deepened toward the player (near edge -0.65 → -0.85) to seat the diegetic
            // scorecard in front of the dice fence. Oma's far edge is unchanged.
            table.transform.localPosition = new Vector3(0f, TableY - 0.05f, 0f);
            table.transform.localScale = new Vector3(1.7f, 0.1f, 1.7f);
            table.GetComponent<Renderer>().material = Mat(new Color(0.42f, 0.28f, 0.16f)); // warm wood
            table.GetComponent<BoxCollider>().material = DiePhysicMaterial();

        }

        private static void BuildFence(Transform root)
        {
            // Invisible walls + ceiling pen the dice into the roll zone (TECH_PLAN §5.4). Walls
            // are centred half a thickness outside the zone so their inner faces are the bounds.
            const float t = 0.02f;
            float width = RollZoneHalfX * 2f + t;
            float depth = RollZoneMaxZ - RollZoneMinZ;
            AddWall(root, "FenceLeft", new Vector3(-RollZoneHalfX - t / 2f, 0.15f, RollZoneCenterZ), new Vector3(t, 0.5f, depth));
            AddWall(root, "FenceRight", new Vector3(RollZoneHalfX + t / 2f, 0.15f, RollZoneCenterZ), new Vector3(t, 0.5f, depth));
            AddWall(root, "FenceFar", new Vector3(0f, 0.15f, RollZoneMaxZ + t / 2f), new Vector3(width, 0.5f, t));
            AddWall(root, "FenceNear", new Vector3(0f, 0.15f, RollZoneMinZ - t / 2f), new Vector3(width, 0.5f, t));
            AddWall(root, "FenceCeiling", new Vector3(0f, 0.42f, RollZoneCenterZ), new Vector3(width, t, depth));
        }

        private static void AddWall(Transform root, string name, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(name, typeof(BoxCollider));
            wall.transform.SetParent(root, false);
            wall.transform.localPosition = center;
            var box = wall.GetComponent<BoxCollider>();
            box.size = size;
            box.material = DiePhysicMaterial();
        }

        private static void BuildLamp(Transform root)
        {
            var lamp = new GameObject("LampLight", typeof(Light));
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, 1.35f, 0.1f);
            var light = lamp.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.62f);
            light.intensity = 1.5f;
            light.range = 4f;

            // Soft warm fill from the player's side so Oma's face isn't in shadow under the
            // overhead lamp (the concept art reads front-lit).
            var fill = new GameObject("FillLight", typeof(Light));
            fill.transform.SetParent(root, false);
            fill.transform.localRotation = Quaternion.Euler(18f, 0f, 0f); // shining toward Oma
            var fillLight = fill.GetComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(1f, 0.88f, 0.72f);
            fillLight.intensity = 0.35f;
        }

        private static DiceView3D BuildDice(Transform root, GameController controller, Camera camera)
        {
            var view = root.gameObject.AddComponent<DiceView3D>();
            var dice = new Die3D[DiceState.DieCount];
            var restSlots = new Vector3[DiceState.DieCount];
            var keepSlots = new Vector3[DiceState.DieCount];
            var keepMarkers = new Transform[DiceState.DieCount];
            var faceMat = Mat(UiPalette.DieFace);

            for (int i = 0; i < DiceState.DieCount; i++)
            {
                float x = (i - 2) * SlotSpacingX;
                restSlots[i] = new Vector3(x, TableY + DieSize / 2f, RollZoneCenterZ);
                keepSlots[i] = new Vector3(x, TableY + DieSize / 2f, KeepRowZ);
                keepMarkers[i] = BuildKeepMarker(root, i, keepSlots[i]);

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Die{i}";
                go.transform.SetParent(root, false);
                go.transform.localScale = Vector3.one * DieSize;
                go.GetComponent<Renderer>().material = faceMat;
                go.GetComponent<BoxCollider>().material = DiePhysicMaterial();

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                // Speculative CCD: fast enough for small dice and legal on kinematic bodies
                // (dice toggle kinematic constantly for keeps/guided settling).
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;

                var die = go.AddComponent<Die3D>();
                die.Init(i, TableY + DieSize / 2f);
                AddPips(go.transform);
                go.SetActive(false); // hidden until first roll
                dice[i] = die;
            }

            view.Init(dice, camera, controller, CupPosition, restSlots, keepSlots, keepMarkers);
            return view;
        }

        /// <summary>Gold pad a kept die sits on. Design §5.5 forbids colour-only signalling, so
        /// "kept" reads twice over: the die moves to the keep row AND lands on a marked spot.</summary>
        private static Transform BuildKeepMarker(Transform root, int index, Vector3 slot)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"KeepMarker{index}";
            marker.transform.SetParent(root, false);
            // Wide enough to read past the die's corners (its half-diagonal is ~0.064).
            marker.transform.localPosition = new Vector3(slot.x, TableY + 0.002f, slot.z);
            marker.transform.localScale = new Vector3(0.17f, 0.002f, 0.17f);
            marker.GetComponent<Renderer>().material = Mat(UiPalette.Gold);
            Object.Destroy(marker.GetComponent<Collider>()); // never blocks a die tap
            marker.SetActive(false);
            return marker.transform;
        }

        /// <summary>Quad pips on each face so values read at a glance even gray-boxed.</summary>
        private static void AddPips(Transform die)
        {
            var pipMat = Mat(UiPalette.DiePip);
            // face value → (local normal, up-axis on that face)
            (int value, Vector3 normal, Vector3 up)[] faces =
            {
                (1, Vector3.up, Vector3.forward),
                (6, Vector3.down, Vector3.forward),
                (2, Vector3.forward, Vector3.up),
                (5, Vector3.back, Vector3.up),
                (3, Vector3.right, Vector3.up),
                (4, Vector3.left, Vector3.up),
            };
            // pip layouts on a unit face (x, y in [-0.3, 0.3])
            Vector2[][] layouts =
            {
                null,
                new[] { Vector2.zero },
                new[] { new Vector2(-0.22f, 0.22f), new Vector2(0.22f, -0.22f) },
                new[] { new Vector2(-0.25f, 0.25f), Vector2.zero, new Vector2(0.25f, -0.25f) },
                new[] { new Vector2(-0.22f, 0.22f), new Vector2(0.22f, 0.22f), new Vector2(-0.22f, -0.22f), new Vector2(0.22f, -0.22f) },
                new[] { new Vector2(-0.24f, 0.24f), new Vector2(0.24f, 0.24f), Vector2.zero, new Vector2(-0.24f, -0.24f), new Vector2(0.24f, -0.24f) },
                new[] { new Vector2(-0.22f, 0.26f), new Vector2(0.22f, 0.26f), new Vector2(-0.22f, 0f), new Vector2(0.22f, 0f), new Vector2(-0.22f, -0.26f), new Vector2(0.22f, -0.26f) },
            };

            foreach (var (value, normal, up) in faces)
            {
                foreach (var p in layouts[value])
                {
                    // Slightly protruding flat cubes: readable from any angle, no quad-normal
                    // pitfalls, and irrelevant to physics (collider removed, sits inside the
                    // die's own box collider).
                    var pip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pip.name = $"Pip{value}";
                    Object.Destroy(pip.GetComponent<Collider>());
                    pip.GetComponent<Renderer>().material = pipMat;
                    pip.transform.SetParent(die, false);
                    var right = Vector3.Cross(up, normal);
                    pip.transform.localPosition = normal * 0.485f + right * p.x + up * p.y;
                    pip.transform.localRotation = Quaternion.LookRotation(normal, up);
                    pip.transform.localScale = new Vector3(0.16f, 0.16f, 0.05f);
                }
            }
        }

        /// <summary>Drops a primitive's collider. Props are decoration — dice are penned inside
        /// the fence and can never touch them.
        ///
        /// Null-checked because a primitive's collider is NOT guaranteed to exist in a player
        /// build. With `stripEngineCode` on (the Android default), IL2CPP strips collider types
        /// the game never names, and nothing here names CapsuleCollider — the one Unity puts on a
        /// Cylinder. So on device CreatePrimitive(Cylinder) came back with no collider, and
        /// dereferencing it threw inside BuildProps, leaving the entire 3D scene, dice and
        /// scorecard unbuilt while the screen-space UI still drew. The editor never reproduces
        /// this: nothing is stripped there. Diagnosed off `adb logcat` (see HANDOFF §2).</summary>
        private static void RemoveCollider(GameObject prop)
        {
            var collider = prop.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
        }

        private static Material Mat(Color color)
        {
            var mat = new Material(Shader.Find("Standard")) { color = color };
            return mat;
        }

        private static PhysicMaterial _diePhysMat;

        private static PhysicMaterial DiePhysicMaterial()
        {
            if (_diePhysMat == null)
                _diePhysMat = new PhysicMaterial("Dice")
                {
                    bounciness = 0.18f,
                    dynamicFriction = 0.55f,
                    staticFriction = 0.55f,
                    bounceCombine = PhysicMaterialCombine.Average,
                };
            return _diePhysMat;
        }
    }
}

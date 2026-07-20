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
            /// <summary>Null until the Oma FBX assets have been imported (setup tool).</summary>
            public OmaView Oma;
        }

        private const float DieSize = 0.09f;
        private const float TableY = 0f;
        // Dice pour in from beside the (visual) cup on the right of the oval.
        private static readonly Vector3 CupPosition = new Vector3(0.30f, 0.30f, 0.24f);

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

            var director = root.gameObject.AddComponent<CameraDirector>();
            director.Init(camera, new[]
            {
                // pos, lookAt, fov — portrait framings tuned against the concept mockup via
                // the FramingCapture renders: Default sees Oma (top) + dice (mid) + near
                // table edge; DiceFocus/ScorecardFocus are subtle push/tilt variants.
                (new Vector3(0f, 1.25f, -1.20f), new Vector3(0f, -0.10f, 0.30f), 58f), // Default
                (new Vector3(0f, 1.00f, -0.95f), new Vector3(0f, -0.12f, 0.06f), 56f), // DiceFocus
                (new Vector3(0f, 1.05f, -1.05f), new Vector3(0f, -0.10f, 0.0f), 52f),  // ScorecardFocus
                (new Vector3(0f, 0.92f, -0.75f), new Vector3(0f, 0.40f, 0.85f), 50f),  // OmaFocus
            });

            return new Refs { Dice = dice, CameraDirector = director, Oma = oma };
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
            cup.GetComponent<Collider>().material = DiePhysicMaterial(); // dice may clip it

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "GameBox";
            box.transform.SetParent(root, false);
            box.transform.localPosition = new Vector3(-0.66f, TableY + 0.05f, 0.32f);
            box.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);
            box.transform.localScale = new Vector3(0.34f, 0.10f, 0.24f);
            box.GetComponent<Renderer>().material = Mat(new Color(0.55f, 0.14f, 0.12f));
            Object.Destroy(box.GetComponent<Collider>()); // outside the fence, decoration

            var mug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mug.name = "OmaMug";
            mug.transform.SetParent(root, false);
            mug.transform.localPosition = new Vector3(-0.30f, TableY + 0.055f, 0.62f);
            mug.transform.localScale = new Vector3(0.10f, 0.055f, 0.10f);
            mug.GetComponent<Renderer>().material = Mat(new Color(0.92f, 0.90f, 0.85f));
            Object.Destroy(mug.GetComponent<Collider>());

            var omaCard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            omaCard.name = "OmaScorecardProp";
            omaCard.transform.SetParent(root, false);
            omaCard.transform.localPosition = new Vector3(0.02f, TableY + 0.003f, 0.60f);
            omaCard.transform.localRotation = Quaternion.Euler(0f, -6f, 0f);
            omaCard.transform.localScale = new Vector3(0.16f, 0.004f, 0.24f);
            omaCard.GetComponent<Renderer>().material = Mat(new Color(0.93f, 0.89f, 0.78f));
            Object.Destroy(omaCard.GetComponent<Collider>());

            var pencil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pencil.name = "OmaPencil";
            pencil.transform.SetParent(root, false);
            pencil.transform.localPosition = new Vector3(0.14f, TableY + 0.008f, 0.58f);
            pencil.transform.localRotation = Quaternion.Euler(90f, 15f, 0f);
            pencil.transform.localScale = new Vector3(0.012f, 0.07f, 0.012f);
            pencil.GetComponent<Renderer>().material = Mat(new Color(0.85f, 0.65f, 0.15f));
            Object.Destroy(pencil.GetComponent<Collider>());
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
            table.transform.localPosition = new Vector3(0f, TableY - 0.05f, 0.1f);
            table.transform.localScale = new Vector3(1.7f, 0.1f, 1.5f);
            table.GetComponent<Renderer>().material = Mat(new Color(0.42f, 0.28f, 0.16f)); // warm wood
            table.GetComponent<BoxCollider>().material = DiePhysicMaterial();

        }

        private static void BuildFence(Transform root)
        {
            // Invisible walls + ceiling keep dice inside the playable oval (TECH_PLAN §5.4).
            AddWall(root, "FenceLeft", new Vector3(-0.55f, 0.15f, 0.05f), new Vector3(0.02f, 0.5f, 0.85f));
            AddWall(root, "FenceRight", new Vector3(0.55f, 0.15f, 0.05f), new Vector3(0.02f, 0.5f, 0.85f));
            AddWall(root, "FenceFar", new Vector3(0f, 0.15f, 0.50f), new Vector3(1.15f, 0.5f, 0.02f));
            AddWall(root, "FenceNear", new Vector3(0f, 0.15f, -0.38f), new Vector3(1.15f, 0.5f, 0.02f));
            AddWall(root, "FenceCeiling", new Vector3(0f, 0.42f, 0.05f), new Vector3(1.15f, 0.02f, 0.9f));
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
            var faceMat = Mat(UiPalette.DieFace);

            for (int i = 0; i < DiceState.DieCount; i++)
            {
                float x = (i - 2) * 0.14f;
                restSlots[i] = new Vector3(x, TableY + DieSize / 2f, 0.10f);
                keepSlots[i] = new Vector3(x, TableY + DieSize / 2f, -0.30f);

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

            view.Init(dice, camera, controller, CupPosition, restSlots, keepSlots);
            return view;
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

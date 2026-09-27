using UnityEngine;
using UnityEditor;
using Game.World;

namespace Game.EditorTools
{
    /// <summary>
    /// '천명 성채' 화이트박스(Greybox) 자동 생성기.
    /// 메뉴: Tools > 천명 성채 > 화이트박스 생성
    ///
    /// 레벨 디자인 가이드의 POI 좌표를 그대로 사용한다 (Unity 좌표, +Z = 북쪽).
    ///   남문(정문) (0,0,-250) · 북문 (0,0,250) · 동문 (200,0,0) · 서문 (-200,0,0)
    ///   중앙 광장 (0,0,0) · 객주소 (-80,0,-150) · 대장간 (100,0,-100) · 영혼의 제단 (0,50,200)
    /// 모든 좌표는 1m 그리드에 스냅된다. 박스들은 추후 실제 모델(Meshy 등)로 교체하면 된다.
    /// </summary>
    public static class CitadelWhiteboxBuilder
    {
        const float Half_X = 200f, Half_Z = 250f, WallH = 18f, WallT = 4f, GateW = 16f;
        const float AltarH = 50f, StepH = 1f, StepD = 1.4f;

        static Material _ground, _road, _stone, _wood, _roof, _gold, _blue;

        [MenuItem("Tools/천명 성채/화이트박스 생성")]
        public static void Build()
        {
            MakeMaterials();
            var root = new GameObject("Citadel_CheonMyeong");
            Undo.RegisterCreatedObjectUndo(root, "Build Citadel Whitebox");

            var env = Group(root, "_Environment");
            var ground = Box(env, "Ground", new Vector3(0, -0.5f, 0), new Vector3(Half_X * 2 + 60, 1, Half_Z * 2 + 60), _ground);
            ground.isStatic = true;
            BuildWalls(Group(env, "Walls"));
            BuildRoads(Group(env, "Roads"));

            BuildGates(Group(root, "Gates"));
            BuildPlaza(Group(root, "District_Plaza", Vector3.zero));
            BuildInn(Group(root, "District_Inn", new Vector3(-80, 0, -150)));
            BuildBlacksmith(Group(root, "District_Blacksmith", new Vector3(100, 0, -100)));
            BuildSoulAltar(Group(root, "District_SoulAltar", new Vector3(0, 0, 200)));

            var sun = new GameObject("Sun (Directional)");
            sun.transform.SetParent(root.transform);
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional; l.color = new Color(1f, 0.93f, 0.82f); l.intensity = 1.1f; l.shadows = LightShadows.Soft;

            Selection.activeGameObject = root;
            Debug.Log("[천명 성채] 화이트박스 생성 완료 — 플레이 동선: 남문 → 광장 → 객주소/대장간 → 북쪽 계단 → 영혼의 제단");
        }

        // ---------------------------------------------------------------- districts

        static void BuildWalls(GameObject p)
        {
            // 각 변의 가운데에 성문 틈(GateW)을 남긴다
            WallWithGap(p, "Wall_South", new Vector3(0, 0, -Half_Z), Half_X * 2, true);
            WallWithGap(p, "Wall_North", new Vector3(0, 0, Half_Z), Half_X * 2, true);
            WallWithGap(p, "Wall_East", new Vector3(Half_X, 0, 0), Half_Z * 2, false);
            WallWithGap(p, "Wall_West", new Vector3(-Half_X, 0, 0), Half_Z * 2, false);
            foreach (var c in new[] { new Vector3(-Half_X, 0, -Half_Z), new Vector3(Half_X, 0, -Half_Z), new Vector3(-Half_X, 0, Half_Z), new Vector3(Half_X, 0, Half_Z) })
                Cyl(p, "Tower", c + Vector3.up * 14, new Vector3(14, 14, 14), _stone);
        }

        static void WallWithGap(GameObject p, string name, Vector3 center, float length, bool alongX)
        {
            float seg = (length - GateW) / 2f, off = GateW / 2f + seg / 2f;
            for (int s = -1; s <= 1; s += 2)
            {
                var pos = center + (alongX ? new Vector3(s * off, WallH / 2, 0) : new Vector3(0, WallH / 2, s * off));
                var size = alongX ? new Vector3(seg, WallH, WallT) : new Vector3(WallT, WallH, seg);
                Box(p, name + (s < 0 ? "_A" : "_B"), pos, size, _stone).isStatic = true;
            }
        }

        static void BuildRoads(GameObject p)
        {
            // 광장에서 네 성문으로 뻗는 주 도로 + POI로 가는 지선 (시선 유도)
            Box(p, "Road_NS", new Vector3(0, 0.02f, 0), new Vector3(14, 0.05f, Half_Z * 2), _road);
            Box(p, "Road_EW", new Vector3(0, 0.02f, 0), new Vector3(Half_X * 2, 0.05f, 14), _road);
            Path(p, "Road_ToInn", new Vector3(0, 0, -150), new Vector3(-80, 0, -150), 8);
            Path(p, "Road_ToSmith", new Vector3(100, 0, 0), new Vector3(100, 0, -100), 8);
        }

        static void BuildGates(GameObject p)
        {
            Gate(p, "Gate_South_Main", new Vector3(0, 0, -Half_Z), 0, "Plains", "FromCitadel", "정문(남문) · 바람노래 평원", true);
            Gate(p, "Gate_North", new Vector3(0, 0, Half_Z), 180, "Asura", "FromCitadel", "북문 · 아수라 흑야 협곡 Lv.70~99", false);
            Gate(p, "Gate_East", new Vector3(Half_X, 0, 0), 270, "Graveyard", "FromCitadel", "동문 · 원혼의 묘지 Lv.15~40", false);
            Gate(p, "Gate_West", new Vector3(-Half_X, 0, 0), 90, "Beasts", "FromCitadel", "서문 · 야수 서식지 Lv.1~15", false);
        }

        /// <param name="inwardYaw">성 안쪽을 바라보는 방향 (도착한 플레이어가 광장을 보도록)</param>
        static void Gate(GameObject p, string name, Vector3 pos, float inwardYaw, string scene, string spawnId, string label, bool isDefault)
        {
            var g = Group(p, name, pos);
            g.transform.rotation = Quaternion.Euler(0, inwardYaw, 0);
            // 문루(아치): 기둥 2개 + 상인방
            Box(g, "Pillar_L", new Vector3(-GateW / 2 - 2, 12, 0), new Vector3(4, 24, 6), _stone, true);
            Box(g, "Pillar_R", new Vector3(GateW / 2 + 2, 12, 0), new Vector3(4, 24, 6), _stone, true);
            Box(g, "Lintel", new Vector3(0, 22, 0), new Vector3(GateW + 8, 4, 6), _stone, true);

            var trig = Group(g, "Trigger", Vector3.zero, true);
            var bc = trig.AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.center = new Vector3(0, 3, -2); bc.size = new Vector3(GateW, 6, 3);
            var zg = trig.AddComponent<ZoneGate>();
            zg.targetScene = scene; zg.targetSpawnId = spawnId; zg.label = label;

            // 트리거 바로 안쪽의 도착 지점 (다른 씬에서 이 문으로 돌아올 때)
            var sp = Group(g, "SpawnPoint", new Vector3(0, 0, 10), true);
            var spc = sp.AddComponent<SpawnPoint>();
            spc.id = name; spc.isDefault = isDefault;
        }

        static void BuildPlaza(GameObject p)
        {
            Cyl(p, "Plaza_Floor", new Vector3(0, 0.05f, 0), new Vector3(60, 0.05f, 60), _road);
            // 랜드마크: 분수 + 기념비 (어디서든 보이는 방향 기준점)
            Cyl(p, "Fountain_Basin", new Vector3(0, 0.6f, 0), new Vector3(16, 0.6f, 16), _stone, true);
            Cyl(p, "Monument", new Vector3(0, 9, 0), new Vector3(3, 9, 3), _stone, true);
            Box(p, "Monument_Top", new Vector3(0, 19, 0), new Vector3(4, 2, 4), _gold, true);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Cyl(p, "Lamp_" + i, new Vector3(Mathf.Sin(a) * 26, 3, Mathf.Cos(a) * 26), new Vector3(0.6f, 3, 0.6f), _stone, true);
            }
        }

        static void BuildInn(GameObject p)
        {
            House(p, "Inn_Building", new Vector3(0, 0, 0), new Vector3(24, 10, 16), _wood);
            Group(p, "NPC_Innkeeper", new Vector3(0, 0, 10), true);
            Box(p, "Signboard", new Vector3(6, 6, 8.5f), new Vector3(3, 2, 0.3f), _gold, true);
            for (int i = 0; i < 3; i++) Cyl(p, "Barrel_" + i, new Vector3(-10 + i * 1.6f, 0.8f, 9.5f), new Vector3(1.2f, 0.8f, 1.2f), _wood, true);
            PointLight(p, "Lantern", new Vector3(0, 4, 9), new Color(1f, 0.75f, 0.45f), 1.2f, 14);
        }

        static void BuildBlacksmith(GameObject p)
        {
            House(p, "Smithy_Building", new Vector3(0, 0, 0), new Vector3(18, 9, 18), _wood);
            Box(p, "Forge", new Vector3(-6, 1, 12), new Vector3(4, 2, 4), _stone, true);
            Box(p, "Anvil", new Vector3(-1, 0.6f, 12), new Vector3(1.6f, 1.2f, 0.8f), _stone, true);
            Group(p, "NPC_Blacksmith", new Vector3(2, 0, 12), true);
            PointLight(p, "ForgeFire", new Vector3(-6, 2.5f, 12), new Color(1f, 0.45f, 0.15f), 2.2f, 16);
        }

        /// <summary>
        /// 영혼의 제단: 높이 50m 테라스 + 광장 방향(남쪽)으로 내려오는 긴 중앙 계단. 좌우 대칭, 청색/금색 조명.
        /// 계단 끝에서 올려다보는 구도가 '권위와 신성함'을 만든다.
        /// </summary>
        static void BuildSoulAltar(GameObject p)
        {
            const float topW = 60, topD = 40;
            Box(p, "Terrace", new Vector3(0, AltarH / 2, 0), new Vector3(topW, AltarH, topD), _stone, true);
            Box(p, "Terrace_Trim", new Vector3(0, AltarH + 0.15f, 0), new Vector3(topW + 1, 0.3f, topD + 1), _gold, true);

            int steps = Mathf.RoundToInt(AltarH / StepH);
            var stairs = Group(p, "Stairs", new Vector3(0, 0, -topD / 2), true);
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * StepH, z = -(steps - i) * StepD + StepD / 2;
                Box(stairs, "Step_" + i.ToString("00"), new Vector3(0, h / 2, z), new Vector3(14, h, StepD), _stone, true);
            }
            float runLen = steps * StepD;
            for (int s = -1; s <= 1; s += 2)   // 대칭 난간
                Box(stairs, s < 0 ? "Rail_L" : "Rail_R", new Vector3(s * 7.5f, AltarH / 2 + 0.6f, -runLen / 2), new Vector3(1, AltarH + 1.2f, runLen), _gold, true);

            var top = Group(p, "AltarTop", new Vector3(0, AltarH, 0), true);
            Cyl(top, "Altar_Dais", new Vector3(0, 0.5f, 4), new Vector3(10, 0.5f, 10), _blue, true);
            Box(top, "Altar_Stone", new Vector3(0, 1.8f, 4), new Vector3(4, 2.6f, 2), _gold, true);
            Group(top, "NPC_HighPriest", new Vector3(0, 0, 0), true);
            foreach (var x in new[] { -24f, -12f, 12f, 24f })
                foreach (var z in new[] { -16f, 16f })
                    Cyl(top, "Pillar", new Vector3(x, 7, z), new Vector3(2, 7, 2), _stone, true);
            PointLight(top, "SoulLight_Blue", new Vector3(0, 8, 4), new Color(0.4f, 0.6f, 1f), 3f, 40);
            PointLight(top, "GoldLight_L", new Vector3(-12, 5, -14), new Color(1f, 0.8f, 0.4f), 1.5f, 20);
            PointLight(top, "GoldLight_R", new Vector3(12, 5, -14), new Color(1f, 0.8f, 0.4f), 1.5f, 20);
        }

        // ---------------------------------------------------------------- helpers

        static void House(GameObject p, string name, Vector3 pos, Vector3 size, Material wall)
        {
            var h = Group(p, name, pos, true);
            Box(h, "Body", new Vector3(0, size.y / 2, 0), size, wall, true);
            Box(h, "Roof", new Vector3(0, size.y + size.y * 0.25f, 0), new Vector3(size.x * 0.8f, size.y * 0.5f, size.z * 0.8f), _roof, true);
            Box(h, "Door", new Vector3(0, 2, size.z / 2 + 0.05f), new Vector3(3, 4, 0.2f), _roof, true);
        }

        static void Path(GameObject p, string name, Vector3 a, Vector3 b, float width)
        {
            var mid = (a + b) / 2f; var d = b - a;
            var go = Box(p, name, new Vector3(mid.x, 0.03f, mid.z), new Vector3(width, 0.05f, d.magnitude + width), _road);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.z));
        }

        static GameObject Group(GameObject parent, string name, Vector3 localPos = default(Vector3), bool local = false)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent.transform, false);
            g.transform.localPosition = Snap(localPos);
            return g;
        }

        static GameObject Box(GameObject parent, string name, Vector3 pos, Vector3 size, Material m, bool local = true)
        {
            return Prim(PrimitiveType.Cube, parent, name, pos, size, m);
        }

        static GameObject Cyl(GameObject parent, string name, Vector3 pos, Vector3 size, Material m, bool local = true)
        {
            return Prim(PrimitiveType.Cylinder, parent, name, pos, size, m);
        }

        static GameObject Prim(PrimitiveType t, GameObject parent, string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.isStatic = true;
            return go;
        }

        static void PointLight(GameObject parent, string name, Vector3 pos, Color c, float intensity, float range)
        {
            var g = Group(parent, name, pos, true);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
        }

        static Vector3 Snap(Vector3 v)
        {
            return new Vector3(Mathf.Round(v.x), Mathf.Round(v.y), Mathf.Round(v.z));
        }

        static void MakeMaterials()
        {
            _ground = Mat("WB_Ground", new Color(0.36f, 0.42f, 0.32f));
            _road = Mat("WB_Road", new Color(0.55f, 0.56f, 0.6f));
            _stone = Mat("WB_Stone", new Color(0.7f, 0.72f, 0.78f));
            _wood = Mat("WB_Wood", new Color(0.55f, 0.36f, 0.22f));
            _roof = Mat("WB_Roof", new Color(0.35f, 0.18f, 0.14f));
            _gold = Mat("WB_Gold", new Color(0.85f, 0.68f, 0.3f));
            _blue = Mat("WB_SoulBlue", new Color(0.25f, 0.4f, 0.9f));
            _blue.EnableKeyword("_EMISSION");
            _blue.SetColor("_EmissionColor", new Color(0.2f, 0.35f, 1f) * 1.5f);
        }

        static Material Mat(string name, Color c)
        {
            // URP 프로젝트면 URP Lit, 아니면 Built-in Standard
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = name, color = c };
            return m;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Game.Ghosts;
using Game.Lighting;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Generates the custom 3D Pac-Man Ghost model with authentic dome head,
    /// flared spectral cloak, 4-pointed scalloped ruffle tentacles, sunken horror eyes,
    /// tracking pupils, and horrified wavy zigzag mouth.
    /// Accessible via Game > Ghosts > Overhaul 3D Horror Ghost Visuals
    /// </summary>
    public static class GhostMeshGenerator
    {
        const string ModelsFolder = "Assets/_Project/Art/Models";
        const string MaterialsFolder = "Assets/_Project/Art/Materials";
        const string PrefabPath = "Assets/_Project/Prefabs/Ghosts/Ghost_Base.prefab";


        [InitializeOnLoadMethod]
        static void AutoRunIfMissing()
        {
            if (!File.Exists($"{ModelsFolder}/Ghost_Pacman_Mesh.asset"))
            {
                GenerateAll();
            }
        }

        [MenuItem("Game/Ghosts/Overhaul 3D Horror Ghost Visuals", priority = 70)]
        public static void GenerateAll()
        {
            EnsureDirectories();

            Mesh ghostMesh = BuildPacmanGhostMesh();
            string meshPath = $"{ModelsFolder}/Ghost_Pacman_Mesh.asset";
            AssetDatabase.CreateAsset(ghostMesh, meshPath);

            Mesh wavyMouthMesh = BuildWavyMouthMesh();
            string wavyMouthPath = $"{ModelsFolder}/Ghost_Mouth_Wavy.asset";
            AssetDatabase.CreateAsset(wavyMouthMesh, wavyMouthPath);

            ExportMeshToObj(ghostMesh, $"{ModelsFolder}/Ghost_Pacman_Body.obj");
            ExportMeshToObj(wavyMouthMesh, $"{ModelsFolder}/Ghost_Mouth_Wavy.obj");

            CreateFaceMaterials();

            UpdateGhostBasePrefab(ghostMesh, wavyMouthMesh);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA>[Ghost Mesh Generator]</color> Successfully generated authentic 3D Pac-Man horror ghost meshes and upgraded Ghost_Base prefab!");
        }

        public static void ExportMeshToObj(Mesh mesh, string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# authentic 3D Pac-Man Ghost Mesh");
            sb.AppendLine($"o {mesh.name}");

            Vector3[] verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                sb.AppendLine($"v {verts[i].x:F4} {verts[i].y:F4} {verts[i].z:F4}");
            }

            Vector3[] norms = mesh.normals;
            bool hasNormals = norms != null && norms.Length >= verts.Length;
            if (hasNormals)
            {
                for (int i = 0; i < norms.Length; i++)
                {
                    sb.AppendLine($"vn {norms[i].x:F4} {norms[i].y:F4} {norms[i].z:F4}");
                }
            }

            sb.AppendLine("s 1");
            int[] tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i] + 1;
                int b = tris[i + 1] + 1;
                int c = tris[i + 2] + 1;
                if (hasNormals)
                    sb.AppendLine($"f {a}//{a} {b}//{b} {c}//{c}");
                else
                    sb.AppendLine($"f {a} {b} {c}");
            }

            File.WriteAllText(path, sb.ToString());
        }

        static void EnsureDirectories()
        {
            if (!Directory.Exists(ModelsFolder)) Directory.CreateDirectory(ModelsFolder);
            if (!Directory.Exists(MaterialsFolder)) Directory.CreateDirectory(MaterialsFolder);
        }

        /// <summary>
        /// Builds an authentic 3D Pac-Man ghost mesh:
        /// - Smooth hemispherical dome cap at top
        /// - Cylindrical bell shroud in the middle
        /// - 4 iconic scalloped wavy ruffle tentacle folds at the bottom with hollow recessed interior
        /// </summary>
        public static Mesh BuildPacmanGhostMesh()
        {
            var mesh = new Mesh { name = "Ghost_Pacman_Body" };

            const int LonSegments = 32;
            const int DomeLatSegments = 12;
            const int TorsoSegments = 8;
            const int SkirtSegments = 6;
            const float HeadCenterY = 1.15f;
            const float DomeRadius = 0.44f;
            const float TorsoBottomY = 0.45f;
            const float TorsoBottomRadius = 0.48f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // --- 1. Dome Cap (Hemisphere) ---
            vertices.Add(new Vector3(0f, HeadCenterY + DomeRadius, 0f));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 1f));

            for (int lat = 1; lat <= DomeLatSegments; lat++)
            {
                float theta = (Mathf.PI * 0.5f) * ((float)lat / DomeLatSegments);
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);
                float y = HeadCenterY + DomeRadius * cosTheta;
                float r = DomeRadius * sinTheta;

                for (int lon = 0; lon <= LonSegments; lon++)
                {
                    float phi = 2f * Mathf.PI * ((float)lon / LonSegments);
                    float x = r * Mathf.Sin(phi);
                    float z = r * Mathf.Cos(phi);

                    Vector3 pos = new Vector3(x, y, z);
                    Vector3 norm = (pos - new Vector3(0f, HeadCenterY, 0f)).normalized;

                    vertices.Add(pos);
                    normals.Add(norm);
                    uvs.Add(new Vector2((float)lon / LonSegments, 0.7f + 0.3f * (1f - (float)lat / DomeLatSegments)));
                }
            }

            // Dome top fan (double sided)
            for (int lon = 0; lon < LonSegments; lon++)
            {
                int vA = 1 + lon;
                int vB = 1 + lon + 1;
                // Clockwise front
                triangles.Add(0);
                triangles.Add(vA);
                triangles.Add(vB);
                // Counter-clockwise back
                triangles.Add(0);
                triangles.Add(vB);
                triangles.Add(vA);
            }

            // Dome lat quads
            for (int lat = 0; lat < DomeLatSegments - 1; lat++)
            {
                int rowStart = 1 + lat * (LonSegments + 1);
                int nextRowStart = rowStart + (LonSegments + 1);

                for (int lon = 0; lon < LonSegments; lon++)
                {
                    int current = rowStart + lon;
                    int next = nextRowStart + lon;

                    // Front faces (clockwise)
                    triangles.Add(current);
                    triangles.Add(current + 1);
                    triangles.Add(next);

                    triangles.Add(current + 1);
                    triangles.Add(next + 1);
                    triangles.Add(next);

                    // Back faces (two-sided insurance)
                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);

                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            // --- 2. Torso Shroud (Flared Cylinder) ---
            int torsoStartRing = vertices.Count - (LonSegments + 1);

            for (int step = 1; step <= TorsoSegments; step++)
            {
                float t = (float)step / TorsoSegments;
                float y = Mathf.Lerp(HeadCenterY, TorsoBottomY, t);
                float r = Mathf.Lerp(DomeRadius, TorsoBottomRadius, t);

                for (int lon = 0; lon <= LonSegments; lon++)
                {
                    float phi = 2f * Mathf.PI * ((float)lon / LonSegments);
                    float x = r * Mathf.Sin(phi);
                    float z = r * Mathf.Cos(phi);

                    Vector3 pos = new Vector3(x, y, z);
                    Vector3 norm = new Vector3(x, 0.05f, z).normalized;

                    vertices.Add(pos);
                    normals.Add(norm);
                    uvs.Add(new Vector2((float)lon / LonSegments, Mathf.Lerp(0.7f, 0.3f, t)));
                }

                int prevRing = torsoStartRing + (step - 1) * (LonSegments + 1);
                int currRing = torsoStartRing + step * (LonSegments + 1);

                for (int lon = 0; lon < LonSegments; lon++)
                {
                    int current = prevRing + lon;
                    int next = currRing + lon;

                    // Front faces
                    triangles.Add(current);
                    triangles.Add(current + 1);
                    triangles.Add(next);

                    triangles.Add(current + 1);
                    triangles.Add(next + 1);
                    triangles.Add(next);

                    // Back faces
                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);

                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            // --- 3. Scalloped 4-Tentacle Ruffle Skirt ---
            int skirtStartRing = vertices.Count - (LonSegments + 1);

            for (int step = 1; step <= SkirtSegments; step++)
            {
                float t = (float)step / SkirtSegments;

                for (int lon = 0; lon <= LonSegments; lon++)
                {
                    float phi = 2f * Mathf.PI * ((float)lon / LonSegments);

                    // 4-fold scallop curve
                    float scallopWave = Mathf.Pow(Mathf.Abs(Mathf.Sin(2f * phi)), 1.3f);
                    float bottomY = 0.34f - 0.20f * scallopWave; // Troughs drop to 0.14m, crests at 0.34m

                    float y = Mathf.Lerp(TorsoBottomY, bottomY, t);
                    float r = TorsoBottomRadius + 0.035f * t; // gentle bell flare

                    float x = r * Mathf.Sin(phi);
                    float z = r * Mathf.Cos(phi);

                    Vector3 pos = new Vector3(x, y, z);
                    Vector3 norm = new Vector3(x, -0.2f, z).normalized;

                    vertices.Add(pos);
                    normals.Add(norm);
                    uvs.Add(new Vector2((float)lon / LonSegments, Mathf.Lerp(0.3f, 0.0f, t)));
                }

                int prevRing = skirtStartRing + (step - 1) * (LonSegments + 1);
                int currRing = skirtStartRing + step * (LonSegments + 1);

                for (int lon = 0; lon < LonSegments; lon++)
                {
                    int current = prevRing + lon;
                    int next = currRing + lon;

                    // Front faces
                    triangles.Add(current);
                    triangles.Add(current + 1);
                    triangles.Add(next);

                    triangles.Add(current + 1);
                    triangles.Add(next + 1);
                    triangles.Add(next);

                    // Back faces
                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);

                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            // --- 4. Recessed Hollow Underside Cone ---
            int hemRing = vertices.Count - (LonSegments + 1);
            int apexIndex = vertices.Count;
            vertices.Add(new Vector3(0f, 0.65f, 0f));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0f));

            for (int lon = 0; lon < LonSegments; lon++)
            {
                triangles.Add(hemRing + lon);
                triangles.Add(hemRing + lon + 1);
                triangles.Add(apexIndex);

                triangles.Add(hemRing + lon + 1);
                triangles.Add(hemRing + lon);
                triangles.Add(apexIndex);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds the iconic 3D wavy zigzag frightened Pac-Man mouth mesh.
        /// </summary>
        public static Mesh BuildWavyMouthMesh()
        {
            var mesh = new Mesh { name = "Ghost_Mouth_Wavy" };

            // 6 segments wavy zig-zag line extruded into a ribbon
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            int points = 16;
            float width = 0.28f;
            float ribbonHeight = 0.035f;

            for (int i = 0; i < points; i++)
            {
                float t = (float)i / (points - 1);
                float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
                // Wavy zig-zag curve
                float yOffset = Mathf.Sin(t * Mathf.PI * 4f) * 0.03f;

                vertices.Add(new Vector3(x, yOffset + ribbonHeight * 0.5f, 0f));
                normals.Add(Vector3.forward);
                uvs.Add(new Vector2(t, 1f));

                vertices.Add(new Vector3(x, yOffset - ribbonHeight * 0.5f, 0f));
                normals.Add(Vector3.forward);
                uvs.Add(new Vector2(t, 0f));
            }

            for (int i = 0; i < points - 1; i++)
            {
                int topA = i * 2;
                int botA = topA + 1;
                int topB = (i + 1) * 2;
                int botB = topB + 1;

                triangles.Add(topA);
                triangles.Add(topB);
                triangles.Add(botA);

                triangles.Add(botA);
                triangles.Add(topB);
                triangles.Add(botB);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void CreateFaceMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            // 1. Ghost Sclera (Creamy White Glossy)
            CreateMaterial("M_Ghost_Sclera", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.95f));
                m.SetFloat("_Smoothness", 0.85f);
            });

            // 2. Ghost Pupil (Piercing Deep Onyx / Glowing Iris)
            CreateMaterial("M_Ghost_Pupil", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.08f));
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.1f, 0.2f, 0.4f) * 2f);
                m.SetFloat("_Smoothness", 0.95f);
            });

            // 3. Ghost Normal Mouth (Dark Sunken Void)
            CreateMaterial("M_Ghost_Mouth", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.03f));
                m.SetFloat("_Smoothness", 0.1f);
            });

            // 4. Ghost Wavy Frightened Mouth (Cyan Glowing Ribbon)
            CreateMaterial("M_Ghost_WavyMouth", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.15f, 0.4f, 1f));
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.2f, 0.6f, 1f) * 3f);
            });
        }

        static void CreateMaterial(string name, Shader shader, Action<Material> configure)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            configure(mat);
            EditorUtility.SetDirty(mat);
        }

        static void UpdateGhostBasePrefab(Mesh ghostMesh, Mesh wavyMouthMesh)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Ensure VisualRoot exists
                Transform visualRoot = root.transform.Find("VisualRoot");
                if (visualRoot == null)
                {
                    var vrGo = new GameObject("VisualRoot");
                    vrGo.transform.SetParent(root.transform, false);
                    visualRoot = vrGo.transform;
                }

                // 1. Move Body under VisualRoot and assign custom ghost mesh
                Transform bodyT = root.transform.Find("Body");
                if (bodyT == null && visualRoot != null) bodyT = visualRoot.Find("Body");
                if (bodyT == null)
                {
                    var bodyGo = new GameObject("Body");
                    bodyT = bodyGo.transform;
                }
                bodyT.SetParent(visualRoot, false);
                bodyT.localPosition = Vector3.zero;
                bodyT.localRotation = Quaternion.identity;
                bodyT.localScale = Vector3.one;

                var meshFilter = bodyT.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = bodyT.gameObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = ghostMesh;

                var bodyRenderer = bodyT.GetComponent<MeshRenderer>();
                if (bodyRenderer == null) bodyRenderer = bodyT.gameObject.AddComponent<MeshRenderer>();

                Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_GhostBody_Placeholder.mat");
                if (bodyMat != null)
                {
                    bodyMat.SetFloat("_Cull", 0f);
                    bodyMat.SetFloat("_Surface", 0f);
                    bodyMat.SetFloat("_ZWrite", 1f);
                    bodyMat.SetColor("_BaseColor", Color.white);
                    bodyRenderer.sharedMaterial = bodyMat;
                    EditorUtility.SetDirty(bodyMat);
                }

                // 2. Setup Face Hierarchy under VisualRoot
                Transform faceRoot = visualRoot.Find("Face");
                if (faceRoot == null)
                {
                    var faceGo = new GameObject("Face");
                    faceGo.transform.SetParent(visualRoot, false);
                    faceRoot = faceGo.transform;
                }
                faceRoot.localPosition = Vector3.zero;

                Material scleraMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Ghost_Sclera.mat");
                Material pupilMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Ghost_Pupil.mat");
                Material mouthMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Ghost_Mouth.mat");
                Material wavyMouthMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Ghost_WavyMouth.mat");

                // Eye Left
                Transform eyeL = SetupEye(faceRoot, "Eye_L", new Vector3(-0.16f, 1.25f, 0.38f), scleraMat, pupilMat, out Transform pupilL);
                // Eye Right
                Transform eyeR = SetupEye(faceRoot, "Eye_R", new Vector3(0.16f, 1.25f, 0.38f), scleraMat, pupilMat, out Transform pupilR);

                // Clean up any old loose eye objects at root
                Transform oldEyeL = root.transform.Find("Eye_L");
                if (oldEyeL != null) UnityEngine.Object.DestroyImmediate(oldEyeL.gameObject);
                Transform oldEyeR = root.transform.Find("Eye_R");
                if (oldEyeR != null) UnityEngine.Object.DestroyImmediate(oldEyeR.gameObject);

                // Normal Mouth (sunken dark cavity)
                Transform mouthT = faceRoot.Find("Mouth_Normal");
                if (mouthT == null)
                {
                    var mouthGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    mouthGo.name = "Mouth_Normal";
                    mouthT = mouthGo.transform;
                    mouthT.SetParent(faceRoot, false);
                    UnityEngine.Object.DestroyImmediate(mouthGo.GetComponent<Collider>());
                }
                mouthT.localPosition = new Vector3(0f, 0.98f, 0.42f);
                mouthT.localScale = new Vector3(0.12f, 0.08f, 0.04f);
                var mouthRenderer = mouthT.GetComponent<MeshRenderer>();
                mouthRenderer.sharedMaterial = mouthMat;

                // Frightened Wavy Mouth
                Transform wavyMouthT = faceRoot.Find("Mouth_Wavy");
                if (wavyMouthT == null)
                {
                    var wavyGo = new GameObject("Mouth_Wavy");
                    wavyMouthT = wavyGo.transform;
                    wavyMouthT.SetParent(faceRoot, false);
                }
                wavyMouthT.localPosition = new Vector3(0f, 0.98f, 0.44f);
                wavyMouthT.localRotation = Quaternion.identity;
                wavyMouthT.localScale = Vector3.one;

                var wavyFilter = wavyMouthT.GetComponent<MeshFilter>();
                if (wavyFilter == null) wavyFilter = wavyMouthT.gameObject.AddComponent<MeshFilter>();
                wavyFilter.sharedMesh = wavyMouthMesh;

                var wavyRenderer = wavyMouthT.GetComponent<MeshRenderer>();
                if (wavyRenderer == null) wavyRenderer = wavyMouthT.gameObject.AddComponent<MeshRenderer>();
                wavyRenderer.sharedMaterial = wavyMouthMat;
                wavyRenderer.enabled = false;

                // 3. Move Glow Light under VisualRoot and add DynamicLightSource
                Transform glowT = root.transform.Find("Glow");
                if (glowT == null && visualRoot != null) glowT = visualRoot.Find("Glow");
                if (glowT != null)
                {
                    glowT.SetParent(visualRoot, false);
                    glowT.localPosition = new Vector3(0f, 1.0f, 0f);
                    var dynamicLight = glowT.GetComponent<DynamicLightSource>();
                    if (dynamicLight == null) dynamicLight = glowT.gameObject.AddComponent<DynamicLightSource>();
                }

                // 4. Wire GhostVisuals component
                var visuals = root.GetComponent<GhostVisuals>();
                if (visuals != null)
                {
                    var so = new SerializedObject(visuals);
                    so.FindProperty("body").objectReferenceValue = bodyRenderer;
                    so.FindProperty("glow").objectReferenceValue = glowT != null ? glowT.GetComponent<Light>() : null;
                    so.FindProperty("visualRoot").objectReferenceValue = visualRoot;
                    so.FindProperty("eyesRoot").objectReferenceValue = faceRoot;
                    so.FindProperty("pupilL").objectReferenceValue = pupilL;
                    so.FindProperty("pupilR").objectReferenceValue = pupilR;
                    so.FindProperty("normalMouth").objectReferenceValue = mouthRenderer;
                    so.FindProperty("wavyMouth").objectReferenceValue = wavyRenderer;
                    so.ApplyModifiedProperties();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform SetupEye(Transform parent, string name, Vector3 localPos, Material scleraMat, Material pupilMat, out Transform pupilTransform)
        {
            Transform eyeT = parent.Find(name);
            if (eyeT == null)
            {
                var eyeGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eyeGo.name = name;
                eyeT = eyeGo.transform;
                eyeT.SetParent(parent, false);
                UnityEngine.Object.DestroyImmediate(eyeGo.GetComponent<Collider>());
            }

            eyeT.localPosition = localPos;
            eyeT.localScale = new Vector3(0.16f, 0.20f, 0.14f); // classic oval eye
            eyeT.localRotation = Quaternion.Euler(-5f, localPos.x > 0 ? 5f : -5f, 0f);
            eyeT.GetComponent<MeshRenderer>().sharedMaterial = scleraMat;

            // Pupil
            Transform pupilT = eyeT.Find("Pupil");
            if (pupilT == null)
            {
                var pupilGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pupilGo.name = "Pupil";
                pupilT = pupilGo.transform;
                pupilT.SetParent(eyeT, false);
                UnityEngine.Object.DestroyImmediate(pupilGo.GetComponent<Collider>());
            }
            pupilT.localPosition = new Vector3(0f, 0f, 0.45f);
            pupilT.localScale = new Vector3(0.55f, 0.55f, 0.25f);
            pupilT.GetComponent<MeshRenderer>().sharedMaterial = pupilMat;

            pupilTransform = pupilT;
            return eyeT;
        }
    }
}

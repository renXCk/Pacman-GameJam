using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor automation tool for generating Bloom-based emissive materials and
    /// pooled particle system prefabs for Blackout Maze under Assets/_Project/.
    /// </summary>
    public static class VFXAssetGenerator
    {
        const string MaterialsPath = "Assets/_Project/Art/Materials";
        const string PrefabsPath = "Assets/_Project/Prefabs/VFX";

        [MenuItem("Game/VFX/Generate VFX Prefabs & Materials", priority = 50)]
        public static void GenerateAll()
        {
            EnsureDirectories();
            GenerateEmissiveMaterials();
            GenerateParticlePrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA>[VFX Asset Generator]</color> Successfully generated Bloom materials and VFX prefabs!");
        }

        static void EnsureDirectories()
        {
            if (!Directory.Exists(MaterialsPath)) Directory.CreateDirectory(MaterialsPath);
            if (!Directory.Exists(PrefabsPath)) Directory.CreateDirectory(PrefabsPath);
        }

        static void GenerateEmissiveMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("Universal Render Pipeline/Lit shader not found.");
                return;
            }

            // 1. Power Cell Emissive (Golden Amber, HDR +2.0)
            CreateEmissiveMaterial(urpLit, "M_PowerCell_Emissive", 
                baseColor: new Color(0.1f, 0.1f, 0.05f),
                emissionColor: new Color(1.0f, 0.85f, 0.2f) * 2.2f);

            // 2. Battery Pickup Emissive (Neon Emerald, HDR +2.5)
            CreateEmissiveMaterial(urpLit, "M_Battery_Emissive",
                baseColor: new Color(0.02f, 0.1f, 0.04f),
                emissionColor: new Color(0.1f, 1.0f, 0.35f) * 2.5f);

            // 3. Corridor Wall with Emissive Accent Strip
            CreateEmissiveMaterial(urpLit, "M_CorridorWall_Emissive",
                baseColor: new Color(0.05f, 0.05f, 0.07f),
                emissionColor: new Color(0.1f, 0.2f, 0.4f) * 1.5f);

            // 4. Flare Pickup Emissive (Blinding White/Orange, HDR +4.0)
            CreateEmissiveMaterial(urpLit, "M_Flare_Emissive",
                baseColor: new Color(0.2f, 0.1f, 0.02f),
                emissionColor: new Color(1.0f, 0.5f, 0.1f) * 4.0f);
        }

        static void CreateEmissiveMaterial(Shader shader, string name, Color baseColor, Color emissionColor)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
        }

        static void GenerateParticlePrefabs()
        {
            // 1. Cell Collect FX (Subtle golden sparkle implosion)
            CreateOrUpdatePrefab("PF_CellCollectFX", go =>
            {
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.45f;
                main.loop = false;
                main.startLifetime = 0.4f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f, 0.9f));
                main.stopAction = ParticleSystemStopAction.None;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.25f;
            });

            // 2. Flare Ignition FX (High-intensity spark & smoke burst)
            CreateOrUpdatePrefab("PF_FlareIgnitionFX", go =>
            {
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 1.2f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 14f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.4f, 0.05f));

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;
            });

            // 3. Ghost Banish FX (Spectral dissipation burst)
            CreateOrUpdatePrefab("PF_GhostBanishFX", go =>
            {
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.9f;
                main.loop = false;
                main.startLifetime = 0.7f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.8f, 1f));

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;
            });

            // 4. Player Caught FX (Screen distortion horror particle burst)
            CreateOrUpdatePrefab("PF_PlayerCaughtFX", go =>
            {
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 1.0f;
                main.loop = false;
                main.startLifetime = 0.8f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.02f, 0.02f, 0.8f));

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 45f;
            });
        }

        static void CreateOrUpdatePrefab(string prefabName, System.Action<GameObject> configure)
        {
            string path = $"{PrefabsPath}/{prefabName}.prefab";
            var tempGo = new GameObject(prefabName);
            try
            {
                configure(tempGo);
                PrefabUtility.SaveAsPrefabAsset(tempGo, path);
            }
            finally
            {
                Object.DestroyImmediate(tempGo);
            }
        }
    }
}

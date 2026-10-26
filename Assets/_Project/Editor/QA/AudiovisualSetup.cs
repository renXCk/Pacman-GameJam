using System.IO;
using Game.Audio;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using Game.Lighting;
using Game.Maze;
using Game.Pickups;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Master setup and automation tool for Developer D's audiovisual systems:
    /// - Generates emissive Bloom materials and pooled VFX prefabs
    /// - Generates procedural audio clips and AudioCue assets
    /// - Creates 3D horror Pac-Man ghost mesh and face rigs
    /// - Installs AudioManager, LightingManager, VFXManager, PostProcessRig & MazePickups into Game.unity
    /// - Validates budget compliance for locked 60 FPS
    /// </summary>
    public static class AudiovisualSetup
    {
        const string Root = "Assets/_Project";
        const string GameScenePath = Root + "/Scenes/Game.unity";
        const string ArtMaterials = Root + "/Art/Materials";
        const string PrefabsVFX = Root + "/Prefabs/VFX";
        const string ConfigFolder = Root + "/ScriptableObjects/Configs";
        const string EventFolder = Root + "/ScriptableObjects/Events";
        const string RuntimeFolder = Root + "/ScriptableObjects/Runtime";
        const string AudioFolder = Root + "/ScriptableObjects/Audio";
        const string HorrorProfilePath = Root + "/Art/VolumeProfile_Horror.asset";

        [InitializeOnLoadMethod]
        static void AutoRunIfMissing()
        {
            if (!File.Exists(Root + "/Art/Audio/sfx_cell_waka.wav") || !File.Exists(Root + "/Art/Models/Ghost_Pacman_Mesh.asset"))
            {
                SetupAll();
            }
        }

        [MenuItem("Game/QA/Setup Developer D Audiovisual Systems", priority = 90)]
        public static void SetupAll()
        {
            Debug.Log("<color=#FFFF00>[Audiovisual Setup]</color> Beginning Developer D automated deployment...");

            // 1. Generate VFX & Materials
            VFXAssetGenerator.GenerateAll();

            // 2. Generate Audio Assets & Cues
            AudioAssetGenerator.GenerateAll();

            // 3. Overhaul Ghost Visuals to 3D Pac-Man Horror
            GhostMeshGenerator.GenerateAll();

            // 4. Update MazeConfig with dark cyber-horror corridor material
            UpdateMazeConfigMaterials();

            // 5. Wire systems into Game.unity
            WireGameScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA><b>[Audiovisual Setup COMPLETE]</b></color> All Developer D systems, ghost visuals, and maze pickups deployed!");

            // Run QA validation check
            AudiovisualQAValidator.ValidateAll();
        }

        static void UpdateMazeConfigMaterials()
        {
            var mazeConfig = AssetDatabase.LoadAssetAtPath<MazeConfig>(ConfigFolder + "/MazeConfig.asset");
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>(ArtMaterials + "/M_CorridorWall_Emissive.mat");
            if (mazeConfig != null && wallMat != null)
            {
                var so = new SerializedObject(mazeConfig);
                so.FindProperty("wallMaterial").objectReferenceValue = wallMat;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mazeConfig);
            }
        }

        public static void WireGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // Load shared assets
            var gameStateChanged = Load<GameStateEventChannel>(EventFolder + "/GameStateChanged.asset");
            var modeChanged = Load<GhostModeEventChannel>(EventFolder + "/ModeChanged.asset");
            var pickupCollected = Load<PickupEventChannel>(EventFolder + "/PickupCollected.asset");
            var flareCollected = Load<VoidEventChannel>(EventFolder + "/FlareCollected.asset");
            var ghostBanished = Load<GhostBanishedEventChannel>(EventFolder + "/GhostBanished.asset");
            var playerCaught = Load<VoidEventChannel>(EventFolder + "/PlayerCaught.asset");
            var levelComplete = Load<VoidEventChannel>(EventFolder + "/LevelComplete.asset");
            var pauseChanged = Load<BoolEventChannel>(EventFolder + "/PauseChanged.asset");
            var extraLifeAwarded = Load<VoidEventChannel>(EventFolder + "/ExtraLifeAwarded.asset");
            var batteryChanged = Load<FloatEventChannel>(EventFolder + "/BatteryChanged.asset");
            var levelStarted = Load<LevelConfigEventChannel>(EventFolder + "/LevelStarted.asset");

            var playerAnchor = Load<TransformAnchor>(RuntimeFolder + "/PlayerAnchor.asset");
            var levelProgress = Load<LevelProgress>(RuntimeFolder + "/LevelProgress.asset");

            var horrorProfile = Load<VolumeProfile>(HorrorProfilePath);

            // --- 1. Global Volume & PostProcessRig ---
            var globalVolGo = GameObject.Find("[Global Volume]");
            if (globalVolGo == null) globalVolGo = new GameObject("[Global Volume]");

            var volume = globalVolGo.GetComponent<Volume>();
            if (volume == null) volume = globalVolGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = horrorProfile;

            var postProcessRig = globalVolGo.GetComponent<PostProcessRig>();
            if (postProcessRig == null) postProcessRig = globalVolGo.AddComponent<PostProcessRig>();
            var pprSo = new SerializedObject(postProcessRig);
            pprSo.FindProperty("volume").objectReferenceValue = volume;
            pprSo.ApplyModifiedProperties();

            // --- 2. Lighting Manager ---
            var flashlight = Object.FindAnyObjectByType<FlashlightController>();

            var lightingGo = GameObject.Find("[Lighting]");
            if (lightingGo == null) lightingGo = new GameObject("[Lighting]");

            var lightingManager = lightingGo.GetComponent<LightingManager>();
            if (lightingManager == null) lightingManager = lightingGo.AddComponent<LightingManager>();

            var lmSo = new SerializedObject(lightingManager);
            lmSo.FindProperty("gameStateChanged").objectReferenceValue = gameStateChanged;
            lmSo.FindProperty("modeChanged").objectReferenceValue = modeChanged;
            lmSo.FindProperty("flareCollected").objectReferenceValue = flareCollected;
            lmSo.FindProperty("playerCaught").objectReferenceValue = playerCaught;
            lmSo.FindProperty("levelComplete").objectReferenceValue = levelComplete;
            lmSo.FindProperty("batteryChanged").objectReferenceValue = batteryChanged;
            lmSo.FindProperty("postProcessRig").objectReferenceValue = postProcessRig;
            lmSo.FindProperty("flashlight").objectReferenceValue = flashlight;
            lmSo.FindProperty("playerAnchor").objectReferenceValue = playerAnchor;
            lmSo.FindProperty("baseAmbientColor").colorValue = new Color(0.003f, 0.003f, 0.005f);
            lmSo.FindProperty("maxDynamicLights").intValue = 8;
            lmSo.ApplyModifiedProperties();

            // --- 3. Audio Manager ---
            var audioGo = GameObject.Find("[Audio]");
            if (audioGo == null) audioGo = new GameObject("[Audio]");

            var audioManager = audioGo.GetComponent<AudioManager>();
            if (audioManager == null) audioManager = audioGo.AddComponent<AudioManager>();

            var amSo = new SerializedObject(audioManager);
            amSo.FindProperty("gameStateChanged").objectReferenceValue = gameStateChanged;
            amSo.FindProperty("modeChanged").objectReferenceValue = modeChanged;
            amSo.FindProperty("pickupCollected").objectReferenceValue = pickupCollected;
            amSo.FindProperty("flareCollected").objectReferenceValue = flareCollected;
            amSo.FindProperty("ghostBanished").objectReferenceValue = ghostBanished;
            amSo.FindProperty("playerCaught").objectReferenceValue = playerCaught;
            amSo.FindProperty("levelComplete").objectReferenceValue = levelComplete;
            amSo.FindProperty("pauseChanged").objectReferenceValue = pauseChanged;
            amSo.FindProperty("extraLifeAwarded").objectReferenceValue = extraLifeAwarded;

            amSo.FindProperty("ambientDroneCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_AmbientDrone.asset");
            amSo.FindProperty("scatterChaseSirenCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_ScatterChaseSiren.asset");
            amSo.FindProperty("frightenedSirenCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_FrightenedSiren.asset");
            amSo.FindProperty("readyJingleCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_ReadyJingle.asset");
            amSo.FindProperty("gameOverCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_GameOver.asset");

            amSo.FindProperty("cellCollectCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_CellCollect.asset");
            amSo.FindProperty("batteryCollectCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_BatteryCollect.asset");
            amSo.FindProperty("flareIgniteCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_FlareIgnite.asset");
            amSo.FindProperty("ghostBanishedCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_GhostBanished.asset");
            amSo.FindProperty("playerCaughtCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_PlayerCaught.asset");
            amSo.FindProperty("levelCompleteCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_LevelComplete.asset");
            amSo.FindProperty("extraLifeCue").objectReferenceValue = Load<AudioCue>(AudioFolder + "/AudioCue_ExtraLife.asset");
            amSo.ApplyModifiedProperties();

            // --- 4. VFX Manager ---
            var vfxGo = GameObject.Find("[VFX]");
            if (vfxGo == null) vfxGo = new GameObject("[VFX]");

            var vfxManager = vfxGo.GetComponent<VFXManager>();
            if (vfxManager == null) vfxManager = vfxGo.AddComponent<VFXManager>();

            var vmSo = new SerializedObject(vfxManager);
            vmSo.FindProperty("pickupCollected").objectReferenceValue = pickupCollected;
            vmSo.FindProperty("flareCollected").objectReferenceValue = flareCollected;
            vmSo.FindProperty("ghostBanished").objectReferenceValue = ghostBanished;
            vmSo.FindProperty("playerCaught").objectReferenceValue = playerCaught;
            vmSo.FindProperty("playerAnchor").objectReferenceValue = playerAnchor;

            vmSo.FindProperty("cellCollectPrefab").objectReferenceValue = LoadPrefabComponent<ParticleSystem>(PrefabsVFX + "/PF_CellCollectFX.prefab");
            vmSo.FindProperty("flareIgnitionPrefab").objectReferenceValue = LoadPrefabComponent<ParticleSystem>(PrefabsVFX + "/PF_FlareIgnitionFX.prefab");
            vmSo.FindProperty("ghostBanishPrefab").objectReferenceValue = LoadPrefabComponent<ParticleSystem>(PrefabsVFX + "/PF_GhostBanishFX.prefab");
            vmSo.FindProperty("playerCaughtPrefab").objectReferenceValue = LoadPrefabComponent<ParticleSystem>(PrefabsVFX + "/PF_PlayerCaughtFX.prefab");
            vmSo.ApplyModifiedProperties();

            // --- 5. Maze Pickups on Maze Builder ---
            var mazeBuilder = Object.FindAnyObjectByType<MazeBuilder>();
            if (mazeBuilder != null)
            {
                var mazePickups = mazeBuilder.GetComponent<MazePickups>();
                if (mazePickups == null) mazePickups = mazeBuilder.gameObject.AddComponent<MazePickups>();

                var mpSo = new SerializedObject(mazePickups);
                mpSo.FindProperty("cellConfig").objectReferenceValue = Load<PickupConfig>(ConfigFolder + "/PickupConfig_Cell.asset");
                mpSo.FindProperty("flareConfig").objectReferenceValue = Load<PickupConfig>(ConfigFolder + "/PickupConfig_Flare.asset");
                mpSo.FindProperty("batteryConfig").objectReferenceValue = Load<PickupConfig>(ConfigFolder + "/PickupConfig_Battery.asset");

                mpSo.FindProperty("cellMaterial").objectReferenceValue = Load<Material>(ArtMaterials + "/M_PowerCell_Emissive.mat");
                mpSo.FindProperty("flareMaterial").objectReferenceValue = Load<Material>(ArtMaterials + "/M_Flare_Emissive.mat");
                mpSo.FindProperty("batteryMaterial").objectReferenceValue = Load<Material>(ArtMaterials + "/M_Battery_Emissive.mat");

                mpSo.FindProperty("progress").objectReferenceValue = levelProgress;
                mpSo.FindProperty("pickupCollected").objectReferenceValue = pickupCollected;
                mpSo.FindProperty("flareCollected").objectReferenceValue = flareCollected;
                mpSo.FindProperty("levelStarted").objectReferenceValue = levelStarted;
                mpSo.ApplyModifiedProperties();
            }

            // --- 6. DynamicLightSource on Flashlight ---
            if (flashlight != null)
            {
                var dls = flashlight.GetComponent<DynamicLightSource>();
                if (dls == null) dls = flashlight.gameObject.AddComponent<DynamicLightSource>();
                var dlsSo = new SerializedObject(dls);
                dlsSo.FindProperty("priority").intValue = 100;
                dlsSo.ApplyModifiedProperties();
            }

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.002f, 0.002f, 0.004f);
            RenderSettings.ambientIntensity = 0.05f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[AudiovisualSetup] Asset not found at path: {path}");
            }
            return asset;
        }

        static T LoadPrefabComponent<T>(string path) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogWarning($"[AudiovisualSetup] Prefab not found at path: {path}");
                return null;
            }
            return go.GetComponent<T>();
        }
    }
}

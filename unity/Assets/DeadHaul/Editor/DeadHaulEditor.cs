using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadHaul.EditorTools
{
    // Dead Haul menu: one-time project setup and a one-click Windows build.
    [InitializeOnLoad]
    public static class DeadHaulEditor
    {
        const string ResDir = "Assets/DeadHaul/Resources";
        const string ScenePath = "Assets/DeadHaul/DeadHaul.unity";

        static DeadHaulEditor()
        {
            EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) EnsureMaterials(false); };
        }

        [MenuItem("Dead Haul/Set Up Project", false, 1)]
        public static void SetUp()
        {
            EnsureMaterials(true);
            EnsureScene();
            PlayerSettings.productName = "Dead Haul";
            PlayerSettings.companyName = "Dead Haul";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUtility.DisplayDialog("Dead Haul", "Project is set up.\n\nPress Play to test, or use Dead Haul > Build Windows .exe.", "OK");
        }

        // Saves copies of URP's default materials so the built game can load them from Resources.
        static void EnsureMaterials(bool loud)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null)
            {
                if (loud) EditorUtility.DisplayDialog("Dead Haul", "This project has no Universal Render Pipeline asset.\n\nCreate the project from the Universal 3D template in Unity Hub, then copy the DeadHaul folder in again.", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder(ResDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/DeadHaul")) AssetDatabase.CreateFolder("Assets", "DeadHaul");
                AssetDatabase.CreateFolder("Assets/DeadHaul", "Resources");
            }
            Save("DH_Lit", rp.defaultMaterial, "Universal Render Pipeline/Lit");
            Save("DH_Unlit", null, "Universal Render Pipeline/Unlit");
            Save("DH_Particle", rp.defaultParticleMaterial, "Universal Render Pipeline/Particles/Unlit");
            AssetDatabase.SaveAssets();
        }

        static void Save(string name, Material source, string shaderName)
        {
            string path = ResDir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Material m = null;
            if (source != null) m = new Material(source);
            else { var sh = Shader.Find(shaderName); if (sh != null) m = new Material(sh); }
            if (m == null) { Debug.LogWarning("Dead Haul: could not create " + name); return; }
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
        }

        static void EnsureScene()
        {
            if (EditorBuildSettings.scenes.Any(s => s.enabled && File.Exists(s.path))) return;
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("Dead Haul/Build Windows .exe", false, 20)]
        public static void BuildWindows()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureMaterials(true);
            EnsureScene();
            PlayerSettings.productName = "Dead Haul";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "DeadHaul"));
            string exe = Path.Combine(dir, "DeadHaul.exe");
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Dead Haul built: " + exe);
                EditorUtility.RevealInFinder(exe);
                EditorUtility.DisplayDialog("Dead Haul", "Build finished.\n\n" + dir + "\n\nTo share it, zip the whole DeadHaul folder (not just the .exe).", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Dead Haul", "Build failed: " + report.summary.result + "\n\nCheck the Console window for the errors.", "OK");
            }
        }

        [MenuItem("Dead Haul/Open Build Folder", false, 21)]
        public static void OpenBuildFolder()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "DeadHaul"));
            if (Directory.Exists(dir)) EditorUtility.RevealInFinder(dir);
            else EditorUtility.DisplayDialog("Dead Haul", "No build yet. Use Dead Haul > Build Windows .exe first.", "OK");
        }

        [MenuItem("Dead Haul/Reset Save Data", false, 40)]
        public static void ResetSave()
        {
            string f = Path.Combine(Application.persistentDataPath, "deadhaul_save.json");
            if (File.Exists(f) && EditorUtility.DisplayDialog("Dead Haul", "Delete your saved cash, stash and guns?", "Delete", "Cancel")) File.Delete(f);
        }
    }
}

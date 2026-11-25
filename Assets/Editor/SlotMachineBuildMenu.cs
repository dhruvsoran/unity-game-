#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LuckySeven.Slot
{
    /// <summary>Convenience menu items for building and inspecting the game.</summary>
    public static class SlotMachineBuildMenu
    {
        private const string BuildFolder = "Build/WebGL";

        [MenuItem("Lucky Seven/Build WebGL")]
        public static void BuildWebGL()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SlotMachine.unity" },
                locationPathName = BuildFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Lucky Seven] WebGL build result: " + report.summary.result);
        }

        [MenuItem("Lucky Seven/Open Project Folder")]
        public static void OpenProjectFolder()
        {
            EditorUtility.RevealInFinder("Assets/Scripts");
        }
    }
}
#endif

using UnityEngine;

namespace LuckySeven.Slot
{
    /// <summary>
    /// Auto-starts the slot game after any scene loads, so the project runs with
    /// a single Play press - no scene hierarchy setup needed. If a level ever
    /// contains its own SlotMachineView, the bootstrap leaves it alone.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (GameObject.Find("LuckySeven_SlotMachine") != null) return;
            if (Object.FindObjectOfType<SlotMachineView>() != null) return;

            GameObject root = new GameObject("LuckySeven_SlotMachine");
            Object.DontDestroyOnLoad(root);

            SlotMachineView view = root.AddComponent<SlotMachineView>();
            view.Initialize();
        }
    }
}

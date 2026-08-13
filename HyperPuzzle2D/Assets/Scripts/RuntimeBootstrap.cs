using HyperPuzzle2D.Core;
using UnityEngine;

namespace HyperPuzzle2D
{
    /// <summary>
    /// Auto-boots a playable smash level when Play is pressed in an empty project.
    /// </summary>
    public static class RuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameDirector>() != null)
            {
                return;
            }

            var go = new GameObject("GameDirector");
            go.AddComponent<GameDirector>();
        }
    }
}

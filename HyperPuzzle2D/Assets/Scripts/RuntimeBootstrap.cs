using HyperPuzzle2D.Paper;
using UnityEngine;

namespace HyperPuzzle2D
{
    public static class RuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<PaperDirector>() != null)
            {
                return;
            }

            var go = new GameObject("PaperDirector");
            go.AddComponent<PaperDirector>();
        }
    }
}

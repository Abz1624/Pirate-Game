using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateGame.Net
{
    public static class MainThreadDispatcher
    {
        static readonly Queue<Action> Queue = new();
        static DispatcherHost host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (host != null) return;
            var go = new GameObject("MainThreadDispatcher");
            host = go.AddComponent<DispatcherHost>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static void Enqueue(Action action)
        {
            lock (Queue)
                Queue.Enqueue(action);
        }

        class DispatcherHost : MonoBehaviour
        {
            void Update()
            {
                lock (Queue)
                {
                    while (Queue.Count > 0)
                        Queue.Dequeue()?.Invoke();
                }
            }
        }
    }
}

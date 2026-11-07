using System;
using System.Collections;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>Lets plain C# services schedule delayed work. Lives on the persistent GameRoot object.</summary>
    public sealed class CoroutineRunner : MonoBehaviour
    {
        public void RunAfter(float seconds, Action action) => StartCoroutine(RunAfterRoutine(seconds, action));

        private static IEnumerator RunAfterRoutine(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action?.Invoke();
        }
    }
}

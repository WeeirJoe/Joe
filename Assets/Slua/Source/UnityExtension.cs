
using System.Collections;
namespace SLua
{
    using UnityEngine;

    public static class UnityExtension
    {
        public static void StartCoroutine(this MonoBehaviour mb, LuaFunction func)
        {
            mb.StartCoroutine(LuaCoroutine(func));
        }

        internal static IEnumerator LuaCoroutine(LuaFunction func)
        {
            var thread = new LuaThreadWrapper(func);
            while (true)
            {
                object obj;
                if (!thread.Resume(out obj))
                {
                    yield break;
                }
                yield return obj;
            }
        }
    }
}
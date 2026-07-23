using UnityEngine;

namespace Micasa
{
    public static class TextResolver
    {
        private static readonly string pcName = System.Environment.UserName;

        public static string Resolve(string text) =>
            text.Replace("{PlayerPCName}", pcName);
    }
}

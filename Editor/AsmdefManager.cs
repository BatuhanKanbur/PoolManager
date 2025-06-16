using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PoolManager.Editor
{
    public static class AsmdefManager
    {
        public static bool AsmdefHasVerified(string asmdefPath)
        {
            if (!File.Exists(asmdefPath)) return false;
            var asmdefText = File.ReadAllText(asmdefPath);
            var asmdef = JsonUtility.FromJson<AsmdefData>(asmdefText);
            if(asmdef.references.Length == 0) return false;
            if (asmdef.defineConstraints.Length == 0) return false;
            var isVerified = false;
            foreach (var constraint in asmdef.defineConstraints)
            {
                isVerified = DefineManager.HasDefineSymbol(constraint);
                if(!isVerified) break;
            }
            return isVerified;
        }
        public static void UpdateAsmdef(string asmdefPath,string defineSymbol,params string[] requiredRefs)
        {
            if (!File.Exists(asmdefPath))
            {
                Debug.LogError($"[PoolManager] asmdef file not found at {asmdefPath}");
                return;
            }
            var asmdefText = File.ReadAllText(asmdefPath);
            var asmdef = JsonUtility.FromJson<AsmdefData>(asmdefText);
            var refs = asmdef.references.ToList();
            var defineConstraints = asmdef.defineConstraints.ToList();
            foreach (var requiredReference in requiredRefs)
                TryAdd(requiredReference);
            void TryAdd(string asmName)
            {
                if (refs.Contains(asmName)) return;
                refs.Add(asmName);
                Debug.Log($"[PoolManager] Added asmdef reference: {asmName}");
            }
            defineConstraints.Clear();
            defineConstraints.Add(defineSymbol);
            Debug.Log($"[PoolManager] Added define constraint: {defineSymbol}");
            asmdef.defineConstraints = defineConstraints.ToArray();
            asmdef.references = refs.ToArray();
            File.WriteAllText(asmdefPath, JsonUtility.ToJson(asmdef, true));
            AssetDatabase.Refresh();
            Debug.Log("[PoolManager] asmdef updated.");
        }
        [Serializable]
        private class AsmdefData
        {
            public string name;
            public string[] references = Array.Empty<string>();
            public string[] defineConstraints = Array.Empty<string>();
        }
    }
}

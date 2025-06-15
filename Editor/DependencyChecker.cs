#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace PoolManager.Editor
{
    [InitializeOnLoad]
    public static class DependencyChecker
    {
        private const string AssetManagerGitUrl = "https://github.com/BatuhanKanbur/AssetManager.git";
        private const string AssetManagerPackageName = "com.batuhankanbur.assetmanager";
        private const string PoolManagerAsmdefPath = "Packages/com.batuhankanbur.poolmanager/Runtime/PoolManager.asmdef";
        private const string DefineSymbol = "POOLMANAGER_INITIALIZED";

        private static readonly string[] RequiredReferences = new[]
        {
            "AssetManager",
            "UniTask",
            "Unity.Addressables"
        };

        private static readonly BuildTargetGroup[] Groups =
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
            BuildTargetGroup.WebGL,
            BuildTargetGroup.WSA,
            BuildTargetGroup.PS4,
            BuildTargetGroup.PS5,
            BuildTargetGroup.XboxOne
        };

        static DependencyChecker()
        {
            EditorApplication.update += Run;
        }

        private static void Run()
        {
            EditorApplication.update -= Run;
            if (!IsPackageInstalled($"Packages/{AssetManagerPackageName}"))
            {
                Debug.Log("[PoolManager] AssetManager not found, installing...");
                Client.Add(AssetManagerGitUrl);
                EditorApplication.update += WaitForPackageInstallation;
                return;
            }

            if (HasDefineSymbol(DefineSymbol) && !AsmdefHasReferences(RequiredReferences))
                RemoveDefineSymbols();
            if(!AsmdefHasReferences(RequiredReferences))
                UpdateAsmdef();
            if (!HasDefineSymbol(DefineSymbol))
                AddDefineSymbols();
        }

        private static void WaitForPackageInstallation()
        {
            if (IsPackageInstalled($"Packages/{AssetManagerPackageName}"))
            {
                Debug.Log("[PoolManager] AssetManager installed.");
                EditorApplication.update -= WaitForPackageInstallation;
                Run();
            }
        }
        private static void AddDefineSymbols()
        {
            foreach (var group in Groups)
            {
                var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                if (symbols.Contains(DefineSymbol)) continue;
                symbols = string.IsNullOrEmpty(symbols) ? DefineSymbol : symbols + ";" + DefineSymbol;
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, symbols);
            }

            Debug.Log("[PoolManager] Define symbol added : " + DefineSymbol);
        }
        private static void RemoveDefineSymbols()
        {
            foreach (var group in Groups)
            {
                var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                if (!symbols.Contains(DefineSymbol)) continue;
                var symbolList = symbols.Split(';').ToList();
                symbolList.RemoveAll(s => s == DefineSymbol);
                var newSymbols = string.Join(";", symbolList);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newSymbols);
            }
            Debug.Log("[PoolManager] Define symbol removed : " + DefineSymbol);
        }

        private static void UpdateAsmdef()
        {
            if (!File.Exists(PoolManagerAsmdefPath))
            {
                Debug.LogWarning($"[PoolManager] asmdef file not found at {PoolManagerAsmdefPath}");
                return;
            }

            var asmdefText = File.ReadAllText(PoolManagerAsmdefPath);
            var asmdef = JsonUtility.FromJson<AsmdefData>(asmdefText);
            var refs = asmdef.references.ToList();
            bool changed = false;

            void TryAdd(string asmName)
            {
                if (!refs.Contains(asmName))
                {
                    refs.Add(asmName);
                    changed = true;
                    Debug.Log($"[PoolManager] Added asmdef reference: {asmName}");
                }
            }

            foreach (var requiredReference in RequiredReferences)
                TryAdd(requiredReference);

            if (!changed) return;
            asmdef.references = refs.ToArray();
            File.WriteAllText(PoolManagerAsmdefPath, JsonUtility.ToJson(asmdef, true));
            AssetDatabase.Refresh();
            Debug.Log("[PoolManager] asmdef updated.");
        }

        private static bool IsPackageInstalled(string path) => Directory.Exists(path);

        private static bool AsmdefHasReferences(params string[] requiredRefs)
        {
            if (!File.Exists(PoolManagerAsmdefPath)) return false;
            var asmdefText = File.ReadAllText(PoolManagerAsmdefPath);
            var asmdef = JsonUtility.FromJson<AsmdefData>(asmdefText);
            var refs = asmdef.references ?? Array.Empty<string>();

            return requiredRefs.All(r => refs.Contains(r));
        }

        private static bool HasDefineSymbol(string symbol)
        {
            return Groups.All(group => PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';').Contains(symbol));
        }

        [Serializable]
        private class AsmdefData
        {
            public string name;
            public string[] references = Array.Empty<string>();
        }
    }
}
#endif

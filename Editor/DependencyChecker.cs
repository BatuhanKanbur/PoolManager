#if UNITY_EDITOR
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

            ApplyAsmdefReferences();
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

        private static void ApplyAsmdefReferences()
        {
            if (!File.Exists(PoolManagerAsmdefPath))
            {
                Debug.LogError($"[PoolManager] Asmdef not found: {PoolManagerAsmdefPath}");
                return;
            }

            var json = File.ReadAllText(PoolManagerAsmdefPath);
            var asmdef = JsonUtility.FromJson<AsmdefJson>(json);

            bool changed = false;

            foreach (var reference in RequiredReferences)
            {
                if (!asmdef.references.Contains(reference))
                {
                    asmdef.references.Add(reference);
                    changed = true;
                }
            }

            if (changed)
            {
                File.WriteAllText(PoolManagerAsmdefPath, JsonUtility.ToJson(asmdef, true));
                AssetDatabase.Refresh();
                Debug.Log("[PoolManager] Asmdef references updated.");
            }
        }

        private static void AddDefineSymbols()
        {
            foreach (var group in Groups)
            {
                string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                if (!symbols.Contains(DefineSymbol))
                {
                    symbols = string.IsNullOrEmpty(symbols) ? DefineSymbol : symbols + ";" + DefineSymbol;
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, symbols);
                }
            }

            Debug.Log("[PoolManager] Define symbol added : " + DefineSymbol);
        }

        private static bool IsPackageInstalled(string path)
        {
            return Directory.Exists(path);
        }

        [System.Serializable]
        private class AsmdefJson
        {
            public string name;
            public List<string> references = new();
            public List<string> includePlatforms = new();
            public List<string> excludePlatforms = new();
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public List<string> precompiledReferences = new();
            public bool autoReferenced = true;
            public List<string> defineConstraints = new();
            public List<string> versionDefines = new();
            public bool noEngineReferences;
        }
    }
}
#endif

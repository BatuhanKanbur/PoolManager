using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace PoolManager.Editor
{
    [InitializeOnLoad]
    public static class DependencyChecker
    {
        private const string DefineSymbol = "POOLMANAGER_INITIALIZED";
        private const string AsmdefPath = "Packages/com.batuhankanbur.poolmanager/Runtime/PoolManager.asmdef";
        private static string _currentInstallingPackage;
        private const string POOLMANAGER_STATE_KEY="POOLMANAGER_VERIFIED";
        private static readonly (string,string)[] RequiredPackages = new[]
        {
            ("com.cysharp.unitask","https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask")
        };
        private static readonly string[] RequiredReferences = new[]
        {
            "UniTask",
            "Unity.Addressables"
        };
        static DependencyChecker() => Run();
        private static void Run()
        {
            if (SessionState.GetBool(POOLMANAGER_STATE_KEY,false)) return;
            foreach (var requiredPackage in RequiredPackages)
            {
                if (IsPackageInstalled($"Packages/{requiredPackage.Item1}")) continue;
                Debug.Log($"[PoolManager] {requiredPackage} not found, installing...");
                _currentInstallingPackage = requiredPackage.Item1;
                Client.Add(requiredPackage.Item2);
                EditorApplication.update += WaitForPackageInstallation;
                return;
            }

            if (AsmdefManager.AsmdefHasVerified(AsmdefPath))
            {
                SessionState.SetBool(POOLMANAGER_STATE_KEY,true);
                Debug.Log("[PoolManager] All dependencies verified.");
                return;
            }
            Debug.Log($"[PoolManager] {AsmdefPath} not verified, installing...");
            DefineManager.RemoveDefineSymbols(DefineSymbol);
            Debug.Log($"[PoolManager] {DefineSymbol} created.");
            Debug.Log($"[PoolManager] {AsmdefPath} found, installing...");
            AsmdefManager.UpdateAsmdef(AsmdefPath, DefineSymbol, RequiredReferences);
            Debug.Log($"[PoolManager] Adding define symbols: {DefineSymbol}");
            DefineManager.AddDefineSymbols(DefineSymbol);
            Debug.Log("[PoolManager] All dependencies installed and verified.");
            SessionState.SetBool(POOLMANAGER_STATE_KEY,true);
        }

        private static void WaitForPackageInstallation()
        {
            if (!IsPackageInstalled($"Packages/{_currentInstallingPackage}")) return;
            Debug.Log("[PoolManager] AssetManager installed successfully.");
            EditorApplication.update -= WaitForPackageInstallation;
            Run();
        }
        private static bool IsPackageInstalled(string path) => Directory.Exists(path);
    }
}

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
        private const string AsmdefPath = "Packages/com.batuhankanbur.poolmanager/Runtime/PoolManager.Runtime.asmdef";
        private static string _currentInstallingPackage;
        private const string POOLMANAGER_STATE_KEY="POOLMANAGER_VERIFIED";
        private static readonly (string,string)[] RequiredPackages = new[]
        {
            ("com.cysharp.unitask","https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask")
        };
        private static readonly (string,string)[] RequiredReferences = new[]
        {
            ("UniTask","UNITASK_INITIALIZED"),
            ("UniTask.Addressables","UNITASK_INITIALIZED"),
            ("Unity.ResourceManager","UNITASK_INITIALIZED"),
            ("Unity.Addressables","ADDRESSABLES_INITIALIZED")
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
            Debug.Log($"[PoolManager] {AsmdefPath} not verified, installing...");
            DefineManager.RemoveDefineSymbols(DefineSymbol);
            Debug.Log($"[PoolManager] {DefineSymbol} created.");
            Debug.Log($"[PoolManager] {AsmdefPath} found, installing...");
            AsmdefManager.UpdateAsmdef(AsmdefPath, DefineSymbol, RequiredReferences.Select(r => r.Item1).ToArray());
            foreach (var requiredReference in RequiredReferences)
            {
                if (DefineManager.HasDefineSymbol(requiredReference.Item2)) continue;
                Debug.LogWarning($"[PoolManager] Missing define symbol: {requiredReference.Item2}. Please ensure that the required package is installed and its asmdef is correctly configured.");
                DefineManager.AddDefineSymbols(requiredReference.Item2);
                Debug.Log($"[PoolManager] Added missing define symbol: {requiredReference.Item2}" );
            }
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

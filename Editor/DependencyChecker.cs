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
        private const string DefineSymbol = "POOLMANAGER_";
        private const string AsmdefPath = "Packages/com.batuhankanbur.poolmanager/Runtime/PoolManager.asmdef";
        private static string _currentInstallingPackage;
        private static readonly (string,string)[] RequiredPackages = new[]
        {
            ("com.batuhankanbur.assetmanager","https://github.com/BatuhanKanbur/AssetManager.git")
        };
        private static readonly string[] RequiredReferences = new[]
        {
            "AssetManager",
            "UniTask",
            "Unity.Addressables"
        };
        static DependencyChecker()
        {
            // EditorApplication.update += Run;
        }

        private static void Run()
        {
            EditorApplication.update -= Run;
            foreach (var requiredPackage in RequiredPackages)
            {
                if (IsPackageInstalled($"Packages/{requiredPackage.Item1}")) continue;
                Debug.Log($"[PoolManager] {requiredPackage} not found, installing...");
                _currentInstallingPackage = requiredPackage.Item1;
                Client.Add(requiredPackage.Item2);
                EditorApplication.update += WaitForPackageInstallation;
                return;
            }
            if(AsmdefManager.AsmdefHasVerified(AsmdefPath)) return;
            var newDefineSymbol = DefineSymbol + DateTime.Now.ToString("yyyyMMddHHmmss");
            Debug.Log($"[PoolManager] {AsmdefPath} not verified, installing...");
            DefineManager.RemoveDefineSymbols(DefineSymbol);
            Debug.Log($"[PoolManager] {newDefineSymbol} created.");
            Debug.Log($"[PoolManager] {AsmdefPath} found, installing...");
            AsmdefManager.UpdateAsmdef(AsmdefPath, newDefineSymbol, RequiredReferences);
            Debug.Log($"[PoolManager] Adding define symbols: {newDefineSymbol}");
            DefineManager.AddDefineSymbols(newDefineSymbol);
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

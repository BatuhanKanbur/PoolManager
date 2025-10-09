#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace PoolManager.Editor
{
    [InitializeOnLoad]
    public static class AutoDependencyInstaller
    {
        private const string ManifestRelativePath = "../Packages/manifest.json";
        private const string RuntimeAsmdefPath = "Packages/com.batuhankanbur.poolmanager/Runtime/PoolManager.Runtime.asmdef";

        private const string UniTaskPackage = "com.cysharp.unitask";
        private const string UniTaskGitUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";
        private const string AddressablesPackage = "com.unity.addressables";

        static AutoDependencyInstaller()
        {
            EditorApplication.update += RunOnce;
        }

        private static void RunOnce()
        {
            EditorApplication.update -= RunOnce;
            EnsureDependencies();
        }

        private static void EnsureDependencies()
        {
            string manifestFullPath = Path.Combine(Application.dataPath, ManifestRelativePath);
            if (!File.Exists(manifestFullPath))
            {
                Debug.LogError("[PoolManager] manifest.json not found, cannot ensure dependencies.");
                return;
            }

            string text = File.ReadAllText(manifestFullPath);
            bool changed = false;

            if (!text.Contains(UniTaskPackage))
            {
                AddDependency(ref text, UniTaskPackage, UniTaskGitUrl);
                changed = true;
                Debug.Log("[PoolManager] Added UniTask dependency.");
            }

            if (!text.Contains(AddressablesPackage))
            {
                AddDependency(ref text, AddressablesPackage, "1.21.19");
                changed = true;
                Debug.Log("[PoolManager] Added Addressables dependency.");
            }

            if (changed)
            {
                File.WriteAllText(manifestFullPath, text);
                Debug.Log("[PoolManager] Dependencies added. Unity will refresh...");
                AssetDatabase.Refresh();
            }
            else
            {
                EditorApplication.delayCall += FixAsmdefReferences;
            }
        }

        private static void AddDependency(ref string manifest, string package, string version)
        {
            const string token = "\"dependencies\":";
            int idx = manifest.IndexOf(token);
            int brace = manifest.IndexOf('{', idx) + 1;
            string entry = $"\n    \"{package}\": \"{version}\",";
            manifest = manifest.Insert(brace, entry);
        }

        [DidReloadScripts]
        private static void FixAsmdefReferences()
        {
            if (!File.Exists(RuntimeAsmdefPath))
            {
                Debug.LogWarning("[PoolManager] Runtime asmdef not found to fix references.");
                return;
            }

            string asmdefText = File.ReadAllText(RuntimeAsmdefPath);
            if (!asmdefText.Contains("\"references\""))
            {
                asmdefText = asmdefText.Replace("}", ",\n  \"references\": []\n}");
            }

            var refs = ExtractReferences(asmdefText);
            var guids = FindAsmdefGuids(new[] { "Addressables", "ResourceManager", "UniTask" });

            bool changed = false;
            foreach (var guid in guids)
            {
                if (!refs.Contains(guid))
                {
                    refs.Add(guid);
                    changed = true;
                }
            }

            if (changed)
            {
                string newRefs = string.Join(",\n    ", refs.Select(g => $"\"GUID:{g}\""));
                int start = asmdefText.IndexOf("\"references\"");
                int openBracket = asmdefText.IndexOf('[', start);
                int closeBracket = asmdefText.IndexOf(']', openBracket);
                asmdefText = asmdefText.Remove(openBracket + 1, closeBracket - openBracket - 1);
                asmdefText = asmdefText.Insert(openBracket + 1, "\n    " + newRefs + "\n  ");
                File.WriteAllText(RuntimeAsmdefPath, asmdefText);
                Debug.Log("[PoolManager] Updated PoolManager.Runtime.asmdef references (GUIDs).");
                AssetDatabase.Refresh();
            }

            AddDefineSymbol("HAS_UNITASK");
            AddDefineSymbol("HAS_ADDRESSABLES");
        }

        private static List<string> ExtractReferences(string json)
        {
            var list = new List<string>();
            int start = json.IndexOf("\"references\"");
            if (start < 0) return list;
            int open = json.IndexOf('[', start);
            int close = json.IndexOf(']', open);
            string inside = json.Substring(open + 1, close - open - 1);
            foreach (var line in inside.Split('\n'))
            {
                var trimmed = line.Trim().Trim(',', '"', ' ');
                if (trimmed.StartsWith("GUID:"))
                    list.Add(trimmed.Replace("GUID:", ""));
            }
            return list;
        }

        private static List<string> FindAsmdefGuids(string[] keywords)
        {
            var result = new List<string>();
            foreach (var keyword in keywords)
            {
                string[] guids = AssetDatabase.FindAssets(keyword + " t:AssemblyDefinitionAsset");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (keywords.Any(k => name.Contains(k)))
                    {
                        result.Add(guid);
                        break;
                    }
                }
            }
            return result;
        }

        private static void AddDefineSymbol(string define)
        {
            foreach (var target in (BuildTargetGroup[])System.Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (target == BuildTargetGroup.Unknown) continue;
                string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
                if (!defines.Split(';').Contains(define))
                {
                    if (!string.IsNullOrEmpty(defines)) defines += ";";
                    defines += define;
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
                }
            }
        }
    }
}
#endif

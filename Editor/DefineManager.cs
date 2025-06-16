using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PoolManager.Editor
{
    public static class DefineManager
    {
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
        public static bool HasDefineSymbol(string symbol)
        {
            return Groups.All(group => PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';').Contains(symbol));
        }
        public static void AddDefineSymbols(params string[] defineSymbols)
        {
            foreach (var defineSymbol in defineSymbols)
            {
                foreach (var group in Groups)
                {
                    var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                    if (symbols.Contains(defineSymbol)) continue;
                    symbols = string.IsNullOrEmpty(symbols) ? defineSymbol : symbols + ";" + defineSymbol;
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, symbols);
                }

                Debug.Log("[PoolManager] Define symbol added : " + defineSymbol);
            }
        }
        public static void RemoveDefineSymbols(params string[] defineSymbols)
        {
            foreach (var defineSymbol in defineSymbols)
            {
                foreach (var group in Groups)
                {
                    var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                    if (!symbols.Contains(defineSymbol)) continue;
                    var symbolList = symbols.Split(';').ToList();
                    symbolList.RemoveAll(s => s == defineSymbol);
                    var newSymbols = string.Join(";", symbolList);
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newSymbols);
                }
                Debug.Log("[PoolManager] Define symbol removed : " + defineSymbol);
            }
        }
      
    }
}

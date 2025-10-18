#if !POOLMANAGER_INITIALIZED
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace PoolManager.Runtime
{
    internal static class PoolManager
    {
        private const string ERROR_MSG = 
            "[PoolManager] Not initialized. Define POOLMANAGER_INITIALIZED or install the PoolManager package.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Warn() => Debug.LogError(ERROR_MSG);

        public static Task GetObjectAsync(AssetReference assetRef, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return Task.FromResult<GameObject>(null);
        }

        public static Task<GameObject> GetObjectAsync(string key, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return Task.FromResult<GameObject>(null);
        }

        public static GameObject GetObjectSync(AssetReference assetRef, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return null;
        }

        public static GameObject GetObjectSync(string key, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return null;
        }

        public static Task CreatePoolAsync(AssetReference assetRef, int count, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return Task.CompletedTask;
        }

        public static Task CreatePoolAsync(string key, int count, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return Task.CompletedTask;
        }

        public static void CreatePoolSync(AssetReference assetRef, int count, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
        }

        public static void CreatePoolSync(string key, int count, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
        }
    }

    internal static class PoolExtensions
    {
        public static GameObject SetParent(this GameObject go, Transform parent, bool worldPositionStays = false)
        {
            Debug.LogError("[PoolManager] Dummy extension invoked.");
            return go;
        }

        public static Task<GameObject> SetParent(this Task<GameObject> task, Transform parent, bool worldPositionStays = false)
        {
            Debug.LogError("[PoolManager] Dummy extension invoked.");
            return Task.FromResult<GameObject>(null);
        }

        // Aynı şekilde diğer extension’lar:
        public static GameObject SetPosition(this GameObject go, Vector3 pos) { Debug.LogError("[PoolManager] Dummy extension invoked."); return go; }
        public static Task<GameObject> SetPosition(this Task<GameObject> task, Vector3 pos) { Debug.LogError("[PoolManager] Dummy extension invoked."); return UniTask.FromResult<GameObject>(null); }
        public static GameObject SetRotation(this GameObject go, Quaternion rot) { Debug.LogError("[PoolManager] Dummy extension invoked."); return go; }
        public static Task<GameObject> SetRotation(this Task<GameObject> task, Quaternion rot) { Debug.LogError("[PoolManager] Dummy extension invoked."); return UniTask.FromResult<GameObject>(null); }
    }
}
#endif

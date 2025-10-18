#if !POOLMANAGER_INITIALIZED
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace PoolManager.Runtime
{
    #region === Dummy UniTask replacements ===

    // Eğer UniTask yüklü değilse kendi minimal sahte UniTask yapımızı ekliyoruz.
    // Bu, sadece compile-time uyumluluk sağlar.
    public readonly struct UniTask<T>
    {
        private readonly T _result;
        public UniTask(T result) => _result = result;
        public T Result => _result;
        public static implicit operator UniTask<T>(T value) => new UniTask<T>(value);
        public UniTaskAwaiter<T> GetAwaiter() => new UniTaskAwaiter<T>(_result);
    }

    public readonly struct UniTask
    {
        public static readonly UniTask CompletedTask = new UniTask();
        public static UniTask<T> FromResult<T>(T result) => new UniTask<T>(result);
    }

    public readonly struct UniTaskAwaiter<T> : System.Runtime.CompilerServices.INotifyCompletion
    {
        private readonly T _result;
        public UniTaskAwaiter(T result) => _result = result;
        public bool IsCompleted => true;
        public T GetResult() => _result;
        public void OnCompleted(Action continuation) => continuation?.Invoke();
    }

    #endregion

    public static class PoolManager
    {
        private const string ERROR_MSG =
            "[PoolManager] Not initialized. Define POOLMANAGER_INITIALIZED or install the PoolManager package.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Warn() => Debug.LogError(ERROR_MSG);

        public static UniTask<GameObject> GetObjectAsync(AssetReference assetRef, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return UniTask.FromResult<GameObject>(null);
        }

        public static UniTask<GameObject> GetObjectAsync(string key, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return UniTask.FromResult<GameObject>(null);
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

        public static UniTask CreatePoolAsync(AssetReference assetRef, int count, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return UniTask.CompletedTask;
        }

        public static UniTask CreatePoolAsync(string key, int count, Scene? targetScene = null)
        {
            Debug.LogError(ERROR_MSG);
            return UniTask.CompletedTask;
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

    public static class PoolExtensions
    {
        // === GameObject chain ===
        public static GameObject SetParent(this GameObject go, Transform parent, bool worldPositionStays = false)
        {
            Debug.LogError("[PoolManager] Dummy extension invoked.");
            return go;
        }

        public static GameObject SetPosition(this GameObject go, Vector3 pos)
        {
            Debug.LogError("[PoolManager] Dummy extension invoked.");
            return go;
        }

        public static GameObject SetRotation(this GameObject go, Quaternion rot)
        {
            Debug.LogError("[PoolManager] Dummy extension invoked.");
            return go;
        }

        // === UniTask<GameObject> chain ===
        public static UniTask<GameObject> SetParent(this UniTask<GameObject> task, Transform parent, bool worldPositionStays = false)
        {
            Debug.LogError("[PoolManager] Dummy extension invoked.");
            return UniTask.FromResult<GameObject>(null);
        }

        public static UniTask<GameObject> SetPosition(this UniTask<GameObject> task, Vector3 pos)
        {
            Debug.LogError("[PoolManager] Dummy extension invoked.");
            return UniTask.FromResult<GameObject>(null);
        }

        public static UniTask<GameObject> SetRotation(this UniTask<GameObject> task, Quaternion rot)
        {
            Debug.LogError("[PoolManager] Dummy extension invoked.");
            return UniTask.FromResult<GameObject>(null);
        }
    }
}
#endif

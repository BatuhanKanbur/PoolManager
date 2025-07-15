/// <summary>
/// Developed by Batuhan Kanbur, this is a fully static, MonoBehaviour-free,
/// UniTask-powered, and AssetReference-based modern Object Pool system for Unity.
///
/// - Requires no ScriptableObject, interfaces, or extra components,
///   managing pools efficiently via simple active/inactive state control.
/// - Fully compatible with Addressables through AssetReference usage.
/// - Thread-safe with SemaphoreSlim and async UniTask-based pool access.
/// - Designed with performance and ease of use as top priorities.
///
/// "True engineering is solving complex problems with simple and sustainable solutions."
/// — Batuhan Kanbur, Game Developer.
///
/// This package is optimized for professionals seeking to boost productivity
/// by combining clean architecture with cutting-edge Unity technologies.
/// </summary>
///
using System;
using System.Collections.Generic;
using System.Threading;
using AssetManager.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PoolManager.Runtime
{
    public static class PoolManager
    {
        private static readonly Dictionary<AssetReference, Queue<GameObject>> ObjectPools = new();
        private static readonly Dictionary<AssetReference, SemaphoreSlim> PoolLocks = new();

        private static readonly Dictionary<string, Queue<GameObject>> StringPools = new();
        private static readonly Dictionary<string, SemaphoreSlim> StringPoolLocks = new();

        #region Initialization
        private static bool _initialized;
        static PoolManager() => Init();
        private static void Init()
        {
            if (_initialized)
            {
                if (ObjectPools.Count == 0 || !SceneManager.GetActiveScene().isLoaded)
                {
                    ObjectPools.Clear();
                    PoolLocks.Clear();
                    StringPools.Clear();
                    StringPoolLocks.Clear();
                    _initialized = false;
                }
                else return;
            }

            SceneManager.activeSceneChanged += OnSceneChanged;
            Application.quitting += Dispose;
            _initialized = true;
        }

        private static void Dispose()
        {
            if (!_initialized) return;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Application.quitting -= Dispose;
            ObjectPools.Clear();
            PoolLocks.Clear();
            StringPools.Clear();
            StringPoolLocks.Clear();
            _initialized = false;
        }

        private static void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            Dispose();
            Resources.UnloadUnusedAssets();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Dispose();

        private static SemaphoreSlim GetOrCreateLock(AssetReference assetReference)
        {
            if (PoolLocks.TryGetValue(assetReference, out var semaphore)) return semaphore;
            semaphore = new SemaphoreSlim(1, 1);
            PoolLocks.Add(assetReference, semaphore);
            return semaphore;
        }

        private static SemaphoreSlim GetOrCreateLock(string key)
        {
            if (StringPoolLocks.TryGetValue(key, out var semaphore)) return semaphore;
            semaphore = new SemaphoreSlim(1, 1);
            StringPoolLocks.Add(key, semaphore);
            return semaphore;
        }
        #endregion

        #region AssetReference Async
        public static async UniTask CreatePool(AssetReference assetReference, int initialSize = 10)
        {
            var semaphore = GetOrCreateLock(assetReference);
            await semaphore.WaitAsync();
            try
            {
                for (var i = 0; i < initialSize; i++)
                    await AddObjectToPool(assetReference);
            }
            finally { semaphore.Release(); }
        }

        private static async UniTask AddObjectToPool(AssetReference assetReference)
        {
            var prefab = await AssetManager<GameObject>.LoadAsset(assetReference);
            if (!prefab)
            {
                Debug.LogError($"[PoolManager] Failed to load asset: {assetReference}");
                return;
            }
            var instance = Object.Instantiate(prefab, Vector3.one * -100f, Quaternion.identity);
            instance.SetActive(false);

            if (!ObjectPools.TryGetValue(assetReference, out var pool))
                ObjectPools.Add(assetReference, pool = new Queue<GameObject>());

            pool.Enqueue(instance);
        }

        public static async UniTask<GameObject> GetObjectAsync(AssetReference assetReference)
        {
            var semaphore = GetOrCreateLock(assetReference);
            await semaphore.WaitAsync();
            try
            {
                if (!ObjectPools.TryGetValue(assetReference, out var pool))
                    ObjectPools.Add(assetReference, pool = new Queue<GameObject>());

                var count = pool.Count;
                for (int i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    if (!obj) continue;
                    if (!obj.activeSelf)
                    {
                        obj.SetActive(true);
                        pool.Enqueue(obj);
                        return obj;
                    }
                    pool.Enqueue(obj);
                }

                await AddObjectToPool(assetReference);

                GameObject newObj = null;
                while (pool.Count > 0)
                {
                    var temp = pool.Dequeue();
                    if (!temp) continue;
                    newObj = temp;
                    break;
                }

                if (newObj)
                {
                    newObj.SetActive(true);
                    pool.Enqueue(newObj);
                    return newObj;
                }

                Debug.LogError($"[PoolManager] Failed to get or create object for: {assetReference}");
                return null;
            }
            finally { semaphore.Release(); }
        }

        public static void ReleaseObject(AssetReference assetReference, GameObject obj)
        {
            if (!obj) return;
            obj.SetActive(false);

            if (!ObjectPools.TryGetValue(assetReference, out var pool))
                ObjectPools.Add(assetReference, pool = new Queue<GameObject>());

            pool.Enqueue(obj);
        }

        [Obsolete("Blocking call, use with caution.")]
        public static GameObject GetObjectSync(AssetReference assetReference)
        {
            var semaphore = GetOrCreateLock(assetReference);
            semaphore.Wait();
            try
            {
                if (!ObjectPools.TryGetValue(assetReference, out var pool))
                    ObjectPools.Add(assetReference, pool = new Queue<GameObject>());

                foreach (var obj in pool)
                {
                    if (!obj || obj.activeSelf) continue;
                    obj.SetActive(true);
                    return obj;
                }

                AddObjectToPool(assetReference).GetAwaiter().GetResult();

                while (pool.Count > 0)
                {
                    var temp = pool.Dequeue();
                    if (!temp) continue;
                    temp.SetActive(true);
                    pool.Enqueue(temp);
                    return temp;
                }

                Debug.LogError($"[PoolManager] Failed to sync instantiate: {assetReference}");
                return null;
            }
            finally { semaphore.Release(); }
        }
        #endregion

        #region String Async + Sync
        public static async UniTask CreatePool(string key, int initialSize = 10)
        {
            var semaphore = GetOrCreateLock(key);
            await semaphore.WaitAsync();
            try
            {
                for (int i = 0; i < initialSize; i++)
                    await AddObjectToPool(key);
            }
            finally { semaphore.Release(); }
        }

        private static async UniTask AddObjectToPool(string key)
        {
            var prefab = await AssetManager<GameObject>.LoadAsset(key);
            if (!prefab)
            {
                Debug.LogError($"[PoolManager] Failed to load asset: {key}");
                return;
            }

            var instance = Object.Instantiate(prefab, Vector3.one * -100f, Quaternion.identity);
            instance.SetActive(false);

            if (!StringPools.TryGetValue(key, out var pool))
                StringPools.Add(key, pool = new Queue<GameObject>());

            pool.Enqueue(instance);
        }

        public static async UniTask<GameObject> GetObjectAsync(string key)
        {
            var semaphore = GetOrCreateLock(key);
            await semaphore.WaitAsync();
            try
            {
                if (!StringPools.TryGetValue(key, out var pool))
                    StringPools.Add(key, pool = new Queue<GameObject>());

                var count = pool.Count;
                for (int i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    if (!obj) continue;
                    if (!obj.activeSelf)
                    {
                        obj.SetActive(true);
                        pool.Enqueue(obj);
                        return obj;
                    }
                    pool.Enqueue(obj);
                }

                await AddObjectToPool(key);

                GameObject newObj = null;
                while (pool.Count > 0)
                {
                    var temp = pool.Dequeue();
                    if (!temp) continue;
                    newObj = temp;
                    break;
                }

                if (newObj)
                {
                    newObj.SetActive(true);
                    pool.Enqueue(newObj);
                    return newObj;
                }

                Debug.LogError($"[PoolManager] Failed to get or create object for key: {key}");
                return null;
            }
            finally { semaphore.Release(); }
        }

        public static void ReleaseObject(string key, GameObject obj)
        {
            if (!obj) return;
            obj.SetActive(false);

            if (!StringPools.TryGetValue(key, out var pool))
                StringPools.Add(key, pool = new Queue<GameObject>());

            pool.Enqueue(obj);
        }

        [Obsolete("Blocking call, use with caution.")]
        public static GameObject GetObjectSync(string key)
        {
            var semaphore = GetOrCreateLock(key);
            semaphore.Wait();
            try
            {
                if (!StringPools.TryGetValue(key, out var pool))
                    StringPools.Add(key, pool = new Queue<GameObject>());

                foreach (var obj in pool)
                {
                    if (!obj || obj.activeSelf) continue;
                    obj.SetActive(true);
                    return obj;
                }

                AddObjectToPool(key).GetAwaiter().GetResult();

                while (pool.Count > 0)
                {
                    var temp = pool.Dequeue();
                    if (!temp) continue;
                    temp.SetActive(true);
                    pool.Enqueue(temp);
                    return temp;
                }

                Debug.LogError($"[PoolManager] Failed to sync instantiate: {key}");
                return null;
            }
            finally { semaphore.Release(); }
        }
        #endregion

        #region Extensions
        public static async UniTask<GameObject> SetPosition(this UniTask<GameObject> task, Vector3 position)
        {
            var go = await task;
            go.transform.position = position;
            return go;
        }
        public static async UniTask<GameObject> SetRotation(this UniTask<GameObject> task, Quaternion rotation)
        {
            var go = await task;
            go.transform.rotation = rotation;
            return go;
        }
        public static async UniTask<GameObject> SetPositionAndRotation(this UniTask<GameObject> task, Transform target)
        {
            var go = await task;
            go.transform.SetPositionAndRotation(target.position, target.rotation);
            return go;
        }
        public static async UniTask<GameObject> SetParent(this UniTask<GameObject> task, Transform parent)
        {
            var go = await task;
            go.transform.SetParent(parent);
            return go;
        }
        public static async UniTask<T> GetComponent<T>(this UniTask<GameObject> task) where T : Component
        {
            var go = await task;
            return go.GetComponent<T>();
        }
        public static async UniTask<(GameObject, T)> GetWithComponent<T>(this UniTask<GameObject> task) where T : Component
        {
            var go = await task;
            return (go, go.GetComponent<T>());
        }
        public static void SetPosition(this GameObject go, Vector3 position) => go.transform.position = position;
        public static void SetRotation(this GameObject go, Quaternion rotation) => go.transform.rotation = rotation;
        public static void SetPositionAndRotation(this GameObject go, Transform target) => go.transform.SetPositionAndRotation(target.position, target.rotation);
        public static void SetPositionAndRotation(this GameObject go, Vector3 position, Quaternion rotation) => go.transform.SetPositionAndRotation(position, rotation);
        public static void SetParent(this GameObject go, Transform parent) => go.transform.SetParent(parent);
        public static T GetComponent<T>(this GameObject go) where T : Component
        {
            return go.GetComponent<T>();
        }
        #endregion
    }
}


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
#if POOLMANAGER_INITIALIZED
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

        #region Initialization and Cleanup
        private static bool _initialized;
        static PoolManager() => Init();
        private static void Init()
        {
            if (_initialized) return;
            SceneManager.activeSceneChanged += OnSceneChanged;
            Application.quitting += Dispose;
            _initialized = true;
        }
        private static void Dispose()
        {
            if (!_initialized) return;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Application.quitting -= Dispose;
            foreach (var pool in ObjectPools.Values)
            {
                foreach (var obj in pool)
                {
                    if (obj)
                    {
                        Object.Destroy(obj);
                    }
                }
            }

            ObjectPools.Clear();
            PoolLocks.Clear();
            _initialized = false;
        }
        private static void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            Dispose();
            Resources.UnloadUnusedAssets();
        }
        private static SemaphoreSlim GetOrCreateLock(AssetReference assetReference)
        {
            if (PoolLocks.TryGetValue(assetReference, out var semaphore)) return semaphore;
            semaphore = new SemaphoreSlim(1, 1);
            PoolLocks.Add(assetReference, semaphore);
            return semaphore;
        }
        public static async UniTask CreatePool(AssetReference assetReference, int initialSize = 10)
        {
            var semaphore = GetOrCreateLock(assetReference);
            await semaphore.WaitAsync();
            try
            {
                for (var i = 0; i < initialSize; i++)
                {
                    await AddObjectToPool(assetReference);
                }
            }
            finally
            {
                semaphore.Release();
            }
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
            {
                pool = new Queue<GameObject>();
                ObjectPools.Add(assetReference, pool);
            }
            pool.Enqueue(instance);
        }
        public static void ReleaseObject(AssetReference assetReference, GameObject obj)
        {
            if (!obj) return;
            obj.SetActive(false);
            if (!ObjectPools.TryGetValue(assetReference, out var pool))
            {
                pool = new Queue<GameObject>();
                ObjectPools.Add(assetReference, pool);
            }
            pool.Enqueue(obj);
        }

        #endregion

        #region Synchronous Methods
        [Obsolete("GetObjectSync is a blocking call and may freeze the main thread. Use with caution!", false)]
        public static GameObject GetObjectSync(AssetReference assetReference)
        {
            var semaphore = GetOrCreateLock(assetReference);
            semaphore.Wait();
            try
            {
                if (!ObjectPools.TryGetValue(assetReference, out var pool))
                {
                    pool = new Queue<GameObject>();
                    ObjectPools.Add(assetReference, pool);
                }
                var count = pool.Count;
                for (var i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    if (!obj)
                        continue;
                    if (!obj.activeSelf)
                    {
                        obj.SetActive(true);
                        pool.Enqueue(obj);
                        return obj;
                    }
                    pool.Enqueue(obj);
                }

                for (var i = 0; i < 3; i++)
                    AddObjectToPool(assetReference).GetAwaiter().GetResult();
                GameObject newObj = null;
                if (ObjectPools.TryGetValue(assetReference, out pool))
                {
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
                }
                Debug.LogError($"[PoolManager] Failed to create object synchronously for: {assetReference}");
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }
        public static void SetPosition(this GameObject gameObject, Vector3 position)
        {
            gameObject.transform.position = position;
        }
        public static void SetRotation(this GameObject gameObject, Quaternion rotation)
        {
            gameObject.transform.rotation = rotation;
        }
        public static void SetPositionAndRotation(this GameObject gameObject, Transform targetTransform)
        {
            gameObject.transform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
        }
        public static void SetPositionAndRotation(this GameObject gameObject, Vector3 position, Quaternion rotation)
        {
            gameObject.transform.SetPositionAndRotation(position, rotation);
        }
        public static void SetParent(this GameObject gameObject, Transform parent)
        {
            gameObject.transform.SetParent(parent);
        }
        #endregion

        #region Asynchronous Methods
        public static async UniTask<GameObject> GetObjectAsync(AssetReference assetReference)
        {
            var semaphore = GetOrCreateLock(assetReference);
            await semaphore.WaitAsync();
            try
            {
                if (!ObjectPools.TryGetValue(assetReference, out var pool))
                {
                    pool = new Queue<GameObject>();
                    ObjectPools.Add(assetReference, pool);
                }
                var count = pool.Count;
                for (var i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    if (!obj)
                    {
                        continue;
                    }
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
                if (ObjectPools.TryGetValue(assetReference, out pool))
                {
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
                }
                Debug.LogError($"[PoolManager] Failed to get or create object for: {assetReference}");
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }
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
        public static async UniTask<GameObject> SetPositionAndRotation(this UniTask<GameObject> task, Transform targetTransform)
        {
            var go = await task;
            go.transform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
            return go;
        }
        public static async UniTask<GameObject> SetPositionAndRotation(this UniTask<GameObject> task, Vector3 position,Quaternion rotation)
        {
            var go = await task;
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }
        public static async UniTask<GameObject> SetParent(this UniTask<GameObject> task, Transform parent)
        {
            var go = await task;
            go.transform.SetParent(parent);
            return go;
        }
        #endregion
    }
}
#endif

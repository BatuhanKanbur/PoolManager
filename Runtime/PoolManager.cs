using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PoolManager.Runtime
{
    internal class PoolableObject : MonoBehaviour
    {
        public AssetReference AssetRef;
        public string StringKey;
        public bool IsAssetReference;

        private void OnDisable()
        {
            if (IsAssetReference && AssetRef != null)
            {
                PoolManager.OnObjectDisabled(AssetRef, gameObject);
            }
            else if (!string.IsNullOrEmpty(StringKey))
            {
                PoolManager.OnObjectDisabled(StringKey, gameObject);
            }
        }
    }

    public static class PoolManager
    {
        private static readonly Dictionary<Scene, Dictionary<AssetReference, Queue<GameObject>>> SceneObjectPools = new();
        private static readonly Dictionary<Scene, Dictionary<AssetReference, SemaphoreSlim>> ScenePoolLocks = new();
        private static readonly Dictionary<Scene, Dictionary<AssetReference, HashSet<GameObject>>> SceneActiveObjects = new();

        private static readonly Dictionary<Scene, Dictionary<string, Queue<GameObject>>> SceneStringPools = new();
        private static readonly Dictionary<Scene, Dictionary<string, SemaphoreSlim>> SceneStringPoolLocks = new();
        private static readonly Dictionary<Scene, Dictionary<string, HashSet<GameObject>>> SceneStringActiveObjects = new();

        #region Initialization
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _initialized = false;
            SceneObjectPools.Clear();
            ScenePoolLocks.Clear();
            SceneActiveObjects.Clear();
            SceneStringPools.Clear();
            SceneStringPoolLocks.Clear();
            SceneStringActiveObjects.Clear();
        }

        static PoolManager() => Init();

        private static void Init()
        {
            if (_initialized) return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Application.quitting += Dispose;
            _initialized = true;
        }

        private static void Dispose()
        {
            if (!_initialized) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Application.quitting -= Dispose;

            SceneObjectPools.Clear();
            ScenePoolLocks.Clear();
            SceneActiveObjects.Clear();
            SceneStringPools.Clear();
            SceneStringPoolLocks.Clear();
            SceneStringActiveObjects.Clear();

            _initialized = false;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Init();

            if (!SceneObjectPools.ContainsKey(scene))
                SceneObjectPools[scene] = new();

            if (!ScenePoolLocks.ContainsKey(scene))
                ScenePoolLocks[scene] = new();

            if (!SceneActiveObjects.ContainsKey(scene))
                SceneActiveObjects[scene] = new();

            if (!SceneStringPools.ContainsKey(scene))
                SceneStringPools[scene] = new();

            if (!SceneStringPoolLocks.ContainsKey(scene))
                SceneStringPoolLocks[scene] = new();

            if (!SceneStringActiveObjects.ContainsKey(scene))
                SceneStringActiveObjects[scene] = new();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (SceneObjectPools.TryGetValue(scene, out var objectPools))
            {
                foreach (var pool in objectPools.Values)
                {
                    while (pool.Count > 0)
                    {
                        var obj = pool.Dequeue();
                        if (obj) Object.Destroy(obj);
                    }
                }
                SceneObjectPools.Remove(scene);
            }

            if (ScenePoolLocks.TryGetValue(scene, out var locks))
            {
                foreach (var semaphore in locks.Values)
                {
                    semaphore?.Dispose();
                }
                ScenePoolLocks.Remove(scene);
            }

            if (SceneActiveObjects.ContainsKey(scene))
                SceneActiveObjects.Remove(scene);

            if (SceneStringPools.TryGetValue(scene, out var stringPools))
            {
                foreach (var pool in stringPools.Values)
                {
                    while (pool.Count > 0)
                    {
                        var obj = pool.Dequeue();
                        if (obj) Object.Destroy(obj);
                    }
                }
                SceneStringPools.Remove(scene);
            }

            if (SceneStringPoolLocks.TryGetValue(scene, out var stringLocks))
            {
                foreach (var semaphore in stringLocks.Values)
                {
                    semaphore?.Dispose();
                }
                SceneStringPoolLocks.Remove(scene);
            }

            if (SceneStringActiveObjects.ContainsKey(scene))
                SceneStringActiveObjects.Remove(scene);

            Resources.UnloadUnusedAssets();
        }

        private static SemaphoreSlim GetOrCreateLock(Scene scene, AssetReference assetReference)
        {
            if (!ScenePoolLocks.TryGetValue(scene, out var locks))
                ScenePoolLocks[scene] = locks = new();

            if (!locks.TryGetValue(assetReference, out var semaphore))
                locks[assetReference] = semaphore = new(1, 1);

            return semaphore;
        }

        private static SemaphoreSlim GetOrCreateLock(Scene scene, string key)
        {
            if (!SceneStringPoolLocks.TryGetValue(scene, out var locks))
                SceneStringPoolLocks[scene] = locks = new();

            if (!locks.TryGetValue(key, out var semaphore))
                locks[key] = semaphore = new(1, 1);

            return semaphore;
        }

        private static Scene GetTargetScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!SceneObjectPools.ContainsKey(scene))
                OnSceneLoaded(scene, LoadSceneMode.Single);
            return scene;
        }

        internal static void OnObjectDisabled(AssetReference assetReference, GameObject obj)
        {
            if (!obj) return;
            var scene = obj.scene;
            if (SceneActiveObjects.TryGetValue(scene, out var sceneActive))
                if (sceneActive.TryGetValue(assetReference, out var activeSet))
                    activeSet.Remove(obj);
        }

        internal static void OnObjectDisabled(string key, GameObject obj)
        {
            if (!obj) return;
            var scene = obj.scene;
            if (SceneStringActiveObjects.TryGetValue(scene, out var sceneActive))
                if (sceneActive.TryGetValue(key, out var activeSet))
                    activeSet.Remove(obj);
        }
        #endregion

        #region AssetReference Async
        public static async UniTask CreatePool(AssetReference assetReference, int initialSize = 10, Scene? targetScene = null)
        {
            Init();
            var scene = targetScene ?? GetTargetScene();
            var semaphore = GetOrCreateLock(scene, assetReference);
            await semaphore.WaitAsync();
            try
            {
                for (int i = 0; i < initialSize; i++)
                    await AddObjectToPool(scene, assetReference);
            }
            finally { semaphore.Release(); }
        }

        private static async UniTask AddObjectToPool(Scene scene, AssetReference assetReference)
        {
            var loadAssetOp = Addressables.LoadAssetAsync<GameObject>(assetReference);
            await loadAssetOp.Task;
            var prefab = loadAssetOp.Result;
            if (!prefab)
            {
                Debug.LogError($"[PoolManager] Failed to load asset: {assetReference}");
                return;
            }

            var instance = Object.Instantiate(prefab, Vector3.one * -100f, Quaternion.identity);
            var poolable = instance.GetComponent<PoolableObject>() ?? instance.AddComponent<PoolableObject>();
            poolable.AssetRef = assetReference;
            poolable.IsAssetReference = true;
            instance.SetActive(false);
            SceneManager.MoveGameObjectToScene(instance, scene);

            if (!SceneObjectPools.TryGetValue(scene, out var scenePools))
                SceneObjectPools[scene] = scenePools = new();

            if (!scenePools.TryGetValue(assetReference, out var pool))
                scenePools[assetReference] = pool = new();

            pool.Enqueue(instance);
        }

        public static async UniTask<GameObject> GetObjectAsync(AssetReference assetReference, Scene? targetScene = null)
        {
            Init();
            var scene = targetScene ?? GetTargetScene();
            var semaphore = GetOrCreateLock(scene, assetReference);
            await semaphore.WaitAsync();
            try
            {
                if (!SceneObjectPools.TryGetValue(scene, out var scenePools))
                    SceneObjectPools[scene] = scenePools = new();

                if (!SceneActiveObjects.TryGetValue(scene, out var sceneActive))
                    SceneActiveObjects[scene] = sceneActive = new();

                if (!scenePools.TryGetValue(assetReference, out var pool))
                    scenePools[assetReference] = pool = new();

                if (!sceneActive.TryGetValue(assetReference, out var activeSet))
                    sceneActive[assetReference] = activeSet = new();

                GameObject selected = null;
                int count = pool.Count;
                for (int i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    if (!obj)
                        continue;

                    if (!obj.activeSelf && !activeSet.Contains(obj))
                    {
                        selected = obj;
                        pool.Enqueue(obj);
                        break;
                    }

                    pool.Enqueue(obj);
                }

                if (selected == null)
                {
                    await AddObjectToPool(scene, assetReference);
                    selected = pool.LastOrDefault();
                }

                if (selected)
                {
                    selected.SetActive(true);
                    activeSet.Add(selected);
                    return selected;
                }

                Debug.LogError($"[PoolManager] Failed to get or create object for: {assetReference} in scene: {scene.name}");
                return null;
            }
            finally { semaphore.Release(); }
        }
        #endregion

        #region String Async
        public static async UniTask CreatePool(string key, int initialSize = 10, Scene? targetScene = null)
        {
            Init();
            var scene = targetScene ?? GetTargetScene();
            var semaphore = GetOrCreateLock(scene, key);
            await semaphore.WaitAsync();
            try
            {
                for (int i = 0; i < initialSize; i++)
                    await AddObjectToPool(scene, key);
            }
            finally { semaphore.Release(); }
        }

        private static async UniTask AddObjectToPool(Scene scene, string key)
        {
            var loadAssetOp = Addressables.LoadAssetAsync<GameObject>(key);
            await loadAssetOp.Task;
            var prefab = loadAssetOp.Result;
            if (!prefab)
            {
                Debug.LogError($"[PoolManager] Failed to load asset: {key}");
                return;
            }

            var instance = Object.Instantiate(prefab, Vector3.one * -100f, Quaternion.identity);
            var poolable = instance.GetComponent<PoolableObject>() ?? instance.AddComponent<PoolableObject>();
            poolable.StringKey = key;
            poolable.IsAssetReference = false;
            instance.SetActive(false);
            SceneManager.MoveGameObjectToScene(instance, scene);

            if (!SceneStringPools.TryGetValue(scene, out var scenePools))
                SceneStringPools[scene] = scenePools = new();

            if (!scenePools.TryGetValue(key, out var pool))
                scenePools[key] = pool = new();

            pool.Enqueue(instance);
        }

        public static async UniTask<GameObject> GetObjectAsync(string key, Scene? targetScene = null)
        {
            Init();
            var scene = targetScene ?? GetTargetScene();
            var semaphore = GetOrCreateLock(scene, key);
            await semaphore.WaitAsync();
            try
            {
                if (!SceneStringPools.TryGetValue(scene, out var scenePools))
                    SceneStringPools[scene] = scenePools = new();

                if (!SceneStringActiveObjects.TryGetValue(scene, out var sceneActive))
                    SceneStringActiveObjects[scene] = sceneActive = new();

                if (!scenePools.TryGetValue(key, out var pool))
                    scenePools[key] = pool = new();

                if (!sceneActive.TryGetValue(key, out var activeSet))
                    sceneActive[key] = activeSet = new();

                GameObject selected = null;
                int count = pool.Count;
                for (int i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    if (!obj)
                        continue;

                    if (!obj.activeSelf && !activeSet.Contains(obj))
                    {
                        selected = obj;
                        pool.Enqueue(obj);
                        break;
                    }

                    pool.Enqueue(obj);
                }

                if (selected == null)
                {
                    await AddObjectToPool(scene, key);
                    selected = pool.LastOrDefault();
                }

                if (selected)
                {
                    selected.SetActive(true);
                    activeSet.Add(selected);
                    return selected;
                }

                Debug.LogError($"[PoolManager] Failed to get or create object for key: {key} in scene: {scene.name}");
                return null;
            }
            finally { semaphore.Release(); }
        }
        #endregion
    }
}

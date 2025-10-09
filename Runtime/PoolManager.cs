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
                SceneObjectPools[scene] = new Dictionary<AssetReference, Queue<GameObject>>();
            
            if (!ScenePoolLocks.ContainsKey(scene))
                ScenePoolLocks[scene] = new Dictionary<AssetReference, SemaphoreSlim>();
            
            if (!SceneActiveObjects.ContainsKey(scene))
                SceneActiveObjects[scene] = new Dictionary<AssetReference, HashSet<GameObject>>();
            
            if (!SceneStringPools.ContainsKey(scene))
                SceneStringPools[scene] = new Dictionary<string, Queue<GameObject>>();
            
            if (!SceneStringPoolLocks.ContainsKey(scene))
                SceneStringPoolLocks[scene] = new Dictionary<string, SemaphoreSlim>();
            
            if (!SceneStringActiveObjects.ContainsKey(scene))
                SceneStringActiveObjects[scene] = new Dictionary<string, HashSet<GameObject>>();
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
            {
                locks = new Dictionary<AssetReference, SemaphoreSlim>();
                ScenePoolLocks[scene] = locks;
            }

            if (locks.TryGetValue(assetReference, out var semaphore)) return semaphore;
            semaphore = new SemaphoreSlim(1, 1);
            locks.Add(assetReference, semaphore);
            return semaphore;
        }

        private static SemaphoreSlim GetOrCreateLock(Scene scene, string key)
        {
            if (!SceneStringPoolLocks.TryGetValue(scene, out var locks))
            {
                locks = new Dictionary<string, SemaphoreSlim>();
                SceneStringPoolLocks[scene] = locks;
            }

            if (locks.TryGetValue(key, out var semaphore)) return semaphore;
            semaphore = new SemaphoreSlim(1, 1);
            locks.Add(key, semaphore);
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
            {
                if (sceneActive.TryGetValue(assetReference, out var activeSet))
                {
                    activeSet.Remove(obj);
                }
            }
        }

        internal static void OnObjectDisabled(string key, GameObject obj)
        {
            if (!obj) return;
            
            var scene = obj.scene;

            if (SceneStringActiveObjects.TryGetValue(scene, out var sceneActive))
            {
                if (sceneActive.TryGetValue(key, out var activeSet))
                {
                    activeSet.Remove(obj);
                }
            }
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
                for (var i = 0; i < initialSize; i++)
                    await AddObjectToPool(scene, assetReference);
            }
            finally { semaphore.Release(); }
        }

        private static async UniTask AddObjectToPool(Scene scene, AssetReference assetReference)
        {
            var prefab = await Addressables.LoadAssetAsync<GameObject>(assetReference);
            if (!prefab)
            {
                Debug.LogError($"[PoolManager] Failed to load asset: {assetReference}");
                return;
            }
            var instance = Object.Instantiate(prefab, Vector3.one * -100f, Quaternion.identity);
            var poolable = instance.GetComponent<PoolableObject>();
            if (!poolable)
            {
                poolable = instance.AddComponent<PoolableObject>();
            }
            poolable.AssetRef = assetReference;
            poolable.IsAssetReference = true;
            
            instance.SetActive(false);
            SceneManager.MoveGameObjectToScene(instance, scene);

            if (!SceneObjectPools.TryGetValue(scene, out var scenePools))
            {
                scenePools = new Dictionary<AssetReference, Queue<GameObject>>();
                SceneObjectPools[scene] = scenePools;
            }

            if (!scenePools.TryGetValue(assetReference, out var pool))
            {
                pool = new Queue<GameObject>();
                scenePools[assetReference] = pool;
            }

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
                {
                    scenePools = new Dictionary<AssetReference, Queue<GameObject>>();
                    SceneObjectPools[scene] = scenePools;
                }

                if (!scenePools.TryGetValue(assetReference, out var pool))
                {
                    pool = new Queue<GameObject>();
                    scenePools[assetReference] = pool;
                }

                if (!SceneActiveObjects.TryGetValue(scene, out var sceneActive))
                {
                    sceneActive = new Dictionary<AssetReference, HashSet<GameObject>>();
                    SceneActiveObjects[scene] = sceneActive;
                }

                if (!sceneActive.TryGetValue(assetReference, out var activeSet))
                {
                    activeSet = new HashSet<GameObject>();
                    sceneActive[assetReference] = activeSet;
                }

                var count = pool.Count;
                for (int i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    
                    if (!obj)
                    {
                        continue;
                    }

                    if (!obj.activeSelf && !activeSet.Contains(obj))
                    {
                        obj.SetActive(true);
                        activeSet.Add(obj);
                        pool.Enqueue(obj);
                        return obj;
                    }
                    
                    pool.Enqueue(obj);
                }

                await AddObjectToPool(scene, assetReference);

                GameObject newObj = null;
                while (pool.Count > 0)
                {
                    var temp = pool.Dequeue();
                    if (!temp)
                    {
                        continue;
                    }
                    
                    if (!activeSet.Contains(temp))
                    {
                        newObj = temp;
                        pool.Enqueue(temp);
                        break;
                    }
                    pool.Enqueue(temp);
                }

                if (newObj)
                {
                    newObj.SetActive(true);
                    activeSet.Add(newObj);
                    return newObj;
                }

                Debug.LogError($"[PoolManager] Failed to get or create object for: {assetReference} in scene: {scene.name}");
                return null;
            }
            finally { semaphore.Release(); }
        }

        [Obsolete("No longer needed. Objects automatically return to pool when disabled.")]
        public static void ReleaseObject(AssetReference assetReference, GameObject obj, Scene? targetScene = null)
        {
            if (!obj) return;
            obj.SetActive(false);
        }

        [Obsolete("Blocking call, use with caution.")]
        public static GameObject GetObjectSync(AssetReference assetReference, Scene? targetScene = null)
        {
            Init();
            var scene = targetScene ?? GetTargetScene();
            var semaphore = GetOrCreateLock(scene, assetReference);
            semaphore.Wait();
            try
            {
                if (!SceneObjectPools.TryGetValue(scene, out var scenePools))
                {
                    scenePools = new Dictionary<AssetReference, Queue<GameObject>>();
                    SceneObjectPools[scene] = scenePools;
                }

                if (!scenePools.TryGetValue(assetReference, out var pool))
                {
                    pool = new Queue<GameObject>();
                    scenePools[assetReference] = pool;
                }

                if (!SceneActiveObjects.TryGetValue(scene, out var sceneActive))
                {
                    sceneActive = new Dictionary<AssetReference, HashSet<GameObject>>();
                    SceneActiveObjects[scene] = sceneActive;
                }

                if (!sceneActive.TryGetValue(assetReference, out var activeSet))
                {
                    activeSet = new HashSet<GameObject>();
                    sceneActive[assetReference] = activeSet;
                }

                var count = pool.Count;
                for (int i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    
                    if (!obj)
                    {
                        continue;
                    }

                    if (!obj.activeSelf && !activeSet.Contains(obj))
                    {
                        obj.SetActive(true);
                        activeSet.Add(obj);
                        pool.Enqueue(obj);
                        return obj;
                    }
                    
                    pool.Enqueue(obj);
                }

                AddObjectToPool(scene, assetReference).GetAwaiter().GetResult();

                while (pool.Count > 0)
                {
                    var temp = pool.Dequeue();
                    if (!temp)
                    {
                        continue;
                    }
                    
                    if (!activeSet.Contains(temp))
                    {
                        temp.SetActive(true);
                        activeSet.Add(temp);
                        pool.Enqueue(temp);
                        return temp;
                    }
                    pool.Enqueue(temp);
                }

                Debug.LogError($"[PoolManager] Failed to sync instantiate: {assetReference} in scene: {scene.name}");
                return null;
            }
            finally { semaphore.Release(); }
        }
        #endregion

        #region String Async + Sync
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
            var prefab = await Addressables.LoadAssetAsync<GameObject>(key);
            if (!prefab)
            {
                Debug.LogError($"[PoolManager] Failed to load asset: {key}");
                return;
            }
            var instance = Object.Instantiate(prefab, Vector3.one * -100f, Quaternion.identity);
            var poolable = instance.GetComponent<PoolableObject>();
            if (!poolable)
            {
                poolable = instance.AddComponent<PoolableObject>();
            }
            poolable.StringKey = key;
            poolable.IsAssetReference = false;
            
            instance.SetActive(false);
            SceneManager.MoveGameObjectToScene(instance, scene);

            if (!SceneStringPools.TryGetValue(scene, out var scenePools))
            {
                scenePools = new Dictionary<string, Queue<GameObject>>();
                SceneStringPools[scene] = scenePools;
            }

            if (!scenePools.TryGetValue(key, out var pool))
            {
                pool = new Queue<GameObject>();
                scenePools[key] = pool;
            }

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
                {
                    scenePools = new Dictionary<string, Queue<GameObject>>();
                    SceneStringPools[scene] = scenePools;
                }

                if (!scenePools.TryGetValue(key, out var pool))
                {
                    pool = new Queue<GameObject>();
                    scenePools[key] = pool;
                }

                if (!SceneStringActiveObjects.TryGetValue(scene, out var sceneActive))
                {
                    sceneActive = new Dictionary<string, HashSet<GameObject>>();
                    SceneStringActiveObjects[scene] = sceneActive;
                }

                if (!sceneActive.TryGetValue(key, out var activeSet))
                {
                    activeSet = new HashSet<GameObject>();
                    sceneActive[key] = activeSet;
                }

                var count = pool.Count;
                for (int i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    
                    if (!obj)
                    {
                        continue;
                    }

                    if (!obj.activeSelf && !activeSet.Contains(obj))
                    {
                        obj.SetActive(true);
                        activeSet.Add(obj);
                        pool.Enqueue(obj);
                        return obj;
                    }
                    
                    pool.Enqueue(obj);
                }

                await AddObjectToPool(scene, key);

                GameObject newObj = null;
                while (pool.Count > 0)
                {
                    var temp = pool.Dequeue();
                    if (!temp)
                    {
                        continue;
                    }
                    
                    if (!activeSet.Contains(temp))
                    {
                        newObj = temp;
                        pool.Enqueue(temp);
                        break;
                    }
                    pool.Enqueue(temp);
                }

                if (newObj)
                {
                    newObj.SetActive(true);
                    activeSet.Add(newObj);
                    return newObj;
                }

                Debug.LogError($"[PoolManager] Failed to get or create object for key: {key} in scene: {scene.name}");
                return null;
            }
            finally { semaphore.Release(); }
        }

        [Obsolete("No longer needed. Objects automatically return to pool when disabled.")]
        public static void ReleaseObject(string key, GameObject obj, Scene? targetScene = null)
        {
            if (!obj) return;
            obj.SetActive(false);
        }

        [Obsolete("Blocking call, use with caution.")]
        public static GameObject GetObjectSync(string key, Scene? targetScene = null)
        {
            Init();
            var scene = targetScene ?? GetTargetScene();
            var semaphore = GetOrCreateLock(scene, key);
            semaphore.Wait();
            try
            {
                if (!SceneStringPools.TryGetValue(scene, out var scenePools))
                {
                    scenePools = new Dictionary<string, Queue<GameObject>>();
                    SceneStringPools[scene] = scenePools;
                }

                if (!scenePools.TryGetValue(key, out var pool))
                {
                    pool = new Queue<GameObject>();
                    scenePools[key] = pool;
                }

                if (!SceneStringActiveObjects.TryGetValue(scene, out var sceneActive))
                {
                    sceneActive = new Dictionary<string, HashSet<GameObject>>();
                    SceneStringActiveObjects[scene] = sceneActive;
                }

                if (!sceneActive.TryGetValue(key, out var activeSet))
                {
                    activeSet = new HashSet<GameObject>();
                    sceneActive[key] = activeSet;
                }

                var count = pool.Count;
                for (int i = 0; i < count; i++)
                {
                    var obj = pool.Dequeue();
                    
                    if (!obj)
                    {
                        continue;
                    }

                    if (!obj.activeSelf && !activeSet.Contains(obj))
                    {
                        obj.SetActive(true);
                        activeSet.Add(obj);
                        pool.Enqueue(obj);
                        return obj;
                    }
                    
                    pool.Enqueue(obj);
                }

                AddObjectToPool(scene, key).GetAwaiter().GetResult();

                while (pool.Count > 0)
                {
                    var temp = pool.Dequeue();
                    if (!temp)
                    {
                        continue;
                    }
                    
                    if (!activeSet.Contains(temp))
                    {
                        temp.SetActive(true);
                        activeSet.Add(temp);
                        pool.Enqueue(temp);
                        return temp;
                    }
                    pool.Enqueue(temp);
                }

                Debug.LogError($"[PoolManager] Failed to sync instantiate: {key} in scene: {scene.name}");
                return null;
            }
            finally { semaphore.Release(); }
        }
        #endregion

        #region Utility Methods
        public static (int totalObjects, int activeObjects) GetPoolStats(AssetReference assetReference, Scene? targetScene = null)
        {
            var scene = targetScene ?? GetTargetScene();
            
            int total = 0;
            int active = 0;

            if (SceneObjectPools.TryGetValue(scene, out var scenePools))
            {
                if (scenePools.TryGetValue(assetReference, out var pool))
                    total = pool.Count;
            }

            if (SceneActiveObjects.TryGetValue(scene, out var sceneActive))
            {
                if (sceneActive.TryGetValue(assetReference, out var activeSet))
                    active = activeSet.Count;
            }

            return (total, active);
        }
        public static Dictionary<string, (int totalObjects, int activeObjects)> GetAllPoolStats()
        {
            var stats = new Dictionary<string, (int, int)>();

            foreach (var scenePool in SceneObjectPools)
            {
                var sceneName = scenePool.Key.name;
                int totalInScene = scenePool.Value.Values.Sum(pool => pool.Count);
                int activeInScene = 0;

                if (SceneActiveObjects.TryGetValue(scenePool.Key, out var sceneActive))
                {
                    activeInScene = sceneActive.Values.Sum(set => set.Count);
                }

                stats[$"{sceneName} (AssetReference)"] = (totalInScene, activeInScene);
            }

            foreach (var scenePool in SceneStringPools)
            {
                var sceneName = scenePool.Key.name;
                int totalInScene = scenePool.Value.Values.Sum(pool => pool.Count);
                int activeInScene = 0;

                if (SceneStringActiveObjects.TryGetValue(scenePool.Key, out var sceneActive))
                {
                    activeInScene = sceneActive.Values.Sum(set => set.Count);
                }

                stats[$"{sceneName} (String)"] = (totalInScene, activeInScene);
            }

            return stats;
        }
        public static void ClearScenePools(Scene scene)
        {
            OnSceneUnloaded(scene);
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
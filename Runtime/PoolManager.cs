/*
 * PoolManager — High-Performance Object Pool for Unity
 * Copyright (c) 2025 Batuhan Kanbur
 * SPDX-License-Identifier: MIT
 * Repo: https://github.com/BatuhanKanbur/PoolManager
 * Contact: https://www.batuhankanbur.com
 * Version: 2.0.0
 */
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
namespace PoolManager.Runtime
{
    internal class PoolableObject : MonoBehaviour
    {
        public AssetReference AssetRef;
        public string StringKey;
        public bool IsAssetReference;

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            PoolManager.MarkAsInactive(this);
        }
    }

    public static class PoolManager
    {
        private static readonly Dictionary<Scene, Dictionary<AssetReference, HashSet<GameObject>>> SceneActiveRef = new();
        private static readonly Dictionary<Scene, Dictionary<AssetReference, Queue<GameObject>>> SceneInactiveRef = new();
        private static readonly Dictionary<Scene, Dictionary<AssetReference, SemaphoreSlim>> SceneLocksRef = new();
        private static readonly Dictionary<Scene, Dictionary<string, HashSet<GameObject>>> SceneActiveStr = new();
        private static readonly Dictionary<Scene, Dictionary<string, Queue<GameObject>>> SceneInactiveStr = new();
        private static readonly Dictionary<Scene, Dictionary<string, SemaphoreSlim>> SceneLocksStr = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneActiveRef.Clear();
            SceneInactiveRef.Clear();
            SceneLocksRef.Clear();
            SceneActiveStr.Clear();
            SceneInactiveStr.Clear();
            SceneLocksStr.Clear();
        }
        #region ==================== GET ASYNC ====================
        public static async UniTask<GameObject> GetObjectAsync(AssetReference assetRef, Scene? targetScene = null)
        {
            var scene = targetScene ?? SceneManager.GetActiveScene();
            var semaphore = GetLock(scene, assetRef);
            await semaphore.WaitAsync();
            try
            {
                var inactive = GetInactive(scene, assetRef);
                var active = GetActive(scene, assetRef);

                if (inactive.Count > 0)
                {
                    var obj = inactive.Dequeue();
                    if (obj)
                    {
                        active.Add(obj);
                        obj.SetActive(true);
                        return obj;
                    }
                }

                var handle = assetRef.InstantiateAsync();
                var go = await handle.ToUniTask();
                Prepare(go, assetRef);
                active.Add(go);
                return go;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static async UniTask<GameObject> GetObjectAsync(string key, Scene? targetScene = null)
        {
            var scene = targetScene ?? SceneManager.GetActiveScene();
            var semaphore = GetLock(scene, key);
            await semaphore.WaitAsync();
            try
            {
                var inactive = GetInactive(scene, key);
                var active = GetActive(scene, key);

                if (inactive.Count > 0)
                {
                    var obj = inactive.Dequeue();
                    if (obj)
                    {
                        active.Add(obj);
                        obj.SetActive(true);
                        return obj;
                    }
                }

                var handle = Addressables.InstantiateAsync(key);
                var go = await handle.ToUniTask();
                Prepare(go, key);
                active.Add(go);
                return go;
            }
            finally
            {
                semaphore.Release();
            }
        }

        #endregion
        #region ==================== GET SYNC ====================

        public static GameObject GetObjectSync(AssetReference assetRef, Scene? targetScene = null)
        {
            var scene = targetScene ?? SceneManager.GetActiveScene();
            var inactive = GetInactive(scene, assetRef);
            var active = GetActive(scene, assetRef);

            if (inactive.Count > 0)
            {
                var obj = inactive.Dequeue();
                if (obj)
                {
                    active.Add(obj);
                    obj.SetActive(true);
                    return obj;
                }
            }

            // Burada UniTask değil, Addressables native sync
            var handle = assetRef.InstantiateAsync();
            var go = handle.WaitForCompletion();
            Prepare(go, assetRef);
            active.Add(go);
            return go;
        }

        public static GameObject GetObjectSync(string key, Scene? targetScene = null)
        {
            var scene = targetScene ?? SceneManager.GetActiveScene();
            var inactive = GetInactive(scene, key);
            var active = GetActive(scene, key);

            if (inactive.Count > 0)
            {
                var obj = inactive.Dequeue();
                if (obj)
                {
                    active.Add(obj);
                    obj.SetActive(true);
                    return obj;
                }
            }

            var handle = Addressables.InstantiateAsync(key);
            var go = handle.WaitForCompletion();
            Prepare(go, key);
            active.Add(go);
            return go;
        }

        #endregion
        #region ==================== INTERNAL HELPERS ====================

        internal static void MarkAsInactive(PoolableObject obj)
        {
            var scene = obj.gameObject.scene;

            if (obj.IsAssetReference)
            {
                if (!SceneActiveRef.TryGetValue(scene, out var activeDict) ||
                    !SceneInactiveRef.TryGetValue(scene, out var inactiveDict))
                    return;

                if (!activeDict.TryGetValue(obj.AssetRef, out var activeSet) ||
                    !inactiveDict.TryGetValue(obj.AssetRef, out var inactiveQueue))
                    return;

                if (activeSet.Remove(obj.gameObject))
                    inactiveQueue.Enqueue(obj.gameObject);
            }
            else
            {
                if (!SceneActiveStr.TryGetValue(scene, out var activeDict) ||
                    !SceneInactiveStr.TryGetValue(scene, out var inactiveDict))
                    return;

                if (!activeDict.TryGetValue(obj.StringKey, out var activeSet) ||
                    !inactiveDict.TryGetValue(obj.StringKey, out var inactiveQueue))
                    return;

                if (activeSet.Remove(obj.gameObject))
                    inactiveQueue.Enqueue(obj.gameObject);
            }
        }

        private static SemaphoreSlim GetLock(Scene scene, AssetReference assetRef)
        {
            if (!SceneLocksRef.TryGetValue(scene, out var lockDict))
                SceneLocksRef[scene] = lockDict = new();

            if (!lockDict.TryGetValue(assetRef, out var sem))
                lockDict[assetRef] = sem = new SemaphoreSlim(1, 1);

            return sem;
        }

        private static SemaphoreSlim GetLock(Scene scene, string key)
        {
            if (!SceneLocksStr.TryGetValue(scene, out var lockDict))
                SceneLocksStr[scene] = lockDict = new();

            if (!lockDict.TryGetValue(key, out var sem))
                lockDict[key] = sem = new SemaphoreSlim(1, 1);

            return sem;
        }

        private static Queue<GameObject> GetInactive(Scene scene, AssetReference assetRef)
        {
            if (!SceneInactiveRef.TryGetValue(scene, out var poolDict))
                SceneInactiveRef[scene] = poolDict = new();

            if (!poolDict.TryGetValue(assetRef, out var q))
                poolDict[assetRef] = q = new Queue<GameObject>();

            return q;
        }

        private static Queue<GameObject> GetInactive(Scene scene, string key)
        {
            if (!SceneInactiveStr.TryGetValue(scene, out var poolDict))
                SceneInactiveStr[scene] = poolDict = new();

            if (!poolDict.TryGetValue(key, out var q))
                poolDict[key] = q = new Queue<GameObject>();

            return q;
        }

        private static HashSet<GameObject> GetActive(Scene scene, AssetReference assetRef)
        {
            if (!SceneActiveRef.TryGetValue(scene, out var poolDict))
                SceneActiveRef[scene] = poolDict = new();

            if (!poolDict.TryGetValue(assetRef, out var s))
                poolDict[assetRef] = s = new HashSet<GameObject>();

            return s;
        }

        private static HashSet<GameObject> GetActive(Scene scene, string key)
        {
            if (!SceneActiveStr.TryGetValue(scene, out var poolDict))
                SceneActiveStr[scene] = poolDict = new();

            if (!poolDict.TryGetValue(key, out var s))
                poolDict[key] = s = new HashSet<GameObject>();

            return s;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private static void Prepare(GameObject go, AssetReference assetRef)
        {
            if (go.TryGetComponent<PoolableObject>(out var poolable))
                return;
            var p = go.AddComponent<PoolableObject>();
            p.AssetRef = assetRef;
            p.IsAssetReference = true;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private static void Prepare(GameObject go, string key)
        {
            if (go.TryGetComponent<PoolableObject>(out var poolable))
                return;
            var p = go.AddComponent<PoolableObject>();
            p.StringKey = key;
            p.IsAssetReference = false;
        }

        #endregion
        #region ==================== CREATE POOL (WARMUP) ====================

        public static async UniTask CreatePoolAsync(AssetReference assetRef, int count, Scene? targetScene = null)
        {
            var scene = targetScene ?? SceneManager.GetActiveScene();
            var semaphore = GetLock(scene, assetRef);
            await semaphore.WaitAsync();
            try
            {
                var inactive = GetInactive(scene, assetRef);
                var active = GetActive(scene, assetRef);

                int currentTotal = inactive.Count + active.Count;
                int toCreate = Mathf.Max(0, count - currentTotal);
                if (toCreate <= 0) return;

                for (int i = 0; i < toCreate; i++)
                {
                    var handle = assetRef.InstantiateAsync();
                    var go = await handle.ToUniTask();
                    Prepare(go, assetRef);
                    go.SetActive(false);
                    inactive.Enqueue(go);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static async UniTask CreatePoolAsync(string key, int count, Scene? targetScene = null)
        {
            var scene = targetScene ?? SceneManager.GetActiveScene();
            var semaphore = GetLock(scene, key);
            await semaphore.WaitAsync();
            try
            {
                var inactive = GetInactive(scene, key);
                var active = GetActive(scene, key);

                int currentTotal = inactive.Count + active.Count;
                int toCreate = Mathf.Max(0, count - currentTotal);
                if (toCreate <= 0) return;

                for (int i = 0; i < toCreate; i++)
                {
                    var handle = Addressables.InstantiateAsync(key);
                    var go = await handle.ToUniTask();
                    Prepare(go, key);
                    go.SetActive(false);
                    inactive.Enqueue(go);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static void CreatePoolSync(AssetReference assetRef, int count, Scene? targetScene = null)
        {
            var scene = targetScene ?? SceneManager.GetActiveScene();
            var inactive = GetInactive(scene, assetRef);
            var active = GetActive(scene, assetRef);

            int currentTotal = inactive.Count + active.Count;
            int toCreate = Mathf.Max(0, count - currentTotal);
            if (toCreate <= 0) return;

            for (int i = 0; i < toCreate; i++)
            {
                var handle = assetRef.InstantiateAsync();
                var go = handle.WaitForCompletion();
                Prepare(go, assetRef);
                go.SetActive(false);
                inactive.Enqueue(go);
            }
        }

        public static void CreatePoolSync(string key, int count, Scene? targetScene = null)
        {
            var scene = targetScene ?? SceneManager.GetActiveScene();
            var inactive = GetInactive(scene, key);
            var active = GetActive(scene, key);

            int currentTotal = inactive.Count + active.Count;
            int toCreate = Mathf.Max(0, count - currentTotal);
            if (toCreate <= 0) return;

            for (int i = 0; i < toCreate; i++)
            {
                var handle = Addressables.InstantiateAsync(key);
                var go = handle.WaitForCompletion();
                Prepare(go, key);
                go.SetActive(false);
                inactive.Enqueue(go);
            }
        }

        #endregion
    }

    public static class PoolExtensions
    {
        #region === GameObject Chain ===

        public static GameObject SetParent(this GameObject go, Transform parent, bool worldPositionStays = false)
        {
            if (go) go.transform.SetParent(parent, worldPositionStays);
            return go;
        }

        public static GameObject SetPosition(this GameObject go, Vector3 position)
        {
            if (go) go.transform.position = position;
            return go;
        }

        public static GameObject SetLocalPosition(this GameObject go, Vector3 localPosition)
        {
            if (go) go.transform.localPosition = localPosition;
            return go;
        }

        public static GameObject SetRotation(this GameObject go, Quaternion rotation)
        {
            if (go) go.transform.rotation = rotation;
            return go;
        }

        public static GameObject SetLocalRotation(this GameObject go, Quaternion localRotation)
        {
            if (go) go.transform.localRotation = localRotation;
            return go;
        }

        public static GameObject SetScale(this GameObject go, Vector3 scale)
        {
            if (go) go.transform.localScale = scale;
            return go;
        }

        public static GameObject SetActiveState(this GameObject go, bool state)
        {
            if (go) go.SetActive(state);
            return go;
        }

        #endregion

        #region === UniTask<GameObject> Chain ===

        public static async UniTask<GameObject> SetParent(this UniTask<GameObject> task, Transform parent, bool worldPositionStays = false)
        {
            var go = await task;
            if (go) go.transform.SetParent(parent, worldPositionStays);
            return go;
        }

        public static async UniTask<GameObject> SetPosition(this UniTask<GameObject> task, Vector3 position)
        {
            var go = await task;
            if (go) go.transform.position = position;
            return go;
        }

        public static async UniTask<GameObject> SetLocalPosition(this UniTask<GameObject> task, Vector3 localPosition)
        {
            var go = await task;
            if (go) go.transform.localPosition = localPosition;
            return go;
        }

        public static async UniTask<GameObject> SetRotation(this UniTask<GameObject> task, Quaternion rotation)
        {
            var go = await task;
            if (go) go.transform.rotation = rotation;
            return go;
        }

        public static async UniTask<GameObject> SetLocalRotation(this UniTask<GameObject> task, Quaternion localRotation)
        {
            var go = await task;
            if (go) go.transform.localRotation = localRotation;
            return go;
        }

        public static async UniTask<GameObject> SetScale(this UniTask<GameObject> task, Vector3 scale)
        {
            var go = await task;
            if (go) go.transform.localScale = scale;
            return go;
        }

        public static async UniTask<GameObject> SetActiveState(this UniTask<GameObject> task, bool state)
        {
            var go = await task;
            if (go) go.SetActive(state);
            return go;
        }

        #endregion
    }
}

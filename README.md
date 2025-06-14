# PoolManager

**A modern, MonoBehaviour-free, UniTask-powered Object Pooling system for Unity.**

Developed by [Batuhan Kanbur](https://github.com/BatuhanKanbur), `PoolManager` is a lightweight and high-performance pooling system built for professionals who value **clean architecture**, **async safety**, and **Addressables compatibility**.

> "True engineering is solving complex problems with simple and sustainable solutions."  
> — Batuhan Kanbur, Senior Game Developer

---

## ✨ Features

- ✅ **Zero MonoBehaviour / ScriptableObject / Interface overhead**
- 🚀 **Fully async-ready** with [`UniTask`](https://github.com/Cysharp/UniTask)
- 🎯 **Addressables-first design** with native `AssetReference` support
- 🧵 **Thread-safe access** with `SemaphoreSlim`
- 🔄 **Automatic cleanup** on scene change and app quit
- 🧼 **Minimal API surface**, maximum control
- 🧱 **Modular & testable** static architecture

---

## 📦 Installation

> **Requirement:** Unity 2021.3+  
> Add `PoolManager` via Git URL in Unity Package Manager:
```
https://github.com/BatuhanKanbur/PoolManager.git
```

## 🚀 Quick Start

```csharp
// Prepare your AssetReference
[SerializeField] private AssetReference myPrefab;

// You can create a pool in advance if you want, but it's not mandatory!
await PoolManager.CreatePool(myPrefab, initialSize: 10);

// You can pull the object asynchronously and do transform interactions with chain methods.
var go = await PoolManager.GetObjectAsync(myPrefab)
                          .SetPosition(targetPosition)
                          .SetParent(targetParent);

//Or you can synchronously pull the object directly from the pool (not recommended).
var go = PoolManager.GetObjectSync(myPrefab)
                    .SetPosition(targetPosition)
                    .SetParent(targetParent);
// Release the object when done
PoolManager.ReleaseObject(myPrefab, go);
```

---

## 🧠 API Overview

### 📌 Pooling

| Method                                | Description                                 |
|--------------------------------------|---------------------------------------------|
| `CreatePool(reference, size)`        | Preloads objects into pool                  |
| `GetObjectAsync(reference)`          | Retrieves an object (async)                 |
| `GetObjectSync(reference)` ⚠️        | Retrieves an object (sync + blocking)       |
| `ReleaseObject(reference, obj)`      | Returns object to pool                      |

### 📌 Async Extension Helpers

Chain object setup with:

```csharp
await PoolManager.GetObjectAsync(reference)
    .SetPosition(pos)
    .SetRotation(rot)
    .SetParent(parent);
```

### 🔧 Utility Extensions

- `SetPosition(Vector3 position)`
- `SetRotation(Quaternion rotation)`
- `SetParent(Transform parent)`
- `SetPositionAndRotation(Vector3 pos, Quaternion rot)`
- `SetPositionAndRotation(Transform transform)`

All of these work on both `GameObject` and `UniTask<GameObject>`.

---

## ⚠️ Warnings

- **Blocking API:**  
  `GetObjectSync()` is marked `[Obsolete]`. Use it **only when absolutely necessary**, such as editor scripts or controlled startup scenarios. It may freeze the main thread.
  
- **Auto-clear on scene change / quit:**  
  Pools are disposed automatically when:
  - Active scene changes
  - Application quits

---

## 🧹 Cleanup Strategy

The following Unity events trigger a complete pool cleanup:
- `SceneManager.activeSceneChanged`
- `Application.quitting`

Objects are destroyed via `Object.Destroy`, and unused memory is reclaimed with `Resources.UnloadUnusedAssets()`.

---

## 🧵 Thread Safety

All pool access is synchronized with `SemaphoreSlim` to prevent race conditions. Both async and sync methods respect locking discipline.

---

## 💡 Design Philosophy

- **Simplicity first:**  
  No custom inspectors, SOs, interfaces or lifecycle boilerplate.

- **Power through extensibility:**  
  Static class model makes it easy to plug into any existing architecture without polluting scene hierarchy.

- **Minimalistic usage:**  
  Designed for runtime only — no special initialization or component setup required.

---

## 👨‍💻 Author

**Batuhan Kanbur**  
Senior Game Developer — [batuhankanbur.com](https://batuhankanbur.com)  
[GitHub](https://github.com/BatuhanKanbur) | [LinkedIn](https://www.linkedin.com/in/bkanbur/)

---

## 📄 License

MIT © Batuhan Kanbur  
Use freely in personal and commercial projects. Attribution appreciated but not required.

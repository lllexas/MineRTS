using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class SingletonData<T> where T : class, new()
{
    private static readonly Lazy<T> _instance = new Lazy<T>(() => new T());
    public static T Instance
    {
        get
        {
            return _instance.Value;
        }
    }
    protected SingletonData()
    {
        // Prevent external instantiation
    }
}

public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object(); // 线程锁，虽然Unity主线程安全，但加了更保险
    private static bool _applicationIsQuitting = false;  // 防止程序退出时报错

    public bool DontDestroyOnLoadEnabled = true;
    public static T Instance
    {
        get
        {
            // 如果程序正在退出，就不再创建新实例了（防止产生残留物体）
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[Singleton] {typeof(T)} 实例在程序退出时被尝试访问，返回null。");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    // 1. 先在场景里找找看有没有现成的
                    _instance = (T)FindObjectOfType(typeof(T));

                    // 2. 如果场景里没找到，我们就自己造一个！
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject();
                        _instance = singletonObject.AddComponent<T>();
                        singletonObject.name = typeof(T).ToString() + " (Singleton)";

                        // 3. 告诉 Unity：这个小管家在切换场景时不要被丢掉
                        DontDestroyOnLoad(singletonObject);

                        Debug.Log($"[Singleton] 自动创建了实例: {singletonObject.name}");
                    }
                }
                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        // 确保场景中手动拖放的单例也能正常工作，且不重复
        if (_instance == null)
        {
            _instance = this as T;
            if (DontDestroyOnLoadEnabled)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[Singleton] 场景中存在重复的 {typeof(T)}，已自动删除！");
            Destroy(gameObject);
        }
    }

    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    private void OnDestroy()
    {
        // 只有当前实例被销毁时才标记退出（如果是重复实例被删则不标记）
        if (_instance == this)
        {
            _applicationIsQuitting = true;
        }
    }
}
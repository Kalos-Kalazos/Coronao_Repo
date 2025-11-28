using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool genérico simple. Guarda GameObjects instanciados y los reutiliza.
/// Funciona con prefabs que contengan un componente T.
/// </summary>
public class ObjectPool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Stack<T> stack = new Stack<T>();

    public ObjectPool(T prefab, int initialSize = 0, Transform parent = null)
    {
        this.prefab = prefab;
        this.parent = parent;
        for (int i = 0; i < initialSize; i++)
        {
            var go = UnityEngine.Object.Instantiate(prefab, parent);
            go.gameObject.SetActive(false);
            stack.Push(go);
        }
    }

    public T Get()
    {
        T inst;
        if (stack.Count > 0)
        {
            inst = stack.Pop();
            inst.gameObject.SetActive(true);
        }
        else
        {
            inst = UnityEngine.Object.Instantiate(prefab, parent);
        }

        // if implements IPoolable, call spawn
        if (inst is IPoolable poolable) poolable.OnPoolSpawn();
        return inst;
    }

    public void Release(T instance)
    {
        if (instance == null) return;

        // if implements IPoolable, call despawn
        if (instance is IPoolable poolable) poolable.OnPoolDespawn();

        instance.gameObject.SetActive(false);
        stack.Push(instance);
    }

    public void Warm(int count)
    {
        for (int i = 0; i < count; i++) Get();
    }
}


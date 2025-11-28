using UnityEngine;

/// <summary>
/// Simple wrapper para el pool de CardsController.
/// - Instanciar en escena y asignar prefab + initialSize.
/// - Exponer Get/Release para TableManager / Factory.
/// </summary>
public class CardsPool : MonoBehaviour
{
    [SerializeField] private CardsController cardsControllerPrefab;
    [SerializeField] private int initialSize = 10;
    [SerializeField] private Transform poolParent;

    private ObjectPool<CardsController> pool;

    private void Awake()
    {
        if (cardsControllerPrefab == null)
        {
            Debug.LogError("CardsPool: falta asignar cardsControllerPrefab");
            return;
        }

        if (poolParent == null)
        {
            // crear parent si no hay
            GameObject p = new GameObject("CardsPoolParent");
            p.transform.SetParent(transform, false);
            poolParent = p.transform;
        }

        pool = new ObjectPool<CardsController>(cardsControllerPrefab, initialSize, poolParent);
    }

    public CardsController Get()
    {
        if (pool == null) return null;
        return pool.Get();
    }

    public void Release(CardsController instance)
    {
        if (pool == null || instance == null) return;
        pool.Release(instance);
    }
}


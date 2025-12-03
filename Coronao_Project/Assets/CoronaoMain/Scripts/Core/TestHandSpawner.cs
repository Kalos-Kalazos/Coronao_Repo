using System;
using UnityEngine;

/// <summary>
/// TestHandSpawner:
/// - Spawnea N cartas desde CardsPool para pruebas y las posiciona en la "mano".
/// - Asigna CardsData desde el inspector (array de CardsData).
/// - Útil para probar drag & drop sin tener que hacerlo manualmente.
/// 
/// Uso:
/// - Añade este componente a un GameObject (ej. "TestHand") en la escena.
/// - Asigna CardsPool (escena), un HandParent Transform (vacío) y el array de CardsData (4).
/// - Presiona Play para que cree la mano.
/// </summary>
[DisallowMultipleComponent]
public class TestHandSpawner : MonoBehaviour
{
    [Header("Pool + data")]
    [SerializeField] private CardsPool cardsPool;
    [SerializeField] private CardsData[] cardsToSpawn; // asigna 4 cards data a probar

    [Header("Hand layout")]
    [SerializeField] private Transform handParent;
    [SerializeField] private float spacing = 0.6f;
    [SerializeField] private Transform startLocalPos;

    private void Start()
    {
        if (cardsPool == null)
        {
            Debug.LogError("[TestHandSpawner] cardsPool no asignado en inspector.");
            return;
        }

        if (cardsToSpawn == null || cardsToSpawn.Length == 0)
        {
            Debug.LogWarning("[TestHandSpawner] cardsToSpawn vacío. Nada que spawnear.");
            return;
        }

        if (handParent == null)
        {
            // crear un HandParent si no existe
            GameObject hp = new GameObject("Hand");
            hp.transform.SetParent(transform, false);
            handParent = hp.transform;
        }

        SpawnHand();
    }

    private void SpawnHand()
    {
        for (int i = 0; i < cardsToSpawn.Length; i++)
        {
            var data = cardsToSpawn[i];
            if (data == null) continue;

            var card = cardsPool.Get();
            if (card == null)
            {
                Debug.LogError("[TestHandSpawner] cardsPool.Get() devolvió null.");
                continue;
            }

            // posicionarlo en la mano
            card.transform.SetParent(handParent, false);
            Vector3 localPos = startLocalPos.position + new Vector3(i * spacing, 0f, 0f);
            card.transform.localPosition = localPos;
            card.transform.localRotation = Quaternion.identity;

            card.Init(data, Guid.NewGuid().ToString());
            card.OwningPool = cardsPool;

            Debug.Log($"[TestHandSpawner] Spawned card '{data.DisplayName}' at hand slot {i}");
        }
    }
}

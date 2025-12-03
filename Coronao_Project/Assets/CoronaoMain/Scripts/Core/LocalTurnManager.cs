using System;
using UnityEngine;

/// <summary>
/// LocalTurnManager (demo local):
/// - Suscribe a CardsController.OnPlayRequested
/// - Valida localmente la petición (puedes implementar INetworkAuthority aquí)
/// - Llama a TableManager.TryPlaceCardInSlot -> si OK llama CardsController.OnPlayApproved
/// - Espera al callback visual (OnVisualPlacementComplete) y entonces llama TableManager.ConfirmPlacementToSlot
/// - Imprime todo por Debug.Log para que puedas seguir el flujo paso a paso
/// 
/// Uso:
/// - Añade este componente a un GameObject vacío en la escena (ej. "LocalTurnManager").
/// - Asigna TableManager en el inspector.
/// </summary>
[DisallowMultipleComponent]
public class LocalTurnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TableManager tableManager;

    private void OnEnable()
    {
        CardsController.OnPlayRequested += HandlePlayRequest;
        if (tableManager == null)
            Debug.LogWarning("[LocalTurnManager] tableManager no asignado en inspector.");
        else
            // Subscribir a eventos de TableManager para debugging
            tableManager.OnSlotReachedExactlyTen += OnSlotReachedExactlyTen;
        tableManager.OnDrawOptionAvailable += OnDrawOptionAvailable;
        tableManager.OnSlotAutoConverted += OnSlotAutoConverted;
    }

    private void OnDisable()
    {
        CardsController.OnPlayRequested -= HandlePlayRequest;
        if (tableManager != null)
        {
            tableManager.OnSlotReachedExactlyTen -= OnSlotReachedExactlyTen;
            tableManager.OnDrawOptionAvailable -= OnDrawOptionAvailable;
            tableManager.OnSlotAutoConverted -= OnSlotAutoConverted;
        }
    }

    private void HandlePlayRequest(CardsController.PlayRequest req)
    {
        Debug.Log($"[LocalTurnManager] Received PlayRequest: cardInstance={req.CardInstanceId} dataId={req.CardDataId} slot={req.SlotIndex}");

        if (tableManager == null)
        {
            Debug.LogWarning("[LocalTurnManager] No TableManager assigned; rejecting play.");
            // intentar encontrar controller y rechazar
            var card = FindCardByInstanceId(req.CardInstanceId);
            if (card != null) card.OnPlayRejected("Server not available");
            return;
        }

        // Encontrar CardsController por InstanceId
        var controller = FindCardByInstanceId(req.CardInstanceId);
        if (controller == null)
        {
            Debug.LogWarning($"[LocalTurnManager] No se encontró CardsController con InstanceId {req.CardInstanceId}");
            return;
        }

        // Validación local simple (aquí puedes llamar INetworkAuthority)
        // Por ahora asumimos que el jugador tiene permiso; si quieres, añade lógica aquí.
        string rejectReason;
        if (!tableManager.TryPlaceCardInSlot(req.SlotIndex, controller, out Vector3 pos, out Quaternion rot, out bool drawAvailable, out rejectReason))
        {
            Debug.Log($"[LocalTurnManager] TryPlaceCardInSlot REJECTED: {rejectReason}");
            controller.OnPlayRejected(rejectReason);
            return;
        }

        // Si está aceptado, subscribe al final visual y aprobar
        Debug.Log($"[LocalTurnManager] TryPlaceCardInSlot ACCEPTED -> approving and animating card to slot {req.SlotIndex}");
        // subscribir al evento visual de la CardView (para saber cuando terminó la "animación")
        var view = controller.GetComponentInChildren<CardView>();
        if (view != null)
        {
            // handler local que será invocado cuando la vista invoque OnVisualPlacementComplete
            void OnVisualComplete()
            {
                // desuscribir
                view.OnVisualPlacementComplete -= OnVisualComplete;

                Debug.Log($"[LocalTurnManager] Visual placement complete for {controller.Data.DisplayName} in slot {req.SlotIndex}. Now confirming in TableManager.");

                // Confirmar en TableManager (resolución de efectos)
                bool triggeredExactlyTen = false;
                bool drawNow = false;
                bool ok = tableManager.ConfirmPlacementToSlot(req.SlotIndex, controller, out triggeredExactlyTen, out drawNow);
                if (!ok)
                {
                    Debug.LogWarning($"[LocalTurnManager] ConfirmPlacementToSlot returned false for slot {req.SlotIndex}");
                    // si falló, decimos al controller que fue rechazado (revert visual)
                    controller.OnPlayRejected("Confirm failed");
                    return;
                }

                Debug.Log($"[LocalTurnManager] ConfirmPlacementToSlot OK. triggeredExactlyTen={triggeredExactlyTen}, drawNow={drawNow}");

                // Si drawNow == true -> puedes abrir UI para robar 1 carta; por ahora solo log:
                if (drawNow) Debug.Log($"[LocalTurnManager] Player may draw 1 card (draw option) due to sum 11/12 in slot {req.SlotIndex}.");
            }

            // subscribir
            view.OnVisualPlacementComplete += OnVisualComplete;
        }
        else
        {
            Debug.LogWarning("[LocalTurnManager] CardView not found; will confirm immediately after approval.");
        }

        // Finalmente avisar al controller que está aprobado (esto provocará que la vista se mueva)
        controller.OnPlayApproved(req.SlotIndex, pos, rot);
    }

    // Busca la instancia activa de CardsController por su InstanceId (usa FindObjectsOfType; suficiente para demo)
    private CardsController FindCardByInstanceId(string instanceId)
    {
        var all = FindObjectsOfType<CardsController>();
        foreach (var c in all)
        {
            if (c.InstanceId == instanceId) return c;
        }
        return null;
    }

    #region TableManager event handlers (debug)
    private void OnSlotReachedExactlyTen(int slotIndex, System.Collections.Generic.IReadOnlyList<CardsController> stack)
    {
        Debug.Log($"[LocalTurnManager] Event: Slot {slotIndex} reached EXACT 10. Stack count={stack.Count}");
        // Si tableManager.autoConvertToFigure == false, aquí podrías decidir qué figura crear y llamar ForceConvertSlotToFigure.
    }

    private void OnDrawOptionAvailable(int slotIndex, CardsController placedCard)
    {
        Debug.Log($"[LocalTurnManager] Event: Draw option available at slot {slotIndex} after placing {placedCard.Data.DisplayName}");
    }

    private void OnSlotAutoConverted(int slotIndex, CardsController newFigure)
    {
        Debug.Log($"[LocalTurnManager] Event: Slot {slotIndex} auto converted to figure {newFigure.Data.DisplayName}");
    }
    #endregion
}

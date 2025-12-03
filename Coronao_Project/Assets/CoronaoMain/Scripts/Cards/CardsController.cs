using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class CardsController : MonoBehaviour,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private CardView view;
    [SerializeField] private float clickDebounce = 0.25f;
    [SerializeField] private LayerMask slotLayerMask;
    [SerializeField] private Camera worldCamera;

    // Datos
    public string InstanceId { get; private set; }
    public CardsData Data { get; private set; }

    // Pool reference (optional)
    public CardsPool OwningPool { get; set; }

    // Estado runtime
    private bool isDragging = false;
    private bool inputBlocked = false;
    private float lastClickTime = -999f;

    // Eventos intención
    public struct PlayRequest
    {
        public string CardInstanceId;
        public string CardDataId;
        public int SlotIndex;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public PlayRequest(string instanceId, string dataId, int slot, Vector3 hp, Vector3 hn)
        {
            CardInstanceId = instanceId; CardDataId = dataId; SlotIndex = slot; HitPoint = hp; HitNormal = hn;
        }
    }
    public static event Action<PlayRequest> OnPlayRequested;

    private void Reset()
    {
        if (view == null) view = GetComponentInChildren<CardView>();
        if (worldCamera == null) worldCamera = Camera.main;
    }

    public void Init(CardsData data, string instanceId = null)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        InstanceId = string.IsNullOrEmpty(instanceId) ? Guid.NewGuid().ToString() : instanceId;
        view?.InitFromData(Data);
        Debug.Log($"[CardsController] Init: {Data.DisplayName} (InstanceId={InstanceId})");
    }

    #region Input handlers
    public void OnPointerDown(PointerEventData eventData)
    {
        if (inputBlocked) return;
        view?.SetHovered(true);
        Debug.Log($"[CardsController] OnPointerDown on {Data?.DisplayName}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inputBlocked) return;
        if (Time.realtimeSinceStartup - lastClickTime < clickDebounce) return;
        lastClickTime = Time.realtimeSinceStartup;

        Debug.Log($"[CardsController] OnPointerClick on {Data?.DisplayName}");
        if (Data != null && Data.TargetReq == CardTargetRequirement.None)
        {
            // pre-visual feedback via log (no animation)
            Debug.Log($"[CardsController] Pre-play (no target) for {Data.DisplayName}");
        }
        else
        {
            view?.ShowTooltip();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inputBlocked) return;
        isDragging = true;
        view?.OnDragStart();
        transform.SetParent(null, true);
        Debug.Log($"[CardsController] OnBeginDrag {Data?.DisplayName}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || inputBlocked) return;
        if (worldCamera == null) worldCamera = Camera.main;

        Ray ray = worldCamera.ScreenPointToRay(eventData.position);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter) + Vector3.up * 0.05f;
            view?.DragFollowTo(worldPoint);
        }
        else
        {
            Vector3 screen = new Vector3(eventData.position.x, eventData.position.y, Vector3.Distance(worldCamera.transform.position, transform.position));
            Vector3 fallback = worldCamera.ScreenToWorldPoint(screen);
            view?.DragFollowTo(fallback);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        if (worldCamera == null) worldCamera = Camera.main;
        Ray ray = worldCamera.ScreenPointToRay(eventData.position);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, slotLayerMask))
        {
            var slot = hit.collider.GetComponent<TableSlot>();
            int slotIndex = slot != null ? slot.SlotIndex : -1;
            var req = new PlayRequest(InstanceId, Data.Id, slotIndex, hit.point, hit.normal);
            Debug.Log($"[CardsController] OnEndDrag -> hit slot {slotIndex} for {Data?.DisplayName}");
            OnPlayRequested?.Invoke(req);
            // pre-visual
            Debug.Log($"[CardsController] Emitted PlayRequest for {Data?.DisplayName} -> slot {slotIndex}");
            inputBlocked = true;
        }
        else
        {
            Debug.Log($"[CardsController] OnEndDrag -> dropped outside slot, reverting {Data?.DisplayName}");
            view?.OnDragCanceled();
        }
    }
    #endregion

    #region API pública (aprobación / rechazo)
    // Cuando la autoridad aprueba, movemos la vista y confirmamos (sin animaciones)
    public void OnPlayApproved(int slotIndex, Vector3 slotWorldPos, Quaternion slotWorldRot)
    {
        Debug.Log($"[CardsController] OnPlayApproved: moving {Data?.DisplayName} to slot {slotIndex}");
        view?.AnimatePlaceOnTable(slotIndex, slotWorldPos, slotWorldRot, () =>
        {
            view?.ConfirmPlacement(slotIndex);
            inputBlocked = false;
            Debug.Log($"[CardsController] Placement completed for {Data?.DisplayName} in slot {slotIndex}");
            // NOTA: la confirmación lógica (TableManager.ConfirmPlacementToSlot) debe ser llamada
            // por el TurnManager/autoridad cuando corresponda.
        });
    }

    public void OnPlayRejected(string reason = null)
    {
        inputBlocked = false;
        Debug.Log($"[CardsController] OnPlayRejected for {Data?.DisplayName}. Reason: {reason}");
        view?.PlayRejectFeedback(reason);
        view?.CancelPlacement();
    }

    public void CancelInteraction()
    {
        isDragging = false;
        inputBlocked = false;
        view?.OnDragCanceled();
        Debug.Log($"[CardsController] CancelInteraction for {Data?.DisplayName}");
    }
    #endregion
}

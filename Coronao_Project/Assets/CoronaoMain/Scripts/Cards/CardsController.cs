// CardsController.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using static CardsData;

[RequireComponent(typeof(Collider))]
public class CardsController : MonoBehaviour,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler,
    IPoolable
{
    [Header("References")]
    [SerializeField] private CardView view;
    [SerializeField] private float clickDebounce = 0.25f;
    [SerializeField] private LayerMask slotLayerMask;
    [SerializeField] private Camera worldCamera;

    // Datos de instancia
    public string InstanceId { get; private set; }
    public CardsData Data { get; private set; }

    // Pool reference (optional)
    public CardsPool OwningPool { get; set; }

    // Estado runtime
    private bool isDragging = false;
    private bool inputBlocked = false;
    private float lastClickTime = -999f;

    // Eventos (intención)
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
    }

    #region Input handlers
    public void OnPointerDown(PointerEventData eventData)
    {
        if (inputBlocked) return;
        view?.SetHovered(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inputBlocked) return;
        if (Time.realtimeSinceStartup - lastClickTime < clickDebounce) return;
        lastClickTime = Time.realtimeSinceStartup;
        if (Data != null && Data.TargetReq == CardTargetRequirement.None)
        {
            view?.PlayPrePlayAnimation();
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
            OnPlayRequested?.Invoke(req);
            view?.PlayPrePlayAnimation();
            inputBlocked = true;
        }
        else
        {
            view?.OnDragCanceled();
        }
    }
    #endregion

    #region Public API called by authoritative logic (TurnManager / TableManager)
    public void OnPlayApproved(int slotIndex, Vector3 slotWorldPos, Quaternion slotWorldRot)
    {
        view?.AnimatePlaceOnTable(slotIndex, slotWorldPos, slotWorldRot, () =>
        {
            view?.ConfirmPlacement(slotIndex);
            inputBlocked = false;
            // Notificar autoridad que la animación terminó y se confirma resolución
            // Esto lo hará el TurnManager; aquí solo visualmente confirmamos.
        });
    }

    public void OnPlayRejected(string reason = null)
    {
        inputBlocked = false;
        view?.PlayRejectFeedback(reason);
        view?.CancelPlacement();
    }

    public void CancelInteraction()
    {
        isDragging = false;
        inputBlocked = false;
        view?.OnDragCanceled();
    }
    #endregion

    #region Pool hooks
    public void OnPoolSpawn()
    {
        // reset runtime state
        isDragging = false;
        inputBlocked = false;
        InstanceId = Guid.NewGuid().ToString(); // new instance id for reuse
        // ensure the object is active and ready visually
        view?.HideTooltip();
    }

    public void OnPoolDespawn()
    {
        // cleanup
        CancelInteraction();
        // reset parent/transform if needed by factory
    }
    #endregion
}

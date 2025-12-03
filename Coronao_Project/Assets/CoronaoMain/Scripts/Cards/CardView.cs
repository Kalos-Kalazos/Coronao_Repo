using System;
using System.Collections;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class CardView : MonoBehaviour
{
    [Header("Renderer - 3D")]
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Material defaultMaterial;

    [Header("Transforming / Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float elevateOnDrag = 0.25f;
    [SerializeField] private float dragLerpSpeed = 20f;
    [SerializeField] private float placementSnapDuration = 0.25f;

    [Header("Optional Tooltip (TextMeshPro)")]
    [SerializeField] private GameObject tooltipGO;
    [SerializeField] private TextMeshPro tooltipText;

    // Estado
    private CardsData data;
    private Coroutine currentMoveCoroutine;
    private bool draggingVisualActive = false;
    private Vector3 originalPositionLocal;
    private Quaternion originalRotationLocal;
    private Transform originalParent;
    private bool isPlacedOnTable = false;
    private int placedSlotIndex = -1;

    // Eventos visuales (opcional)
    public event Action OnVisualPlacementComplete;
    public event Action OnVisualCancelPlacement;
    public event Action OnEvolveAnimationComplete; // seguirá existiendo como hook, pero se usará solo para log

    private void Reset()
    {
        // nothing
    }

    // Inicialización desde factory
    public void InitFromData(CardsData cardsData)
    {
        data = cardsData;
        originalParent = transform.parent;
        originalPositionLocal = transform.localPosition;
        originalRotationLocal = transform.localRotation;

        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (meshRenderer != null && defaultMaterial != null && meshRenderer.sharedMaterial == null)
            meshRenderer.sharedMaterial = defaultMaterial;

        if (tooltipText != null && data != null)
        {
            tooltipText.text = $"{data.DisplayName}\n{data.Description}";
            if (tooltipGO != null) tooltipGO.SetActive(false);
        }

        Debug.Log($"[CardView] InitFromData: {data?.DisplayName ?? "NULL"}");
    }

    public void SetCardMaterial(Material mat)
    {
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null && mat != null)
            meshRenderer.material = mat;
    }

    public void SetHovered(bool hovered)
    {
        // Solo logueamos la acción; no animamos.
        Debug.Log($"[CardView] SetHovered: {(hovered ? "ON" : "OFF")} for {data?.DisplayName}");
        if (tooltipGO != null && !draggingVisualActive)
            tooltipGO.SetActive(hovered);
    }

    public void OnDragStart()
    {
        draggingVisualActive = true;
        StopCurrentMove();
        Vector3 elevated = transform.position + Vector3.up * elevateOnDrag;
        currentMoveCoroutine = StartCoroutine(MoveToWorldPosition(elevated, 0.12f));
        if (tooltipGO != null) tooltipGO.SetActive(false);
        Debug.Log($"[CardView] OnDragStart for {data?.DisplayName}");
    }

    public void DragFollowTo(Vector3 worldTarget, float followLerp = -1f)
    {
        if (followLerp <= 0f) followLerp = dragLerpSpeed;
        StopCurrentMove();
        currentMoveCoroutine = StartCoroutine(SmoothFollow(worldTarget, followLerp));
    }

    public void OnDragCanceled()
    {
        draggingVisualActive = false;
        StopCurrentMove();
        if (!isPlacedOnTable)
            currentMoveCoroutine = StartCoroutine(MoveToLocalPosition(originalPositionLocal, 0.15f));
        Debug.Log($"[CardView] OnDragCanceled for {data?.DisplayName}");
        OnVisualCancelPlacement?.Invoke();
    }

    // Animate place on table -> ahora no hay animación: movemos suavemente y llamamos al callback inmediatamente al terminar
    public void AnimatePlaceOnTable(int slotIndex, Vector3 slotWorldPosition, Quaternion slotWorldRotation, Action onComplete = null)
    {
        StopCurrentMove();
        Debug.Log($"[CardView] AnimatePlaceOnTable -> slot {slotIndex} for {data?.DisplayName}");
        currentMoveCoroutine = StartCoroutine(PlaceSequence(slotIndex, slotWorldPosition, slotWorldRotation, onComplete));
    }

    public void ConfirmPlacement(int slotIndex)
    {
        isPlacedOnTable = true;
        placedSlotIndex = slotIndex;
        Debug.Log($"[CardView] ConfirmPlacement: card {data?.DisplayName} confirmed in slot {slotIndex}");
    }

    public void CancelPlacement()
    {
        isPlacedOnTable = false;
        placedSlotIndex = -1;
        StopCurrentMove();
        currentMoveCoroutine = StartCoroutine(MoveToLocalPosition(originalPositionLocal, placementSnapDuration));
        Debug.Log($"[CardView] CancelPlacement for {data?.DisplayName}");
        OnVisualCancelPlacement?.Invoke();
    }

    public void AnimateSwapTo(Vector3 otherSlotWorldPos, Quaternion otherSlotRot, Action onComplete = null)
    {
        StopCurrentMove();
        Debug.Log($"[CardView] AnimateSwapTo called for {data?.DisplayName}");
        currentMoveCoroutine = StartCoroutine(SwapSequence(otherSlotWorldPos, otherSlotRot, onComplete));
    }

    // Evolve animation replaced by simple log + immediate callback
    public void PlayEvolveAnimation(Action onComplete = null)
    {
        Debug.Log($"[CardView] PlayEvolveAnimation (LOG ONLY) for {data?.DisplayName}");
        // Aquí iría VFX/SFX cuando lo añadas
        OnEvolveAnimationComplete?.Invoke();
        onComplete?.InvokeSafely();
    }

    public void PlayPrePlayAnimation()
    {
        Debug.Log($"[CardView] PlayPrePlayAnimation (LOG) for {data?.DisplayName}");
    }

    public void PlayRejectFeedback(string reason = null)
    {
        Debug.Log($"[CardView] PlayRejectFeedback for {data?.DisplayName}. Reason: {reason}");
        if (!string.IsNullOrEmpty(reason) && tooltipText != null && tooltipGO != null)
        {
            tooltipText.text = reason;
            tooltipGO.SetActive(true);
            StopCoroutine(nameof(AutoHideTooltip));
            StartCoroutine(AutoHideTooltip(1.2f));
        }
    }

    private IEnumerator PlaceSequence(int slotIndex, Vector3 slotWorldPos, Quaternion slotWorldRot, Action onComplete)
    {
        Debug.Log($"[CardView] PlaceSequence START slot {slotIndex} for {data?.DisplayName}");
        yield return MoveWorldTo(slotWorldPos, slotWorldRot, placementSnapDuration);
        isPlacedOnTable = true;
        placedSlotIndex = slotIndex;
        Debug.Log($"[CardView] PlaceSequence COMPLETE slot {slotIndex} for {data?.DisplayName}");
        OnVisualPlacementComplete?.Invoke();
        onComplete?.InvokeSafely();
    }

    private IEnumerator SwapSequence(Vector3 otherWorldPos, Quaternion otherRot, Action onComplete)
    {
        yield return MoveWorldTo(otherWorldPos, otherRot, placementSnapDuration);
        onComplete?.InvokeSafely();
    }

    private IEnumerator SmoothFollow(Vector3 worldTarget, float lerpSpeed)
    {
        while (true)
        {
            transform.position = Vector3.Lerp(transform.position, worldTarget, Time.deltaTime * lerpSpeed);
            yield return null;
        }
    }

    private IEnumerator MoveToWorldPosition(Vector3 worldTarget, float duration)
    {
        if (duration <= 0f) { transform.position = worldTarget; yield break; }
        Vector3 start = transform.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, worldTarget, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        transform.position = worldTarget;
    }

    private IEnumerator MoveToLocalPosition(Vector3 localTarget, float duration)
    {
        Vector3 start = transform.localPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(start, localTarget, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        transform.localPosition = localTarget;
    }

    private IEnumerator MoveWorldTo(Vector3 worldPos, Quaternion worldRot, float duration)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.SmoothStep(0f, 1f, t / duration);
            transform.position = Vector3.Lerp(startPos, worldPos, n);
            transform.rotation = Quaternion.Slerp(startRot, worldRot, n);
            yield return null;
        }
        transform.position = worldPos;
        transform.rotation = worldRot;
    }

    private IEnumerator AutoHideTooltip(float wait)
    {
        yield return new WaitForSeconds(wait);
        if (tooltipGO != null) tooltipGO.SetActive(false);
        if (tooltipText != null && data != null)
            tooltipText.text = $"{data.DisplayName}\n{data.Description}";
    }

    private void StopCurrentMove()
    {
        if (currentMoveCoroutine != null)
        {
            StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = null;
        }
    }

    public void ShowTooltip()
    {
        if (tooltipGO == null || tooltipText == null || data == null) return;
        tooltipText.text = $"{data.DisplayName}\n{data.Description}";
        tooltipGO.SetActive(true);
        Debug.Log($"[CardView] ShowTooltip for {data.DisplayName}");
    }

    public void HideTooltip()
    {
        if (tooltipGO != null) tooltipGO.SetActive(false);
    }

    private void OnDisable()
    {
        StopCurrentMove();
        if (tooltipGO != null) tooltipGO.SetActive(false);
    }
}

// Extension helper
public static class ActionExtensions
{
    public static void InvokeSafely(this Action a)
    {
        a?.Invoke();
    }

    public static void InvokeSafely<T>(this Action<T> a, T arg)
    {
        a?.Invoke(arg);
    }
}

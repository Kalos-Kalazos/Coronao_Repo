using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Animator))]

public class CardView : MonoBehaviour
{
    [Header("Renderer - 3D")]
    [SerializeField] private MeshRenderer meshRenderer;        // material / textura diferente por carta
    [SerializeField] private Material defaultMaterial;
    // Si usas texturas en lugar de materiales, el factory puede crear un material instanciado y
    // asignarlo aquí con SetCardMaterial(material).

    [Header("Transforming / Movement")]
    [SerializeField] private float moveSpeed = 8f;             // velocidad de Lerp para movimientos
    [SerializeField] private float elevateOnDrag = 0.25f;      // altura relativa cuando se arrastra
    [SerializeField] private float dragLerpSpeed = 20f;        // suavizado cuando sigue cursor
    [SerializeField] private float placementSnapDuration = 0.25f;

    [Header("Animator parameters")]
    [SerializeField] private Animator animator;
    [SerializeField] private string triggerPlay = "Play";
    [SerializeField] private string triggerReject = "Reject";
    [SerializeField] private string boolHovered = "Hovered";
    [SerializeField] private string triggerPrePlay = "PrePlay";
    [SerializeField] private string triggerEvolve = "Evolve"; // animación de convertirse en figura
    [SerializeField] private float playAnimationFallback = 0.6f;

    [Header("Optional UI Tooltip (can be a small world-space canvas)")]
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

    // Eventos visuales (opcional) - el controlador puede suscribirse si quiere
    public event Action OnVisualPlacementComplete;
    public event Action OnVisualCancelPlacement;
    public event Action OnEvolveAnimationComplete;

    private void Reset()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    public void InitFromData(CardsData cardsdata)
    {
        data = cardsdata;
        // guardamos transform original para revertir si hace falta
        originalParent = transform.parent;
        originalPositionLocal = transform.localPosition;
        originalRotationLocal = transform.localRotation;

        // Asignar material: el factory idealmente hará una instancia de material por carta
        // y llamará a SetCardMaterial(instancedMaterial). Si no, usamos defaultMaterial.
        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (meshRenderer != null)
        {
            if (defaultMaterial != null && meshRenderer.sharedMaterial == null)
                meshRenderer.sharedMaterial = defaultMaterial;
        }

        // Tooltip
        if (tooltipText != null && data != null)
        {
            tooltipText.text = $"{data.DisplayName}\n{data.Description}";
            if (tooltipGO != null) tooltipGO.SetActive(false);
        }
    }

    public void SetCardMaterial(Material mat)
    {
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null && mat != null)
            meshRenderer.material = mat; // usar instancia de material para no modificar el sharedMaterial global
    }

    // ---------------------------
    // Hover / highlight
    // ---------------------------
    public void SetHovered(bool hovered)
    {
        if (animator != null && !string.IsNullOrEmpty(boolHovered))
            animator.SetBool(boolHovered, hovered);

        if (tooltipGO != null && !draggingVisualActive)
            tooltipGO.SetActive(hovered);
    }

    // ---------------------------
    // Dragging visuals / follow cursor
    // ---------------------------
    public void OnDragStart()
    {
        draggingVisualActive = true;
        // elevar un poco el objeto para dar sensación de "levantar"
        StopCurrentMove();
        Vector3 elevated = transform.position + Vector3.up * elevateOnDrag;
        currentMoveCoroutine = StartCoroutine(MoveToWorldPosition(elevated, 0.12f, true)); // pequeño lift
        if (tooltipGO != null) tooltipGO.SetActive(false);
    }

    /// <summary>
    /// Llamar cada frame desde CardController.OnDrag para que la carta siga al cursor.
    /// Se asume que CardController calcula la worldPosition objetivo del cursor y lo pasa aquí.
    /// </summary>
    public void DragFollowTo(Vector3 worldTarget, float followLerp = -1f)
    {
        if (followLerp <= 0f) followLerp = dragLerpSpeed;
        StopCurrentMove(); // dejamos cualquier movimiento suave anterior
        // simple Lerp en Update estilo: usamos coroutine para suavizado con Lerp
        currentMoveCoroutine = StartCoroutine(SmoothFollow(worldTarget, followLerp));
    }

    public void OnDragCanceled()
    {
        draggingVisualActive = false;
        StopCurrentMove();
        // volver a la posición original si no estaba colocada
        if (!isPlacedOnTable)
            currentMoveCoroutine = StartCoroutine(MoveToLocalPosition(originalPositionLocal, 0.15f, true));
    }

    // ---------------------------
    // Placement on table (visual)
    // ---------------------------
    public void AnimatePlaceOnTable(int slotIndex, Vector3 slotWorldPosition, Quaternion slotWorldRotation, Action onComplete = null)
    {
        StopCurrentMove();
        // animación de ir a la posición (snap suave)
        currentMoveCoroutine = StartCoroutine(PlaceSequence(slotIndex, slotWorldPosition, slotWorldRotation, onComplete));
    }
    public void ConfirmPlacement(int slotIndex)
    {
        isPlacedOnTable = true;
        placedSlotIndex = slotIndex;
        // aquí podrías activar una animación de "placed" si la tienes
        // animator.SetTrigger("Placed"); // opcional
    }

    /// <summary>
    /// Si la colocación es rechazada o cancelada, revertimos visualmente.
    /// </summary>
    public void CancelPlacement()
    {
        isPlacedOnTable = false;
        placedSlotIndex = -1;
        StopCurrentMove();
        currentMoveCoroutine = StartCoroutine(MoveToLocalPosition(originalPositionLocal, placementSnapDuration, true));
        OnVisualCancelPlacement?.Invoke();
    }

    public void AnimateSwapTo(Vector3 otherSlotWorldPos, Quaternion otherSlotRot, Action onComplete = null)
    {
        StopCurrentMove();
        currentMoveCoroutine = StartCoroutine(SwapSequence(otherSlotWorldPos, otherSlotRot, onComplete));
    }

    // ---------------------------
    // Evolve animation (become figure)
    // ---------------------------
    public void PlayEvolveAnimation(Action onComplete = null)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerEvolve))
            animator.SetTrigger(triggerEvolve);

        // Aquí se podría cambiar material / mesh para la versión "figura".
        // El cambio real del modelo (mesh/material) deberías hacerlo desde el factory o
        // en la respuesta del GameLogic tras confirmar la evolución.
        // VFX / SFX de evolución: lugar donde se llamarían (comentario)
        // -- Instanciar VFX en un spawnPoint child aquí --
        // -- Reproducir SFX con AudioSource aquí --

        // si la animación tiene duración, esperar y llamar onComplete.
        float wait = GetPlayAnimationDuration();
        if (currentMoveCoroutine != null) StopCoroutine(currentMoveCoroutine);
        currentMoveCoroutine = StartCoroutine(WaitAndInvoke(wait, () =>
        {
            OnEvolveAnimationComplete?.Invoke();
            onComplete?.Invoke();
        }));
    }

    // ---------------------------
    // Play / Reject (visual feedback)
    // ---------------------------
    public void PlayPrePlayAnimation()
    {
        if (animator != null && !string.IsNullOrEmpty(triggerPrePlay))
            animator.SetTrigger(triggerPrePlay);
    }

    public void PlayAnimation(string animationTrigger)
    {
        if (animator == null || string.IsNullOrEmpty(animationTrigger)) return;
        animator.SetTrigger(animationTrigger);
    }

    public void PlayRejectFeedback(string reason = null)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerReject))
            animator.SetTrigger(triggerReject);

        // Mostrar tooltip con la razón (opcional)
        if (!string.IsNullOrEmpty(reason) && tooltipText != null && tooltipGO != null)
        {
            tooltipText.text = reason;
            tooltipGO.SetActive(true);
            // hide after short time
            if (currentMoveCoroutine != null) StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = StartCoroutine(AutoHideTooltip(1.2f));
        }
    }

    // ---------------------------
    // Helpers / Coroutines
    // ---------------------------
    private IEnumerator PlaceSequence(int slotIndex, Vector3 slotWorldPos, Quaternion slotWorldRot, Action onComplete)
    {
        // opcional: small pop or pre-play trigger
        PlayPrePlayAnimation();

        // mover suavemente al slot
        yield return MoveWorldTo(slotWorldPos, slotWorldRot, placementSnapDuration);

        // asiento final
        isPlacedOnTable = true;
        placedSlotIndex = slotIndex;

        // callback visual
        OnVisualPlacementComplete?.Invoke();
        onComplete?.InvokeSafely();
    }

    private IEnumerator SwapSequence(Vector3 otherWorldPos, Quaternion otherRot, Action onComplete)
    {
        // anim de swap (podrías añadir rotacion/tilt)
        yield return MoveWorldTo(otherWorldPos, otherRot, placementSnapDuration);
        onComplete?.InvokeSafely();
    }

    private IEnumerator SmoothFollow(Vector3 worldTarget, float lerpSpeed)
    {
        // Sigue al target hasta que alguien lo detenga
        while (true)
        {
            transform.position = Vector3.Lerp(transform.position, worldTarget, Time.deltaTime * lerpSpeed);
            yield return null;
        }
    }

    private IEnumerator MoveToWorldPosition(Vector3 worldTarget, float duration, bool useLocal = false)
    {
        if (duration <= 0f)
        {
            transform.position = worldTarget;
            yield break;
        }

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

    private IEnumerator MoveToLocalPosition(Vector3 localTarget, float duration, bool restoreParent = false)
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
        if (restoreParent && originalParent != null)
            transform.SetParent(originalParent, true);
    }

    private IEnumerator MoveWorldTo(Vector3 worldPos, Quaternion worldRot, float duration)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.SmoothStep(0f, 1f, t / duration);
            transform.position = Vector3.Lerp(startPos, worldPos, normalized);
            transform.rotation = Quaternion.Slerp(startRot, worldRot, normalized);
            yield return null;
        }
        transform.position = worldPos;
        transform.rotation = worldRot;
    }

    private IEnumerator WaitAndInvoke(float seconds, Action callback)
    {
        yield return new WaitForSeconds(seconds);
        callback?.Invoke();
    }

    private IEnumerator AutoHideTooltip(float wait)
    {
        yield return new WaitForSeconds(wait);
        if (tooltipGO != null) tooltipGO.SetActive(false);
        // restaurar tooltip original si tienes data
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

    private float GetPlayAnimationDuration()
    {
        if (animator == null) return playAnimationFallback;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.length > 0.01f) return info.length;
        var clips = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : null;
        if (clips != null && clips.Length > 0)
            return clips[0].length;
        return playAnimationFallback;
    }  
    
    // ---------------------------
    // Utilities / Cleanup
    // ---------------------------
    public void ShowTooltip()
    {
        if (tooltipGO == null || tooltipText == null || data == null) return;
        tooltipText.text = $"{data.DisplayName}\n{data.Description}";
        tooltipGO.SetActive(true);
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


/// <summary>
/// Helpers extension to safely call Action (avoid null checks everywhere)
/// </summary>
public static class ActionExtensions
{
    public static void InvokeSafely(this Action a)
    {
        a?.Invoke();
    }
}


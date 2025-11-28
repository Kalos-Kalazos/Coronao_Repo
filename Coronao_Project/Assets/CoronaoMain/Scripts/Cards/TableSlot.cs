using UnityEngine;

/// <summary>
/// TableSlot: representa una posición en la mesa donde una carta puede colocarse.
/// - Tiene un transform objetivo (slotAnchor) que define la posición/rotación final de la carta.
/// - Mantiene referencia a la CardController que ocupa el slot (si hay).
/// - Proporciona métodos para ocupar/vaciar y consultar disponibilidad.
/// - Dibuja gizmos para facilitar el diseño en el editor.
/// </summary>
[DisallowMultipleComponent]
public class TableSlot : MonoBehaviour
{
    [Header("Slot Settings")]
    [Tooltip("Índice identificador del slot (0..N-1).")]
    public int SlotIndex = 0;

    [Tooltip("Transform que marca la posición exacta donde la carta debe quedar. " +
             "Si está vacío, se usará el transform de este GameObject.")]
    public Transform SlotAnchor;

    [Tooltip("Radio para gizmo / snap visual")]
    public float gizmoRadius = 0.12f;

    [Header("Debug / Runtime (read-only in inspector)")]
    [SerializeField] // ReadOnly es solo indicativo (requiere custom attribute o inspector), no obligatorio
    private bool occupied = false;

    [SerializeField]
    private GameObject occupyingCard = null;

    /// <summary>
    /// Posición final en world space del slot.
    /// </summary>
    public Vector3 WorldPosition => (SlotAnchor != null) ? SlotAnchor.position : transform.position;

    /// <summary>
    /// Rotación final en world space del slot.
    /// </summary>
    public Quaternion WorldRotation => (SlotAnchor != null) ? SlotAnchor.rotation : transform.rotation;

    /// <summary>
    /// ¿Está libre el slot?
    /// </summary>
    public bool IsAvailable => !occupied;

    /// <summary>
    /// Referencia a la card que ocupa el slot (null si libre).
    /// </summary>
    public GameObject OccupyingCard => occupyingCard;

    /// <summary>
    /// Marca el slot como ocupado por la card indicada.
    /// Devuelve true si se pudo ocupar; false si ya estaba ocupado.
    /// </summary>
    public bool Occupy(GameObject card)
    {
        if (card == null) return false;
        if (occupied) return false;

        occupyingCard = card;
        occupied = true;
        return true;
    }

    /// <summary>
    /// Libera el slot si la card indicada coincide con la que lo ocupa.
    /// Si card == null vacía el slot sin comprobación adicional.
    /// Devuelve true si se liberó, false si no (ej. card no coincidía).
    /// </summary>
    public bool Vacate(GameObject card = null)
    {
        if (!occupied) return false;

        if (card == null || occupyingCard == card)
        {
            occupyingCard = null;
            occupied = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Forzar vaciado (útil para reset desde TableManager).
    /// </summary>
    public void ForceVacate()
    {
        occupyingCard = null;
        occupied = false;
    }

    /// <summary>
    /// Intento de swap: si el slot está ocupado devuelve la referencia actual,
    /// y ocupa con la nueva card (atomically swapping outside si se desea).
    /// Devuelve la previous occupying card (null si estaba libre).
    /// </summary>
    public GameObject ReplaceOccupant(GameObject newCard)
    {
        GameObject previous = occupyingCard;
        occupyingCard = newCard;
        occupied = newCard != null;
        return previous;
    }

    #region Editor Gizmos
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 pos = WorldPosition;
        Gizmos.color = occupied ? Color.red : Color.green;
        Gizmos.DrawSphere(pos, gizmoRadius);

        // dibujar orientación: una pequeña línea en forward
        Vector3 forward = (SlotAnchor != null ? SlotAnchor.forward : transform.forward);
        Gizmos.DrawLine(pos, pos + forward * gizmoRadius * 2f);

        // Etiqueta con índice
#if UNITY_EDITOR
        UnityEditor.Handles.Label(pos + Vector3.up * gizmoRadius * 0.15f, $"Slot {SlotIndex}");
#endif
    }
#endif
    #endregion
}


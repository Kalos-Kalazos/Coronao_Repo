using System;
using System.Collections.Generic;
using UnityEngine;
using static CardsData;

[DisallowMultipleComponent]
public class TableManager : MonoBehaviour
{
    [Header("Slots (assign children or auto-find)")]
    [Tooltip("Si está vacío, buscará TableSlot en los hijos.")]
    [SerializeField] private TableSlot[] slots;

    // Stack por slot: lista con orden de colocación [0]=first, last = top
    private List<CardsController>[] slotStacks;

    // Flags por slot
    private bool[] slotHasFigure;
    private bool[] slotPendingConversion;

    [Header("Auto conversion settings")]
    [Tooltip("Si true: TableManager convertirá automáticamente una pila que llegue a 10 en una figura.")]
    [SerializeField] private bool autoConvertToFigure = true;

    [Tooltip("Prefab de CardController que se usará para instanciar la figura resultante.")]
    [SerializeField] private CardsController cardControllerPrefab;

    [Tooltip("CardData por defecto que se usará para crear la figura (si no usas mapeo). Debe tener IsFigure = true).")]
    [SerializeField] private CardsData defaultFigureCardData;

    [Tooltip("Opcional: mapping que permite seleccionar diferentes figuras en función de la suma u otra llave.")]
    [SerializeField] private SumToFigureMapping[] sumToFigureMappings = new SumToFigureMapping[0];

    [Tooltip("Parent transform donde instanciar las figuras (opcional). Si es null, se instanciará en root.")]
    [SerializeField] private Transform figuresParent;

    #region Events
    public event Action<int, CardsController> OnCardPlaced;
    public event Action<int, IReadOnlyList<CardsController>> OnSlotReachedExactlyTen;
    public event Action<int, CardsController> OnDrawOptionAvailable;
    public event Action<int> OnSlotCleared;

    // Nuevo: cuando TableManager convierta automáticamente la pila a figura
    public event Action<int, CardsController> OnSlotAutoConverted;
    #endregion

    [Serializable]
    public struct SumToFigureMapping
    {
        public int sumKey;          // por ejemplo 10 -> figuraA
        public CardsData figureData; // debe ser tipo figura
    }

    private void Awake()
    {
        if (slots == null || slots.Length == 0)
        {
            slots = GetComponentsInChildren<TableSlot>(true);
            Array.Sort(slots, (a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
        }

        int n = slots.Length;
        slotStacks = new List<CardsController>[n];
        slotHasFigure = new bool[n];
        slotPendingConversion = new bool[n];
        for (int i = 0; i < n; i++)
        {
            slotStacks[i] = new List<CardsController>();
            slotHasFigure[i] = false;
            slotPendingConversion[i] = false;
        }
    }

    // -----------------------
    // TryPlace & Confirm (idéntico al anterior)
    // -----------------------
    public bool TryPlaceCardInSlot(int slotIndex, CardsController card, out Vector3 outPosition, out Quaternion outRotation, out bool drawAvailable, out string rejectReason)
    {
        outPosition = Vector3.zero;
        outRotation = Quaternion.identity;
        drawAvailable = false;
        rejectReason = null;

        if (!IsValidSlotIndex(slotIndex))
        {
            rejectReason = "Slot inválido";
            return false;
        }

        if (card == null || card.Data == null)
        {
            rejectReason = "Carta inválida";
            return false;
        }

        if (slotHasFigure[slotIndex] || slotPendingConversion[slotIndex])
        {
            rejectReason = "Slot ocupado por una figura";
            return false;
        }

        var stack = slotStacks[slotIndex];

        bool incomingIsNumeric = !card.Data.IsFigure;

        if (stack.Count == 0)
        {
            outPosition = slots[slotIndex].WorldPosition;
            outRotation = slots[slotIndex].WorldRotation;
            return true;
        }

        foreach (var c in stack)
        {
            if (c != null && c.Data != null && c.Data.IsFigure)
            {
                rejectReason = "Slot contiene figura";
                return false;
            }
        }

        if (!incomingIsNumeric)
        {
            rejectReason = "No se puede colocar figura en un slot con números";
            return false;
        }

        int currentSum = 0;
        foreach (var c in stack)
        {
            if (c != null && c.Data != null)
                currentSum += c.Data.BaseValue;
        }
        int newSum = currentSum + card.Data.BaseValue;

        if (newSum == 10)
        {
            slotPendingConversion[slotIndex] = true;
            outPosition = slots[slotIndex].WorldPosition;
            outRotation = slots[slotIndex].WorldRotation;
            return true;
        }

        if (newSum == 11 || newSum == 12)
        {
            drawAvailable = true;
            outPosition = slots[slotIndex].WorldPosition;
            outRotation = slots[slotIndex].WorldRotation;
            return true;
        }

        outPosition = slots[slotIndex].WorldPosition;
        outRotation = slots[slotIndex].WorldRotation;
        return true;
    }

    public bool ConfirmPlacementToSlot(int slotIndex, CardsController card, out bool triggeredExactlyTen, out bool drawAvailable)
    {
        triggeredExactlyTen = false;
        drawAvailable = false;

        if (!IsValidSlotIndex(slotIndex) || card == null) return false;
        if (slotHasFigure[slotIndex])
        {
            Debug.LogWarning($"ConfirmPlacement: slot {slotIndex} ya tiene figura.");
            return false;
        }

        var stack = slotStacks[slotIndex];
        stack.Add(card);

        int sum = 0;
        foreach (var c in stack)
            sum += (c?.Data != null ? c.Data.BaseValue : 0);

        if (sum == 10)
        {
            triggeredExactlyTen = true;
            slotPendingConversion[slotIndex] = true;
            OnSlotReachedExactlyTen?.Invoke(slotIndex, stack.AsReadOnly());

            // Si la conversión automática está activa, la realizamos aquí.
            if (autoConvertToFigure)
            {
                // Forzamos conversión automática (internamente vacía y crea figura)
                var figureController = AutoConvertSlotToFigure(slotIndex);
                if (figureController != null)
                {
                    // notificar conversión automática
                    OnSlotAutoConverted?.Invoke(slotIndex, figureController);
                }
            }
        }
        else if (sum == 11 || sum == 12)
        {
            drawAvailable = true;
            OnDrawOptionAvailable?.Invoke(slotIndex, card);
        }

        OnCardPlaced?.Invoke(slotIndex, card);

        foreach (var c in stack)
        {
            if (c != null && c.Data != null && c.Data.IsFigure)
            {
                slotHasFigure[slotIndex] = true;
                break;
            }
        }

        return true;
    }
    private CardsController AutoConvertSlotToFigure(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) return null;

        // choose figure data
        CardsData figureData = ResolveFigureDataForSlot(slotIndex);
        if (figureData == null)
        {
            Debug.LogWarning("AutoConvert: no figure data.");
            return null;
        }

        // keep previous stack references
        var prevStack = new List<CardsController>(slotStacks[slotIndex]);

        // Try to use CardsPool in scene
        CardsPool pool = FindObjectOfType<CardsPool>();

        // Clear stack state first
        slotStacks[slotIndex].Clear();
        slotHasFigure[slotIndex] = true;
        slotPendingConversion[slotIndex] = false;

        // If pool exists: return each card to pool. Otherwise Destroy.
        foreach (var c in prevStack)
        {
            if (c == null) continue;
            if (pool != null)
            {
                pool.Release(c); // CardsPool.Release handles OnPoolDespawn + deactivate
            }
            else
            {
                Destroy(c.gameObject);
            }
        }

        // Instantiate new figure via pool if possible
        CardsController newController = null;
        if (pool != null)
        {
            newController = pool.Get();
            newController.transform.SetPositionAndRotation(slots[slotIndex].WorldPosition, slots[slotIndex].WorldRotation);
            newController.Init(figureData, Guid.NewGuid().ToString());
            newController.OwningPool = pool;
        }
        else
        {
            // fallback: instantiate prefab directly (requires cardControllerPrefab reference)
            if (cardControllerPrefab == null)
            {
                Debug.LogWarning("AutoConvert requires cardControllerPrefab or CardsPool.");
                return null;
            }
            var go = Instantiate(cardControllerPrefab.gameObject, slots[slotIndex].WorldPosition, slots[slotIndex].WorldRotation, figuresParent != null ? figuresParent : null);
            newController = go.GetComponent<CardsController>();
            newController.Init(figureData, Guid.NewGuid().ToString());
        }

        // Play evolve animation on its CardView (visual hook)
        newController.GetComponent<CardView>()?.PlayEvolveAnimation();

        OnSlotCleared?.Invoke(slotIndex);
        return newController;
    }

    /// <summary>
    /// Busca el CardData adecuado según sum -> figure mapping; si no encuentra, devuelve defaultFigureCardData.
    /// </summary>
    private CardsData ResolveFigureDataForSlot(int slotIndex)
    {
        // calcular suma actual (antes de vaciar) si necesitas mapear según suma; pero en AutoConvertSlotToFigure
        // ya vaciamos la pila arriba. Si quieres mapear por la suma previa, deberías calcularla antes de vaciar.
        // Aquí asumimos que el mapeo es por la suma que originó la conversion y que el evento OnSlotReachedExactlyTen se lanzó antes.
        // Para simplicidad, si tienes mappings, buscamos mapping con sumKey==10; si no, usamos default.
        foreach (var m in sumToFigureMappings)
        {
            if (m.sumKey == 10 && m.figureData != null)
                return m.figureData;
        }
        return defaultFigureCardData;
    }

    // -----------------------
    // Otros helpers (idénticos)
    // -----------------------
    public bool IsValidSlotIndex(int idx) => (idx >= 0 && idx < slots.Length);
    public TableSlot GetSlot(int idx) => IsValidSlotIndex(idx) ? slots[idx] : null;
    public IReadOnlyList<CardsController> GetStackReadonly(int idx) => IsValidSlotIndex(idx) ? slotStacks[idx].AsReadOnly() : null;

    public List<CardsController> ClearSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) return null;
        var prev = new List<CardsController>(slotStacks[slotIndex]);
        // destruirlos / devolver al pool
        foreach (var c in prev) if (c != null) Destroy(c.gameObject);
        slotStacks[slotIndex].Clear();
        slotHasFigure[slotIndex] = false;
        slotPendingConversion[slotIndex] = false;
        OnSlotCleared?.Invoke(slotIndex);
        return prev;
    }

    public List<CardsController> ForceConvertSlotToFigure(int slotIndex)
    {
        // Método mantenido para compatibilidad si quieres más control externo.
        // Llama a AutoConvertSlotToFigure internamente.
        var controller = AutoConvertSlotToFigure(slotIndex);
        return controller != null ? new List<CardsController> { controller } : null;
    }

    public bool TrySwapBetweenSlots(int slotA, int slotB, CardsController cardFromA, out string rejectReason)
    {
        rejectReason = null;
        if (!IsValidSlotIndex(slotA) || !IsValidSlotIndex(slotB))
        {
            rejectReason = "Slot inválido";
            return false;
        }

        if (slotHasFigure[slotB])
        {
            rejectReason = "Destino ocupado por figura";
            return false;
        }

        var stackA = slotStacks[slotA];
        if (!stackA.Remove(cardFromA))
        {
            rejectReason = "La carta no estaba en el slot origen";
            return false;
        }

        slotStacks[slotB].Add(cardFromA);
        return true;
    }

    public bool SlotHasFigure(int slotIndex) => IsValidSlotIndex(slotIndex) && slotHasFigure[slotIndex];
    public bool SlotIsPendingConversion(int slotIndex) => IsValidSlotIndex(slotIndex) && slotPendingConversion[slotIndex];
    public int SlotCount(int slotIndex) => IsValidSlotIndex(slotIndex) ? slotStacks[slotIndex].Count : 0;
}

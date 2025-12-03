using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TableManager : MonoBehaviour
{
    [Header("Slots (assign children or auto-find)")]
    [SerializeField] private TableSlot[] slots;

    private List<CardsController>[] slotStacks;
    private bool[] slotHasFigure;
    private bool[] slotPendingConversion;

    [Header("Auto conversion settings")]
    [SerializeField] private bool autoConvertToFigure = true;
    [SerializeField] private CardsController cardControllerPrefab;
    [SerializeField] private CardsData defaultFigureCardData;
    [SerializeField] private SumToFigureMapping[] sumToFigureMappings = new SumToFigureMapping[0];
    [SerializeField] private Transform figuresParent;

    public event Action<int, CardsController> OnCardPlaced;
    public event Action<int, IReadOnlyList<CardsController>> OnSlotReachedExactlyTen;
    public event Action<int, CardsController> OnDrawOptionAvailable;
    public event Action<int> OnSlotCleared;
    public event Action<int, CardsController> OnSlotAutoConverted;

    [Serializable]
    public struct SumToFigureMapping
    {
        public int sumKey;
        public CardsData figureData;
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

    public bool TryPlaceCardInSlot(int slotIndex, CardsController card, out Vector3 outPosition, out Quaternion outRotation, out bool drawAvailable, out string rejectReason)
    {
        outPosition = Vector3.zero;
        outRotation = Quaternion.identity;
        drawAvailable = false;
        rejectReason = null;

        if (!IsValidSlotIndex(slotIndex)) { rejectReason = "Slot inválido"; return false; }
        if (card == null || card.Data == null) { rejectReason = "Carta inválida"; return false; }
        if (slotHasFigure[slotIndex] || slotPendingConversion[slotIndex]) { rejectReason = "Slot ocupado por figura"; return false; }

        var stack = slotStacks[slotIndex];
        bool incomingIsNumeric = !card.Data.IsFigure;

        if (stack.Count == 0)
        {
            outPosition = slots[slotIndex].WorldPosition;
            outRotation = slots[slotIndex].WorldRotation;
            Debug.Log($"[TableManager] TryPlace: slot {slotIndex} empty -> accept for {card.Data.DisplayName}");
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
            currentSum += (c?.Data != null ? c.Data.BaseValue : 0);

        int newSum = currentSum + card.Data.BaseValue;

        if (newSum == 10)
        {
            slotPendingConversion[slotIndex] = true;
            outPosition = slots[slotIndex].WorldPosition;
            outRotation = slots[slotIndex].WorldRotation;
            Debug.Log($"[TableManager] TryPlace: slot {slotIndex} will reach EXACT 10 by placing {card.Data.DisplayName}");
            return true;
        }

        if (newSum == 11 || newSum == 12)
        {
            drawAvailable = true;
            outPosition = slots[slotIndex].WorldPosition;
            outRotation = slots[slotIndex].WorldRotation;
            Debug.Log($"[TableManager] TryPlace: slot {slotIndex} sum {newSum} -> drawAvailable for {card.Data.DisplayName}");
            return true;
        }

        outPosition = slots[slotIndex].WorldPosition;
        outRotation = slots[slotIndex].WorldRotation;
        Debug.Log($"[TableManager] TryPlace: slot {slotIndex} accepted for {card.Data.DisplayName} (newSum={newSum})");
        return true;
    }

    public bool ConfirmPlacementToSlot(int slotIndex, CardsController card, out bool triggeredExactlyTen, out bool drawAvailable)
    {
        triggeredExactlyTen = false;
        drawAvailable = false;

        if (!IsValidSlotIndex(slotIndex) || card == null) return false;
        if (slotHasFigure[slotIndex]) { Debug.LogWarning($"ConfirmPlacement: slot {slotIndex} ya tiene figura."); return false; }

        var stack = slotStacks[slotIndex];
        stack.Add(card);

        int sum = 0;
        foreach (var c in stack) sum += (c?.Data != null ? c.Data.BaseValue : 0);

        if (sum == 10)
        {
            triggeredExactlyTen = true;
            slotPendingConversion[slotIndex] = true;
            OnSlotReachedExactlyTen?.Invoke(slotIndex, stack.AsReadOnly());
            Debug.Log($"[TableManager] ConfirmPlacement: slot {slotIndex} reached EXACT 10. Triggering conversion.");

            if (autoConvertToFigure)
            {
                var figureController = AutoConvertSlotToFigure(slotIndex);
                if (figureController != null)
                {
                    OnSlotAutoConverted?.Invoke(slotIndex, figureController);
                    Debug.Log($"[TableManager] Auto-converted slot {slotIndex} to figure {figureController.Data.DisplayName}");
                }
            }
        }
        else if (sum == 11 || sum == 12)
        {
            drawAvailable = true;
            OnDrawOptionAvailable?.Invoke(slotIndex, card);
            Debug.Log($"[TableManager] ConfirmPlacement: slot {slotIndex} sum {sum} -> draw option");
        }

        OnCardPlaced?.Invoke(slotIndex, card);
        Debug.Log($"[TableManager] ConfirmPlacement: card {card.Data.DisplayName} placed in slot {slotIndex}");

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

        CardsData figureData = ResolveFigureDataForSlot(slotIndex);
        if (figureData == null)
        {
            Debug.LogWarning("AutoConvert: no figure data.");
            return null;
        }

        var prevStack = new List<CardsController>(slotStacks[slotIndex]);

        CardsPool pool = FindObjectOfType<CardsPool>();

        // Vaciar estado antes de devolver/destruir
        slotStacks[slotIndex].Clear();
        slotHasFigure[slotIndex] = true;
        slotPendingConversion[slotIndex] = false;

        foreach (var c in prevStack)
        {
            if (c == null) continue;
            if (pool != null)
            {
                pool.Release(c);
                Debug.Log($"[TableManager] AutoConvert: returned card to pool {c.Data.DisplayName}");
            }
            else
            {
                Destroy(c.gameObject);
                Debug.Log($"[TableManager] AutoConvert: destroyed card {c.Data.DisplayName}");
            }
        }

        CardsController newController = null;
        if (pool != null)
        {
            newController = pool.Get();
            newController.transform.SetPositionAndRotation(slots[slotIndex].WorldPosition, slots[slotIndex].WorldRotation);
            newController.Init(figureData, Guid.NewGuid().ToString());
            newController.OwningPool = pool;
            Debug.Log($"[TableManager] AutoConvert: instantiated figure from pool {figureData.DisplayName}");
        }
        else
        {
            if (cardControllerPrefab == null)
            {
                Debug.LogWarning("AutoConvert requires cardControllerPrefab or CardsPool.");
                return null;
            }
            var go = Instantiate(cardControllerPrefab.gameObject, slots[slotIndex].WorldPosition, slots[slotIndex].WorldRotation, figuresParent != null ? figuresParent : null);
            newController = go.GetComponent<CardsController>();
            newController.Init(figureData, Guid.NewGuid().ToString());
            Debug.Log($"[TableManager] AutoConvert: instantiated figure prefab {figureData.DisplayName}");
        }

        // No animation; solo log
        Debug.Log($"[TableManager] AutoConvertSlotToFigure: completed for slot {slotIndex} with figure {figureData.DisplayName}");

        OnSlotCleared?.Invoke(slotIndex);
        return newController;
    }

    private CardsData ResolveFigureDataForSlot(int slotIndex)
    {
        foreach (var m in sumToFigureMappings)
        {
            if (m.sumKey == 10 && m.figureData != null)
                return m.figureData;
        }
        return defaultFigureCardData;
    }

    public bool IsValidSlotIndex(int idx) => (idx >= 0 && idx < slots.Length);
    public TableSlot GetSlot(int idx) => IsValidSlotIndex(idx) ? slots[idx] : null;
    public IReadOnlyList<CardsController> GetStackReadonly(int idx) => IsValidSlotIndex(idx) ? slotStacks[idx].AsReadOnly() : null;

    public List<CardsController> ClearSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) return null;
        var prev = new List<CardsController>(slotStacks[slotIndex]);
        foreach (var c in prev) if (c != null) Destroy(c.gameObject);
        slotStacks[slotIndex].Clear();
        slotHasFigure[slotIndex] = false;
        slotPendingConversion[slotIndex] = false;
        OnSlotCleared?.Invoke(slotIndex);
        return prev;
    }

    public List<CardsController> ForceConvertSlotToFigure(int slotIndex)
    {
        var controller = AutoConvertSlotToFigure(slotIndex);
        return controller != null ? new List<CardsController> { controller } : null;
    }

    public bool TrySwapBetweenSlots(int slotA, int slotB, CardsController cardFromA, out string rejectReason)
    {
        rejectReason = null;
        if (!IsValidSlotIndex(slotA) || !IsValidSlotIndex(slotB)) { rejectReason = "Slot inválido"; return false; }
        if (slotHasFigure[slotB]) { rejectReason = "Destino ocupado por figura"; return false; }
        var stackA = slotStacks[slotA];
        if (!stackA.Remove(cardFromA)) { rejectReason = "La carta no estaba en el slot origen"; return false; }
        slotStacks[slotB].Add(cardFromA);
        Debug.Log($"[TableManager] TrySwap: moved card {cardFromA.Data.DisplayName} from slot {slotA} to {slotB}");
        return true;
    }

    public bool SlotHasFigure(int slotIndex) => IsValidSlotIndex(slotIndex) && slotHasFigure[slotIndex];
    public bool SlotIsPendingConversion(int slotIndex) => IsValidSlotIndex(slotIndex) && slotPendingConversion[slotIndex];
    public int SlotCount(int slotIndex) => IsValidSlotIndex(slotIndex) ? slotStacks[slotIndex].Count : 0;
}

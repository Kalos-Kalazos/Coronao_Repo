using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CardView : MonoBehaviour
{
    [Header("=== Renderer ===")]
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Material defaultMaterial;

    [Header("=== Movement ===")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float elevateOnDrag = 0.25f;
    [SerializeField] private float dragLerpSpeed = 20f;
    [SerializeField] private float placementSnapDuration = 0.25f;



    // -------------------------------------------------
    // ESTADO
    // -------------------------------------------------
    private CardsData data;
    private Coroutine currentMoveCoroutine;
    private bool draggingVisualActive = false;
    private Vector3 originalPositionLocal;
    private Quaternion originalRotationLocal;
    private Transform originalParent;
    private bool isPlacedOnTable = false;












}

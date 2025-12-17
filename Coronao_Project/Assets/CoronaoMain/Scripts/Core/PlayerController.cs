using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("=== Raycast ===")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask interactableLayers;
    [SerializeField] private float raycastDistance = 100f;

    // --- Runtime state ---
    private CardView hoveredCard;
    private CardView preSelectCard;
    private CardView card;

    private void Update()
    {

    }

    // -------------------------------------------------
    // RAYCAST DETECTION
    // -------------------------------------------------
    public void HandleRaycast(InputAction.CallbackContext context)
    {
        Vector2 screenPosition = context.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, interactableLayers))
        {
            card = hit.collider.GetComponent<CardView>();

            PreSelectCard(card);
        }
        else
        {
            ClearSelection();
        }
    }

    // -------------------------------------------------
    // HOVER LOGIC
    // -------------------------------------------------
    
    private void PreSelectCard(CardView card)
    {
        if (card != preSelectCard)
        {
            preSelectCard = card;
            preSelectCard.GetComponent<Animator>().SetBool("Selected", true);
        }
    }
    private void HandleCardHover(CardView preSelectCard)
    {
        if (preSelectCard != hoveredCard)
        {
            hoveredCard = preSelectCard;
        }
    }
    private void ClearSelection()
    {
        if (preSelectCard != null)
        {
            preSelectCard.gameObject.GetComponent<Animator>().SetBool("Selected", false);
            preSelectCard = null;
        }
    }

    public void OnCardHover(InputAction.CallbackContext context)
    {
        if (hoveredCard != null)
        {

        }
    }


    // -------------------------------------------------
    // IENUMERATORS
    // -------------------------------------------------
}

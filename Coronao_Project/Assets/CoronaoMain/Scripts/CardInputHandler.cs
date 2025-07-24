using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CardInputHandler : MonoBehaviour
{
    public LayerMask cardLayer;
    public float followSpeed = 15f;
    public float snapDistance = 1.5f;
    public Transform attackZone;
    public Transform defenseZone;
    public Camera mainCamera;

    private GameObject selectedCard;
    private Vector3 originalPosition;
    private Transform targetSnapZone;

    private bool isHoldingCard = false;

    public void SelectInput(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnClick();
        }
    }

    private void Update()
    {
        if (isHoldingCard && selectedCard != null)
        {
            Vector3 mouseWorld = GetMouseWorldPosition();

            // Check distance to snap zones
            float distToAttack = Vector3.Distance(mouseWorld, attackZone.position);
            float distToDefense = Vector3.Distance(mouseWorld, defenseZone.position);

            if (distToAttack < snapDistance)
            {
                targetSnapZone = attackZone;
            }
            else if (distToDefense < snapDistance)
            {
                targetSnapZone = defenseZone;
            }
            else
            {
                targetSnapZone = null;
            }

            // Move card toward mouse or snap zone
            Vector3 targetPos = targetSnapZone != null ? targetSnapZone.position : mouseWorld;
            selectedCard.transform.position = Vector3.Lerp(selectedCard.transform.position, targetPos, Time.deltaTime * followSpeed);
        }
    }

    void OnClick()
    {
        if (selectedCard == null)
        {
            TrySelectCard();
        }
        else
        {
            ConfirmCardPlacement();
        }
    }

    void TrySelectCard()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, cardLayer))
        {
            selectedCard = hit.collider.GetComponent<GameObject>();
            if (selectedCard != null)
            {
                originalPosition = selectedCard.transform.position;
                isHoldingCard = true;
            }
        }
    }

    void ConfirmCardPlacement()
    {
        if (targetSnapZone != null)
        {
            selectedCard.transform.position = targetSnapZone.position;
            //efectos, lógica de batalla, etc.
        }
        else
        {
            //volver a la mano
            selectedCard.transform.position = originalPosition;
        }

        selectedCard = null;
        isHoldingCard = false;
        targetSnapZone = null;
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane plane = new Plane(Vector3.up, Vector3.zero);

        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return Vector3.zero;
    }
}
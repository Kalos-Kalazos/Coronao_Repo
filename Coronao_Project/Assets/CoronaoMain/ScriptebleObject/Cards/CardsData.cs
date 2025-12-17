using System;
using System.Collections.Generic;
using UnityEngine;

#region Enums

//Lo que puede hacer la carta en concreto durante el turno del jugador.

public enum CardPlayType
{
    PlayToSum, //= jugar la carta en una pila.
    Discard,   //= descarta la primera carta de una de tus pilas.
    MoveToSum, //= si la carta esta en una pila aliada se puede mover a otra pila sin figura.
    EndTurn    //= saltar el turno solo cuando ya has usado un 5 gratis o movido un 6 gratis.
}

public enum SkillType
{
    Clear,          //(As) limpia la posicion del tablero aliada. (As)x2 sirve para eliminar las figuras J y Q
    PlaceFree,      //(5) se puede poner el primer 5 del turno sin gastar tu accion
    Steal,          //(3)x3 se pueden poner ambos numeros en la pila de un enemigo y asi terminar su turno. Tambien le robas una carta de la mano
    MoveFree,       //(6) se puede mover el primer 6 del turno de una pila a otra sin gastar tu acción
    Swap,           //(2)x2 puedes ponerlos en tu pila e intercambiarla por la pila de otro
    Rotate,         //(4)x3 se descartan los tres y todas las manos rotan hacia la derecha
    Evolve,         // al llegar a 10 en una pila se convierte en una figura y en la posicion ya no se puede poner mas cartas.
    EndOtherTurn    //(n)x2 se pueden poner ambos numeros en la pila de un enemigo y asi terminar su turno. en uno mismo no lo termina
}

public enum CardTargetRequirement
{
    NoAction,           //no te quedan acciones
    SingleAlly,         //una de tus pilas que no tenga una figura
    None,               //una pila cualquiera, incluida de un enemigo, solo con (7) o (9) o (n) doble
}

public enum Palo
{
    Corazon,
    Diamante,
    Trebol,
    Pica
}
#endregion

[CreateAssetMenu(fileName = "CardsData_", menuName = "Cards/CardsData", order = 100)]
public class CardsData : ScriptableObject
{
    [Header("Basic")]
    [SerializeField] private string displayName = "New Card";
    [SerializeField, TextArea(3, 8)] private string description = "Description";
    [SerializeField] private Palo defaultPalo = new();
    [SerializeField] private int baseValue = 0;             // valor que suma a la pila (0 para figuras)
    [SerializeField] private bool isFigure = false;         // si la carta es figura (J/Q/K) -> no admite cartas encima

    [Header("Presentation")]
    [SerializeField] private Sprite artwork = null;         // sprite para UI/prefab
    [SerializeField] private string symbol = "0";           // "3", "22", "K", etc.
    [SerializeField] private string internalId = "";        // id legible para importar/exportar

    [Header("Gameplay meta")]
    [SerializeField, Range(1, 4)] private int sameNumberCount = 1; // 1 normal, 2 doble, 3 triple, 4 cuadruple
    [SerializeField] private bool costsActionByDefault = true;    // por defecto consume accion

    [Header("Default behavior")]
    [SerializeField] private List<CardPlayType> defaultPlayTypes = new();                                  // cómo puede jugarse potencialmente
    [SerializeField] private List<SkillType> defaultSkills = new();                                        // habilidades por potenciales de la carta
    [SerializeField] private CardTargetRequirement defaultTargetRequirement = CardTargetRequirement.None;

    [Header("Optional UX")]
    [SerializeField] private AudioClip sfxOnPlay = null;
    [SerializeField] private AudioClip sfxOnEvolve = null;

    // --- Properties públicas (solo lectura desde runtime) ---
    public string DisplayName => displayName;
    public string Description => description;
    public int BaseValue => baseValue;
    public bool IsFigure => isFigure;
    public Sprite Artwork => artwork;
    public string Symbol => symbol;
    public string InternalId => internalId;

    public int SameNumberCount => sameNumberCount;
    public bool CostsActionByDefault => costsActionByDefault;

    public IReadOnlyList<CardPlayType> DefaultPlayTypes => defaultPlayTypes;
    public IReadOnlyList<SkillType> DefaultSkills => defaultSkills;
    public CardTargetRequirement DefaultTargetRequirement => defaultTargetRequirement;

    public AudioClip SfxOnPlay => sfxOnPlay;
    public AudioClip SfxOnEvolve => sfxOnEvolve;

#if UNITY_EDITOR
    // Editor-time sanity checks para ayudarte a detectar errores al crear assets
    private void OnValidate()
    {
        // Asegurarse que las figuras no tengan baseValue por accidente
        if (isFigure && baseValue != 0)
        {
            Debug.LogWarning($"[CardsData] '{displayName}' está marcada como figura pero tiene baseValue != 0. Se recomienda baseValue = 0 para figuras.");
        }

        // sameNumberCount mín/máximos
        if (sameNumberCount < 1) sameNumberCount = 1;
        if (sameNumberCount > 4) sameNumberCount = 4;
    }
#endif
}


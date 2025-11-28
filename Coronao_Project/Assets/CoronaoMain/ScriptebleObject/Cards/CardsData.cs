using System;
using System.Collections.Generic;
using UnityEngine;

#region Enums
public enum CardPlayType
{
    DrawCard,
    PlayToOwnBoard,
    MoveOnBoard,
    EndTurn
}

public enum SkillType
{
    Clear,
    PlaceFree,
    Steal,
    MoveFree,
    Swap,
    Rotate,
    Evolve,
    EndNotMyTurn
}

public enum CardTargetRequirement
{
    None,
    SingleAlly,
    SingleEnemy,
    Any,
    Area
}
#endregion

[CreateAssetMenu(fileName = "CardsData_", menuName = "Cards/CardsData", order = 100)]
public class CardsData : ScriptableObject
{
    [SerializeField] private string id = "";
    [SerializeField] private string displayName = "New Card";
    [SerializeField, TextArea(3, 8)] private string description = "Description";

    [SerializeField] private CardPlayType playType = CardPlayType.PlayToOwnBoard;
    [SerializeField] private List<SkillType> skills = new List<SkillType>();
    [SerializeField] private CardTargetRequirement targetRequirement = CardTargetRequirement.None;

    [SerializeField] private int baseValue = 0;
    [SerializeField] private bool isFigure = false;
    [SerializeField] private bool costsTurn = false;
    [SerializeField] private bool hasActivation = false;

    [SerializeField] private Sprite icon = null;
    [SerializeField] private GameObject visualPrefab = null;
    [SerializeField] private GameObject playVFX = null;
    [SerializeField] private AudioClip playSFX = null;

    [SerializeField] private string[] tags = new string[0];

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;

    public CardPlayType PlayType => playType;
    public IReadOnlyList<SkillType> Skills => skills;
    public CardTargetRequirement TargetReq => targetRequirement;

    public int BaseValue => baseValue;
    public bool IsFigure => isFigure;
    public bool CostsTurn => costsTurn;
    public bool HasActivation => hasActivation;

    public Sprite Icon => icon;
    public GameObject VisualPrefab => visualPrefab;
    public GameObject PlayVFX => playVFX;
    public AudioClip PlaySFX => playSFX;
    public IReadOnlyList<string> Tags => tags;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString();
    }
#endif
}

using UnityEngine;

public enum CardCategory
{
    Normal,     // Standard card
    Spell       // Special card with extra actions
}

public enum SpellAction
{
    None,
    LookAtOwn,      // For card values 7/8 – look at one of your own cards.
    LookAtOpponent, // For card values 9/10 – look at an opponent's card.
    TakeAndGive,    // Swap one of your cards with an opponent’s card.
    Sterling,       // Choose to view your cards or an opponent’s.
    NasserMansy     // Release one of your cards.
}

[CreateAssetMenu(fileName = "New Card", menuName = "Skrew Card Game/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    public int cardValue;
    public CardCategory category;
    public SpellAction spellAction;

    // UI for User A
    public Sprite frontSpriteUserA;
    public Sprite backSpriteUserA;

    // UI for User B
    public Sprite frontSpriteUserB;
    public Sprite backSpriteUserB;
}

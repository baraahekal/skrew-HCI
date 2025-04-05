using UnityEngine;

public class Card : MonoBehaviour
{
    public CardData cardData;           // Assign via Inspector or at runtime.
    public bool isInPlayerHand = false; // Set by GameManager when dealing the card.

    private SpriteRenderer spriteRenderer;
    private bool isFaceUp = false;
    public bool IsFaceUp { get { return isFaceUp; } }  // Public accessor for GameManager.

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (cardData != null)
        {
            spriteRenderer.sprite = cardData.backSprite; // Start face down.
        }
    }

    private void OnMouseDown()
    {
        // If the card is in the player's hand and it's the player's turn, trigger the observe action.
        if (isInPlayerHand && GameManager.Instance != null && GameManager.Instance.IsPlayerTurn)
        {
            GameManager.Instance.ObserveCard(this);
        }
        else
        {
            // Otherwise, simply flip the card.
            Flip();
        }
    }

    public void Flip()
    {
        isFaceUp = !isFaceUp;
        if (cardData != null)
        {
            spriteRenderer.sprite = isFaceUp ? cardData.frontSprite : cardData.backSprite;
        }
    }
}

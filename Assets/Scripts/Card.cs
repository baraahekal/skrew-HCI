using UnityEngine;

public class Card : MonoBehaviour
{
    public CardData cardData;           // Assigned via Inspector or at runtime.
    public bool isInPlayerHand = false; // Set by GameManager when dealing the card.

    private SpriteRenderer spriteRenderer;
    private bool isFaceUp = false;
    public bool IsFaceUp { get { return isFaceUp; } }  // Public accessor for GameManager.

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (cardData != null)
        {
            // Start face down using the appropriate back sprite based on the signed-in user.
            string userId = PlayerPrefs.GetString("UserID", "UserA");
            if (userId == "UserB")
                spriteRenderer.sprite = cardData.backSpriteUserB;
            else
                spriteRenderer.sprite = cardData.backSpriteUserA;
        }
    }

    // Flip the card and choose the appropriate sprite based on the user.
    public void Flip()
    {
        isFaceUp = !isFaceUp;
        if (cardData != null)
        {
            string userId = PlayerPrefs.GetString("UserID", "UserA");
            if (isFaceUp)
            {
                // Use the front sprite.
                if (userId == "UserB")
                    spriteRenderer.sprite = cardData.frontSpriteUserB;
                else
                    spriteRenderer.sprite = cardData.frontSpriteUserA;
            }
            else
            {
                // Use the back sprite.
                if (userId == "UserB")
                    spriteRenderer.sprite = cardData.backSpriteUserB;
                else
                    spriteRenderer.sprite = cardData.backSpriteUserA;
            }
        }
    }
}

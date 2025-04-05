using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum GameAction
{
    DrawFromDeck,
    DrawFromGround,
    DeclareSkro
}

public enum TurnState
{
    Idle,
    AwaitingDecision
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // Singleton for global access

    [Header("Prefabs & Areas")]
    public GameObject cardPrefab;         // Card prefab (with Card.cs attached)
    public Transform playerHandArea;      // Container for player's cards
    public Transform opponentHandArea;    // Container for opponent's cards
    public Transform deckArea;            // Location of the deck (should have a Collider2D)
    public Transform groundPileArea;      // Location for the ground pile (should have a Collider2D)

    [Header("Deck & Data")]
    public Deck deck;                   // Deck script holding the list of CardData assets

    [Header("Game State")]
    public bool isPlayerTurn = true;    // Indicates if it's the player's turn
    public bool IsPlayerTurn { get { return isPlayerTurn; } }
    public int roundCount = 0;          // Number of rounds played
    public bool CanDeclareSkro { get { return roundCount >= 3; } } // Declare Skro available after 3 rounds
    public int playerScore = 0;         // Player's score

    // Data structure for player's hand.
    public List<Card> playerHandList = new List<Card>();
    public List<Card> opponentHandList = new List<Card>();

    // Drawn card (active decision card).
    private Card lastDrawnCard;

    // Ground pile as a stack.
    private Stack<Card> groundPileStack = new Stack<Card>();
    private Card currentGroundCard;     // The top card on the ground.

    // Variables for ground swap mode.
    private bool swapWithGroundMode = false;
    private Card groundCardToSwap = null;

    // Turn state.
    private TurnState currentTurnState = TurnState.Idle;

    // Scale factor for all cards.
    private const float cardScale = 0.2f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        deck.ShuffleDeck();
        DrawInitialGroundCard();
        StartCoroutine(DealInitialHandsAndPreview());
    }

    // --- GROUND PILE METHODS ---

    /// <summary>
    /// Updates the ground pile display so that only the top card is active.
    /// </summary>
    void UpdateGroundPileDisplay()
    {
        int childCount = groundPileArea.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = groundPileArea.GetChild(i);
            child.gameObject.SetActive(i == childCount - 1);
        }
    }

    // --- GAME START METHODS ---

    /// <summary>
    /// Draws one card from the deck and places it in the ground pile to start the game.
    /// </summary>
    void DrawInitialGroundCard()
    {
        CardData groundData = deck.DrawCardData();
        if (groundData != null)
        {
            GameObject groundCardObj = Instantiate(cardPrefab, deckArea.position, Quaternion.identity);
            groundCardObj.transform.SetParent(groundPileArea);
            groundCardObj.transform.position = groundPileArea.position;
            groundCardObj.transform.localScale = new Vector3(cardScale, cardScale, cardScale);

            Card groundCard = groundCardObj.GetComponent<Card>();
            groundCard.cardData = groundData;
            groundCard.isInPlayerHand = false;
            groundCard.Flip(); // Force face up.
            currentGroundCard = groundCard;
            groundPileStack.Push(groundCard);
            UpdateGroundPileDisplay();
        }
        else
        {
            Debug.LogWarning("Deck is empty when drawing the initial ground card!");
        }
    }

    /// <summary>
    /// Deals initial hands to both the player and the opponent.
    /// </summary>
    IEnumerator DealInitialHands()
    {
        for (int i = 0; i < 4; i++)
        {
            AddCardToPlayerHand(DealCardFromDeck());
            AddCardToOpponentHand(DealCardFromDeck());
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// Draws a card from the deck and instantiates it.
    /// </summary>
    GameObject DealCardFromDeck()
    {
        CardData data = deck.DrawCardData();
        if (data != null)
        {
            GameObject newCard = Instantiate(cardPrefab, deckArea.position, Quaternion.identity);
            newCard.transform.localScale = new Vector3(cardScale, cardScale, cardScale);
            Card cardComp = newCard.GetComponent<Card>();
            cardComp.cardData = data;
            return newCard;
        }
        else
        {
            Debug.LogWarning("Deck is empty!");
            return null;
        }
    }

    /// <summary>
    /// Adds a card to the player's hand list and repositions the hand.
    /// </summary>
    void AddCardToPlayerHand(GameObject cardObj)
    {
        if (cardObj == null)
            return;
        cardObj.transform.SetParent(playerHandArea);
        Card cardComp = cardObj.GetComponent<Card>();
        cardComp.isInPlayerHand = true;
        playerHandList.Add(cardComp);
        RepositionPlayerHand();
    }

    /// <summary>
    /// Adds a card to the opponent's hand.
    /// </summary>
    void AddCardToOpponentHand(GameObject cardObj)
    {
        if (cardObj == null) return;

        cardObj.transform.SetParent(opponentHandArea);
        Card cardComp = cardObj.GetComponent<Card>();
        cardComp.isInPlayerHand = false;

        opponentHandList.Add(cardComp);
        RepositionOpponentHand();
    }

    void RepositionOpponentHand()
    {
        float spacing = 1.5f;
        float startX = opponentHandArea.position.x - ((opponentHandList.Count - 1) * spacing) / 2f;
        Vector3 basePosition = opponentHandArea.position;

        for (int i = 0; i < opponentHandList.Count; i++)
        {
            Vector3 newPos = new Vector3(startX + i * spacing, basePosition.y, basePosition.z);
            opponentHandList[i].transform.position = newPos;
        }
    }



    /// <summary>
    /// Repositions all cards in the player's hand based on playerHandList.
    /// </summary>
    void RepositionPlayerHand()
    {
        float spacing = 1.5f;
        float startX = playerHandArea.position.x - ((playerHandList.Count - 1) * spacing) / 2f;
        Vector3 basePosition = playerHandArea.position;

        for (int i = 0; i < playerHandList.Count; i++)
        {
            Vector3 newPos = new Vector3(startX + i * spacing, basePosition.y, basePosition.z);
            playerHandList[i].transform.position = newPos;
        }
    }


    /// <summary>
    /// Deals initial hands and then previews the two rightmost cards.
    /// </summary>
    IEnumerator DealInitialHandsAndPreview()
    {
        yield return StartCoroutine(DealInitialHands());
        yield return StartCoroutine(PreviewPlayerHand());
        currentTurnState = TurnState.Idle;
    }

    /// <summary>
    /// Previews the two rightmost cards in the player's hand by animating them.
    /// </summary>
    IEnumerator PreviewPlayerHand()
    {
        int count = playerHandList.Count;
        if (count < 2)
        {
            Debug.Log("Not enough cards for preview.");
            yield break;
        }
        Card card1 = playerHandList[count - 2];
        Card card2 = playerHandList[count - 1];

        // Start preview animations concurrently.
        StartCoroutine(PreviewCardAnimation(card1));
        StartCoroutine(PreviewCardAnimation(card2));

        yield return new WaitForSeconds(4f); // 0.5 up + 3 wait + 0.5 down
        Debug.Log("Preview complete. Game starting...");
    }

    /// <summary>
    /// Animates a card: moves it up, scales it up, flips face-up, waits 3 seconds, then flips back and returns to original position.
    /// </summary>
    IEnumerator PreviewCardAnimation(Card card)
    {
        Vector3 originalPos = card.transform.position;
        Vector3 originalScale = card.transform.localScale;
        Vector3 targetPos = originalPos + new Vector3(0, 0.5f, 0);
        Vector3 targetScale = originalScale * 1.1f;
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            card.transform.position = Vector3.Lerp(originalPos, targetPos, elapsed / duration);
            card.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        card.transform.position = targetPos;
        card.transform.localScale = targetScale;

        if (!card.IsFaceUp)
            card.Flip();

        yield return new WaitForSeconds(3f);

        if (card.IsFaceUp)
            card.Flip();

        elapsed = 0f;
        while (elapsed < duration)
        {
            card.transform.position = Vector3.Lerp(targetPos, originalPos, elapsed / duration);
            card.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        card.transform.position = originalPos;
        card.transform.localScale = originalScale;
    }

    // --- PLAYER TURN & INPUT HANDLING ---

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 clickPos = new Vector2(worldPoint.x, worldPoint.y);
            RaycastHit2D hit = Physics2D.Raycast(clickPos, Vector2.zero);
            if (hit.collider != null)
            {
                GameObject clicked = hit.collider.gameObject;
                Card clickedCard = clicked.GetComponent<Card>();

                // Decision state: waiting for the player to decide what to do with a drawn card.
                if (currentTurnState == TurnState.AwaitingDecision)
                {
                    // If player clicks on groundPileArea, discard drawn card onto ground.
                    if (clicked == groundPileArea.gameObject)
                    {
                        HandleGroundOption();
                        currentTurnState = TurnState.Idle;
                    }
                    // If player clicks on one of their own cards (to swap with the drawn card).
                    else if (clickedCard != null && clickedCard.isInPlayerHand && clickedCard != lastDrawnCard)
                    {
                        HandleSwapOption(clickedCard);
                        currentTurnState = TurnState.Idle;
                    }
                }
                else
                {
                    // Not in decision state.
                    if (clicked == deckArea.gameObject)
                    {
                        if (lastDrawnCard == null)
                            PerformAction(GameAction.DrawFromDeck);
                    }
                    else if (clickedCard != null)
                    {
                        // You clicked the ground card (top of the ground pile)
                        if (lastDrawnCard != null && currentTurnState == TurnState.AwaitingDecision)
                        {
                            Debug.Log("Throwing drawn card to the ground...");
                            HandleGroundOption();
                            currentTurnState = TurnState.Idle;
                        }
                        else if (lastDrawnCard == null)
                        {
                            // Enable ground swap mode
                            swapWithGroundMode = true;
                            groundCardToSwap = clickedCard;
                            Debug.Log("Ground card selected for swapping. Now click one of your own cards to swap with it.");
                        }
                    }




                    else if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        if (swapWithGroundMode && groundCardToSwap != null)
                        {
                            HandleSwapOptionWithGround(clickedCard);
                            swapWithGroundMode = false;
                            groundCardToSwap = null;
                        }
                        else if (lastDrawnCard != null && clickedCard != lastDrawnCard)
                        {
                            HandleSwapOption(clickedCard);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Called to perform a game action (can be called via UI button wrappers).
    /// </summary>
    public void PerformAction(GameAction action)
    {
        if (!isPlayerTurn)
        {
            Debug.Log("It's not the player's turn.");
            return;
        }
        switch (action)
        {
            case GameAction.DrawFromDeck:
                StartCoroutine(HandleDrawFromDeck());
                break;
            case GameAction.DrawFromGround:
                StartCoroutine(HandleDrawFromGround());
                break;
            case GameAction.DeclareSkro:
                if (!CanDeclareSkro)
                {
                    Debug.Log("Declare Skro is not available until after 3 rounds.");
                    return;
                }
                StartCoroutine(HandleDeclareSkro());
                break;
            default:
                Debug.LogWarning("Unknown game action.");
                break;
        }
    }

    /// <summary>
    /// Handles drawing one card from the deck during the player's turn.
    /// The drawn card is flipped to reveal its value, added to the player's hand list,
    /// and the game enters the AwaitingDecision state.
    /// </summary>
    IEnumerator HandleDrawFromDeck()
    {
        if (lastDrawnCard != null)
        {
            Debug.Log("A card is already drawn. Resolve that action first.");
            yield break;
        }

        Debug.Log("Player draws from deck.");
        CardData drawnData = deck.DrawCardData();
        if (drawnData == null)
        {
            Debug.LogWarning("Deck is empty!");
            yield break;
        }

        GameObject drawnCardObj = Instantiate(cardPrefab, deckArea.position, Quaternion.identity);
        drawnCardObj.transform.SetParent(transform); // Make it float in world space
        drawnCardObj.transform.localScale = new Vector3(cardScale, cardScale, cardScale);

        // Place the card slightly above the deck
        Vector3 floatOffset = new Vector3(0, 1.5f, 0);
        drawnCardObj.transform.position = deckArea.position + floatOffset;

        Card cardComp = drawnCardObj.GetComponent<Card>();
        cardComp.cardData = drawnData;
        cardComp.isInPlayerHand = true;

        if (!cardComp.IsFaceUp)
            cardComp.Flip();

        // DO NOT add it to playerHandList
        lastDrawnCard = cardComp;
        Debug.Log("Drawn card: " + drawnData.cardName);
        currentTurnState = TurnState.AwaitingDecision;
        yield return null;
    }


    /// <summary>
    /// Handles drawing a card from the ground pile.
    /// </summary>
    IEnumerator HandleDrawFromGround()
    {
        Debug.Log("Handling drawing from ground.");
        if (groundPileStack.Count > 0)
        {
            Card drawnGround = groundPileStack.Pop();
            drawnGround.transform.SetParent(playerHandArea);
            playerHandList.Add(drawnGround);
            int cardCount = playerHandArea.childCount;
            float spacing = 2.0f;
            drawnGround.transform.position = playerHandArea.position + new Vector3((cardCount - 1) * spacing, 0, 0);
            drawnGround.isInPlayerHand = true;
            currentGroundCard = (groundPileStack.Count > 0) ? groundPileStack.Peek() : null;
            RepositionPlayerHand();
        }
        else
        {
            Debug.Log("No card on the ground to draw.");
        }
        yield return null;
    }

    /// <summary>
    /// Handles the Declare Skro action: reveals all cards, recalculates score, and increments round count.
    /// </summary>
    IEnumerator HandleDeclareSkro()
    {
        Debug.Log("Declaring Skro: revealing all cards.");
        foreach (Card card in playerHandList)
        {
            if (card != null && !card.IsFaceUp)
                card.Flip();
        }
        // (Opponent's cards could be revealed similarly.)
        RecalculateScore();
        Debug.Log("Scores recalculated. Determine winner.");
        roundCount++;
        yield return null;
    }

    // --- PLAYER DECISION HANDLERS ---

    /// <summary>
    /// Handles discarding the drawn card onto the ground.
    /// Called when the player clicks on the ground area during the decision state.
    /// </summary>
    void HandleGroundOption()
    {
        Debug.Log("Player chooses to discard the drawn card onto the ground.");

        if (lastDrawnCard != null)
        {
            // Move the card to the ground area
            lastDrawnCard.transform.SetParent(groundPileArea, false);
            lastDrawnCard.transform.localPosition = Vector3.zero;
            lastDrawnCard.isInPlayerHand = false;

            if (!lastDrawnCard.IsFaceUp)
                lastDrawnCard.Flip();

            // Add to stack and update reference
            groundPileStack.Push(lastDrawnCard);
            currentGroundCard = lastDrawnCard;

            // IMPORTANT: Don't try to remove it from the playerHandList
            // Just clear the drawn card and update ground
            lastDrawnCard = null;

            UpdateGroundPileDisplay();
            currentTurnState = TurnState.Idle;
            RecalculateScore();
        }
        else
        {
            Debug.Log("No drawn card available to discard on the ground.");
        }
    }


    /// <summary>
    /// Handles swapping when the player chooses to swap the drawn card with one of their own cards.
    /// The target card is moved to the ground pile and becomes the new ground card.
    /// The drawn card takes the target card's position and is flipped back (hidden).
    /// </summary>
    void HandleSwapOption(Card targetCard)
    {
        Debug.Log("Player chooses to swap the drawn card with: " + targetCard.cardData.cardName);
        Vector3 targetPos = targetCard.transform.position;

        targetCard.transform.SetParent(groundPileArea, false);
        targetCard.transform.localPosition = Vector3.zero;
        targetCard.isInPlayerHand = false;
        if (!targetCard.IsFaceUp)
            targetCard.Flip();
        groundPileStack.Push(targetCard);
        playerHandList.Remove(targetCard);

        lastDrawnCard.transform.position = targetPos;
        lastDrawnCard.transform.SetParent(playerHandArea, false);
        lastDrawnCard.isInPlayerHand = true;
        if (lastDrawnCard.IsFaceUp)
            lastDrawnCard.Flip();

        currentGroundCard = targetCard;
        lastDrawnCard = null;
        UpdateGroundPileDisplay();
        currentTurnState = TurnState.Idle;
        RepositionPlayerHand();
        RecalculateScore();
    }

    /// <summary>
    /// Handles swapping when the player selects the ground card first,
    /// then chooses one of their own cards to swap with it.
    /// </summary>
    void HandleSwapOptionWithGround(Card targetCard)
    {
        Debug.Log("Player chooses to swap the ground card with: " + targetCard.cardData.cardName);
        Vector3 targetPos = targetCard.transform.position;

        targetCard.transform.SetParent(groundPileArea, false);
        targetCard.transform.localPosition = Vector3.zero;
        targetCard.isInPlayerHand = false;
        if (!targetCard.IsFaceUp)
            targetCard.Flip();
        groundPileStack.Push(targetCard);
        playerHandList.Remove(targetCard);

        groundCardToSwap.transform.position = targetPos;
        groundCardToSwap.transform.SetParent(playerHandArea, false);
        groundCardToSwap.isInPlayerHand = true;
        if (!playerHandList.Contains(groundCardToSwap))
            playerHandList.Add(groundCardToSwap);

        currentGroundCard = targetCard;
        UpdateGroundPileDisplay();
        currentTurnState = TurnState.Idle;
        RepositionPlayerHand();
        RecalculateScore();
    }

    /// <summary>
    /// Repositions all cards in the player's hand based on playerHandList.
    /// </summary>
    
    /// <summary>
    /// Recalculates the player's score by summing the card values in playerHandList.
    /// </summary>
    void RecalculateScore()
    {
        int score = 0;
        foreach (Card card in playerHandList)
        {
            if (card != null)
                score += card.cardData.cardValue;
        }
        playerScore = score;
        Debug.Log("Player score recalculated: " + playerScore);
    }

    // --- SPELL CARD HANDLING (DUMMY IMPLEMENTATIONS) ---

    IEnumerator HandleSpellCard(Card card)
    {
        Debug.Log("Processing spell card: " + card.cardData.spellAction);
        switch (card.cardData.spellAction)
        {
            case SpellAction.LookAtOwn:
                Debug.Log("Spell Action: Look at one of your own cards.");
                yield return StartCoroutine(SelectCardFromHand(playerHandArea, "Select a card to reveal (Your Card):"));
                break;
            case SpellAction.LookAtOpponent:
                Debug.Log("Spell Action: Look at an opponent's card.");
                yield return StartCoroutine(SelectCardFromHand(opponentHandArea, "Select a card to reveal (Opponent's Card):"));
                break;
            case SpellAction.TakeAndGive:
                Debug.Log("Spell Action: Take and Give Card.");
                yield return StartCoroutine(HandleTakeAndGive());
                break;
            case SpellAction.Sterling:
                Debug.Log("Spell Action: Sterling Card.");
                yield return StartCoroutine(HandleSterling());
                break;
            case SpellAction.NasserMansy:
                Debug.Log("Spell Action: Nasser Mansy Card.");
                yield return StartCoroutine(SelectCardFromHand(playerHandArea, "Select a card to discard:"));
                break;
            default:
                Debug.LogWarning("No valid spell action defined.");
                break;
        }
        yield return null;
    }

    IEnumerator HandleTakeAndGive()
    {
        Debug.Log("Initiating Take and Give action.");
        yield return new WaitForSeconds(1f);
        Debug.Log("Take and Give completed.");
        yield return null;
    }

    IEnumerator HandleSterling()
    {
        Debug.Log("Initiating Sterling action.");
        yield return new WaitForSeconds(1f);
        Debug.Log("Sterling action completed.");
        yield return null;
    }

    IEnumerator SelectCardFromHand(Transform handArea, string prompt)
    {
        Debug.Log(prompt);
        // Insert UI prompt here for card selection.
        yield return new WaitForSeconds(1f);
        Debug.Log("Card selected from hand.");
        yield return null;
    }

    // --- OBSERVE ACTION ---

    /// <summary>
    /// Called when the player clicks on one of their own cards to "observe".
    /// Checks if the drawn card is a Spell card and compares it with the ground card.
    /// If they match (by cardValue), the drawn card is removed and the player's score is reduced;
    /// if not, the ground card is added to the player's hand.
    /// </summary>
    public void ObserveCard(Card card)
    {
        Debug.Log("Player attempts to observe using card: " + card.cardData.cardName);
        if (lastDrawnCard == null)
        {
            Debug.Log("No drawn card available for comparison.");
            return;
        }
        if (lastDrawnCard.cardData.category != CardCategory.Spell)
        {
            Debug.Log("Observation not allowed: drawn card is not a Spell card.");
            return;
        }
        if (currentGroundCard == null)
        {
            Debug.Log("No ground card available for comparison.");
            return;
        }
        if (lastDrawnCard.cardData.cardValue == currentGroundCard.cardData.cardValue)
        {
            Debug.Log("Match found! Removing drawn card and reducing player's score.");
            Destroy(lastDrawnCard.gameObject);
            playerScore -= lastDrawnCard.cardData.cardValue;
            Debug.Log("Player score: " + playerScore);
            lastDrawnCard = null;
        }
        else
        {
            Debug.Log("No match: adding the ground card to player's hand.");
            currentGroundCard.transform.SetParent(playerHandArea);
            playerHandList.Add(currentGroundCard);
            int cardCount = playerHandArea.childCount;
            float spacing = 2.0f;
            currentGroundCard.transform.position = playerHandArea.position + new Vector3((cardCount - 1) * spacing, 0, 0);
            currentGroundCard.isInPlayerHand = true;
            currentGroundCard = null;
        }
    }
}

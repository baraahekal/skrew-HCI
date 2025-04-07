using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TuioNet.Tuio11;

public enum GameAction
{
    DrawFromDeck,
    DrawFromGround,
    DeclareSkro
}

public enum TurnState
{
    Idle,                         // Normal game mode.
    AwaitingDeckDecision,         // A normal drawn card awaits discard or swap decision.
    AwaitingBasraDecision,        // A Basra spell card is drawn.
    AwaitingBasraForcedObservation, // Basra spell discarded; waiting for forced observation.
    AwaitingGroundSwap,           // The ground card is selected for swapping.
    AwaitingTakeAndGiveDecision,  // A "take and give" spell card is drawn.
    AwaitingTakeAndGiveSelection, // Waiting for selections for take and give swap.
    AwaitingSterlingDecision,     // A "sterling" spell card is drawn.
    AwaitingSterlingSelection,    // Waiting for selections for sterling.
    AwaitingLookAtOwnDecision,    // A LookAtOwn spell card is drawn.
    AwaitingLookAtOwnSelection,   // Waiting for player to select one of his own cards.
    AwaitingLookAtOpponentDecision, // A LookAtOpponent spell card is drawn.
    AwaitingLookAtOpponentSelection // Waiting for player to select one opponent card.
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Prefabs & Areas")]
    public GameObject cardPrefab;
    public Transform playerHandArea;
    public Transform opponentHandArea;
    public Transform deckArea;
    public Transform groundPileArea;

    [Header("Deck & Data")]
    public Deck deck;

    [Header("Game State")]
    public bool isPlayerTurn = true;
    public bool IsPlayerTurn { get { return isPlayerTurn; } }
    public int roundCount = 0;
    public bool CanDeclareSkro { get { return roundCount >= 3; } }
    public int playerScore = 0;

    public List<Card> playerHandList = new List<Card>();
    public List<Card> opponentHandList = new List<Card>();

    private Card lastDrawnCard;
    private Stack<Card> groundPileStack = new Stack<Card>();
    private Card currentGroundCard;
    private Card groundCardToSwap = null;

    // For "take and give" spell:
    private Card takeAndGivePlayerCard = null;
    private Card takeAndGiveOpponentCard = null;

    // Variables for comparing rotation angles.
    private float initialTuioAngle = 0f;
    private bool initialAngleSet = false;
    public float rotationThreshold = 0.1f; // Adjust as needed.

    // For delay between TUIO actions.
    private float lastTuioActionTime = -10f;
    private const float actionDelay = 2f;

    private int sterlingObservations = 0;

    private float lastCardSelectionTime = 0f;
    private int currentSelectedCardIndex = -1;
    private Card currentSelectedCard = null;
    private const float CARD_SELECTION_TIMEOUT = 5.0f; // Time


    // New for LookAtOwn/LookAtOpponent:
    // (No extra fields needed; selection will be handled directly via input.)

    private TurnState currentTurnState = TurnState.Idle;
    public TurnState CurrentTurnState { get { return currentTurnState; } }
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

    public void RecordInitialTuioAngle(float angle)
    {
        initialTuioAngle = angle;
        initialAngleSet = true;
    }

    

    // ---------------------------
    // GROUND PILE METHODS
    // ---------------------------
    void UpdateGroundPileDisplay()
    {
        int childCount = groundPileArea.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = groundPileArea.GetChild(i);
            child.gameObject.SetActive(i == childCount - 1);
        }
        if (childCount > 0)
        {
            Card topCard = groundPileArea.GetChild(childCount - 1).GetComponent<Card>();
            if (topCard != null && !topCard.IsFaceUp)
                topCard.Flip();
        }
        // Enforce consistent scale on all cards in the ground pile.
        EnforceConsistentCardScale(groundPileArea);
    }

    /// <summary>
    /// Ensures that every Card (child) under the given parent Transform has the consistent scale.
    /// </summary>
    void EnforceConsistentCardScale(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Card card = child.GetComponent<Card>();
            if (card != null)
            {
                // Set the card's scale to the standard cardScale.
                child.localScale = new Vector3(cardScale, cardScale, cardScale);
            }
        }
    }


    // ---------------------------
    // GAME INITIALIZATION
    // ---------------------------
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
            groundCard.Flip();
            currentGroundCard = groundCard;
            groundPileStack.Push(groundCard);
            UpdateGroundPileDisplay();
            Debug.Log("ana 3malt one card draw");
        }
        else
        {
            Debug.LogWarning("Deck is empty when drawing the initial ground card!");
        }
    }

    IEnumerator DealInitialHands()
    {
        int cardsToDeal = 4;
        for (int i = 0; i < cardsToDeal; i++)
        {
            AddCardToPlayerHand(DealCardFromDeck());
            AddCardToOpponentHand(DealCardFromDeck());
            yield return new WaitForSeconds(0.5f);
        }
        Debug.Log("ana wazza3t el cards");
    }

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

    void AddCardToPlayerHand(GameObject cardObj)
    {
        if (cardObj == null) return;
        cardObj.transform.SetParent(playerHandArea);
        Card cardComp = cardObj.GetComponent<Card>();
        cardComp.isInPlayerHand = true;
        playerHandList.Add(cardComp);
        RepositionPlayerHand();
    }

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
        Vector3 basePos = opponentHandArea.position;
        for (int i = 0; i < opponentHandList.Count; i++)
        {
            opponentHandList[i].transform.position = new Vector3(startX + i * spacing, basePos.y, basePos.z);
        }
        // Enforce consistent scale on all cards in the opponent's hand.
        EnforceConsistentCardScale(opponentHandArea);
    }


    IEnumerator DealInitialHandsAndPreview()
    {
        yield return StartCoroutine(DealInitialHands());
        yield return StartCoroutine(PreviewPlayerHand());
        currentTurnState = TurnState.Idle;
    }

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
        StartCoroutine(PreviewCardAnimation(card1));
        StartCoroutine(PreviewCardAnimation(card2));
        yield return new WaitForSeconds(4f);
        Debug.Log("Preview complete. Game starting...");
    }

    IEnumerator PreviewCardAnimation(Card card)
    {
        Vector3 origPos = card.transform.position;
        Vector3 origScale = card.transform.localScale;
        Vector3 targetPos = origPos + new Vector3(0, 0.5f, 0);
        Vector3 targetScale = origScale * 1.1f;
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            card.transform.position = Vector3.Lerp(origPos, targetPos, elapsed / duration);
            card.transform.localScale = Vector3.Lerp(origScale, targetScale, elapsed / duration);
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
            card.transform.position = Vector3.Lerp(targetPos, origPos, elapsed / duration);
            card.transform.localScale = Vector3.Lerp(targetScale, origScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        card.transform.position = origPos;
        card.transform.localScale = origScale;
    }

    public void ProcessGesture(string gesture)
    {
        Debug.Log("Processing gesture: " + gesture);

        // Here you can add your logic based on the gesture.
        // For example:
        switch (gesture)
        {
            case "Open_Palm":
                // Trigger some action, e.g., draw a card.
                PerformAction(GameAction.DrawFromDeck);
                break;
            case "Victory":
                // Trigger another action.
                break;
            case "Closed_Fist":
                // And so on...
                break;
            case "Thumb_Down":
                // For example, discard a card.
                //DiscardDrawnCard();
                break;
            case "Thumb_Up":
                // For example, accept a drawn card.
                //AcceptDrawnCard();
                break;
            default:
                Debug.Log("Unhandled gesture: " + gesture);
                break;
        }
    }


    public void ProcessTuioInput(TuioNet.Tuio11.Tuio11Object tuioObj)
    {
        Debug.LogFormat("ANAAAA TUIO = " + tuioObj.SymbolId);
        // Enforce a delay between TUIO actions.
        if (Time.time - lastTuioActionTime < actionDelay)
            return;

        // We're only interested in processing input when in the AwaitingDeckDecision state.
        if ((currentTurnState == TurnState.AwaitingDeckDecision ||
     currentTurnState == TurnState.AwaitingBasraForcedObservation ||
     currentTurnState == TurnState.AwaitingLookAtOpponentDecision ||
     currentTurnState == TurnState.AwaitingTakeAndGiveDecision ||
     currentTurnState == TurnState.AwaitingLookAtOwnDecision) && tuioObj.SymbolId == 2)
        {
            if (initialAngleSet)
            {
                float currentAngle = tuioObj.Angle;
                float deltaAngle = currentAngle - initialTuioAngle;
                Debug.LogFormat("Initial angle: {0:F3}, Current angle: {1:F3}, Delta: {2:F3}",
                    initialTuioAngle, currentAngle, deltaAngle);

                if (currentAngle >= 0.0f && currentAngle < 2.0f)
                {
                    if (currentAngle >= 1.0f)
                    {
                        StartCoroutine(DelayedDiscard());
                        initialAngleSet = false;

                        // Reset any active card selection
                        if (currentSelectedCard != null)
                        {
                            StartCoroutine(ScaleDownCard(currentSelectedCard));
                            currentSelectedCard = null;
                            currentSelectedCardIndex = -1;
                        }
                    }
                }
                else
                {
                    // Use the existing playerHandList
                    int totalCards = playerHandList.Count;
                    if (totalCards > 0)
                    {
                        // Map angle range from 2 to 6
                        // Limiting the angle to be within 2 to 6 range
                        float clampedAngle = Mathf.Clamp(currentAngle, 2.0f, 6.0f);

                        // Calculate the angle per card based on total cards and our range (2 to 6 = 4 units total)
                        float totalAngleRange = 4.0f; // 6 - 2 = 4
                        float anglePerCard = 0.2f;

                        // Map the angle to card index
                        // We subtract from 6 to make higher angles map to lower indices (first cards)
                        int newSelectedIndex = Mathf.Clamp(Mathf.FloorToInt((6.0f - clampedAngle) / anglePerCard), 0, totalCards - 1);

                        if (newSelectedIndex != currentSelectedCardIndex)
                        {
                            // Selection changed, reset timer
                            // Scale down previous selection if any
                            if (currentSelectedCard != null)
                            {
                                StartCoroutine(ScaleDownCard(currentSelectedCard));
                            }

                            // Update to new selection
                            currentSelectedCardIndex = newSelectedIndex;
                            currentSelectedCard = playerHandList[currentSelectedCardIndex];

                            // Use your existing scale up method for visual feedback
                            StartCoroutine(ScaleUpCard(currentSelectedCard));
                            lastCardSelectionTime = Time.time;
                        }
                        else if (currentSelectedCard != null && Time.time - lastCardSelectionTime >= CARD_SELECTION_TIMEOUT)
                        {
                            // User has stayed on this card for the required time
                            // Execute the swap
                            Debug.Log("Card selected through angle rotation: " + currentSelectedCardIndex);
                            HandleSwapOption(currentSelectedCard);
                            currentTurnState = TurnState.Idle;
                            initialAngleSet = false;

                            // Reset selection
                            StartCoroutine(ScaleDownCard(currentSelectedCard));
                            currentSelectedCard = null;
                            currentSelectedCardIndex = -1;
                            lastTuioActionTime = Time.time;
                        }
                    }
                }
            }
            
        }
        else if ((currentTurnState == TurnState.AwaitingBasraForcedObservation || currentTurnState == TurnState.Idle) && tuioObj.SymbolId == 3)
        {
            Debug.LogFormat("ana gowaaa tuio 3" + initialAngleSet);
            if (initialAngleSet)
            {
                float currentAngle = tuioObj.Angle;

                // Use the existing playerHandList
                int totalCards = playerHandList.Count;
                if (totalCards > 0)
                {
                    // Map angle range from 0 to 6.28 (full rotation)
                    float clampedAngle = Mathf.Clamp(currentAngle, 0.0f, 6.28f);

                    // Calculate angle per card based on total cards
                    float anglePerCard = 0.2f;

                    // Map the angle to card index
                    int newSelectedIndex = Mathf.FloorToInt(clampedAngle / anglePerCard);
                    newSelectedIndex = Mathf.Clamp(newSelectedIndex, 0, totalCards - 1);

                    if (newSelectedIndex != currentSelectedCardIndex)
                    {
                        // Selection changed, reset timer
                        // Scale down previous selection if any
                        if (currentSelectedCard != null)
                        {
                            StartCoroutine(ScaleDownCard(currentSelectedCard));
                        }

                        // Update to new selection
                        currentSelectedCardIndex = newSelectedIndex;
                        currentSelectedCard = playerHandList[currentSelectedCardIndex];

                        // Use your existing scale up method for visual feedback
                        StartCoroutine(ScaleUpCard(currentSelectedCard));
                        lastCardSelectionTime = Time.time;
                    }
                    else if (currentSelectedCard != null && Time.time - lastCardSelectionTime >= CARD_SELECTION_TIMEOUT)
                    {
                        // User has stayed on this card for the required time
                        Debug.Log("Card selected through angle rotation: " + currentSelectedCardIndex);

                        // Different handling based on current turn state
                        if (currentTurnState == TurnState.AwaitingBasraForcedObservation)
                        {
                            Debug.Log("Handling forced observation for Basra");
                            HandleForcedObservation(currentSelectedCard);
                            currentTurnState = TurnState.Idle;
                        }
                        else
                        {
                            // For normal observation when in Idle state
                            Debug.Log("Handling normal observation");
                            HandleNormalObservation(currentSelectedCard);
                        }

                        initialAngleSet = false;

                        // Reset selection
                        StartCoroutine(ScaleDownCard(currentSelectedCard));
                        currentSelectedCard = null;
                        currentSelectedCardIndex = -1;
                        lastTuioActionTime = Time.time;
                    }
                }
            }
        }
        else if (tuioObj.SymbolId == 4)
        {
            
            // We assume that marker ID 4 should trigger a swap-from-ground action.
            // For example, we require that the game state allows a ground swap.
            if (currentTurnState == TurnState.Idle)
            {
                Debug.LogFormat("anaaa gowwwaaaa 44444444");
                if (groundPileStack.Count > 0)
                {
                    groundCardToSwap = groundPileStack.Peek();
                }
                float currentAngle = tuioObj.Angle;
                // Map the angle (for example, assume full rotation 0 to 6.28 radians)
                float clampedAngle = Mathf.Clamp(currentAngle, 0.0f, 6.28f);
                float anglePerCard = 0.2f; // Adjust as needed.
                int totalCards = playerHandList.Count;
                if (totalCards > 0)
                {
                    // Map the angle to a card index.
                    int newSelectedIndex = Mathf.Clamp(Mathf.FloorToInt(clampedAngle / anglePerCard), 0, totalCards - 1);
                    if (newSelectedIndex != currentSelectedCardIndex)
                    {
                        if (currentSelectedCard != null)
                        {
                            StartCoroutine(ScaleDownCard(currentSelectedCard));
                        }
                        currentSelectedCardIndex = newSelectedIndex;
                        currentSelectedCard = playerHandList[currentSelectedCardIndex];
                        StartCoroutine(ScaleUpCard(currentSelectedCard));
                        lastCardSelectionTime = Time.time;
                        Debug.Log("New ground swap selection index (ID 4): " + currentSelectedCardIndex);
                    }
                    else if (currentSelectedCard != null && Time.time - lastCardSelectionTime >= CARD_SELECTION_TIMEOUT)
                    {
                        Debug.Log("Ground swap selection confirmed (ID 4): " + currentSelectedCardIndex);
                        // Execute the swap using your predefined method.
                        HandleSwapOptionWithGround(currentSelectedCard);
                        initialAngleSet = false;
                        currentSelectedCard = null;
                        lastTuioActionTime = Time.time;
                    }
                }
            }
        }
        // Inside your ProcessTuioInput(Tuio11Object tuioObj) method

        // Existing branches for other states and for tuioObj.SymbolId == 2 for draw/decision go here...
        // Then add an additional branch for take-and-give selection:
        else if (currentTurnState == TurnState.AwaitingTakeAndGiveSelection && tuioObj.SymbolId == 2)
        {
            // First, if we haven't selected the player's card yet:
            if (takeAndGivePlayerCard == null)
            {
                int totalPlayerCards = playerHandList.Count;
                if (totalPlayerCards > 0)
                {
                    float currentAngle = tuioObj.Angle;
                    // Clamp the angle to a range that maps to the player's hand (for example, 2.0 to 6.0 radians)
                    float clampedAngle = Mathf.Clamp(currentAngle, 2.0f, 6.0f);
                    float anglePerCard = 0.2f; // Adjust as needed
                    int newSelectedIndex = Mathf.Clamp(Mathf.FloorToInt((6.0f - clampedAngle) / anglePerCard), 0, totalPlayerCards - 1);

                    if (newSelectedIndex != currentSelectedCardIndex)
                    {
                        // If selection changes, scale down the previous selection if any.
                        if (currentSelectedCard != null)
                        {
                            StartCoroutine(ScaleDownCard(currentSelectedCard));
                        }
                        currentSelectedCardIndex = newSelectedIndex;
                        currentSelectedCard = playerHandList[currentSelectedCardIndex];
                        StartCoroutine(ScaleUpCard(currentSelectedCard));
                        lastCardSelectionTime = Time.time;
                        Debug.Log("TakeAndGive: New player selection index: " + currentSelectedCardIndex);
                    }
                    else if (currentSelectedCard != null && Time.time - lastCardSelectionTime >= CARD_SELECTION_TIMEOUT)
                    {
                        // The player has held the selection long enough.
                        takeAndGivePlayerCard = currentSelectedCard;
                        Debug.Log("TakeAndGive: Player card selected: " + takeAndGivePlayerCard.cardData.cardName);
                        // Reset selection variables before selecting opponent card.
                        StartCoroutine(ScaleDownCard(currentSelectedCard));
                        currentSelectedCard = null;
                        currentSelectedCardIndex = -1;
                        lastCardSelectionTime = Time.time;
                    }
                }
            }
            // If player's card is already selected, then use the same marker to select from opponent's hand.
            else if (takeAndGiveOpponentCard == null)
            {
                int totalOpponentCards = opponentHandList.Count;
                if (totalOpponentCards > 0)
                {
                    float currentAngle = tuioObj.Angle;
                    // Again, clamp the angle to a range (you can choose a different range if desired).
                    float clampedAngle = Mathf.Clamp(currentAngle, 0.0f, 6.0f);
                    float anglePerCard = 0.2f; // Adjust as needed.
                    int newSelectedIndex = Mathf.Clamp(Mathf.FloorToInt((6.0f - clampedAngle) / anglePerCard), 0, totalOpponentCards - 1);

                    if (newSelectedIndex != currentSelectedCardIndex)
                    {
                        if (currentSelectedCard != null)
                        {
                            StartCoroutine(ScaleDownCard(currentSelectedCard));
                        }
                        currentSelectedCardIndex = newSelectedIndex;
                        currentSelectedCard = opponentHandList[currentSelectedCardIndex];
                        StartCoroutine(ScaleUpCard(currentSelectedCard));
                        lastCardSelectionTime = Time.time;
                        Debug.Log("TakeAndGive: New opponent selection index: " + currentSelectedCardIndex);
                    }
                    else if (currentSelectedCard != null && Time.time - lastCardSelectionTime >= CARD_SELECTION_TIMEOUT)
                    {
                        // Confirm opponent selection and execute swap.
                        takeAndGiveOpponentCard = currentSelectedCard;
                        Debug.Log("TakeAndGive: Opponent card selected: " + takeAndGiveOpponentCard.cardData.cardName);
                        if (takeAndGivePlayerCard != null && takeAndGiveOpponentCard != null)
                        {
                            StartCoroutine(HandleTakeAndGiveSwap(takeAndGivePlayerCard, takeAndGiveOpponentCard));
                            currentTurnState = TurnState.Idle;
                            // Reset selection variables.
                            takeAndGivePlayerCard = null;
                            takeAndGiveOpponentCard = null;
                            currentSelectedCard = null;
                            currentSelectedCardIndex = -1;
                            lastTuioActionTime = Time.time;
                        }
                    }
                }
            }
        }
        else if ((currentTurnState == TurnState.AwaitingSterlingSelection ||
              currentTurnState == TurnState.AwaitingLookAtOwnSelection ||
              currentTurnState == TurnState.AwaitingLookAtOpponentSelection) &&
             tuioObj.SymbolId == 2)
        {
            // Determine which list we use based on the current state.
            // For LookAtOwn, we select from the player's hand.
            // For LookAtOpponent or Sterling, we select from the opponent's hand.
            List<Card> selectionList = null;
            if (currentTurnState == TurnState.AwaitingLookAtOwnSelection)
                selectionList = playerHandList;
            else if (currentTurnState == TurnState.AwaitingLookAtOpponentSelection || currentTurnState == TurnState.AwaitingSterlingSelection)
                selectionList = opponentHandList;

            int totalCards = selectionList.Count;
            if (totalCards > 0)
            {
                // Get the current angle from the TUIO marker.
                float currentAngle = tuioObj.Angle;
                // Clamp the angle to a full rotation (0 to 6.28 radians).
                float clampedAngle = Mathf.Clamp(currentAngle, 0.0f, 6.28f);
                // Define how much of the angle corresponds to one card.
                float anglePerCard = 0.2f; // Adjust as needed.
                int newSelectedIndex = Mathf.Clamp(Mathf.FloorToInt(clampedAngle / anglePerCard), 0, totalCards - 1);

                if (newSelectedIndex != currentSelectedCardIndex)
                {
                    // If the selection has changed, scale down the previous selection (if any)
                    if (currentSelectedCard != null)
                    {
                        StartCoroutine(ScaleDownCard(currentSelectedCard));
                    }
                    currentSelectedCardIndex = newSelectedIndex;
                    currentSelectedCard = selectionList[currentSelectedCardIndex];
                    StartCoroutine(ScaleUpCard(currentSelectedCard));
                    lastCardSelectionTime = Time.time;
                    Debug.Log("New selection index for state " + currentTurnState + ": " + currentSelectedCardIndex);
                }
                else if (currentSelectedCard != null && Time.time - lastCardSelectionTime >= CARD_SELECTION_TIMEOUT)
                {
                    // If the same selection has been held for the required timeout, execute the corresponding action.
                    Debug.Log("Selection confirmed for state " + currentTurnState + ": index " + currentSelectedCardIndex);
                    if (currentTurnState == TurnState.AwaitingSterlingSelection)
                    {
                        StartCoroutine(HandleSterlingObservation(currentSelectedCard));
                        sterlingObservations++;
                        if (sterlingObservations >= 2)
                        {
                            currentTurnState = TurnState.Idle;
                        }
                    }
                    else if (currentTurnState == TurnState.AwaitingLookAtOwnSelection)
                    {
                        StartCoroutine(HandleLookAtOwnAction(currentSelectedCard));
                        currentTurnState = TurnState.Idle;
                    }
                    else if (currentTurnState == TurnState.AwaitingLookAtOpponentSelection)
                    {
                        StartCoroutine(HandleLookAtOpponentAction(currentSelectedCard));
                        currentTurnState = TurnState.Idle;
                    }
                    initialAngleSet = false;
                    StartCoroutine(ScaleDownCard(currentSelectedCard));
                    currentSelectedCard = null;
                    currentSelectedCardIndex = -1;
                    lastTuioActionTime = Time.time;
                }
            }
        }

        else
        {
            // Initialize angle tracking
            initialTuioAngle = tuioObj.Angle;
            initialAngleSet = true;
            lastCardSelectionTime = Time.time;
            currentSelectedCardIndex = -1;
            currentSelectedCard = null;
        }

    }
    IEnumerator DelayedDiscard()
    {
        Debug.Log("Marker rotated right: Discarding drawn card.");
        DiscardDrawnCard();  // Call your discard method.
        initialAngleSet = false;
        lastTuioActionTime = Time.time;

        // Wait for 2 seconds before resetting state.
        yield return new WaitForSeconds(2f);
    }


    // --- Public Methods to Trigger Actions ---
    public void DiscardDrawnCard()
    {
        Debug.Log("DiscardDrawnCard() called.");
        // Implement your discard logic; for example:
        HandleGroundOption();
    }

    public void AcceptDrawnCard()
    {
        Debug.Log("AcceptDrawnCard() called.");
        // Implement your accept logic; for example, reparent the drawn card to the player's hand:
        if (lastDrawnCard != null)
        {
            lastDrawnCard.transform.SetParent(playerHandArea, false);
            RepositionPlayerHand();
            lastDrawnCard = null;
        }
    }

    // ---------------------------
    // INPUT & TURN HANDLING
    // ---------------------------
    void Update()
    {
        // Check for TUIO input first.
       
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 clickPos = new Vector2(wp.x, wp.y);
            RaycastHit2D hit = Physics2D.Raycast(clickPos, Vector2.zero);
            if (hit.collider != null)
            {
                GameObject clicked = hit.collider.gameObject;
                Card clickedCard = clicked.GetComponent<Card>();

                // CASE 1: AwaitingDeckDecision (normal drawn card)
                if (currentTurnState == TurnState.AwaitingDeckDecision)
                {
                    if (clicked.transform.IsChildOf(groundPileArea))
                    {
                        HandleGroundOption();
                        currentTurnState = TurnState.Idle;
                    }
                    else if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        HandleSwapOption(clickedCard);
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 2: AwaitingBasraDecision (Basra spell drawn)
                else if (currentTurnState == TurnState.AwaitingBasraDecision)
                {
                    if (clicked.transform.IsChildOf(groundPileArea))
                    {
                        HandleGroundOption();
                        currentTurnState = TurnState.AwaitingBasraForcedObservation;
                    }
                    else if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        // For Basra, clicking a hand card takes the spell (swap).
                        HandleSwapOption(clickedCard);
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 3: AwaitingBasraForcedObservation (Basra spell discarded)
                else if (currentTurnState == TurnState.AwaitingBasraForcedObservation)
                {
                    if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        HandleForcedObservation(clickedCard);
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 4: AwaitingGroundSwap (ground swap mode)
                else if (currentTurnState == TurnState.AwaitingGroundSwap)
                {
                    if (clicked.transform.IsChildOf(groundPileArea))
                    {
                        StartCoroutine(ScaleDownGroundCard(currentGroundCard));
                        currentTurnState = TurnState.Idle;
                        groundCardToSwap = null;
                        return;
                    }
                    if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        HandleSwapOptionWithGround(clickedCard);
                        currentTurnState = TurnState.Idle;
                        StartCoroutine(ScaleDownGroundCard(currentGroundCard));
                    }
                    return;
                }
                // CASE 5: AwaitingTakeAndGiveDecision (Take and Give spell drawn)
                else if (currentTurnState == TurnState.AwaitingTakeAndGiveDecision)
                {
                    if (clicked.transform.IsChildOf(groundPileArea))
                    {
                        HandleGroundOption(); // Discards the spell.
                        currentTurnState = TurnState.AwaitingTakeAndGiveSelection;
                    }
                    else if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        HandleSwapOption(clickedCard); // Normal swap.
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 6: AwaitingTakeAndGiveSelection (selecting cards for Take and Give)
                else if (currentTurnState == TurnState.AwaitingTakeAndGiveSelection)
                {
                    if (clickedCard != null)
                    {
                        if (clickedCard.transform.parent == playerHandArea && takeAndGivePlayerCard == null)
                        {
                            takeAndGivePlayerCard = clickedCard;
                            StartCoroutine(ScaleUpCard(takeAndGivePlayerCard));
                        }
                        else if (clickedCard.transform.parent == opponentHandArea && takeAndGiveOpponentCard == null)
                        {
                            takeAndGiveOpponentCard = clickedCard;
                            StartCoroutine(ScaleUpCard(takeAndGiveOpponentCard));
                        }
                        if (takeAndGivePlayerCard != null && takeAndGiveOpponentCard != null)
                        {
                            StartCoroutine(HandleTakeAndGiveSwap(takeAndGivePlayerCard, takeAndGiveOpponentCard));
                        }
                    }
                    return;
                }
                // CASE 7: AwaitingSterlingDecision (Sterling spell drawn)
                else if (currentTurnState == TurnState.AwaitingSterlingDecision)
                {
                    if (clicked.transform.IsChildOf(groundPileArea))
                    {
                        HandleGroundOption(); // Discards sterling.
                        currentTurnState = TurnState.AwaitingSterlingSelection;
                        sterlingObservations = 0;
                    }
                    else if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        HandleSwapOption(clickedCard); // Spell not activated.
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 8: AwaitingSterlingSelection (selecting opponent cards for sterling)
                else if (currentTurnState == TurnState.AwaitingSterlingSelection)
                {
                    if (clickedCard != null && clickedCard.transform.parent == opponentHandArea)
                    {
                        StartCoroutine(HandleSterlingObservation(clickedCard));
                        sterlingObservations++;
                        if (sterlingObservations >= 2)
                        {
                            currentTurnState = TurnState.Idle;
                        }
                    }
                    return;
                }
                // CASE 9: AwaitingLookAtOwnDecision (LookAtOwn spell drawn)
                else if (currentTurnState == TurnState.AwaitingLookAtOwnDecision)
                {
                    if (clicked.transform.IsChildOf(groundPileArea))
                    {
                        HandleGroundOption(); // Discards the LookAtOwn spell.
                        currentTurnState = TurnState.AwaitingLookAtOwnSelection;
                    }
                    else if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        // If taken into hand, treat it as a swap.
                        HandleSwapOption(clickedCard);
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 10: AwaitingLookAtOwnSelection (selecting one card from player's hand)
                else if (currentTurnState == TurnState.AwaitingLookAtOwnSelection)
                {
                    if (clickedCard != null && clickedCard.transform.parent == playerHandArea)
                    {
                        StartCoroutine(HandleLookAtOwnAction(clickedCard));
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 11: AwaitingLookAtOpponentDecision (LookAtOpponent spell drawn)
                else if (currentTurnState == TurnState.AwaitingLookAtOpponentDecision)
                {
                    if (clicked.transform.IsChildOf(groundPileArea))
                    {
                        HandleGroundOption(); // Discards the LookAtOpponent spell.
                        currentTurnState = TurnState.AwaitingLookAtOpponentSelection;
                    }
                    else if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        HandleSwapOption(clickedCard); // Spell not activated.
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 12: AwaitingLookAtOpponentSelection (selecting one opponent card)
                else if (currentTurnState == TurnState.AwaitingLookAtOpponentSelection)
                {
                    if (clickedCard != null && clickedCard.transform.parent == opponentHandArea)
                    {
                        StartCoroutine(HandleLookAtOpponentAction(clickedCard));
                        currentTurnState = TurnState.Idle;
                    }
                    return;
                }
                // CASE 13: Idle state – no drawn card/spell pending.
                else if (currentTurnState == TurnState.Idle)
                {
                    Debug.Log("ana gowa el idle");
                    if (clicked.transform.IsChildOf(deckArea))
                    {

                        if (lastDrawnCard == null)
                        {
                            StartCoroutine(HandleDrawFromDeck());
                        }
                        return;
                    }
                    else if (clicked.transform.IsChildOf(groundPileArea))
                    {
                        StartCoroutine(ScaleUpGroundCard(currentGroundCard));
                        currentTurnState = TurnState.AwaitingGroundSwap;
                        groundCardToSwap = currentGroundCard;
                        return;
                    }
                    else if (clickedCard != null && clickedCard.isInPlayerHand)
                    {
                        HandleNormalObservation(clickedCard);
                        return;
                    }
                }
            }
        }
    }


    public void HandleCardSelected(Card card)
    {
        Debug.Log("GameManager handling card selected: " + card.cardData.cardName);
        // Use your existing game logic here.
        // For example, if you're in Idle state, you might process it as a normal observation:
        if (currentTurnState == TurnState.Idle)
        {
            HandleNormalObservation(card);
        }
        // You can expand this method to handle different states as needed.
    }


    // ---------------------------
    // ACTION HANDLERS
    // ---------------------------
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

        // Instantiate card at deck position
        GameObject drawnCardObj = Instantiate(cardPrefab, deckArea.position, Quaternion.identity);
        drawnCardObj.transform.SetParent(transform);
        drawnCardObj.transform.localScale = new Vector3(cardScale, cardScale, cardScale);

        // Initial position of card
        Vector3 floatOffset = new Vector3(0, 1.5f, 0);
        drawnCardObj.transform.position = deckArea.position + floatOffset;

        // Get card component and set data
        Card cardComp = drawnCardObj.GetComponent<Card>();
        cardComp.cardData = drawnData;
        cardComp.isInPlayerHand = true;

        // Calculate position slightly to the right of center screen
        Vector3 offsetCenterPosition = Camera.main.ViewportToWorldPoint(new Vector3(0.65f, 0.5f, Camera.main.nearClipPlane + 5f));
        offsetCenterPosition.z = drawnCardObj.transform.position.z; // Keep the same z position

        // Define the enlarged scale for the card when displayed
        Vector3 enlargedScale = new Vector3(cardScale * 1.5f, cardScale * 1.5f, cardScale * 1.5f);

        // Animation parameters
        float animationDuration = 1.5f;
        float elapsedTime = 0;
        Vector3 startPosition = drawnCardObj.transform.position;
        Vector3 startScale = drawnCardObj.transform.localScale;
        Quaternion startRotation = drawnCardObj.transform.rotation;

        // MODIFIED: Faster flip timing - compressed timeframe for the flip
        float flipStartTime = 0.4f * animationDuration;  // Start flip slightly later
        float flipEndTime = 0.4f * animationDuration;    // End flip much sooner (0.1 instead of 0.3 duration)

        // For SpriteRenderer-based cards
        SpriteRenderer spriteRenderer = cardComp.GetComponent<SpriteRenderer>();
        bool flipped = false;

        while (elapsedTime < animationDuration)
        {
            float t = elapsedTime / animationDuration;

            // Custom easing function for position and scale
            float easeValue = 1 - Mathf.Pow(1 - t, 3);

            // Simultaneous position and scale animation
            drawnCardObj.transform.position = Vector3.Lerp(startPosition, offsetCenterPosition, easeValue);
            drawnCardObj.transform.localScale = Vector3.Lerp(startScale, enlargedScale, easeValue);

            // Handle card flip animation
            if (!cardComp.IsFaceUp && elapsedTime >= flipStartTime && elapsedTime <= flipEndTime)
            {
                // Calculate flip progress (0 to 1)
                float flipProgress = (elapsedTime - flipStartTime) / (flipEndTime - flipStartTime);

                // MODIFIED: Use a more aggressive easing curve for faster perceived flip
                float flipEase = Mathf.SmoothStep(0, 1, flipProgress);

                // Rotate from 0 to 180 degrees
                float flipAngle = Mathf.Lerp(0, 180, flipEase);
                drawnCardObj.transform.rotation = startRotation * Quaternion.Euler(0, flipAngle, 0);

                // Change sprite exactly at the midpoint (when card is edge-on)
                if (flipProgress >= 0.5f && !flipped)
                {
                    cardComp.Flip(); // This changes the sprite based on your Card.Flip() implementation
                    flipped = true;
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure final position, rotation and scale are exact
        drawnCardObj.transform.position = offsetCenterPosition;
        drawnCardObj.transform.rotation = startRotation;
        drawnCardObj.transform.localScale = enlargedScale;

        // Make sure the card is face up at the end
        if (!cardComp.IsFaceUp)
        {
            cardComp.Flip();
        }

        // Set last drawn card
        lastDrawnCard = cardComp;
        Debug.Log("Drawn card: " + drawnData.cardName);
        if (drawnData.category == CardCategory.Spell)
        {
            // Check specific spell cards by name
            if (drawnData.cardName.ToLower() == "basra")
                currentTurnState = TurnState.AwaitingBasraDecision;
            else if (drawnData.cardName.ToLower() == "take and give")
                currentTurnState = TurnState.AwaitingTakeAndGiveDecision;
            else if (drawnData.cardName.ToLower() == "sterling")
                currentTurnState = TurnState.AwaitingSterlingDecision;
            // Check by spell action
            else if (drawnData.spellAction == SpellAction.LookAtOwn)
                currentTurnState = TurnState.AwaitingLookAtOwnDecision;
            else if (drawnData.spellAction == SpellAction.LookAtOpponent)
                currentTurnState = TurnState.AwaitingLookAtOpponentDecision;
            else
                currentTurnState = TurnState.AwaitingDeckDecision;
        }
        else
        {
            currentTurnState = TurnState.AwaitingDeckDecision;
        }

        // Card stays in offset position until player makes a decision
        yield return null;
    }
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

    public Card LastDrawnCard { get { return lastDrawnCard; } }


    IEnumerator HandleDeclareSkro()
    {
        Debug.Log("Declaring Skro: revealing all cards.");
        foreach (Card card in playerHandList)
        {
            if (card != null && !card.IsFaceUp)
                card.Flip();
        }
        RecalculateScore();
        Debug.Log("Scores recalculated. Determine winner.");
        roundCount++;
        yield return null;
    }

    // Normal observation: remove the card only if its value matches (with special 0/25 rule).
    void HandleNormalObservation(Card targetCard)
    {
        StartCoroutine(HandleObservation_Normal(targetCard));
    }

    IEnumerator HandleObservation_Normal(Card targetCard)
    {
        Debug.Log("Normal observation on card: " + targetCard.cardData.cardName);
        if (currentGroundCard == null)
        {
            Debug.Log("No ground card available for comparison.");
            yield break;
        }
        if (targetCard.cardData.cardValue == currentGroundCard.cardData.cardValue ||
           (targetCard.cardData.cardValue == 0 && currentGroundCard.cardData.cardValue == 25) ||
           (targetCard.cardData.cardValue == 25 && currentGroundCard.cardData.cardValue == 0))
        {
            Debug.Log("Observation match! Removing card: " + targetCard.cardData.cardName);
            playerHandList.Remove(targetCard);
            targetCard.transform.SetParent(groundPileArea, false);
            targetCard.transform.localPosition = Vector3.zero;
            targetCard.isInPlayerHand = false;
            playerScore -= targetCard.cardData.cardValue;
            Debug.Log("Player score updated: " + playerScore);
        }
        else
        {
            Debug.Log("Observation mismatch. Flipping both cards face up for 2 seconds.");
            if (!targetCard.IsFaceUp)
                targetCard.Flip();
            if (!currentGroundCard.IsFaceUp)
                currentGroundCard.Flip();
            yield return new WaitForSeconds(2f);
            Debug.Log("Flipping both cards face down.");
            if (targetCard.IsFaceUp)
                targetCard.Flip();
            if (currentGroundCard.IsFaceUp)
                currentGroundCard.Flip();
            // Remove the ground card and add it to the player's hand.
            Card removedGround = currentGroundCard;
            groundPileStack.Pop();
            removedGround.transform.SetParent(playerHandArea, false);
            removedGround.gameObject.SetActive(true);
            removedGround.isInPlayerHand = true;
            playerHandList.Add(removedGround);
        }
        UpdateGroundPileDisplay();
        RepositionPlayerHand();
        yield return null;
        currentTurnState = TurnState.Idle;
    }

    void HandleForcedObservation(Card targetCard)
    {
        StartCoroutine(HandleBasraAction_Forced(targetCard));
    }

    IEnumerator HandleBasraAction_Forced(Card targetCard)
    {
        Debug.Log("Forced Basra observation on card: " + targetCard.cardData.cardName);
        playerHandList.Remove(targetCard);
        targetCard.transform.SetParent(groundPileArea, false);
        targetCard.transform.localPosition = Vector3.zero;
        targetCard.isInPlayerHand = false;
        playerScore -= targetCard.cardData.cardValue;
        Debug.Log("Player score updated: " + playerScore);
        UpdateGroundPileDisplay();
        RepositionPlayerHand();
        yield return null;
        currentTurnState = TurnState.Idle;
    }

    void HandleObservationAction(Card targetCard)
    {
        HandleNormalObservation(targetCard);
    }

    // Swap option using a drawn card.
    void HandleSwapOption(Card targetCard)
    {
        Debug.Log("Swapping drawn card with player's card: " + targetCard.cardData.cardName);
        int targetIndex = playerHandList.IndexOf(targetCard);
        if (targetIndex < 0)
        {
            Debug.LogWarning("Target card not found in player's hand.");
            return;
        }
        playerHandList.RemoveAt(targetIndex);
        targetCard.transform.SetParent(groundPileArea, false);
        targetCard.transform.localPosition = Vector3.zero;
        targetCard.isInPlayerHand = false;
        if (!targetCard.IsFaceUp)
            targetCard.Flip();
        groundPileStack.Push(targetCard);
        Vector3 targetPos = targetCard.transform.position;
        lastDrawnCard.transform.position = targetPos;
        lastDrawnCard.transform.SetParent(playerHandArea, false);
        lastDrawnCard.isInPlayerHand = true;
        playerHandList.Insert(targetIndex, lastDrawnCard);
        currentGroundCard = targetCard;
        lastDrawnCard = null;
        UpdateGroundPileDisplay();
        currentTurnState = TurnState.Idle;
        RepositionPlayerHand();
        RecalculateScore();
        ResetGroundCardScale();
        FlipPlayerHandCardsDown();
    }

    IEnumerator HandleSterlingObservation(Card opponentCard)
    {
        Debug.Log("Sterling observation on opponent card: " + opponentCard.cardData.cardName);
        yield return StartCoroutine(ScaleUpCard(opponentCard));
        if (!opponentCard.IsFaceUp)
            opponentCard.Flip();
        yield return new WaitForSeconds(2f);
        if (opponentCard.IsFaceUp)
            opponentCard.Flip();
        yield return StartCoroutine(ScaleDownCard(opponentCard));
        // Note: The card remains in the opponent's hand.
        yield return null;
    }

    // Swap option using a pre-selected ground card.
    void HandleSwapOptionWithGround(Card targetCard)
    {
        int targetIndex = playerHandList.IndexOf(targetCard);
        Debug.Log("Swapping ground card with player's card: " + targetCard.cardData.cardName);

        if (targetIndex < 0)
        {
            Debug.LogWarning("Target card not found in player's hand.");
            return;
        }
        playerHandList.RemoveAt(targetIndex);
        targetCard.transform.SetParent(groundPileArea, false);
        targetCard.transform.localPosition = Vector3.zero;
        targetCard.isInPlayerHand = false;
        if (!targetCard.IsFaceUp)
            targetCard.Flip();
        groundPileStack.Push(targetCard);
        groundCardToSwap.transform.position = targetCard.transform.position;
        groundCardToSwap.transform.SetParent(playerHandArea, false);
        groundCardToSwap.isInPlayerHand = true;
        playerHandList.Insert(targetIndex, groundCardToSwap);
        currentGroundCard = targetCard;
        UpdateGroundPileDisplay();
        currentTurnState = TurnState.Idle;
        RepositionPlayerHand();
        RecalculateScore();
        ResetGroundCardScale();
        FlipPlayerHandCardsDown();
    }

    // Helper: Reset the ground card's scale.
    void ResetGroundCardScale()
    {
        if (currentGroundCard != null)
        {
            currentGroundCard.transform.localScale = new Vector3(cardScale, cardScale, cardScale);
        }
    }

    // Helper: Flip all player's hand cards face down.
    void FlipPlayerHandCardsDown()
    {
        foreach (Card card in playerHandList)
        {
            if (card.IsFaceUp)
                card.Flip();
        }
    }

    void RepositionPlayerHand()
    {
        float spacing = 1.5f;
        float startX = playerHandArea.position.x - ((playerHandList.Count - 1) * spacing) / 2f;
        Vector3 basePos = playerHandArea.position;
        for (int i = 0; i < playerHandList.Count; i++)
        {
            playerHandList[i].transform.position = new Vector3(startX + i * spacing, basePos.y, basePos.z);
        }
        // Enforce consistent scale on all cards in the player's hand.
        EnforceConsistentCardScale(playerHandArea);
    }



    // Option 1 for drawn card decision: Discard the drawn card onto the ground pile.
    void HandleGroundOption()
    {
        Debug.Log("Discarding drawn card onto the ground.");
        if (lastDrawnCard != null)
        {
            lastDrawnCard.transform.SetParent(groundPileArea, false);
            lastDrawnCard.transform.localPosition = Vector3.zero;
            lastDrawnCard.isInPlayerHand = false;
            if (!lastDrawnCard.IsFaceUp)
                lastDrawnCard.Flip();
            groundPileStack.Push(lastDrawnCard);
            currentGroundCard = lastDrawnCard;
            lastDrawnCard = null;
            UpdateGroundPileDisplay();
            RecalculateScore();
            if (currentTurnState == TurnState.AwaitingBasraDecision)
            {
                currentTurnState = TurnState.AwaitingBasraForcedObservation;
            }
            else if (currentTurnState == TurnState.AwaitingTakeAndGiveDecision)
            {
                currentTurnState = TurnState.AwaitingTakeAndGiveSelection;
            }
            else if (currentTurnState == TurnState.AwaitingSterlingDecision)
            {
                currentTurnState = TurnState.AwaitingSterlingSelection;
                sterlingObservations = 0;
            }
            else if (currentTurnState == TurnState.AwaitingLookAtOwnDecision)
            {
                currentTurnState = TurnState.AwaitingLookAtOwnSelection;
            }
            else if (currentTurnState == TurnState.AwaitingLookAtOpponentDecision)
            {
                currentTurnState = TurnState.AwaitingLookAtOpponentSelection;
            }
            else
            {
                currentTurnState = TurnState.Idle;
            }
        }
        else
        {
            Debug.Log("No drawn card available to discard.");
        }
    }

    // ---------------------------
    // BASRA SPELL HANDLING
    // ---------------------------
    IEnumerator HandleBasraAction_Normal(Card targetCard)
    {
        Debug.Log("Basra normal observation on card: " + targetCard.cardData.cardName);
        if (currentGroundCard == null)
        {
            Debug.Log("No ground card available for comparison.");
        }
        else if (targetCard.cardData.cardValue == currentGroundCard.cardData.cardValue)
        {
            Debug.Log("Basra match! Removing card: " + targetCard.cardData.cardName);
            playerHandList.Remove(targetCard);
            targetCard.transform.SetParent(groundPileArea, false);
            targetCard.transform.localPosition = Vector3.zero;
            targetCard.isInPlayerHand = false;
            playerScore -= targetCard.cardData.cardValue;
            Debug.Log("Player score updated: " + playerScore);
        }
        else
        {
            Debug.Log("Basra observation mismatch. Flipping both cards face up for 2 seconds.");
            if (!targetCard.IsFaceUp)
                targetCard.Flip();
            if (!currentGroundCard.IsFaceUp)
                currentGroundCard.Flip();
            yield return new WaitForSeconds(2f);
            Debug.Log("Flipping both cards face down.");
            if (targetCard.IsFaceUp)
                targetCard.Flip();
            if (currentGroundCard.IsFaceUp)
                currentGroundCard.Flip();
            Card removedGround = currentGroundCard;
            groundPileStack.Pop();
            removedGround.transform.SetParent(playerHandArea, false);
            removedGround.gameObject.SetActive(true);
            removedGround.isInPlayerHand = true;
            playerHandList.Add(removedGround);
        }
        UpdateGroundPileDisplay();
        RepositionPlayerHand();
        yield return null;
        currentTurnState = TurnState.Idle;
    }

  

    // ---------------------------
    // LOOK AT OWN SPELL HANDLING
    // ---------------------------
    // When a LookAtOwn spell card (card "7" or "8") is drawn, the game enters AwaitingLookAtOwnDecision.
    // If the player clicks on the ground pile, the spell is discarded and enters AwaitingLookAtOwnSelection.
    // In AwaitingLookAtOwnSelection, clicking a player's hand card triggers the reveal animation.
    IEnumerator HandleLookAtOwnAction(Card targetCard)
    {
        Debug.Log("LookAtOwn: Revealing player's card: " + targetCard.cardData.cardName);
        yield return StartCoroutine(ScaleUpCard(targetCard));
        if (!targetCard.IsFaceUp)
            targetCard.Flip();
        yield return new WaitForSeconds(2f);
        if (targetCard.IsFaceUp)
            targetCard.Flip();
        yield return StartCoroutine(ScaleDownCard(targetCard));
        yield return null;
    }

    // ---------------------------
    // LOOK AT OPPONENT SPELL HANDLING
    // ---------------------------
    // When a LookAtOpponent spell card (card "9" or "10") is drawn, the game enters AwaitingLookAtOpponentDecision.
    // If the player clicks on the ground pile, the spell is discarded and enters AwaitingLookAtOpponentSelection.
    // In AwaitingLookAtOpponentSelection, clicking an opponent's hand card triggers the reveal animation.
    IEnumerator HandleLookAtOpponentAction(Card targetCard)
    {
        Debug.Log("LookAtOpponent: Revealing opponent's card: " + targetCard.cardData.cardName);
        yield return StartCoroutine(ScaleUpCard(targetCard));
        if (!targetCard.IsFaceUp)
            targetCard.Flip();
        yield return new WaitForSeconds(2f);
        if (targetCard.IsFaceUp)
            targetCard.Flip();
        yield return StartCoroutine(ScaleDownCard(targetCard));
        yield return null;
    }

    // ---------------------------
    // TAKE AND GIVE SPELL HANDLING
    // ---------------------------
    IEnumerator HandleTakeAndGiveSwap(Card playerCard, Card opponentCard)
    {
        Debug.Log("Performing Take and Give swap between " + playerCard.cardData.cardName + " and " + opponentCard.cardData.cardName);

        // Animate the swap transition between the two cards.
        Vector3 playerOrigPos = playerCard.transform.position;
        Vector3 opponentOrigPos = opponentCard.transform.position;
        float duration = 1.0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            playerCard.transform.position = Vector3.Lerp(playerOrigPos, opponentOrigPos, elapsed / duration);
            opponentCard.transform.position = Vector3.Lerp(opponentOrigPos, playerOrigPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        playerCard.transform.position = opponentOrigPos;
        opponentCard.transform.position = playerOrigPos;

        // Scale both cards down to normal size.
        yield return StartCoroutine(ScaleDownCard(playerCard));
        yield return StartCoroutine(ScaleDownCard(opponentCard));

        // Determine indices in their current lists.
        int playerIndex = playerHandList.IndexOf(playerCard);
        int opponentIndex = opponentHandList.IndexOf(opponentCard);

        if (playerIndex >= 0 && opponentIndex >= 0)
        {
            // Remove the cards from their current lists.
            playerHandList.RemoveAt(playerIndex);
            opponentHandList.RemoveAt(opponentIndex);

            // Reparent: the card from the opponent's hand now moves to the player's hand.
            opponentCard.transform.SetParent(playerHandArea, false);
            opponentCard.gameObject.SetActive(true); // Ensure it's active.
            opponentCard.isInPlayerHand = true;

            // And the card from the player's hand moves to the opponent's hand.
            playerCard.transform.SetParent(opponentHandArea, false);
            playerCard.gameObject.SetActive(true);
            playerCard.isInPlayerHand = false;

            // Update the lists accordingly.
            playerHandList.Insert(playerIndex, opponentCard);
            opponentHandList.Insert(opponentIndex, playerCard);
        }

        // Reposition both hands.
        RepositionPlayerHand();
        RepositionOpponentHand();

        yield return null;

        // Reset state and clear selections.
        currentTurnState = TurnState.Idle;
        takeAndGivePlayerCard = null;
        takeAndGiveOpponentCard = null;
    }


    // ---------------------------
    // SCALE COROUTINES (Generic)
    // ---------------------------
    IEnumerator ScaleUpCard(Card card)
    {
        Vector3 origScale = card.transform.localScale;
        Vector3 targetScale = origScale * 1.2f;
        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            card.transform.localScale = Vector3.Lerp(origScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        card.transform.localScale = targetScale;
    }

    IEnumerator ScaleDownCard(Card card)
    {
        Vector3 origScale = card.transform.localScale;
        Vector3 targetScale = new Vector3(cardScale, cardScale, cardScale);
        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            card.transform.localScale = Vector3.Lerp(origScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        card.transform.localScale = targetScale;
    }

    IEnumerator ScaleUpGroundCard(Card groundCard)
    {
        yield return StartCoroutine(ScaleUpCard(groundCard));
    }

    IEnumerator ScaleDownGroundCard(Card groundCard)
    {
        yield return StartCoroutine(ScaleDownCard(groundCard));
    }

    // ---------------------------
    // SCORE CALCULATION
    // ---------------------------
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
}

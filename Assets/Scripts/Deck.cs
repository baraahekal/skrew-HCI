using UnityEngine;
using System.Collections.Generic;

public class Deck : MonoBehaviour
{
    // List to populate via Inspector; represents the full set of cards.
    public List<CardData> cardDataList = new List<CardData>();

    // Internal stack to represent the deck.
    private Stack<CardData> deckStack = new Stack<CardData>();

    void Awake()
    {
        // When the game starts, shuffle and push cards into the stack.
        ShuffleDeck();
    }

    /// <summary>
    /// Shuffles the cardDataList and loads it into deckStack.
    /// </summary>
    public void ShuffleDeck()
    {
        // Fisher-Yates shuffle.
        for (int i = 0; i < cardDataList.Count; i++)
        {
            int rnd = Random.Range(i, cardDataList.Count);
            CardData temp = cardDataList[rnd];
            cardDataList[rnd] = cardDataList[i];
            cardDataList[i] = temp;
        }

        deckStack.Clear();
        // Push each card onto the stack.
        foreach (CardData card in cardDataList)
        {
            deckStack.Push(card);
        }
    }

    /// <summary>
    /// Draws the top card from the deck.
    /// </summary>
    public CardData DrawCardData()
    {
        if (deckStack.Count > 0)
            return deckStack.Pop();
        return null;
    }
}

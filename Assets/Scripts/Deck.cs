using UnityEngine;
using System.Collections.Generic;

public class Deck : MonoBehaviour
{
    // List to populate via Inspector; represent one instance of each card type.
    public List<CardData> cardDataList = new List<CardData>();

    // Internal stack to represent the deck.
    private Stack<CardData> deckStack = new Stack<CardData>();

    void Awake()
    {
        // When the game starts, build and shuffle the deck.
        ShuffleDeck();
        Debug.Log("Deck initialized with " + deckStack.Count + " cards.");
    }

    /// <summary>
    /// Creates copies of cards based on desired counts and shuffles the deck.
    /// Desired counts:
    /// - For cards "1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 20": 4 copies each.
    /// - For cards "sterling, nasser mansy, take and give": 4 copies each.
    /// - For cards "0, 25": 2 copies each.
    /// - For card "-1": 1 copy.
    /// </summary>
    public void ShuffleDeck()
    {
        // Define desired copy counts (using lower-case keys for case-insensitive matching).
        Dictionary<string, int> copyCounts = new Dictionary<string, int>()
        {
            { "1", 4 },
            { "2", 4 },
            { "3", 4 },
            { "4", 4 },
            { "5", 4 },
            { "6", 4 },
            { "7", 4 },
            { "8", 4 },
            { "9", 4 },
            { "10", 4 },
            { "20", 4 },
            { "sterling", 4 },
            { "basra", 4 },
            { "take and give", 4 },
            { "0", 2 },
            { "25", 2 },
            { "-1", 1 }
        };

        // Create a new list to hold the deck data with the correct number of copies.
        List<CardData> deckDataList = new List<CardData>();

        foreach (CardData cardData in cardDataList)
        {
            // Assume cardData.cardName is not null.
            string nameKey = cardData.cardName.ToLower();
            if (copyCounts.ContainsKey(nameKey))
            {
                int count = copyCounts[nameKey];
                for (int i = 0; i < count; i++)
                {
                    deckDataList.Add(cardData);
                }
            }
            else
            {
                // Optionally, you can choose to add a default count if the card is not specified.
            }
        }

        // Shuffle the deckDataList using Fisher-Yates shuffle.
        for (int i = 0; i < deckDataList.Count; i++)
        {
            int rnd = Random.Range(i, deckDataList.Count);
            CardData temp = deckDataList[rnd];
            deckDataList[rnd] = deckDataList[i];
            deckDataList[i] = temp;
        }

        // Clear the existing deckStack and push all cards from the shuffled list.
        deckStack.Clear();
        foreach (CardData card in deckDataList)
        {
            deckStack.Push(card);
        }
    }

    /// <summary>
    /// Draws the top card from the deck.
    /// </summary>
    public CardData DrawCardData()
    {
        Debug.Log("Deck initialized with " + deckStack.Count + " cards.");
        if (deckStack.Count > 0)
            return deckStack.Pop();
        return null;
    }
}

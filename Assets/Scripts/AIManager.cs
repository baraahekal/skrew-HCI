using UnityEngine;
using System.Collections;

public class AIManager : MonoBehaviour
{
    public Transform opponentHandArea;
    public GameManager gameManager;

    public void StartTurn()
    {
        StartCoroutine(HandleAITurn());
    }

    IEnumerator HandleAITurn()
    {
        Debug.Log("AI Turn started.");
        yield return new WaitForSeconds(1.5f);
        int rand = Random.Range(0, 3); // 0: Draw from Deck, 1: Draw from Ground, 2: Declare Skro
        switch (rand)
        {
            case 0:
                Debug.Log("AI draws from deck.");
                gameManager.PerformAction(GameAction.DrawFromDeck);
                break;
            case 1:
                Debug.Log("AI draws from ground.");
                gameManager.PerformAction(GameAction.DrawFromGround);
                break;
            case 2:
                Debug.Log("AI declares Skro.");
                gameManager.PerformAction(GameAction.DeclareSkro);
                break;
        }
        yield return null;
    }
}

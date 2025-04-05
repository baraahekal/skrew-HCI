using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Button drawDeckButton;
    public Button drawGroundButton;
    public Button declareSkroButton;

    public GameManager gameManager;

    void Start()
    {
        drawDeckButton.onClick.AddListener(() => gameManager.PerformAction(GameAction.DrawFromDeck));
        drawGroundButton.onClick.AddListener(() => gameManager.PerformAction(GameAction.DrawFromGround));
        declareSkroButton.onClick.AddListener(() => gameManager.PerformAction(GameAction.DeclareSkro));
    }
}

using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // References to your background images.
    public Image backgroundImageA; // For UserA.
    public Image backgroundImageB; // For UserB.

    // Other UI elements and prefabs...
    public GameObject cardPrefabUserA;
    public GameObject cardPrefabUserB;
    public GameObject deckPrefabUserA;
    public GameObject deckPrefabUserB;
    public Transform deckArea; // This should be inside your interactive UI Canvas.

    void Start()
    {
        // Retrieve the signed-in user ID from PlayerPrefs.
        string userId = PlayerPrefs.GetString("UserID", "UserA");
        Debug.Log("UIManager starting with UserID: " + userId);

        // Disable both background images initially.
        backgroundImageA.gameObject.SetActive(false);
        backgroundImageB.gameObject.SetActive(false);

        // Clear any existing children from deckArea.
        foreach (Transform child in deckArea)
        {
            child.gameObject.SetActive(false);
        }

        // Switch UI based on the user ID.
        if (userId == "UserB")
        {
            // Enable UserB background.
            backgroundImageB.gameObject.SetActive(true);

            // Set the appropriate card prefab.
            GameManager.Instance.cardPrefab = cardPrefabUserB;

            // Instantiate the deck prefab for UserB.
            if (deckPrefabUserB != null && deckArea != null)
            {
                GameObject deckInstance = Instantiate(deckPrefabUserB, deckArea);
                // Reset its anchored position so that it follows deckArea's layout.
                RectTransform rt = deckInstance.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = Vector2.zero;
                // Bring it to the front.
                deckInstance.transform.SetAsLastSibling();
                // Explicitly set it active.
                deckInstance.SetActive(true);
                Debug.Log("Deck for UserB instantiated.");
            }
            else
            {
                Debug.LogError("Deck prefab for UserB or deckArea not assigned.");
            }
        }
        else // Default to UserA.
        {
            // Enable UserA background.
            backgroundImageA.gameObject.SetActive(true);

            // Set the appropriate card prefab.
            GameManager.Instance.cardPrefab = cardPrefabUserA;

            // Instantiate the deck prefab for UserA.
            if (deckPrefabUserA != null && deckArea != null)
            {
                GameObject deckInstance = Instantiate(deckPrefabUserA, deckArea);
                RectTransform rt = deckInstance.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = Vector2.zero;
                deckInstance.transform.SetAsLastSibling();
                deckInstance.SetActive(true);
                Debug.Log("Deck for UserA instantiated.");
            }
            else
            {
                Debug.LogError("Deck prefab for UserA or deckArea not assigned.");
            }
        }
    }
}

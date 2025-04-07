using UnityEngine;
using UnityEngine.UI;

public class CursorController : MonoBehaviour
{
    public static CursorController Instance;  // Singleton instance.
    public Image cursorImage;                 // Reference to your UI Image.

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Sets the cursor's anchored position on the Canvas based on normalized x and y values (0 to 1).
    /// </summary>
    public void UpdateCursorPosition(float normX, float normY)
    {
        // Get the RectTransform of the canvas (assumes the cursorImage is on a Canvas)
        RectTransform canvasRect = cursorImage.canvas.GetComponent<RectTransform>();

        // Calculate a position in canvas space:
        // Multiply the normalized value by the canvas size, then subtract half the size
        // so that (0,0) maps to the bottom-left and (1,1) maps to the top-right.
        Vector2 newAnchoredPos = new Vector2(normX * canvasRect.sizeDelta.x, normY * canvasRect.sizeDelta.y);
        newAnchoredPos -= canvasRect.sizeDelta / 2f;

        // Set the anchored position of the cursor.
        cursorImage.rectTransform.anchoredPosition = newAnchoredPos;
    }
}

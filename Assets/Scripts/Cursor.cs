using UnityEngine;
using TuioNet.Tuio11;       // For Tuio11 types.
using TuioUnity.Tuio11;      // For Tuio11Dispatcher.
using TuioUnity.Common;      // For TuioSessionBehaviour.
using UnityEngine.UI;

public class TUIOCursor : MonoBehaviour
{
    // The marker ID to track.
    public int targetSymbolID = 5;

    // Reference to the UI element that will represent the cursor.
    public RectTransform cursorIndicator;

    private Tuio11Dispatcher _dispatcher;

    void Awake()
    {
        // Find the TuioSessionBehaviour in the scene.
        TuioSessionBehaviour session = FindObjectOfType<TuioSessionBehaviour>();
        if (session == null)
        {
            Debug.LogError("TuioSessionBehaviour not found in scene! Add the TUIO Session object.");
            return;
        }

        // Get the dispatcher and cast it.
        _dispatcher = session.TuioDispatcher as Tuio11Dispatcher;
        if (_dispatcher == null)
        {
            Debug.LogError("Dispatcher is not a Tuio11Dispatcher or not available.");
            return;
        }

        // Subscribe to object update events.
        _dispatcher.OnObjectUpdate += HandleObjectUpdate;
    }

    void OnDestroy()
    {
        if (_dispatcher != null)
        {
            _dispatcher.OnObjectUpdate -= HandleObjectUpdate;
        }
    }

    void HandleObjectUpdate(object sender, Tuio11Object tuioObject)
    {
        // Check if this object is the one we want to track.
        if (tuioObject != null && tuioObject.SymbolId == targetSymbolID)
        {
            // TUIO 1.1 sends normalized coordinates (0 to 1).
            Vector3 viewportPos = new Vector3(tuioObject.Position.X, tuioObject.Position.Y, Camera.main.nearClipPlane + 5f);

            // Convert to world coordinates.
            Vector3 worldPos = Camera.main.ViewportToWorldPoint(viewportPos);

            // If your cursor indicator is a UI element (RectTransform), you might need to convert world coordinates to canvas space.
            // One way is to use the canvas’s Render Mode and a utility like RectTransformUtility.ScreenPointToLocalPointInRectangle.
            // Here we assume the Canvas is in Screen Space – Overlay, so you can directly use the screen point.
            Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPos);

            // Set the cursor indicator position.
            if (cursorIndicator != null)
            {
                cursorIndicator.position = screenPoint;
            }
        }
    }
}

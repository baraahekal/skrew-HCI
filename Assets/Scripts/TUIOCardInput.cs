using UnityEngine;
using TuioNet.Tuio11;      // Contains Tuio11Object and related types.
using TuioUnity.Common;    // Contains TuioSessionBehaviour.
using TuioUnity.Tuio11;    // Contains Tuio11Dispatcher.

public class TUIOInputManager : MonoBehaviour
{
    // This static property holds the latest TUIO object.
    public static Tuio11Object CurrentTuioObject { get; private set; }

    // The target marker SymbolID to use (adjust as needed).
    public int targetSymbolID = 2;

    private Tuio11Dispatcher _dispatcher;

    void Awake()
    {
        // Find the TuioSessionBehaviour (it must be in your scene).
        TuioSessionBehaviour session = FindObjectOfType<TuioSessionBehaviour>();
        if (session == null)
        {
            Debug.LogError("TuioSessionBehaviour not found! Add a TUIO Session object to the scene.");
            return;
        }

        // Access its dispatcher and cast it to Tuio11Dispatcher.
        _dispatcher = session.TuioDispatcher as Tuio11Dispatcher;
        if (_dispatcher == null)
        {
            Debug.LogError("Dispatcher is not available or not a Tuio11Dispatcher.");
            return;
        }

        // Subscribe to the TUIO update events.
        _dispatcher.OnObjectAdd += OnObjectAdd;
        _dispatcher.OnObjectUpdate += OnObjectUpdate;
    }

    void OnDestroy()
    {
        if (_dispatcher != null)
        {
            _dispatcher.OnObjectAdd -= OnObjectAdd;
            _dispatcher.OnObjectUpdate -= OnObjectUpdate;
        }
    }

    // When a marker is added:
    void OnObjectAdd(object sender, Tuio11Object tuioObj)
    {
        if (tuioObj != null && tuioObj.SymbolId == targetSymbolID)
        {
            CurrentTuioObject = tuioObj;
            // In Idle state, if no card is drawn, immediately trigger a draw action.
            if (GameManager.Instance.CurrentTurnState == TurnState.Idle &&
                GameManager.Instance.LastDrawnCard == null)
            {
                GameManager.Instance.PerformAction(GameAction.DrawFromDeck);
                // Record the initial angle for later comparison.
                GameManager.Instance.RecordInitialTuioAngle(tuioObj.Angle);
            }
        }
    }

    // On every update, store the latest marker.
    void OnObjectUpdate(object sender, Tuio11Object tuioObj)
    {
        if (tuioObj != null)
        {
            CurrentTuioObject = tuioObj;
            Debug.Log("TUIO Object Angle: " + tuioObj.Angle);
            // Forward the TUIO data to GameManager for processing.
            GameManager.Instance.ProcessTuioInput(tuioObj);
        }
    }
}

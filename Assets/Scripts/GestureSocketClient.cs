using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class GestureSocketClient : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int port = 65433;

    private Thread clientThread;
    private TcpClient client;
    private NetworkStream stream;

    // These fields store the latest received data.
    private volatile string receivedGesture = "";
    private volatile float receivedX = 0f;
    private volatile float receivedY = 0f;
    private volatile bool dataReceived = false;

    void Start()
    {
        clientThread = new Thread(new ThreadStart(ConnectToPython));
        clientThread.IsBackground = true;
        clientThread.Start();
    }

    void Update()
    {
        // On the main thread, update the cursor position using the latest values.
        if (dataReceived)
        {
            // If you're using a CursorController singleton, update it like this:
            if (CursorController.Instance != null)
            {
                CursorController.Instance.UpdateCursorPosition(receivedX, receivedY);
            }
            // Optionally, you could also forward the gesture string to GameManager:
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ProcessGesture(receivedGesture);
            }
            dataReceived = false; // Reset flag until new data arrives.
        }
    }

    void ConnectToPython()
    {
        try
        {
            client = new TcpClient(serverIP, port);
            stream = client.GetStream();
            byte[] buffer = new byte[256];

            while (true)
            {
                int bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes > 0)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                    Debug.Log("Received: " + msg);

                    // Expected format: "Gesture:Victory|X:0.6234|Y:0.4231"
                    string[] parts = msg.Split('|');
                    if (parts.Length == 3)
                    {
                        // Parse gesture
                        if (parts[0].StartsWith("Gesture:"))
                        {
                            receivedGesture = parts[0].Substring("Gesture:".Length);
                        }
                        // Parse X
                        if (parts[1].StartsWith("X:"))
                        {
                            float.TryParse(parts[1].Substring("X:".Length), out receivedX);
                        }
                        // Parse Y
                        if (parts[2].StartsWith("Y:"))
                        {
                            float.TryParse(parts[2].Substring("Y:".Length), out receivedY);
                        }
                        dataReceived = true;
                    }
                }
                Thread.Sleep(10);
            }
        }
        catch (SocketException e)
        {
            Debug.LogError("Socket error: " + e);
        }
    }

    void OnApplicationQuit()
    {
        if (clientThread != null)
            clientThread.Abort();
        if (stream != null)
            stream.Close();
        if (client != null)
            client.Close();
    }
}

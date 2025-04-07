using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine.SceneManagement;

public class BluetoothSocketClient : MonoBehaviour
{
    public string serverIP = "127.0.0.1"; // Use localhost if running on the same machine.
    public int port = 65432;
    private Thread clientThread;
    public string userID = "";

    // Flag indicating that a user ID has been received.
    private volatile bool userIDReceived = false;

    // Flag to trigger scene load on the main thread.
    private volatile bool shouldLoadMainScene = false;

    void Start()
    {
        clientThread = new Thread(new ThreadStart(ConnectToServer));
        clientThread.IsBackground = true;
        clientThread.Start();
    }

    void Update()
    {
        // If a userID was received on the background thread, update PlayerPrefs on the main thread.
        if (userIDReceived)
        {
            // Update PlayerPrefs on the main thread.
            PlayerPrefs.SetString("UserID", userID);
            PlayerPrefs.Save();

            // Signal that we should load the MainScene.
            shouldLoadMainScene = true;
            userIDReceived = false; // Reset the flag.
        }

        if (shouldLoadMainScene)
        {
            shouldLoadMainScene = false;
            SceneManager.LoadScene("MainScene");
        }
    }

    void ConnectToServer()
    {
        try
        {
            TcpClient client = new TcpClient(serverIP, port);
            Byte[] data = new Byte[256];
            NetworkStream stream = client.GetStream();

            // Read data from the server.
            int bytes = stream.Read(data, 0, data.Length);
            userID = Encoding.UTF8.GetString(data, 0, bytes);
            Debug.Log("Received user ID: " + userID);

            // Set the flag that we received the user ID.
            userIDReceived = true;

            stream.Close();
            client.Close();
        }
        catch (SocketException e)
        {
            Debug.Log("SocketException: " + e);
        }
    }

    void OnApplicationQuit()
    {
        if (clientThread != null)
            clientThread.Abort();
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public enum NetworkEngineState {ERR, CONNECTING, CONNECTED, WORKING};

public class NetworkEngine : MonoBehaviour
{
	public string serverIp;
	public Int32 serverPort;
	public Int32 retryCount;
	public Int32 retryDelay;
	public Int32 bufferSize;

	public NetworkEngineState state;
	public GameObject blocker;

	private Socket socket;
	private Thread thread;
	private Byte[] buffer;


	//=================Get Info=============================
	Dropdown playerList, historyList;
	public void updatePlayerList()
	{


	}
	//======================================================


	// Use this for initialization
	void Start()
	{
		buffer = new byte[bufferSize];
		thread = new Thread(connectToServer);
		socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		thread.Start();

		state = NetworkEngineState.CONNECTING;
	}
	
	// Update is called once per frame
	void Update()
	{
		bool unblock = false;
		unblock |= state == NetworkEngineState.CONNECTED;
		blocker.SetActive(!unblock);
	}

	void connectToServer()
	{
		int retry = 0;
		while(true)
		{
			try
			{
				IPAddress ip = IPAddress.Parse(serverIp);
				socket.Connect(new IPEndPoint(ip, serverPort));
				Debug.Log("Connected to server");
				state = NetworkEngineState.CONNECTED;
				thread.Abort();
			}
			catch
			{
				Debug.Log("Connection failed");

				Thread.Sleep(retryDelay);
				++retry;
				if (retry > retryCount)
				{
					state = NetworkEngineState.CONNECTED;
					break;
				}
			}
		}

	}

	public void GetHistory()
	{
		thread = new Thread(RecvMsg);
		thread.Start();
	}

	void RecvMsg()
	{
		state = NetworkEngineState.WORKING;
		Debug.Log("Receiving");
		socket.Receive(buffer, 4, SocketFlags.None);
		Debug.Log(buffer);
		byte len = buffer[3];
		socket.Receive(buffer, 4, len, SocketFlags.None);
		Debug.Log(buffer);
		state = NetworkEngineState.CONNECTED;
		thread.Abort();
	}

	void OnDestroy()
	{
		if (thread.IsAlive)
			thread.Abort();
	}
}

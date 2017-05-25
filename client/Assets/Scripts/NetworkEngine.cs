 using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public enum NetworkEngineState {ERR, CONNECTING, CONNECTED};

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


	// Use this for initialization
	void Start()
	{
		buffer = new byte[bufferSize];
		thread = new Thread(connectToServer);
		thread.Start();


		socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		state = NetworkEngineState.CONNECTING;
	}
	
	// Update is called once per frame
	void Update()
	{
		if (state == NetworkEngineState.CONNECTED)
		{
			blocker.SetActive(false);
		}
		else
		{

			blocker.SetActive(true);
		}
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
				break;
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

	void OnDestroy()
	{
		thread.Abort();
	}
}

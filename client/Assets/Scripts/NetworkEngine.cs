using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;


public enum NetworkEngineState {ERR, CONNECTING, READY, WORKING};

public class NetworkEngine : MonoBehaviour
{
	public string serverIp;
	public Int32 serverPort;
	public Int32 retryCount;
	public Int32 retryDelay;
	public Int32 bufferSize;
	public Int32 ver;
	public string msg, notificationMsg;

	public NetworkEngineState state;
	public GameObject blocker;
	public Text blockMessage;

    public InputField userName, passwd;
    public BattleField battleField;

	private Socket socket;
	private Thread thread;
	private Byte[] buffer;
	private Int16 dataLength;

    public GameObject notificationPanel;
    public Text notificationText;

    private Boolean notificationEnable, loginSuccess;

    public GameObject goLogin, goMenu, goTitle;

    public byte oswitcha, oswitchb;
    public byte[] ostrike;
    public byte selfHp, oppoHp;

	//=================Get Info=============================
	Dropdown playerList, historyList;
	public void UpdatePlayerList()
	{


	}

	public void CheckVersionInfo()
	{
		state = NetworkEngineState.WORKING;
		thread = new Thread(WaitVersionInfo);
		thread.Start();
	}

	void WaitVersionInfo()
	{
		msg = "Waiting Version Info";
		GetPackage();
		CheckPackHead(0x01);
		Int32 sver = BitConverter.ToInt32(buffer, 4);
		sver = IPAddress.NetworkToHostOrder(sver);
		Debug.Log(sver);
		if (sver != ver)
		{
			state = NetworkEngineState.ERR;
			msg = "Version Err: server: " + sver + " client: " + ver;
		}
		else
		{
			state = NetworkEngineState.READY;
		}
	}

    public void Register()
    {
		state = NetworkEngineState.WORKING;
		thread = new Thread(SendRegister);
		thread.Start();
    }

    public void Login()
    {
        state = NetworkEngineState.WORKING;
		thread = new Thread(SendLogin);
		thread.Start();
    }

    public void Action()
    {
        msg = "Waiting for opponent's action";
        state = NetworkEngineState.WORKING;
		thread = new Thread(SendAction);
		thread.Start();
    }

    void SetBufferHead(byte head0, byte head1, Int16 len)
    {
        buffer[0] = head0;
        buffer[1] = head1;
        dataLength = len;
        len = IPAddress.HostToNetworkOrder(len);
        System.Array.Copy(BitConverter.GetBytes(len), 0, buffer, 2, 2);
    }

    void SendUPPack(byte head)
    {
        byte[] userNameBuffer = Encoding.ASCII.GetBytes(userName.text);
        byte[] passwdBuffer = Encoding.ASCII.GetBytes(passwd.text);
        byte ulen = (byte)userNameBuffer.Length;
        byte plen = (byte)passwdBuffer.Length;
        dataLength = (short)(ulen + plen + 2);
        SetBufferHead(0x91, head, dataLength);
        buffer[4] = ulen;
        buffer[5 + ulen] = plen;
        System.Array.Copy(userNameBuffer, 0, buffer, 5, ulen);
        System.Array.Copy(passwdBuffer, 0, buffer, 6 + ulen, plen);
        socket.Send(buffer, dataLength + 4, SocketFlags.None);
        GetPackage();
        CheckPackHead(head);
    }

    void SendRegister()
    {
		msg = "Waiting Register Result";
        SendUPPack(0x02);
        ShowNotification(Encoding.ASCII.GetString(buffer, 4, dataLength));
        state = NetworkEngineState.READY;
    }

    void SendLogin()
    {
		msg = "Waiting Log In Result";
        SendUPPack(0x03);
        if (loginSuccess = (buffer[4] == 1))
        {
            ShowNotification("Success!");
        }
        else
            ShowNotification("Failed! Please check username and passwd");
        state = NetworkEngineState.READY;
    }

    void SendAction()
    {
        Debug.Log("SEND ACTION");
        SetBufferHead(0x91, 0x11, 6);
        buffer[4] = battleField.switcha;
        buffer[5] = battleField.switchb;
        for (int i = 0; i < 4; ++i)
        {
            Debug.Log(i);
            buffer[6 + i] = battleField.strike[i];
        }
        socket.Send(buffer, dataLength, SocketFlags.None);

        GetPackage();
        CheckPackHead(0x11);
        oswitcha = buffer[4];
        oswitchb = buffer[5];
        for (int i = 0; i < 4; ++i)
            ostrike[i] = buffer[6 + i];
        oppoHp = buffer[10];
        selfHp = buffer[11];
        battleField.enableResult = true;
        state = NetworkEngineState.READY;

    }

	//======================================================

	void GetPackage()
	{
		socket.Receive(buffer, 4, SocketFlags.None);
		dataLength = BitConverter.ToInt16(buffer, 2);
		dataLength = IPAddress.NetworkToHostOrder(dataLength);
        Debug.Log(dataLength);
		socket.Receive(buffer, 4, dataLength, SocketFlags.None);
	}

	void CheckPackHead(Byte head)
	{
		if (buffer[1] != head)
		{
			msg = "Head Err: found " + (Int32)buffer[1] + " expected " + (Int32)head;
			state = NetworkEngineState.ERR;
		}
	}

	// Use this for initialization
	void Start()
	{
		buffer = new byte[bufferSize];
        ostrike = new byte[4];
		thread = new Thread(connectToServer);
		socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		thread.Start();

		state = NetworkEngineState.CONNECTING;
	}
	
	// Update is called once per frame
	void Update()
	{
		bool unblock = false;
		unblock |= state == NetworkEngineState.READY;
		blocker.SetActive(!unblock);
		blockMessage.text = msg;
        notificationText.text = notificationMsg;

		if (state == NetworkEngineState.ERR)
		{
			thread.Abort();
		}

        if (notificationEnable)
        {
            notificationEnable = false;
            notificationPanel.SetActive(true);
        }

        if (loginSuccess)
        {
            loginSuccess = false;
            goLogin.SetActive(false);
            goTitle.SetActive(false);
            goMenu.SetActive(true);
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
				state = NetworkEngineState.READY;
				thread.Abort();
			}
			catch
			{
				Debug.Log("Connection failed");

				Thread.Sleep(retryDelay);
				++retry;
				if (retry > retryCount)
				{
					state = NetworkEngineState.ERR;
					msg = "Lost connection to server. Please restart game";
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
		state = NetworkEngineState.READY;
		thread.Abort();
	}

	void OnDestroy()
	{
		if (thread.IsAlive)
			thread.Abort();
	}

	public void RestartGame()
	{
		SceneManager.LoadScene("main");
	}

    public void ShowNotification(string msg)
    {
        notificationEnable = true;
        notificationMsg = msg;
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;


public enum NetworkEngineState {ERR, CONNECTING, READY, WORKING};

public struct User
{
    public Int16 uid;
    public string name;
}

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
	public Text blockMessage, selfNameText, enemyNameText;

    public InputField userName, passwd;
    public BattleField battleField;

	private Socket socket;
	private Thread thread;
	private Byte[] buffer;
	private Int16 dataLength;

    public GameObject notificationPanel;
    public Text notificationText;

    public bool enableWait;

    private Boolean notificationEnable, loginSuccess, refreshUserList, foundOpponent, returnToMenu;

    public GameObject goLogin, goMenu, goTitle, goBattlePanel, goPlayerList;

    public byte oswitcha, oswitchb;
    public byte[] ostrike;
    public byte selfHp, oppoHp;

    private List<User> userList;
    public Dropdown userListDd;

    private int selectResult;

    private string oppoName;
    private int[] oppoCard;

    public Material c01, c02, c11, c12;
    public MeshRenderer[] cardMeshes;
     


	//=================Get Info=============================
	Dropdown playerList, historyList;
    public void SetWait(bool enable)
    {
		state = NetworkEngineState.WORKING;
        enableWait = enable;
        thread.Abort();
		thread = new Thread(SendSetWait);
		thread.Start();
    }

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

    public void Select()
    {
        msg = "Waiting for result";
        state = NetworkEngineState.WORKING;
		thread = new Thread(SendSelect);
		thread.Start();
    }

    public void GetUserList()
    {
        msg = "Waiting for online user list";
        state = NetworkEngineState.WORKING;
		thread = new Thread(SendUserListReq);
		thread.Start();

    }

    public void StartBattle()
    {
        state = NetworkEngineState.WORKING;
		thread = new Thread(BattlePreparation);
		thread.Start();
    }

    void SendRequest(int header)
    {
        
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
        Send();
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
        Send();

        GetPackage();
        if (buffer[1] == 0x13)
        {
            returnToMenu = true;
            thread.Abort();
        }
        else
        {
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
    }

    void SendUserListReq()
    {
        SetBufferHead(0x91, 0x06, 0);
        GetPackage();
        CheckPackHead(0x06);
        int offset = 4;
        userList.Clear();
        while (offset - 4 < dataLength)
        {
            User user;
            user.uid = BitConverter.ToInt16(buffer, offset);
            user.uid = IPAddress.NetworkToHostOrder(user.uid);
            user.name = Encoding.ASCII.GetString(buffer, offset + 3, buffer[offset + 2]);
 //           user.name = BitConverter.ToString(buffer, offset + 3, buffer[offset + 2]);
            userList.Add(user);
            offset += buffer[offset + 2] + 3;
        }

        refreshUserList = true;

        state = NetworkEngineState.READY;

    }

    void SendSelect()
    {
        SetBufferHead(0x91, 0x08, 2);
        System.Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(userList[userListDd.value].uid)), 0, buffer, 4,  2);
        Send();
        GetPackage();
        CheckPackHead(0x08);
        selectResult = buffer[4];
        state = NetworkEngineState.READY;
    }

    void Send()
    {
        socket.Send(buffer, dataLength + 4, SocketFlags.None);
        Debug.Log("Send package:     " + BitConverter.ToString(buffer, 0, dataLength + 4));
    }

    void BattlePreparation()
    {
        msg = "Waiting for opponent's info";
        GetPackage();
        CheckPackHead(0x09);
        oppoName = Encoding.ASCII.GetString(buffer, 4, buffer[3]);
        SetBufferHead(0x91, 0x10, 9);
        buffer[4] = buffer[8] = 0;
        buffer[6] = buffer[10] = 1;
        buffer[5] = buffer[11] = 1;
        buffer[7] = buffer[9] = 2;
        buffer[12] = 20;
        Send();
        state = NetworkEngineState.WORKING;
        GetPackage();
        CheckPackHead(0x10);
        for (int i = 0; i < 4; ++i)
            oppoCard[i] = buffer[4 + i * 2] * 10 + buffer[5 + i * 2];
        oppoHp = buffer[12];
        foundOpponent = true;
       
        state = NetworkEngineState.READY;
    }

    void SendSetWait()
    {
        SetBufferHead(0x91, 0x07, 1);
        if (enableWait)
            buffer[4] = 1;
        else
            buffer[4] = 0;
        Send();
        state = NetworkEngineState.READY;
        BattlePreparation();

    }

	//======================================================

	void GetPackage()
	{
		socket.Receive(buffer, 4, SocketFlags.None);
        Debug.Log("Received header:  " + BitConverter.ToString(buffer, 0, 4));
		dataLength = BitConverter.ToInt16(buffer, 2);
		dataLength = IPAddress.NetworkToHostOrder(dataLength);
		socket.Receive(buffer, 4, dataLength, SocketFlags.None);
        Debug.Log("Received package: " + BitConverter.ToString(buffer, 0, dataLength + 4));
	}

	void CheckPackHead(Byte head)
	{
        if (buffer[0] != 0x11)
        {
			msg = "Head Err: found " + (Int32)buffer[0] + " in [0] expected 0x11";
			state = NetworkEngineState.ERR;
            thread.Abort();
        }
		if (buffer[1] != head)
		{
			msg = "Head Err: found " + (Int32)buffer[1] + " in [1] expected " + (Int32)head;
			state = NetworkEngineState.ERR;
            thread.Abort();
		}
	}


	// Use this for initialization
	void Start()
	{
        oppoCard = new int[4];
        selectResult = 2;
        userList = new List<User>();
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

        selfNameText.text = userName.text;
        enemyNameText.text = oppoName;

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

        if (refreshUserList)
        {
            refreshUserList = false;
            userListDd.ClearOptions();
            List<string> userNames = new List<string>();
            foreach (User user in userList)
                userNames.Add(user.name);
            userListDd.AddOptions(userNames);

        }
        if (selectResult == 0)
        {
            selectResult = 2;
            ShowNotification("Failed.  The opponent may be offline");
        }
        if (selectResult == 1)
        {
            selectResult = 2;
            goPlayerList.SetActive(false);
            goBattlePanel.SetActive(true);
            StartBattle();
        }

        if (foundOpponent)
        {
            for (int i = 0; i < 4; ++i)
            {
                if (oppoCard[i] == 1)
                    cardMeshes[i].material = c01;
                if (oppoCard[i] == 2)
                    cardMeshes[i].material = c02;
                if (oppoCard[i] == 11)
                    cardMeshes[i].material = c11;
                if (oppoCard[i] == 12)
                    cardMeshes[i].material = c12;
            }
            foundOpponent = false;
            goPlayerList.SetActive(false);
            goBattlePanel.SetActive(true);
        }
        if (returnToMenu)
        {
            returnToMenu = false;
            state = NetworkEngineState.READY;
            goMenu.SetActive(true);
            goBattlePanel.SetActive(false);

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
		byte len = buffer[3];
		socket.Receive(buffer, 4, len, SocketFlags.None);
		state = NetworkEngineState.READY;
		thread.Abort();
	}

	void OnDestroy()
	{
        socket.Close();
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

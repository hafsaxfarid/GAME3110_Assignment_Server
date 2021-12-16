using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    LinkedList<PlayerAccount> playerAccounts;

    // Player Accounts File Location
    static string playerAccountsFilePath;

    int playersWaitingToPlay;

    void Start()
    {
        // Player Accounts File Location
        playerAccountsFilePath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccountsData.txt";

        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccounts = new LinkedList<PlayerAccount>();

        // Loading Player Accounts
        LoadPlayerAccounts();

        playersWaitingToPlay = -1;
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
         byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if(signifier == ClientToSeverSignifiers.CreateAccount)
        {
            string n = csv[1];
            string p = csv[2];

            bool isUnique = true;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if(pa.playerName == n)
                {
                    isUnique = false;
                    break;
                }
            }

            if (isUnique) // when is unique name
            {
                playerAccounts.AddLast(new PlayerAccount(n, p));
                SendMessageToClient(SeverToClientSignifiers.LoginResponse + "," + LoginResponses.Success, id);

                // Saving Player Account
                SavePlayerAccounts();
            }
            else // when not unique name
            {
                SendMessageToClient(SeverToClientSignifiers.LoginResponse + "," + LoginResponses.FailureNameInUse, id);
            }

        }
        else if(signifier == ClientToSeverSignifiers.Login)
        {
            string n = csv[1];
            string p = csv[2];

            bool hasBeenFound = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.playerName == n)
                {
                    if (pa.playerPassword == p)
                    {
                        SendMessageToClient(SeverToClientSignifiers.LoginResponse + "," + LoginResponses.Success, id);           
                    }
                    else
                    {
                        SendMessageToClient(SeverToClientSignifiers.LoginResponse + "," + LoginResponses.IncorrectPassword, id);
                    }
                    hasBeenFound = true;
                    break;
                }
            }

            if (!hasBeenFound)
            {
                SendMessageToClient(SeverToClientSignifiers.LoginResponse + "," + LoginResponses.FailureNameNotFound, id);
            }
        }
        else if(signifier == ClientToSeverSignifiers.LookingForGameRoom)
        {
            if (playersWaitingToPlay == -1)
            {
                playersWaitingToPlay = id;
            }
            else
            {
                TicTacToeGameSession tttgs = new TicTacToeGameSession(playersWaitingToPlay, id);
                
                SendMessageToClient(SeverToClientSignifiers.TicTacToeGameStarted + ",", id);
                SendMessageToClient(SeverToClientSignifiers.TicTacToeGameStarted + ",", playersWaitingToPlay);
                
                playersWaitingToPlay = -1;
            }
        }
        else if (signifier == ClientToSeverSignifiers.PlayingTicTacToe)
        {
            Debug.Log("TO DO: Make Game Playable");
        }
    }

    private void SavePlayerAccounts()
    {
        StreamWriter sw = new StreamWriter(playerAccountsFilePath);
     
        foreach (PlayerAccount pa in playerAccounts)
        {
            sw.WriteLine(pa.playerName + "," + pa.playerPassword);
        }
        sw.Close();
    }

    private void LoadPlayerAccounts()
    {
        if(File.Exists(playerAccountsFilePath))
        {
            StreamReader sr = new StreamReader(playerAccountsFilePath);

            string line;

            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                PlayerAccount pa = new PlayerAccount(csv[0], csv[1]);
                playerAccounts.AddLast(pa);
            }
            sr.Close();
        }
    }
}

public class PlayerAccount
{
    public string playerName, playerPassword;

    public PlayerAccount(string name, string password)
    {
        playerName = name;
        playerPassword = password;
    }
}

public class TicTacToeGameSession
{
    int player1ID, player2ID;

    public TicTacToeGameSession(int player1, int player2)
    {
        player1 = player1ID;
        player2 = player2ID;
    }
}

public static class ClientToSeverSignifiers
{
    public const int Login = 1;
    public const int CreateAccount = 2;
    public const int LookingForGameRoom = 3;
    public const int PlayingTicTacToe = 4;
}

public static class SeverToClientSignifiers
{
    public const int LoginResponse = 1;  
    public const int TicTacToeGameStarted = 2;
}

public static class LoginResponses
{
    public const int Success = 1;
    public const int FailureNameInUse = 2;
    public const int FailureNameNotFound = 3;
    public const int IncorrectPassword = 4;
}
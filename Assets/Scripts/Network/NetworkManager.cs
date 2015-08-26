using UnityEngine;
using System.Collections;

public class NetworkManager : MonoBehaviour
{
	private const string typeName = "nbillingUniqueGameName";
	private const string gameName = "nbillingRoomName";
	
	private HostData[] _hostList;
	private SyncManager _syncManager;
	
	void Awake()
	{
	}
	
	void OnGUI()
	{
		if (!Network.isClient && !Network.isServer)
		{
            ServerBrowserGui();
		}
        else if (Input.GetButton("ServerInfo"))
        {
            ServerInfoGui();
        }
	}
	
	void OnMasterServerEvent(MasterServerEvent msEvent)
	{
		if (msEvent == MasterServerEvent.HostListReceived)
		{
            Debug.Log("Successfully recieved host list.");
			_hostList = MasterServer.PollHostList();
		}
        else if (msEvent == MasterServerEvent.RegistrationSucceeded)
        {
            Debug.Log("Successfully registered server on master server.");
        }
	}

    void OnFailedToConnectToMasterServer(NetworkConnectionError info)
    {
        if (Network.isServer)
        {
            Debug.Log("Start Server: Failed to connect to master server. Retrying...");
            MasterServer.RegisterHost(typeName, gameName);
        }
        else
        {
            Debug.Log("Refresh Host List: Failed to connect to master server. Retying...");
            MasterServer.RequestHostList(typeName);
        }
    }
	
	private void StartServer()
	{
		Network.InitializeServer(4, 25000, !Network.HavePublicAddress());
		MasterServer.RegisterHost(typeName, gameName);
	}
	
	private void RefreshHostList()
	{
		MasterServer.RequestHostList(typeName);
	}
	
	private void JoinServer(HostData hostData)
	{
		Network.Connect(hostData);
	}

    private void JoinServer()
    {
        Network.Connect("localhost", 25000);
    }
	
	#region Server
	void OnServerInitialized()
	{
		Debug.Log("Server Initialized");
        GameObject networkController = Network.Instantiate(ResourceDirectory.Instance.Infrastructure["NetworkController"],
                Vector3.zero,
                Quaternion.identity,
                0
            ) as GameObject;
        _syncManager = networkController.GetComponent<SyncManager>();
	}
	
	void OnPlayerConnected(NetworkPlayer networkPlayer)
	{
		_syncManager.SpawnRemotePlayer(networkPlayer.guid);
	}

    void OnPlayerDisconnected(NetworkPlayer networkPlayer)
    {
        _syncManager.DeleteRemotePlayer(networkPlayer.guid);
    }
	#endregion
	
	#region Client
	void OnConnectedToServer()
	{
		Debug.Log("Server Joined");
	}
	#endregion

    #region GUI
    private void ServerBrowserGui()
    {
        if (GUI.Button(new Rect(100, 100, 250, 100), "Start Server"))
        {
            StartServer();
        }
        
        if (GUI.Button(new Rect(100, 250, 250, 100), "Refresh Host List"))
        {
            RefreshHostList();
        }
        
        if (_hostList != null)
        {
            for (int i = 0; i < _hostList.Length; i++)
            {
                if (GUI.Button(new Rect(400, 100 + (110 * i), 300, 100), _hostList[i].gameName))
                {
                    JoinServer(_hostList[i]);
                }
            }
        }
        
        if (GUI.Button(new Rect(100, 400, 250, 100), "Connect to hardcoded IP"))
        {
            JoinServer();
        }
    }

    private void ServerInfoGui()
    {
        if (_syncManager == null)
        {
            _syncManager = GameObject.FindGameObjectWithTag(Tags.networkController).GetComponent<SyncManager>();
        }

        GUI.Label(new Rect(20, 40, 200, 20), string.Format("Latency(ms): {0}.", _syncManager.averageOneWayTripTime * 2 * 1000));
        //GUI.Window(0, new Rect(200, 200, 400, 200), ServerInfoWindow, "Server Info");
    }

    private void ServerInfoWindow(int id)
    {
        // Assume latency is symmetrical
        GUI.Label(new Rect(20, 40, 200, 20), string.Format("Latency(ms): {0}.", _syncManager.averageOneWayTripTime * 2 * 1000));
    }
    #endregion
}

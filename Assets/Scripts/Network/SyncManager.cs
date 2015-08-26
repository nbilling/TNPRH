using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// This class has a dual purpose:
/// - On the SERVER it:
///   + Handles the queue of CLIENT inputs.
///   + Maintains the authoritative state of the game.
///   + Periodically updates CLIENTS of state change.
/// - On the CLIENT it:
///   + Applies SERVER messages (deltas) to own view of state.
///   + Sends user input updates to SERVER.
/// </summary>
using System;

public class RollbackContext : IDisposable
{
    private SyncManager _syncManager;

    public RollbackContext(SyncManager syncManager, double time)
    {
        if (syncManager == null)
        {
            throw new ArgumentNullException("syncManager");
        }

        if (time < 0d)
        {
            throw new ArgumentException("time");
        }

        _syncManager = syncManager;

        _syncManager.RollbackStateToNetworkTime(time);
    }

    public void Dispose()
    {
        _syncManager.UnrollbackState();
    }
}

public class SyncManager : MonoBehaviour
{
	public float clientServerDelay = 0.1f; // 100ms
	public float snapshotTtl = 0.5f; // 500ms
    public float renderDelay = 0.1f; // 100ms
	
	private LinkedList<Snapshot> _snapshots;
	
	// TODO: A priority queue would be much fairer, especially if
	//       broadcast time increases relative to tick time.
	private Queue<KeyValuePair<string, Command>> _messageQueue;
	public IDictionary<string, Player> _playerLookup;
    public ICollection<Shot> _shots;
    private Snapshot _currentState;

    // Server diagnostics
    public double averageOneWayTripTime = 0d;
    private int _pingCount = 0;
    private StreamWriter _logStreamWriter;
    private string _logFilePath = @"C:\Users\nbilling\serverLogFile.txt";
    private bool _isLogging = false;
	
	void Awake()
	{
		_snapshots = new LinkedList<Snapshot>();
		_messageQueue = new Queue<KeyValuePair<string, Command>>();
		_playerLookup = new Dictionary<string, Player>();
        _shots = new List<Shot>();

        if (Network.isServer)
        {
            _logStreamWriter = new StreamWriter(_logFilePath, false);
        }
    }
	
	void Update()
	{
		if (Network.isServer)
		{
			// Handle command queue
            while (_messageQueue.Count > 0)
            {
                HandleMessage(_messageQueue.Dequeue());
			}

            // Detect melee weapon collisions
            foreach (var player in _playerLookup.Values)
            {
                player.DetectMeleeWeaponCollision();
            }
		}
		else if (Network.isClient)
		{
            float renderTime = (float)Network.time - renderDelay;
            Snapshot start, end; // The first snapshot before current render time, and first snapshot after current render time
            FindSnapshots(renderTime, out start, out end);

            if (start != null)
            {
                if (end != null)
                {
                    UpdateRemotePlayers(start, end, renderTime);
                }
                else
                {
                    // TODO: Extrapolate in this case?
                    Debug.LogWarning(string.Format("No end snapshot for renderTime {0}.", renderTime));
                }

                UpdateLocalPlayer(start, renderTime);
            }
            else
            {
                Debug.LogWarning(string.Format("No start snapshot for renderTime {0}.", renderTime));
            }
		}
        else
        {
            throw new UnityException("SyncManager should not be instantiated without connection.");
        }
	}

	void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
	{
		if (stream.isWriting && Network.isServer)
		{
			// Generate, record, and broadcast delta to clients 
			var players = GenerateDelta();
            var shots = _shots.ToArray();
            _shots.Clear();

            // Save copies of player delta and shots in snapshot
            Snapshot newSnapshot = new Snapshot(Network.time, players, shots);
			RecordSnapshot(newSnapshot);
            LogSnapshotSent(newSnapshot.TimeStamp);

            stream.Serialize(players);
            stream.Serialize(shots);
		}
		else if (stream.isReading && Network.isClient)
		{
            LogPing(info);

			// Get and record new snapshot
			IDictionary<string, PlayerSnapshot> players = new Dictionary<string, PlayerSnapshot>();
			stream.Serialize(players);
            ICollection<Shot> shots = new List<Shot>();
            stream.Serialize(shots);
			Snapshot newSnapshot = new Snapshot(info.timestamp, players, shots);
			RecordSnapshot(newSnapshot);
			
			// Create any new players in this snapshot
			CreateNewPlayers();

            // Create any new shot particle emitters in this snapshot
            CreateNewShots();
		}
		else
		{
			throw new UnityException("Invalid state in OnSerializeNetworkView.");
		}
	}
	
	private void CreateNewPlayers()
	{
		// Find guids of new players in most recent snapshot
		IEnumerable<string> newPlayerGuids = _snapshots.First().Players.Keys.Where(guid => !_playerLookup.ContainsKey(guid));
        IEnumerable<string> stalePlayerGuids = _playerLookup.Keys.Where(guid => !_snapshots.First().Players.ContainsKey(guid));
		
		foreach (var guid in newPlayerGuids)
		{
            if (guid == Network.player.guid)
            {
                SpawnLocalPlayer();
            }
            else
            {
                SpawnRemotePlayer(guid);
            }
		}

        foreach (var guid in stalePlayerGuids)
        {
            GameObject.Destroy(_playerLookup[guid]);
            _playerLookup.Remove(guid);
        }
	}
	
    private void SpawnLocalPlayer()
    {
        SpawnPlayer<LocalPlayer>(Network.player.guid);
    }

    public void SpawnRemotePlayer(string guid)
    {
        SpawnPlayer<RemotePlayer>(guid);
    }

    private void SpawnPlayer<T>(string guid) where T : Player
    {
        // Create player instance with owner guid
        GameObject newPlayerObject = GameObject.Instantiate(ResourceDirectory.Instance.Creatures["Player"]) as GameObject;
        newPlayerObject.name += guid;
        Player newPlayerScript;
        newPlayerScript = newPlayerObject.AddComponent<T>();
        newPlayerScript.networkGuid = guid;
        
        // Add to dictionary
        _playerLookup.Add(newPlayerScript.networkGuid, newPlayerScript);
    }

    public void DeleteRemotePlayer(string guid)
    {
        Player player;
        if (_playerLookup.TryGetValue(guid, out player))
        {
            GameObject.Destroy(player.gameObject);
            _playerLookup.Remove(guid);
        }
    }

    private void CreateNewShots()
    {
        IEnumerable<Shot> shots = _snapshots.First().Shots;

        // Render other players' shots
        foreach (var shot in shots.Where(s => s.playerGuid != Network.player.guid))
        {
            RenderShot(shot.destination, shot.source);
        }
    }

    // This renders a shot in the local game view
    public void RenderShot(Vector3 destination, Vector3 source)
    {
        GameObject emitter = GameObject.Instantiate(ResourceDirectory.Instance.Effects["DustFly"]) as GameObject;
        emitter.transform.position = destination;
        emitter.transform.LookAt(source);
    }

    // This records a shot on the server state so that it can be sent out in next snapshot
    public void RecordShot(string playerGuid, Vector3 destination, Vector3 source)
    {
        _shots.Add(new Shot(playerGuid, destination, source));
    }

    #region RPC
	public void BroadcastPlayerInput(IEnumerable<Command> commands)
	{
		GetComponent<NetworkView>().RPC("RpcTargetBroadcastPlayerInput", RPCMode.Server, CommandPacker.Pack(commands));
	}
	
	[RPC]
	void RpcTargetBroadcastPlayerInput(int[] data, NetworkMessageInfo info)
	{
        LogPing(info);
        
        // Get new commands
		IEnumerable<Command> commands = CommandPacker.Unpack(data);
		
		// Add commands to queue
		foreach (var command in commands)
		{
			_messageQueue.Enqueue(new KeyValuePair<string, Command>(info.sender.guid, command));
		}
	}

    public void BroadcastRespawn()
    {
        GetComponent<NetworkView>().RPC("RpcTargetBroadcastRespawn", RPCMode.Server);
    }

    [RPC]
    void RpcTargetBroadcastRespawn(NetworkMessageInfo info)
    {
        string respawningPlayerGuid = info.sender.guid;

        GameObject.Destroy(_playerLookup[respawningPlayerGuid].gameObject);
        _playerLookup.Remove(respawningPlayerGuid);

        SpawnRemotePlayer(respawningPlayerGuid);
    }
    #endregion
	
	private IDictionary<string, PlayerSnapshot> GenerateDelta()
	{
		return _playerLookup.ToDictionary<KeyValuePair<string, Player>, string, PlayerSnapshot>(
			p => p.Key,
			p => new PlayerSnapshot(p.Value as RemotePlayer)
			);
	}
	
	private void RecordSnapshot(Snapshot snapshot)
	{
		// Remove any stale snapshots
		while (_snapshots.Count > 0
		       && Network.time - _snapshots.Last.Value.TimeStamp > snapshotTtl)
		{
			_snapshots.RemoveLast();
		}
		
		// Add new snapshot
		_snapshots.AddFirst(snapshot);
	}

    private void LogPing(NetworkMessageInfo info)
    {
        // Every 1000 ping checks, take the current average as the first poll and restart
        if (_pingCount >= 1000)
        {
            _pingCount = 1;
        }

        averageOneWayTripTime = ((Network.time - info.timestamp) + (_pingCount * averageOneWayTripTime)) / (_pingCount + 1);
        ++_pingCount;
    }

    // Finds the snapshots immediately before and after renderTime.
    private void FindSnapshots(float renderTime, out Snapshot start, out Snapshot end)
    {
        start = null;
        end = null;

        foreach (var snapshot in _snapshots)
        {
            if (snapshot.TimeStamp < renderTime)
            {
                start = snapshot;
                break;
            }
            
            end = snapshot;
        }
    }

    private void HandleMessage(KeyValuePair<string, Command> message)
    {
        RemotePlayer player = _playerLookup[message.Key] as RemotePlayer;

        if (message.Value.timestamp > player.lastAppliedCommandTime)
        {
            // Simulate input
            player.HandleInput(message.Value);
            
            LogHandledMessage(player);
        }
    }

    private void UpdateRemotePlayers(Snapshot start, Snapshot end, float renderTime)
    {
        if (start != null && end != null)
        {
            foreach (var player in _playerLookup.Values.OfType<RemotePlayer>())
            {
                player.InterpolateMovement(start, end, renderTime);
            }
        }
        else if (start != null)
        {
            foreach (var player in _playerLookup.Values.OfType<RemotePlayer>())
            {
                Debug.LogWarning(string.Format("No end snapshot for remote {0}.", player.name));
                //player.ExtrapolateMovement(start, renderTime);
            }
        }
        else
        {
            Debug.LogWarning("No start or end snapshot for remote player.");
            return;
        }
    }

    private void UpdateLocalPlayer(Snapshot start, float renderTime)
    {
        if (start != null)
        {
            (_playerLookup.Values.First(p => p is LocalPlayer) as LocalPlayer).PredictMovement(start, renderTime);
        }
    }

    private void LogHandledMessage(Player player)
    {
        if (_isLogging)
        {
            _logStreamWriter.WriteLine("{0:0.000}: Applied {1:0.000} => {2}", Network.time,
                                       player.lastAppliedCommandTime, player);
        }
    }

    private void LogSnapshotSent(double snapshotTime)
    {
        if (_isLogging)
        {
            _logStreamWriter.WriteLine("{0:0.000}: Snapshot sent={1:0.000}", Network.time, snapshotTime);
        }
    }

    public void RollbackStateToNetworkTime(double time)
    {
        // Ensure that we aren't already rolled back
        if (_currentState != null)
        {
            throw new Exception("Already in rolled back state.");
        }

        // Save current state to restore afterwards
        _currentState = new Snapshot(Network.time, GenerateDelta(), _shots);

        // Find start and end snapshots for interpolation
        Snapshot start, end;
        FindSnapshots((float)time, out start, out end);

        if (start != null)
        {
            if (end == null)
            {
                // Not going back past last saved snapshot so use current state for end.
                end = _currentState; 
            }

            // Apply state rollback
            UpdateRemotePlayers(start, end, (float)time);
        }
        else
        {
            // Failed to rollback.
            Debug.LogWarning(string.Format("Failed to rollback to time {0}.", time));
        }
    }

    public void UnrollbackState()
    {
        if (_currentState == null)
        {
            // Not rolled back, nothing to do
            return;
        }

        // Revert all players to current state
        Player player;

        foreach (KeyValuePair<string, PlayerSnapshot> item in _currentState.Players)
        {
            if (_playerLookup.TryGetValue(item.Key, out player))
            {
                player.SyncToSnapshot(item.Value);
            }
        }

        // Forget cached current state
        _currentState = null;
    }
}
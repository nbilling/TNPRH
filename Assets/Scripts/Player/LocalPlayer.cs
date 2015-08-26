using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class LocalPlayer : Player
{
    #region Configuration
    public float gameStep = 0.033f;
    #endregion

    private float _timeSinceLastInputBroadcast = 0f;
    private ICollection<Command> _unsentCommandBuffer;
    private Queue<Command> _unacknowledgedCommandBuffer;
    private GameObject _camera;
    private double _currentSnapshotTime;
    private Snapshot _oldSnapshot;
    private StreamWriter _logStreamWriter;
    private string _logFilePath = @"C:\Users\nbilling\clientLogFile.txt";
    private bool _isLogging = false;

    protected override void Awake()
    {
        base.Awake();

        _unsentCommandBuffer = new List<Command>();
        _unacknowledgedCommandBuffer = new Queue<Command>();

        _camera = GameObject.FindGameObjectWithTag(Tags.mainCamera);

        if (_isLogging)
        {
            _logStreamWriter = new StreamWriter(_logFilePath, false);
        }
    }

    void Update()
    {
		// Get player input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool sneak = false; //Input.GetButton("Sneak");
		float mouseX = Input.GetAxis("Mouse X");
		float mouseY = Input.GetAxis("Mouse Y");
        bool mouse0 = Input.GetMouseButton(0);
        bool mouse0Down = Input.GetMouseButtonDown(0);
        bool mouse1 = Input.GetMouseButton(1);
        bool equip1 = Input.GetButtonDown("Equip1");
        bool equip2 = Input.GetButtonDown("Equip2");

		// Buffer command, then send to server if necessary
        Command newCommand = new Command(Network.time, h, v, sneak, mouseX, mouseY, mouse0, mouse0Down, mouse1, equip1, equip2);
        SyncInput(newCommand);

        // Apply command to local player immediately
        ApplyCommand(newCommand);
        LogCommandApplication("Applied", newCommand.timestamp);
    }

    private void SyncInput(Command newCommand)
    {
        _unsentCommandBuffer.Add(newCommand);

        _timeSinceLastInputBroadcast += Time.deltaTime;

        if (_timeSinceLastInputBroadcast > gameStep)
        {
            // Send unsent commands to server
            _syncManager.BroadcastPlayerInput(_unsentCommandBuffer);

            // Move commands from unsent to unacknowledged buffer
            foreach (var command in _unsentCommandBuffer)
            {
                _unacknowledgedCommandBuffer.Enqueue(command);
            }
            _unsentCommandBuffer.Clear();

            // Reset broadcast timer
            _timeSinceLastInputBroadcast = 0f;
        }
    }

    public void PredictMovement(Snapshot start, float renderTime)
    {
        // If new snapshot
        if (start.TimeStamp != _currentSnapshotTime)
        {
            LogSyncToSnapshot("New snapshot", start.TimeStamp, start.Players[networkGuid]);

            _currentSnapshotTime = start.TimeStamp;
            List<Command> acknowledgedCommands = new List<Command>();

            // Remove acknowledged commands from buffer
            double lastCommandAcknowledgedInSnapshot = start.Players[networkGuid].lastAppliedCommandTime;
            while (_unacknowledgedCommandBuffer.Count > 0 &&
                   _unacknowledgedCommandBuffer.Peek().timestamp <= lastCommandAcknowledgedInSnapshot)
            {
                Command acknowledgedCommand = _unacknowledgedCommandBuffer.Dequeue();
                acknowledgedCommands.Add(acknowledgedCommand);
            }

            LogStuff(start, acknowledgedCommands);

            // Sync to new snapshot
            SyncToSnapshot(start.Players[networkGuid]);

            // Replay unacknowledged commands
            foreach (var command in _unacknowledgedCommandBuffer)
            {
                ApplyCommand(command);
                LogCommandApplication("Replayed(unack)", command.timestamp);
            }

            // Replay unsent commands
            foreach (var command in _unsentCommandBuffer)
            {
                ApplyCommand(command);
                LogCommandApplication("Replayed(unsent)", command.timestamp);
            }
        }
    }

    private void ApplyCommand(Command command)
    {
        HandleInput(command);
        
        // Sync main camera to mouse orbiter
        _camera.transform.position = MouseOrbit.transform.position;
        _camera.transform.rotation = MouseOrbit.transform.rotation;
    }

    private void LogStuff(Snapshot newSnapshot, List<Command> acknowledgedCommands)
    {
        if (_isLogging)
        {
            if (_oldSnapshot != null)
            {
                PlayerSnapshot newPlayerSnapshot = newSnapshot.Players[networkGuid];
                PlayerSnapshot oldPlayerSnapshot = _oldSnapshot.Players[networkGuid];

                // Sync to old snapshot
                SyncToSnapshot(oldPlayerSnapshot);
                LogSyncToSnapshot("Old snapshot", _oldSnapshot.TimeStamp, oldPlayerSnapshot);

                // Replay acknowledged commands
                foreach (var command in acknowledgedCommands)
                {
                    ApplyCommand(command);
                    LogCommandApplication("Acknowledged", command.timestamp);
                }

                Vector3 positionDelta = newPlayerSnapshot.mouseOrbitPosition - MouseOrbit.transform.position;
                float xDelta = newPlayerSnapshot.mouseOrbitX - MouseOrbit.x;
                float yDelta = newPlayerSnapshot.mouseOrbitY - MouseOrbit.y;
                if (positionDelta == Vector3.zero && newPlayerSnapshot.mouseOrbitrotation == MouseOrbit.transform.rotation
                    && xDelta == 0 && yDelta == 0)
                {
                    _logStreamWriter.WriteLine("{0:0.000}: Camera fully synced.", Network.time);
                }
                else
                {
                    _logStreamWriter.WriteLine("{5:0.000}: Pos delta: {0}. Rotations: {1} vs {2}. xDelta: {3}. yDelta: {4}.", positionDelta,
                                               MouseOrbit.transform.rotation, newPlayerSnapshot.mouseOrbitrotation, xDelta, yDelta,
                                            Network.time);
                }
            }

            _oldSnapshot = newSnapshot;
        }
    }

    private void LogCommandApplication(string context, double commandTime)
    {
        if (_isLogging)
        {
            _logStreamWriter.WriteLine("{0:0.000}: {1} {2:0.000} => {3}", Network.time, context,
                                       commandTime, this);
        }
    }

    private void LogSyncToSnapshot(string context, double snapshotTime, PlayerSnapshot playerSnapshot)
    {
        if (_isLogging)
        {
            _logStreamWriter.WriteLine("{0:0.000}: {1}=[time={2:0.000}, lastack={3:0.000}], {4}]",
                                       Network.time, context, snapshotTime, playerSnapshot.lastAppliedCommandTime,
                                       playerSnapshot);
        }
    }
}
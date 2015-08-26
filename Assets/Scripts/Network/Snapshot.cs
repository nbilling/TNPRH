using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerSnapshot
{
    public Vector3 position;
    public float xMovement;
    public float zMovement;
    public Quaternion rotation;
    public bool isSneaking;
    public Vector3 mouseOrbitPosition;
    public Quaternion mouseOrbitrotation;
    public float mouseOrbitX;
    public float mouseOrbitY;
    public string equippedWeapon;
    public bool isWeaponRaised;
    public bool isWeaponBlocking;
    public double lastAppliedCommandTime;
    public int hp;
    public bool isDead;
    public bool hasSwingStruck;

    public PlayerSnapshot()
    {
    }

    public PlayerSnapshot(RemotePlayer player)
    {
        position = player.transform.position;
        xMovement = player.XMovement;
        zMovement = player.ZMovement;
        rotation = player.transform.rotation;
        mouseOrbitPosition = player.MouseOrbit.transform.position;
        mouseOrbitrotation = player.MouseOrbit.transform.rotation;
        mouseOrbitX = player.MouseOrbit.x;
        mouseOrbitY = player.MouseOrbit.y;
        equippedWeapon = player.EquippedWeaponInfo.weaponName;
        isWeaponRaised = player.IsWeaponRaised;
        isWeaponBlocking = player.IsWeaponBlocking;
        lastAppliedCommandTime  = player.lastAppliedCommandTime;
        hp = player.hp;
        isDead = player.isDead;
        hasSwingStruck = player.hasSwingStruck;
    }

    public override string ToString()
    {
        return string.Format("playerdata=[pos={0}, rot={1}, mousepos={2}, mouserot={3}, mousex={4}, mousey={5}], hp={6}",
                             position, rotation, mouseOrbitPosition,
                             mouseOrbitrotation, mouseOrbitX, mouseOrbitY, hp);
    }
}

public class Shot
{
    public Vector3 destination;
    public Vector3 source;
    public string playerGuid;

    public Shot()
    {
    }

    public Shot(string playerGuid, Vector3 destination, Vector3 source)
    {
        this.playerGuid = playerGuid;
        this.destination = destination;
        this.source = source;
    }
}

public class Snapshot
{
    private readonly double _timeStamp;
    public double TimeStamp { get { return _timeStamp; } }

    private readonly IDictionary<string, PlayerSnapshot> _players;
    public IDictionary<string, PlayerSnapshot> Players { get { return _players; } }

    public IEnumerable<Shot> Shots { get; private set; }

    public Snapshot(double timeStamp, IDictionary<string, PlayerSnapshot> players, IEnumerable<Shot> shots)
    {
        _timeStamp = timeStamp;
        _players = players;
        Shots = shots;
    }
}

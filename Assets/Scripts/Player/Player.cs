using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public enum EquippedWeaponType
{
    None             = 0,
    OneHandMelee     = 1,
    OneHandPistol    = 2
}

public abstract class Player : MonoBehaviour
{
    public float renderDelay = 0.1f; // 100ms

    #region Configuration
    public float turnSmoothing = 15f;
    public float moveSmoothing = 10f;
    public float speedDampTime = 0.1f;
    public float runSpeed = 4f;
    public float smoothInterpolationTime = 0.05f;
    #endregion
    
    public string networkGuid;
    public int hp = 100;
    public bool isDead = false;
    // When swinging weapon AND on server: this indicates whether or not we have struck a
    // target yet, otherwise: value is meaningless.
    public bool hasSwingStruck;
    public float XMovement
    {
        get
        {
            return _anim.GetFloat(_hash.xMovementFloat);
        }

        set
        {
            _anim.SetFloat(_hash.xMovementFloat, value);
        }
    }
    public float ZMovement
    {
        get
        {
            return _anim.GetFloat(_hash.zMovementFloat);
        }
        
        set
        {
            // TODO: Do we really still need the damp?
            _anim.SetFloat(_hash.zMovementFloat, value);
        }
    }
    public bool IsWeaponRaised
    {
        get
        {
            return _anim.GetBool(_hash.isWeaponRaisedBool);
        }
        
        set
        {
            _anim.SetBool(_hash.isWeaponRaisedBool, value);
        }
    }
    public bool IsWeaponStriking
    {
        get
        {
            return !hasSwingStruck && 
                _anim.GetCurrentAnimatorStateInfo(_hash.oneHandMeleeLayer).IsName("1HandSwordSwingStrike");
            //return !hasSwingStruck &&
            //    _anim.GetCurrentAnimatorStateInfo(_hash.oneHandMeleeLayer).tagHash == _hash.oneHandMeleeStrikeState;
        }
    }
    public bool IsWeaponBlocking
    {
        get
        {
            return _anim.GetBool(_hash.isWeaponBlockingBool);
        }
        
        set
        {
            _anim.SetBool(_hash.isWeaponBlockingBool, value);
        }
    }
    public WeaponInfo EquippedWeaponInfo
    {
        get
        {
            return EquippedWeapon.GetComponent<WeaponInfo>();
        }
    }
    public float Pitch
    {
        get
        {
            return _anim.GetFloat(_hash.pitchFloat);
        }

        set
        {
            _anim.SetFloat(_hash.pitchFloat, value);
        }
    }

    private GameObject _equippedWeapon;
    public GameObject EquippedWeapon
    {
        get
        {
            return _equippedWeapon;
        }
    }

    public Vector3 PointOfAim
    {
        get;
        private set;
    }
    
    public MouseOrbit MouseOrbit
    {
        get;
        private set;
    }

    public double lastAppliedCommandTime = 0f;
    
    protected Animator _anim;
    protected HashIDs _hash;
    protected SyncManager _syncManager;
    protected ModelInfo _modelInfo;
    protected IkLimb _rArmIkLimb;
    protected GameObject _rArmIkHandTarget;
    protected GameObject _rArmIkElbowTarget;
    protected IkLimb _lArmIkLimb;
    protected GameObject _lArmIkHandTarget;
    protected GameObject _lArmIkElbowTarget;
    protected GameObject _rangedTarget;
    public float angle;
    
    protected Vector3 _positionInterpolationSmoothVelocity = Vector3.zero;

    public override string ToString()
    {
        return string.Format("playerdata=[pos={0}, rot={1}, mousepos={2}, mouserot={3}, mousex={4}, mousey={5}]",
                             transform.position, transform.rotation, MouseOrbit.transform.position,
                             MouseOrbit.transform.rotation, MouseOrbit.x, MouseOrbit.y);
    }

    protected virtual void Awake()
    {
        _anim = GetComponent<Animator>();

        _hash = GameObject.FindGameObjectWithTag(Tags.gameController).GetComponent<HashIDs>();
        _syncManager = GameObject.FindGameObjectWithTag(Tags.networkController).GetComponent<SyncManager>();
        _modelInfo = GetComponent<ModelInfo>();

        // Create a marker object for player's ranged target.
        _rangedTarget = InstantiateAsChild(ResourceDirectory.Instance.Creatures["redMarker"], transform) as GameObject;

        // Create IK controller for right arm.
        GameObject rArmIkController = GameObject.Instantiate(Resources.Load("Prefabs/Animation/LimbIkController")) as GameObject;
        rArmIkController.transform.parent = gameObject.transform;
        _rArmIkLimb = rArmIkController.GetComponent<IkLimb>();
        _rArmIkLimb.upperArm = _modelInfo.rArm.transform;
        _rArmIkLimb.forearm = _modelInfo.rForearm.transform;
        _rArmIkLimb.hand = _modelInfo.rHand.transform;
        _rArmIkLimb.elbowTarget = _modelInfo.rArmPistolIkElbowTarget;
        _rArmIkLimb.target = _modelInfo.rArmPistolIkHandTarget.transform;
        _rArmIkLimb.IsEnabled = false;
        _rArmIkLimb.debug = true;
        _rArmIkLimb.transition = 0.65f;
        _rArmIkLimb.handRotationPolicy = IkLimb.HandRotations.UseTargetRotation;

        // Create IK controller for left arm.
        GameObject lArmIkController = GameObject.Instantiate(Resources.Load("Prefabs/Animation/LimbIkController")) as GameObject;
        lArmIkController.transform.parent = gameObject.transform;
        _lArmIkLimb = lArmIkController.GetComponent<IkLimb>();
        _lArmIkLimb.upperArm = _modelInfo.lArm.transform;
        _lArmIkLimb.forearm = _modelInfo.lForearm.transform;
        _lArmIkLimb.hand = _modelInfo.lHand.transform;
        _lArmIkLimb.elbowTarget = _modelInfo.lArmPistolIkElbowTarget;
        _lArmIkLimb.target = _modelInfo.lArmPistolIkHandTarget;
        _lArmIkLimb.IsEnabled = false;
        _lArmIkLimb.debug = true;
        _lArmIkLimb.transition = 0.9f;

        // Add DamageLocation scripts to skeleton.
        DamageLocation headDamageLocation = _modelInfo.head.AddComponent<DamageLocation>();
        headDamageLocation.damageLocationType = DamageLocationType.Head;
        headDamageLocation.player = this.gameObject;
        DamageLocation torsoDamageLocation = _modelInfo.torso.AddComponent<DamageLocation>();
        torsoDamageLocation.damageLocationType = DamageLocationType.Torso;
        torsoDamageLocation.player = this.gameObject;
        DamageLocation legsDamageLocation = _modelInfo.legs.AddComponent<DamageLocation>();
        legsDamageLocation.damageLocationType = DamageLocationType.Legs;
        legsDamageLocation.player = this.gameObject;


        // Create initial NoWeapon object.
        EquipWeaponByName("NoWeapon");

        // Create camera orbiter object.
        MouseOrbit = (GameObject.Instantiate(ResourceDirectory.Instance.Camera["Orbiter"]) as GameObject).GetComponent<MouseOrbit>();
        MouseOrbit.transform.parent = transform;
        MouseOrbit.target = transform;
    }
    
    void Update()
    {
        if (Network.isServer)
        {
            ProcessStatusEffects();
        }
    }

    public void InterpolateMovement(Snapshot start, Snapshot end, float currentRenderingTime)
    {
        PlayerSnapshot startPlayerSnapshot, endPlayerSnapshot;
        if (!start.Players.TryGetValue(networkGuid, out startPlayerSnapshot)
            || !end.Players.TryGetValue(networkGuid, out endPlayerSnapshot))
        {
            // This player can't be found in one or both snapshots, can't interpolate.
            Debug.LogWarning("Player not found in one or both snapshots, interpolation failed.");
            return;
        }

        float t = (float)((currentRenderingTime - start.TimeStamp) / (end.TimeStamp - start.TimeStamp));

        Vector3 targetPosition = Vector3.Lerp(startPlayerSnapshot.position, endPlayerSnapshot.position, t);
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _positionInterpolationSmoothVelocity, smoothInterpolationTime);
        transform.rotation = Quaternion.Lerp(startPlayerSnapshot.rotation, endPlayerSnapshot.rotation, t);

        XMovement = Mathf.Lerp(startPlayerSnapshot.xMovement, endPlayerSnapshot.xMovement, t);
        ZMovement = Mathf.Lerp(startPlayerSnapshot.zMovement, endPlayerSnapshot.zMovement, t);

        MouseOrbit.transform.position = Vector3.Lerp(startPlayerSnapshot.mouseOrbitPosition, endPlayerSnapshot.mouseOrbitPosition, t);
        MouseOrbit.transform.rotation = Quaternion.Lerp(startPlayerSnapshot.mouseOrbitrotation, endPlayerSnapshot.mouseOrbitrotation, t);

        EquipWeaponByName(startPlayerSnapshot.equippedWeapon);

        IsWeaponRaised = startPlayerSnapshot.isWeaponRaised;
        IsWeaponBlocking = startPlayerSnapshot.isWeaponBlocking;

        UpdatePointOfAim();

        hp = startPlayerSnapshot.hp;
        isDead = startPlayerSnapshot.isDead;
        hasSwingStruck = startPlayerSnapshot.hasSwingStruck;
    }

    public void SyncToSnapshot(PlayerSnapshot playerSnapshot)
    {
        transform.position = playerSnapshot.position;
        transform.rotation = playerSnapshot.rotation;
        
        MouseOrbit.transform.position = playerSnapshot.mouseOrbitPosition;
        MouseOrbit.transform.rotation = playerSnapshot.mouseOrbitrotation;
        MouseOrbit.x = playerSnapshot.mouseOrbitX;
        MouseOrbit.y = playerSnapshot.mouseOrbitY;
        
        XMovement = playerSnapshot.xMovement;
        ZMovement = playerSnapshot.zMovement;
        
        EquipWeaponByName(playerSnapshot.equippedWeapon);
        IsWeaponRaised = playerSnapshot.isWeaponRaised;
        IsWeaponBlocking = playerSnapshot.isWeaponBlocking;
        
        UpdatePointOfAim();
        
        lastAppliedCommandTime = playerSnapshot.lastAppliedCommandTime;
        
        hp = playerSnapshot.hp;
        isDead = playerSnapshot.isDead;
        
        hasSwingStruck = playerSnapshot.hasSwingStruck;
    }
    
    /// <summary>
    /// Apply player input to simulation (as in on server or in input prediction at client on local player).
    /// </summary>
    public virtual void HandleInput(Command command)
    {
        double step = command.timestamp - lastAppliedCommandTime;
        
        WeaponStateManagement(command.equip1, command.equip2);
        
        double playerRenderTimeAtCommand = command.timestamp - renderDelay;
        AttackManagement(playerRenderTimeAtCommand, command.mouse0, command.mouse0Down, command.mouse1);
        
        // Rotate player camera/view
        MouseOrbit.HandleInput(command.mouseX, command.mouseY);
        
        // Move player rigidbody
        MovementManagement(step, command.h, command.v, command.sneak);
        
        UpdatePointOfAim();
        
        lastAppliedCommandTime = command.timestamp;
    }
    
    public void DetectMeleeWeaponCollision()
    {
        if (IsWeaponStriking)
        {
            Collider bodyPartStruckCollider;
            
            // We have some notion of how far through the swing the player is, call it X%, but we want
            // to find the state of the world that the player was in when they saw themselves X% through
            // their swing. To do this we use an offset between the time when the player initiated their
            // swing from their perspective and when the server actually ran their swing command.
            // NOTE: This is not taking render delay into account since we are rolling the player back as
            //       well as everyone else. If we want to be as precise as possible we need to roll everyone
            //       else back with render delay and the player without render delay (since the player sees
            //       everyone else with render delay).
            using (new RollbackContext(_syncManager, Network.time - (this as RemotePlayer).swingStartTimeOffset))
            {
                // First body part that is in range AND does not belong to this player AND intersects weapon
                bodyPartStruckCollider = Physics
                    .OverlapSphere(EquippedWeapon.transform.position, 2f)
                        .FirstOrDefault(c =>
                                        c.gameObject.tag == Tags.bodyPart 
                                        && c.gameObject.GetComponent<DamageLocation>()
                                        .player.GetComponent<Player>().networkGuid != networkGuid
                                        && EquippedWeapon.GetComponent<Collider>().bounds.Intersects(c.bounds));
            }
            
            // If another player was struck
            if (bodyPartStruckCollider != null)
            {
                DamageLocationScaled(bodyPartStruckCollider.gameObject.GetComponent<DamageLocation>(), 30);
                
                // Set _hasSwingStruck so that this swing won't hit again in future updates
                hasSwingStruck = true;
            }
        }
    }

    private void EquipWeaponByName(string weaponName)
    {
        // If some weapon already equipped.
        if (_equippedWeapon != null)
        {
            if (weaponName == EquippedWeaponInfo.weaponName)
            {
                // Weapon unchanged.
                return;
            }

            GameObject.Destroy(_equippedWeapon);
            _equippedWeapon.transform.parent = null;
        }

        // Create new weapon object.
        _equippedWeapon = InstantiateAsChild(ResourceDirectory.Instance.Weapons[weaponName], _modelInfo.rHand.transform);

        EquippedWeaponType equippedWeaponType = _equippedWeapon.GetComponent<WeaponInfo>().type;
        
        // Update animation controller with new equipped weapon type.
        _anim.SetInteger(_hash.equippedWeaponTypeInt, (int)equippedWeaponType);
        
        // Turn IK on/off for weapon type.
        if (equippedWeaponType == EquippedWeaponType.OneHandPistol)
        {
            _rArmIkLimb.IsEnabled = true;
            _lArmIkLimb.IsEnabled = true;
        }
        else
        {
            _rArmIkLimb.IsEnabled = false;
            _lArmIkLimb.IsEnabled = false;
        }
    }

    private void UpdatePointOfAim()
    {
        // Only have a point of aim when using a projectile weapon.
        if (EquippedWeaponInfo.type == EquippedWeaponType.OneHandPistol)
        {
            _rangedTarget.transform.position = FindPointOfAim();
            Transform muzzle = EquippedWeapon.GetComponent<WeaponInfo>().muzzle.transform;
            Debug.DrawLine(muzzle.position, _rangedTarget.transform.position);
            Vector3 direction = _rangedTarget.transform.position - muzzle.position;
            angle = Vector3.Angle(muzzle.forward, direction);
            if (_rangedTarget.transform.position.y < muzzle.position.y)
            {
                angle = -angle;
            }
            Pitch = Mathf.Lerp(Pitch, Mathf.Clamp(angle, -90f, 90f), Time.deltaTime);
        }
    }

    public void ExtrapolateMovement(Snapshot start, float currentRenderingTime)
    {
        throw new NotImplementedException();
    }

    private Vector3 FindPointOfAim()
    {
        RaycastHit hit1, hit2;
        Vector3 muzzlePosition = GetMuzzlePosition();
        
        // Raycast from camera in direction of crosshair.
        if (Physics.Raycast(MouseOrbit.transform.position, MouseOrbit.transform.forward, out hit1))
        {
            // Linecast from weapon muzzle to point under crosshair.
            if (Physics.Linecast(muzzlePosition, hit1.point, out hit2))
            {
                // Something blocking muzzle from hitting point under crosshair. Return hit on that.
                return hit2.point;
            }
            else
            {
                // Return point under crosshair.
                return hit1.point;
            }
        }
        else
        {
            return _rangedTarget.transform.position;
        }
    }
    
    private void ProcessStatusEffects()
    {
        isDead = (hp <= 0);
    }

    private void WeaponStateManagement(bool equip1, bool equip2)
    {
        if (equip1)
        {
            EquipWeaponByName("Machette");
        }
        else if (equip2)
        {
            EquipWeaponByName("Revolver");
        }
    }

    private void AttackManagement(double time, bool mouse0, bool mouse0Down, bool mouse1)
    {
        switch (EquippedWeaponInfo.type)
        {
            case EquippedWeaponType.None:
                IsWeaponRaised = false;
                break;
            case EquippedWeaponType.OneHandMelee:
                IsWeaponRaised = mouse0;
                IsWeaponBlocking = mouse1;
                if (mouse0)
                {
                    hasSwingStruck = false;
                }
                break;
            case EquippedWeaponType.OneHandPistol:
                IsWeaponRaised = true;
                if (mouse0Down)
                {
                    Shoot(time);
                }
                break;
        }
    }

    private void MovementManagement(double step, float horizontal, float vertical, bool sneaking)
    {
        // Make sure rigidbody isn't somehow picking up velocity
        GetComponent<Rigidbody>().velocity = Vector3.zero;

        // NOTE: There is a sort of dichotomy here between wanting the player's movement inputs to
        // instantly apply to the actual movement of the player model through space, and also 
        // wanting the player's animation to smoothly transition between strafing, going forward,
        // going backward, and all states in between. That's why we store a lerp-ed 'Movement' value
        // in the animator controller but step the player's position using another 'Velocity' value.

        // Calculate player motion
        float xVelocity = horizontal * runSpeed;
        float zVelocity = vertical * runSpeed;

        // Set player animation's movement direction/speed.
        // If magnitude < 0.1 and going down then snap to zero, to avoid asymptote.
        float smoothedXMovement = Mathf.Lerp(XMovement, xVelocity, moveSmoothing * Time.deltaTime);
        float smoothedZMovement = Mathf.Lerp(ZMovement, zVelocity, moveSmoothing * Time.deltaTime);
        XMovement = (Mathf.Abs(smoothedXMovement) < 0.1f) && (Mathf.Abs(smoothedXMovement) < Mathf.Abs(XMovement)) ?
                0f :
                smoothedXMovement;
        ZMovement = (Mathf.Abs(smoothedZMovement) < 0.1f) && (Mathf.Abs(smoothedZMovement) < Mathf.Abs(ZMovement)) ?
                0f :
                smoothedZMovement;
        
        // Player tracks camera direction if moving, weapon raised, or weapon blocking.
        if (XMovement != 0 || ZMovement != 0 ||
            IsWeaponRaised || IsWeaponStriking || IsWeaponBlocking)
        {
            TrackCameraRotation();
        }

        // Move step player through motion
        transform.position += ((float)step) * ((transform.forward * zVelocity) + (transform.right * xVelocity));
    }
    
    private void TrackCameraRotation()
    {
        // Get player movement direction relative to camera
        Transform cameraTransform = MouseOrbit.transform;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 targetDirection = new Vector3(cameraForward.x, 0f, cameraForward.z);

        // Rotate entire player in direction of movement
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
        Quaternion newRotation = Quaternion.Lerp(transform.rotation, targetRotation, turnSmoothing * Time.deltaTime);
        
        transform.rotation = newRotation;
    }

    private void Shoot(double time)
    {
        if (Network.isServer)
        {
            _anim.SetTrigger(_hash.pistolFireTrigger);

            // 
            DamageLocation historicalHitLocation = null;
            Vector3 historicalPointOfAim;

            // Rollback world state to match what shooter saw at the time they shot.
            using (new RollbackContext(_syncManager, time))
            {
                historicalPointOfAim = _rangedTarget.transform.position;

                // Get colliders where shot lands
                Collider[] colliders = Physics.OverlapSphere(historicalPointOfAim, 0.001f);

                GameObject hitBodyPart = colliders
                    .Select(c => c.gameObject)
                    .FirstOrDefault(go => go.tag == Tags.bodyPart);

                var gos = colliders.Select(c => c.gameObject).ToArray();
                var gox = gos.FirstOrDefault(x => x.tag == Tags.bodyPart);

                if (hitBodyPart != null)
                {
                   // Shot hit a player's body part.
                   historicalHitLocation = hitBodyPart.GetComponent<DamageLocation>();
                }
            }

            _syncManager.RecordShot(networkGuid, historicalPointOfAim, GetMuzzlePosition());

            if (historicalHitLocation != null)
            {
                DamageLocationScaled(historicalHitLocation, 30);
            }
        }
        else
        {
            _anim.SetTrigger(_hash.pistolFireTrigger);
            _syncManager.RenderShot(_rangedTarget.transform.position, GetMuzzlePosition());
        }
    }

    private void DamageLocationScaled(DamageLocation damageLocation, int damage)
    {
        Player otherPlayer = damageLocation.player.GetComponent<Player>();

        switch (damageLocation.damageLocationType)
        {
            case DamageLocationType.Head:
                otherPlayer.hp -= (int)(damage * 3.34f);
                break;
            case DamageLocationType.Torso:
                otherPlayer.hp -= damage;
                break;
            case DamageLocationType.Legs:
                otherPlayer.hp -= (int)(damage * 0.67f);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private Vector3 GetMuzzlePosition()
    {
        return EquippedWeapon.GetComponent<WeaponInfo>().muzzle.transform.position;
    }

    private GameObject InstantiateAsChild(GameObject prefab, Transform parentTransform)
    {
        GameObject newObject = GameObject.Instantiate(prefab, parentTransform.position, parentTransform.rotation) as GameObject;
        newObject.transform.parent = parentTransform;
        newObject.transform.localPosition = prefab.transform.localPosition;
        newObject.transform.localRotation = prefab.transform.localRotation;

        return newObject;
    }
}

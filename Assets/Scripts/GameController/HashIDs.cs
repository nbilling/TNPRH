using UnityEngine;
using System.Collections;

public class HashIDs : MonoBehaviour
{
    public int pistolLayer;
    public int oneHandMeleeLayer;

    public int oneHandMeleeStrikeState;
    public int oneHandMeleeBlockState;

    public int xMovementFloat;
    public int zMovementFloat;
    public int equippedWeaponTypeInt;
    public int isWeaponRaisedBool;
    public int isWeaponBlockingBool;
    public int pistolFireTrigger;
    public int pitchFloat;

    void Awake()
    {
        // TODO: discover layer indices at startup
        pistolLayer = 1;
        oneHandMeleeLayer = 2;

        oneHandMeleeStrikeState = Animator.StringToHash("1HandMelee.1HandSwordSwingStrike");
        oneHandMeleeBlockState = Animator.StringToHash("1HandMelee.1HandSwordBlock");

        xMovementFloat = Animator.StringToHash("XMovement");
        zMovementFloat = Animator.StringToHash("ZMovement");
        equippedWeaponTypeInt = Animator.StringToHash("EquippedWeaponType");
        isWeaponRaisedBool = Animator.StringToHash("IsWeaponRaised");
        isWeaponBlockingBool = Animator.StringToHash("IsWeaponBlocking");
        pistolFireTrigger = Animator.StringToHash("PistolFire");
        pitchFloat = Animator.StringToHash("Pitch");
    }
}

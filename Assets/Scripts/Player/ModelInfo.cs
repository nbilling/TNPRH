using UnityEngine;
using System.Collections;

public class ModelInfo : MonoBehaviour
{
    // References to points in this model's skeleton we care about.
    public GameObject head;
    public GameObject rArm;
    public GameObject rForearm;
    public GameObject rHand;
    public GameObject lArm;
    public GameObject lForearm;
    public GameObject lHand;
    public GameObject torso;
    public GameObject legs;

    // IK targets
    public Transform rArmPistolIkHandTarget;
    public Transform rArmPistolIkElbowTarget;
    public Transform lArmPistolIkHandTarget;
    public Transform lArmPistolIkElbowTarget;
}

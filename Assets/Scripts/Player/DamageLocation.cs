using UnityEngine;
using System.Collections;

public enum DamageLocationType
{
    Head,
    Torso,
    Legs
}

public class DamageLocation : MonoBehaviour
{
    public GameObject player;
    public DamageLocationType damageLocationType;

    public Vector3 eulerAngles;
    public Vector3 localEulerAngles;

    public void Update()
    {
        eulerAngles = transform.eulerAngles;
        localEulerAngles = transform.localEulerAngles;
    }
}

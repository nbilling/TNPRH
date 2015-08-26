using System.Collections;
using UnityEngine;

public class MouseOrbit : MonoBehaviour
{
	public Transform target;
    public float distanceX = 0.5f;
    public float distanceY = 2.50f;
	public float distanceZ = 2.75f;
	public float xSpeed = 15.0f;
	public float ySpeed = 15.0f;
	
	public float yMinLimit = -20f;
	public float yMaxLimit = 80f;

	public float x = 0.0f;
	public float y = 0.0f;

    public Vector3 eulerAngles;
    public Vector3 localEulerAngles;

    public void Update()
    {
        eulerAngles = transform.eulerAngles;
        localEulerAngles = transform.localEulerAngles;
    }
	
	// Use this for initialization
	void Start()
	{
		Vector3 angles = transform.eulerAngles;
		x = angles.y;
		y = angles.x;
		
		// Make the rigid body not change rotation
		if (GetComponent<Rigidbody>())
		{
			GetComponent<Rigidbody>().freezeRotation = true;
		}
	}
	
	public void HandleInput(float mouseX, float mouseY)
	{
		if (target)
		{
			x += mouseX * xSpeed * 0.02f;
			y -= mouseY * ySpeed * 0.02f;
			
			y = ClampAngle(y, yMinLimit, yMaxLimit); 
			
			Quaternion rotation = Quaternion.Euler(y, x, 0);
			
			Vector3 negdistanceZ = new Vector3(distanceX, distanceY, -distanceZ);
			Vector3 position = (rotation * negdistanceZ) + target.position;

			transform.rotation = rotation;
			transform.position = position;
		}
	}
	
	public static float ClampAngle(float angle, float min, float max)
	{
		if (angle < -360F)
		{
			angle += 360F;
		}
		if (angle > 360F)
		{
			angle -= 360F;
		}
		return Mathf.Clamp(angle, min, max);
	}
}

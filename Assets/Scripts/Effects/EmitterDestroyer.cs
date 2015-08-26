using UnityEngine;
using System.Collections;

public class EmitterDestroyer : MonoBehaviour
{
    private ParticleSystem _emitter;

    void Awake()
    {
        _emitter = gameObject.GetComponent<ParticleSystem>();
    }

	void Update ()
    {
	    if (!_emitter.IsAlive())
        {
            GameObject.Destroy(gameObject);
        }
	}
}

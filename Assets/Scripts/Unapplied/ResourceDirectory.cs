using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ResourceDirectory
{
    private static ResourceDirectory _instance;
    public static ResourceDirectory Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ResourceDirectory();
            }

            return _instance;
        }
    }

    public IDictionary<string, GameObject> Creatures
    {
        get;
        private set;
    }

    public IDictionary<string, GameObject> Weapons
    {
        get;
        private set;
    }

    public IDictionary<string, GameObject> Camera
    {
        get;
        private set;
    }

    public IDictionary<string, GameObject> Effects
    {
        get;
        private set;
    }

    public IDictionary<string, GameObject> Infrastructure
    {
        get;
        private set;
    }
    
    private ResourceDirectory()
    {
        Creatures = CreateMapFromPath("Prefabs/Creatures");
        Weapons = CreateWeaponMap();
        Camera = CreateMapFromPath("Prefabs/Camera");
        Effects = CreateMapFromPath("Prefabs/Effects");
        Infrastructure = CreateMapFromPath("Prefabs/Infrastructure");
    }

    private static IDictionary<string, GameObject> CreateMapFromPath(string path)
    {
        return Resources.LoadAll<GameObject>(path)
            .ToDictionary<GameObject, string>(go => go.name);
    }

    private static IDictionary<string, GameObject> CreateWeaponMap()
    {
        return Resources.LoadAll<GameObject>("Prefabs/Weapons")
            .ToDictionary<GameObject, string>(go => go.GetComponent<WeaponInfo>().weaponName);
    }
}

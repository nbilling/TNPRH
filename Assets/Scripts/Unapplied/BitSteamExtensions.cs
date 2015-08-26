using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class BitStreamExtensions
{
    private const int MaxStringLength = 250;

    // TODO: If you ever figure out how to find the end of a BitStream, remove the count header from this.
    public static void Serialize(this BitStream @this, IDictionary<string, PlayerSnapshot> value)
    {
        if (@this.isWriting)
        {
            // Write count to stream first
            int count = value.Count();
            @this.Serialize(ref count);
            
            // Then write all deltas
            foreach (var item in value)
            {
                string guid = item.Key;
                @this.Serialize(ref guid);
                @this.Serialize(item.Value);
            }
        }
        else
        {
            // Read count from stream first
            int count = 0;
            @this.Serialize(ref count);
            
            // Then read all deltas
            for (int i = 0; i < count; i++)
            {
                string guid = null;
                @this.Serialize(ref guid);

                PlayerSnapshot snapshot = new PlayerSnapshot();
                @this.Serialize(snapshot);

                value.Add(guid, snapshot);
            }
        }
    }

    public static void Serialize(this BitStream @this, PlayerSnapshot value)
    {
        @this.Serialize(ref value.position);
        @this.Serialize(ref value.xMovement);
        @this.Serialize(ref value.zMovement);
        @this.Serialize(ref value.rotation);
        @this.Serialize(ref value.mouseOrbitPosition);
        @this.Serialize(ref value.mouseOrbitrotation);
        @this.Serialize(ref value.mouseOrbitX);
        @this.Serialize(ref value.mouseOrbitY);
        @this.Serialize(ref value.equippedWeapon);
        @this.Serialize(ref value.isWeaponRaised);
        @this.Serialize(ref value.isWeaponBlocking);
        @this.Serialize(ref value.lastAppliedCommandTime);
        @this.Serialize(ref value.hp);
        @this.Serialize(ref value.isDead);
        @this.Serialize(ref value.hasSwingStruck);
    }

    public static void Serialize(this BitStream @this, ICollection<Shot> value)
    {
        if (@this.isWriting)
        {
            // Write count first
            int count = value.Count;
            @this.Serialize(ref count);

            // Then write shots
            foreach (var shot in value)
            {
                @this.Serialize(shot);
            }
        }
        else
        {
            // Read count first
            int count = 0;
            @this.Serialize(ref count);

            // Then read shots
            for (int i = 0; i < count; i++)
            {
                Shot shot = new Shot();
                @this.Serialize(shot);
                value.Add(shot);
            }
        }
    }

    public static void Serialize(this BitStream @this, Shot value)
    {
        @this.Serialize(ref value.playerGuid);
        @this.Serialize(ref value.destination);
        @this.Serialize(ref value.source);
    }

    // TODO: this is really shitty, replace this ASAP
    public static void Serialize(this BitStream @this, ref double value)
    {
        if (@this.isWriting)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            int firstFourBytesAsInt = BitConverter.ToInt32(bytes, 0);
            int secondFourBytesAsInt = BitConverter.ToInt32(bytes, 4);
            @this.Serialize(ref firstFourBytesAsInt);
            @this.Serialize(ref secondFourBytesAsInt);
        }
        else
        {
            int firstFourBytesAsInt = 0;
            int secondFourBytesAsInt = 0;
            @this.Serialize(ref firstFourBytesAsInt);
            @this.Serialize(ref secondFourBytesAsInt);
            byte[] bytes = new byte[8];
            byte[] firstFourBytes = BitConverter.GetBytes(firstFourBytesAsInt);
            byte[] secondFourBytes = BitConverter.GetBytes(secondFourBytesAsInt);
            bytes[0] = firstFourBytes[0];
            bytes[1] = firstFourBytes[1];
            bytes[2] = firstFourBytes[2];
            bytes[3] = firstFourBytes[3];
            bytes[4] = secondFourBytes[0];
            bytes[5] = secondFourBytes[1];
            bytes[6] = secondFourBytes[2];
            bytes[7] = secondFourBytes[3];

            value = BitConverter.ToDouble(bytes, 0);
        }
    }

    unsafe public static void Serialize(this BitStream @this, ref string value)
    {
        if (@this.isWriting)
        {
            if (value.Length > MaxStringLength)
            {
                throw new UnityException(string.Format("String longer than {0} characters.", MaxStringLength));
            }

            // Serialize string into null terminated byte stream
            char b;
            foreach (var c in value)
            {
                if (c <= 0  || c > 255)
                {
                    throw new UnityException("Non-ASCII character in string.");
                }
                b = (char)c;
                
                @this.Serialize(ref b);
            }
            
            b = (char)0;
            @this.Serialize(ref b);
        }
        else
        {
            // Deserialize null terminated byte stream into a string
            char[] bs = new char[MaxStringLength + 1]; // Max string length + null terminator

            int count = 0;
            do
            {
                if (count > MaxStringLength)
                {
                    throw new UnityException(string.Format("String longer than {0} characters.", MaxStringLength));
                }

                @this.Serialize(ref bs[count]);

                ++count;
            } while (bs[count - 1] != 0);

            fixed (char* pBs = bs)
            {
                value = new string(pBs);
            }
        }
    }
}

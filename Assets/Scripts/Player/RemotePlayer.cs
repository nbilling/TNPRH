using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class RemotePlayer : Player
{
    // How long the player had been swinging before server noticed, if currently swinging.
    // Otherwise meaningless.
    public double swingStartTimeOffset = 0d;
}

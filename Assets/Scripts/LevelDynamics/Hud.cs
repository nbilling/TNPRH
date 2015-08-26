using UnityEngine;
using System.Collections;

public class Hud : MonoBehaviour
{
    public int crosshairRadius = 5;
    public int crosshairThickness = 1;

    public int respawnButtonWidth = 250;
    public int respawnButtonHeight = 100;

    public int hpTextAreaWidth = 250;
    public int hpTextAreaHeight = 100;

    private Texture2D _crosshairTexture;
    private SyncManager _syncManager;
    private Player _player;

    void Awake()
    {
        _crosshairTexture = new Texture2D(1, 1);
        _crosshairTexture.SetPixel(0, 0, Color.white);
    }

    void OnGUI()
    {
        if (Network.isClient)
        {
            PingSyncManager();
            PingPlayer();

            CrosshairGui();
            RespawnGui();
            HpGui();
        }
    }

    private void PingSyncManager()
    {
        if (_syncManager == null)
        {
            _syncManager = GameObject.FindGameObjectWithTag(Tags.networkController).GetComponent<SyncManager>();
        }
    }

    private void PingPlayer()
    {
        // TODO: The player should only ever be null at the very beginning (if ever).
        //       Consider taking a closer look at this scenario to simplify code.

        // If player unset or player dead then check for new player instance
        if (_player == null || _player.isDead)
        {
            _player = null;
            _syncManager._playerLookup.TryGetValue(Network.player.guid, out _player);
        }
    }

    private void CrosshairGui()
    {
        // If player set and not dead then display crosshair
        if (_player != null && !_player.isDead)
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);

            Rect verticalBar = new Rect(screenCenter.x - (crosshairThickness / 2), screenCenter.y - crosshairRadius, crosshairThickness, crosshairRadius * 2 + crosshairThickness);
            Rect horizontalBar = new Rect(screenCenter.x - crosshairRadius, screenCenter.y - (crosshairThickness / 2), crosshairRadius * 2 + crosshairThickness, crosshairThickness);

            GUI.DrawTexture(verticalBar, _crosshairTexture);
            GUI.DrawTexture(horizontalBar, _crosshairTexture);
        }
    }

    private void RespawnGui()
    {
        // If player set and dead then display respawn button
        if (_player != null && _player.isDead)
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
            if (GUI.Button(
                new Rect(screenCenter.x - (respawnButtonWidth / 2),
                     screenCenter.y - (respawnButtonHeight / 2),
                     respawnButtonWidth,
                     respawnButtonHeight),
                "Respawn"))
            {
                _syncManager.BroadcastRespawn();
            }
        }
    }

    private void HpGui()
    {
        if (_player != null && !_player.isDead)
        {
            GUI.TextArea(
                new Rect(
                    Screen.width - hpTextAreaWidth,
                    Screen.height - hpTextAreaHeight,
                    hpTextAreaWidth,
                    hpTextAreaHeight),
                string.Format("HP: {0}", _player.hp));
        }
    }
}

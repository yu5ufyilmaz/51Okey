using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class SetupPlayer : MonoBehaviourPunCallbacks
{
    public string playerName;
    public int playerQueue;
    public List<Tiles> playerTilesInfo = new List<Tiles>();



    [PunRPC]
    public void SetPlayerName(string _playerName)
    {
        playerName = _playerName;

        /*if (nickNameText != null)
        {
            nickNameText.text = playerName;  
        }
        else
        {
            Debug.LogError("NickNameText is not assigned! Please assign it in the inspector.");
        }*/
    }

    [PunRPC]
    public void SetPlayerQueue(int _playerQueue)
    {
        playerQueue = _playerQueue;
    }

    [PunRPC]
    public void SetPlayerSeat(Vector3 newPosition)
    {
        transform.position = newPosition;
    }


    [PunRPC]
    public void DistributeTiles(int playerId, List<TileDataInfo> tileDataList)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber == playerId)
        {
            List<Tiles> playerTiles = new List<Tiles>();
            foreach (var tileData in tileDataList)
            {
                playerTiles.Add(tileData.ToTile());
                playerTilesInfo.Add(tileData.ToTile());
            }
            Debug.Log("Taşlar başarıyla dağıtıldı.");

            // Taşları UI'de veya oyun alanında gösterebilirsiniz
        }
    }

    public void AssignTilesOnce(List<Tiles> tiles)
    {
        if (playerTilesInfo.Count > 0)
        {
            Debug.LogWarning($"Player {playerName} already has assigned tiles. Skipping re-assignment.");
            return;
        }

        playerTilesInfo.AddRange(tiles);
        Debug.Log($"Assigned {tiles.Count} tiles to player {playerName}.");

        // UI veya diğer güncellemeler yapılabilir
    }
}
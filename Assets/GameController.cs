using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameController : MonoBehaviourPunCallbacks
{
    private List<int> allTiles;
    private int tilesPerPlayer = 14;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            InitializeTiles(); // Only the MasterClient will initialize the tiles
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount == 4)
        {
            photonView.RPC("DistributeTiles", RpcTarget.AllBuffered); // Distribute to all players
        }
    }

    [PunRPC]
    void DistributeTiles()
    {
        List<int> playerTiles = new List<int>();

        for (int i = 0; i < tilesPerPlayer; i++)
        {
            int tileIndex = Random.Range(0, allTiles.Count);
            playerTiles.Add(allTiles[tileIndex]);
            allTiles.RemoveAt(tileIndex); // Remove the tile from the pool
        }

        Debug.Log("Tiles distributed to player: " + PhotonNetwork.LocalPlayer.NickName);
        // Now you have the tiles for the player in playerTiles.
        // You can use this list for further gameplay mechanics.
    }

    void InitializeTiles()
    {
        // Example: Initialize 52 tiles. Modify this according to your game's rules.
        for (int i = 1; i <= 52; i++)
        {
            allTiles.Add(i); // Add tiles 1-52 to the list
        }
    }
}
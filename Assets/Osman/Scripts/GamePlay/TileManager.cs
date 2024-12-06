using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Newtonsoft.Json; // JSON dönüştürme için kullanılıyor.
public class TileManager : MonoBehaviourPunCallbacks
{
    public GameObject tilePrefab; // Tile prefab
    public Transform playerTileContainer; // Tile container for player


    public TileDataInfo[] tileDataArray; // Array of tile data (including Jokers)
    public List<TileData> alltiles = new List<TileData>();
    public List<TileDataInfo> gameTiles = new List<TileDataInfo>(); // All game tiles
    private Transform[] playerTileContainers; // Player tile placeholders



    private void Start()
    {
        InitializePlaceholders();
        GenerateTiles();
    }

    // Initialize placeholders for player tiles
    private void InitializePlaceholders()
    {
        int placeholderCount = playerTileContainer.childCount;
        playerTileContainers = new Transform[placeholderCount];

        for (int i = 0; i < placeholderCount; i++)
        {
            playerTileContainers[i] = playerTileContainer.GetChild(i);
        }
    }






    // Generate and shuffle tiles, then synchronize with other players
    public void GenerateTiles()
    {
        gameTiles.Clear();

        // Convert TileData to TileDataInfo and add to gameTiles list
        foreach (TileDataInfo tileDataInfo in tileDataArray)
        {
            gameTiles.Add(tileDataInfo);
            gameTiles.Add(tileDataInfo); // Add each tile twice, including Jokers
        }

        //ShuffleTiles(gameTiles); // Shuffle tiles

        string serializedTiles = JsonConvert.SerializeObject(gameTiles);
        photonView.RPC(nameof(SyncTileList), RpcTarget.AllBuffered, serializedTiles);

    }
    [PunRPC]
    public void SyncTileList(string serializedTiles)
    {
        List<TileDataInfo> deserializedTiles = JsonConvert.DeserializeObject<List<TileDataInfo>>(serializedTiles);

        // Bu noktada deserializedTiles kullanılarak gerekli işlemler yapılabilir
        foreach (TileDataInfo tile in deserializedTiles)
        {
            // TileDataInfo'yu kullanarak gerekli işlemleri yapabilirsiniz
            Debug.Log($"Tile Info: Color = {tile.color}, Number = {tile.number}");
        }
    }

    // Shuffle tiles list
    [PunRPC]
    public void ShuffleTiles(List<TileDataInfo> tiles)
    {
        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            TileDataInfo temp = tiles[i];
            tiles[i] = tiles[randomIndex];
            tiles[randomIndex] = temp;
        }
        DistributeTiles();
    }

    // Distribute tiles to players
    public void DistributeTiles()
    {
        if (!photonView.IsMine) return;

        int playersCount = PhotonNetwork.PlayerList.Length;
        int tilesPerPlayer = 14;

        // Ensure enough tiles for distribution
        if (gameTiles.Count < playersCount * tilesPerPlayer)
        {
            Debug.LogWarning("Not enough tiles to distribute.");
            return;
        }

        for (int i = 0; i < tilesPerPlayer; i++)
        {
            if (i >= playerTileContainers.Length)
            {
                Debug.LogWarning("Index out of bounds for tile container.");
                continue;
            }

            // Clear previous children
            foreach (Transform child in playerTileContainers[i])
            {
                Destroy(child.gameObject);
            }

            // Instantiate and set tile data
            TileDataInfo tileDataInfo = gameTiles[i];
            GameObject tileInstance = Instantiate(tilePrefab, playerTileContainers[i]);

            TileUI tileUI = tileInstance.GetComponent<TileUI>();
            if (tileUI != null)
            {
                //tileUI.SetTileData(tileDataInfo);
            }
            else
            {
                Debug.LogError("TileUI component missing on tilePrefab.");
            }

            tileInstance.transform.localPosition = Vector3.zero;
        }
    }
}

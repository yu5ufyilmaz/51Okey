using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class TileManager : MonoBehaviourPunCallbacks
{
    public GameObject tilePrefab; // Prefab for tiles
    public Transform playerTileContainer; // Parent container for arranging tiles
    public TileData[] tileDataArray; // All tiles data, including Jokers

    [SerializeField] private List<TileData> allTiles = new List<TileData>();

    private Transform[] playerTileContainers; // Array for individual placeholders

    void Start()
    {
        InitializePlaceholders();  // Placeholders'ı diziye ekle
        //photonView.RPC("GenerateTiles", RpcTarget.AllBuffered);
        //photonView.RPC("ShuffleTiles", RpcTarget.AllBuffered, allTiles);
        GenerateTiles();  // Create all tiles

        ShuffleTiles(allTiles);  // Shuffle tiles for random distribution

    }

    // Placeholders'ı playerTileContainers dizisine otomatik ekle
    void InitializePlaceholders()
    {
        int placeholderCount = playerTileContainer.childCount;
        playerTileContainers = new Transform[placeholderCount];

        for (int i = 0; i < placeholderCount; i++)
        {
            playerTileContainers[i] = playerTileContainer.GetChild(i);
        }
    }

    // Tiles'ı oluştur ve listeye ekle
    [PunRPC]
    void GenerateTiles()
    {
        foreach (TileData tileData in tileDataArray)
        {
            if (tileData.IsJoker())
            {
                // Jokers are added only twice
                allTiles.Add(tileData);
                allTiles.Add(tileData);
            }
            else
            {
                // Regular tiles are added twice
                allTiles.Add(tileData);
                allTiles.Add(tileData);
            }
        }
    }

    [PunRPC]
    // Tiles'ı karıştır
    void ShuffleTiles(List<TileData> tiles)
    {
        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            TileData temp = tiles[i];
            tiles[i] = tiles[randomIndex];
            tiles[randomIndex] = temp;
        }
    }
    void RemoveTileFromList(TileData tileData)
    {
        allTiles.Remove(tileData);
    }
    // Tiles'ı placeholders'a dağıt
    public void DistributeTiles()
    {
        for (int player = 0; player < PhotonNetwork.PlayerList.Length; player++)
            for (int i = 0; i < 14; i++)  // Her oyuncuya 14 taş dağıt
            {
                if (i >= allTiles.Count || i >= playerTileContainers.Length)
                {
                    Debug.LogWarning("Index out of bounds. Check the number of tiles and placeholders.");
                    continue;
                }

                // Daha önceki çocukları temizle
                foreach (Transform child in playerTileContainers[i])
                {
                    Destroy(child.gameObject);
                }

                TileData tileData = allTiles[i];
                GameObject tileInstance = Instantiate(tilePrefab, playerTileContainers[i]);
                photonView.RPC("RemoveTileFromList", RpcTarget.AllBuffered, tileData);
                TileUI tileUI = tileInstance.GetComponent<TileUI>();
                allTiles.Remove(tileData);
                if (tileUI != null)
                {
                    tileUI.SetTileData(tileData); // TileData'yı TileUI'a aktar
                }
                else
                {
                    Debug.LogError("TileUI component is missing on tilePrefab.");
                }

                // Pozisyon sıfırlama: Tile'ın placeholder'ın merkezine oturmasını sağlar
                tileInstance.transform.localPosition = Vector3.zero;

                Debug.Log("Tile parent: " + tileInstance.transform.parent.name);

                // Dağıtılan taşı currentTiles listesinden sil

            }
    }
}

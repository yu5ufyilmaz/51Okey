using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    public GameObject tilePrefab; // Taşların prefab'i
    public Transform playerTileContainer; // Istaka üzerindeki Grid Layout Container
    private List<Tile> allTiles = new List<Tile>();

    void Start()
    {
        GenerateTiles();  // Taşları oluştur
        ShuffleTiles(allTiles);  // Taşları karıştır
        DistributeTiles(); // Taşları dağıt
    }

    void GenerateTiles()
    {
        string[] colors = { "Red", "Black", "Blue", "Yellow" };
        foreach (string color in colors)
        {
            for (int i = 1; i <= 13; i++)
            {
                allTiles.Add(new Tile(color, i));
                allTiles.Add(new Tile(color, i));
            }
        }
        // Joker ekle
        allTiles.Add(new Tile("Joker", 0));
        allTiles.Add(new Tile("Joker", 0));
    }

    void ShuffleTiles(List<Tile> tiles)
    {
        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Tile temp = tiles[i];
            tiles[i] = tiles[randomIndex];
            tiles[randomIndex] = temp;
        }
    }

    void DistributeTiles()
    {
        // Placeholder'lardaki yerleri doldurmak üzere taşları oluşturuyoruz
        for (int i = 0; i < 14; i++)  // Başlangıçta her oyuncuya 14 taş verilecek (ilk oyuncuya 15)
        {
            Tile tileData = allTiles[i];
            GameObject tileInstance = Instantiate(tilePrefab, playerTileContainer);

            // Taşın verilerini TileUI aracılığıyla atıyoruz
            TileUI tileUI = tileInstance.GetComponent<TileUI>();
            if (tileUI != null)
            {
                tileUI.SetTileData(tileData);
            }
        }
    }
}
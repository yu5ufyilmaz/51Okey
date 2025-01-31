using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private Dictionary<int, int> playerScores; // Oyuncu ID'si ve puanı
    private Dictionary<int, int> playerPerCounts; // Oyuncu ID'si ve per sayısı
    private TileDistrubite tileDistrubite; // Taşları yöneten sınıf

    // Editörde görünür hale getirmek için
    [Header("Player Scores")]
    public int player1Score;
    public int player2Score;
    public int player3Score;
    public int player4Score;

    [Header("Player Per Counts")]
    public int player1PerCount;
    public int player2PerCount;
    public int player3PerCount;
    public int player4PerCount;

    private void Start()
    {
        playerScores = new Dictionary<int, int>();
        playerPerCounts = new Dictionary<int, int>();
        tileDistrubite = FindObjectOfType<TileDistrubite>();
    }

    public void AddScore(int playerId, int score)
    {
        if (playerScores.ContainsKey(playerId))
        {
            playerScores[playerId] += score;
        }
        else
        {
            playerScores[playerId] = score;
        }

        UpdatePlayerScoreInEditor(playerId);
    }

    public void UpdatePlayerScoreInEditor(int playerId)
    {
        switch (playerId)
        {
            case 1:
                player1Score = playerScores[playerId];
                break;
            case 2:
                player2Score = playerScores[playerId];
                break;
            case 3:
                player3Score = playerScores[playerId];
                break;
            case 4:
                player4Score = playerScores[playerId];
                break;
        }
    }

    public void SetPerCount(int playerId, int count)
    {
        if (playerPerCounts.ContainsKey(playerId))
        {
            playerPerCounts[playerId] = count;
        }
        else
        {
            playerPerCounts[playerId] = count;
        }

        UpdatePlayerPerCountInEditor(playerId);
    }

    public void UpdatePlayerPerCountInEditor(int playerId)
    {
        switch (playerId)
        {
            case 1:
                player1PerCount = playerPerCounts[playerId];
                break;
            case 2:
                player2PerCount = playerPerCounts[playerId];
                break;
            case 3:
                player3PerCount = playerPerCounts[playerId];
                break;
            case 4:
                player4PerCount = playerPerCounts[playerId];
                break;
        }
    }

    public void CalculateScoreForPlayer(int playerId)
    {
        int totalScore = 0;

        // Oyuncunun elindeki taşları kontrol et
        List<Tiles> playerTiles = tileDistrubite.GetPlayerTiles(playerId);
        List<List<Tiles>> perGroups = FindPerGroups(playerTiles);

        foreach (var per in perGroups)
        {
            totalScore += CalculatePerScore(per);
        }

        AddScore(playerId, totalScore);
        SetPerCount(playerId, perGroups.Count); // Per sayısını güncelle
    }

    private List<List<Tiles>> FindPerGroups(List<Tiles> tiles)
    {
        List<List<Tiles>> perGroups = new List<List<Tiles>>();

        // Aynı renkten taşları gruplama
        var groupedByColor = tiles.GroupBy(t => t.color);
        foreach (var group in groupedByColor)
        {
            if (group.Count() >= 3)
            {
                perGroups.Add(group.ToList());
            }
        }

        // Ardışık numaraları gruplama
        var orderedTiles = tiles.OrderBy(t => t.number).ToList();
        List<Tiles> currentPer = new List<Tiles>();

        for (int i = 0; i < orderedTiles.Count; i++)
        {
            if (currentPer.Count == 0 || orderedTiles[i].number == currentPer.Last().number + 1)
            {
                currentPer.Add(orderedTiles[i]);
            }
            else
            {
                if (currentPer.Count >= 3)
                {
                    perGroups.Add(new List<Tiles>(currentPer));
                }
                currentPer.Clear();
                currentPer.Add(orderedTiles[i]);
            }
        }

        if (currentPer.Count >= 3)
        {
            perGroups.Add(currentPer);
        }

        return perGroups;
    }

    private int CalculatePerScore(List<Tiles> per)
    {
        int score = 0;
        foreach (var tile in per)
        {
            score += tile.number; // Taşın numarasını puan olarak kullan
        }
        return score;
    }
}

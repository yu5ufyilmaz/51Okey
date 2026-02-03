using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Bu sınıf sadece Okey Kurallarını bilir. UI veya Network bilmez.
public static class OkeyRuleEngine
{
    // --- YARDIMCI: GÖSTERGE KONTROLÜ ---
    // ScoreManager'dan buraya "indicator" (gösterge taşı) parametre olarak gelir.
    public static bool IsIndicator(Tiles tile, Tiles indicator)
    {
        if (indicator == null || tile == null)
            return false;
        return tile.color == indicator.color && tile.number == indicator.number;
    }

    // --- 1. TEK RENK (SERİ) KONTROLLERİ ---
    public static bool IsSingleColor(List<Tiles> tiles, Tiles indicator)
    {
        TileColor? firstColor = null;
        bool isFirstJoker = false;

        if (tiles.Count > 0 && tiles[0].type == TileType.Joker)
        {
            isFirstJoker = true;
        }

        foreach (var tile in tiles)
        {
            if (isFirstJoker && tile == tiles[0])
                continue;

            // Gösterge veya Joker renk kuralını bozmaz
            if (tile.type == TileType.Joker || IsIndicator(tile, indicator))
                continue;

            if (firstColor == null)
                firstColor = tile.color;
            else if (firstColor == tile.color)
                continue;
            else
                return false; // Renkler uyuşmuyor
        }
        return true;
    }

    public static bool SingleColorCheck(List<Tiles> tiles)
    {
        if (tiles.Count < 3)
            return false;

        int[] pattern = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
        int[] revPattern = pattern.Reverse().ToArray();

        if (CheckPattern(tiles, pattern) || CheckPattern(tiles, revPattern))
        {
            return true;
        }
        return false;
    }

    private static bool CheckPattern(List<Tiles> tiles, int[] pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            bool valid = true;
            for (int j = 0; j < tiles.Count; j++)
            {
                int expectedNumber = pattern[
                    (i + j > pattern.Length - 1 ? pattern.Length - 1 : i + j)
                ];

                if (tiles[j].type == TileType.Joker)
                {
                    bool isValidJoker = false;
                    // (Senin orijinal Joker mantığın aynen korundu)
                    if (j == 0)
                    {
                        if (
                            (tiles.Count > 1 && tiles[j + 1].number == expectedNumber + 1)
                            || (tiles.Count > 1 && tiles[j + 1].number == expectedNumber - 1)
                        )
                        {
                            tiles[j].number = expectedNumber;
                            tiles[j].color = tiles[j + 1].color;
                            isValidJoker = true;
                        }
                    }
                    else if (j == tiles.Count - 1)
                    {
                        if (
                            tiles[j - 1].number == expectedNumber - 1
                            || tiles[j - 1].number == expectedNumber + 1
                        )
                        {
                            tiles[j].number = expectedNumber;
                            tiles[j].color = tiles[j - 1].color;
                            isValidJoker = true;
                        }
                    }
                    else
                    {
                        if (
                            (
                                tiles[j - 1].number == expectedNumber - 1
                                || tiles[j - 1].number == expectedNumber + 1
                            )
                        )
                        {
                            tiles[j].number = expectedNumber;
                            tiles[j].color = tiles[j - 1].color;
                            isValidJoker = true;
                        }
                        else if (
                            (
                                tiles[j + 1].number == expectedNumber + 1
                                || tiles[j + 1].number == expectedNumber - 1
                            )
                        )
                        {
                            tiles[j].number = expectedNumber;
                            tiles[j].color = tiles[j + 1].color;
                            isValidJoker = true;
                        }
                    }

                    if (!isValidJoker)
                    {
                        valid = false;
                        break;
                    }
                }
                else if (tiles[j].number == expectedNumber)
                {
                    continue;
                }
                else
                {
                    valid = false;
                    break;
                }
            }
            if (valid)
                return true;
        }
        return false;
    }

    // --- 2. ÇOK RENK (GRUP) KONTROLLERİ ---
    public static bool MultiColorCheck(List<Tiles> tiles)
    {
        if (tiles.Count < 3)
            return false;
        if (tiles.Count > 4)
            return false;

        Tiles notJokerStones = null;
        List<Tiles> jokerStones = new List<Tiles>();

        foreach (var tile in tiles)
        {
            if (tile.type != TileType.Joker)
                notJokerStones = tile;
            else
                jokerStones.Add(tile);
        }

        foreach (var tile in tiles)
        {
            if (tile.type == TileType.Joker)
                continue;
            else if (tile.number == notJokerStones.number)
                continue;
            else
                return false;
        }

        IEnumerable<Tiles> filteredList = tiles
            .Where(x => x.type != TileType.Joker)
            .GroupBy(a => a.color)
            .Select(group => group.First());

        if (filteredList.Count() + (jokerStones.Count) != tiles.Count)
        {
            return false;
        }

        foreach (var joker in jokerStones)
        {
            joker.number = notJokerStones.number;
        }
        return true;
    }

    // --- 3. ÇİFT (PAIR) KONTROLLERİ ---
    public static bool CheckForDoublePer(List<Tiles> tiles, Tiles indicator)
    {
        if (tiles.Count != 2)
            return false;

        Tiles tile1 = tiles[0];
        Tiles tile2 = tiles[1];

        // Gösterge Kontrolü
        if (IsIndicator(tile1, indicator) || IsIndicator(tile2, indicator))
            return true;

        Tiles normalTile = null;
        List<Tiles> jokerStones = new List<Tiles>();

        foreach (var tile in tiles)
        {
            if (tile.type == TileType.Joker)
                jokerStones.Add(tile);
            else
            {
                if (normalTile == null)
                    normalTile = tile;
                else if (normalTile.number != tile.number)
                    return false;
            }
        }

        if (jokerStones.Count == 2)
            return true;
        if (jokerStones.Count == 1 && normalTile != null)
            return true;

        return normalTile != null && tiles[0].color == tiles[1].color;
    }

    // --- 4. PUAN HESAPLAMALARI ---
    public static int CalculateGroupScore(List<Tiles> tiles)
    {
        int score = 0;
        foreach (var tile in tiles)
        {
            score += tile.number;
        }
        return score;
    }

    public static int CalculateDoublePerScore(List<Tiles> tiles)
    {
        int score = 0;
        foreach (var tile in tiles)
        {
            score += tile.number;
        }
        return score;
    }
}

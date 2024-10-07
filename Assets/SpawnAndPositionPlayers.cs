using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class SpawnAndPositionPlayers : MonoBehaviourPunCallbacks
{
    public GameObject playerPrefab;  // Oyuncu prefab'i
    public RectTransform[] spawnPositions;  // UI'daki oyuncu pozisyonları
    public TextMeshProUGUI[] playerNameTexts;  // UI'daki oyuncu isimlerinin yazdırılacağı TextMeshPro alanları

    private List<int> availablePositions = new List<int>();  // Kullanılabilir pozisyonların listesi

    public override void OnJoinedRoom()
    {
        AssignRandomPositionAndInstantiate();  // Oyuncu odaya katıldığında rastgele pozisyon ve instantiate işlemi yap
        AssignRelativePlayerPositions();       // Göreceli oyuncu pozisyonlarını UI'da göster
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AssignRelativePlayerPositions();  // Yeni oyuncu katıldığında sıralamayı yeniden yap
    }

    private void AssignRandomPositionAndInstantiate()
    {
        // Kullanılabilir pozisyonları oluştur
        availablePositions.Clear();
        for (int i = 0; i < spawnPositions.Length; i++)
        {
            availablePositions.Add(i);
        }

        if (availablePositions.Count > 0 && playerPrefab != null)
        {
            // Rastgele bir pozisyon seç
            int randomIndex = Random.Range(0, availablePositions.Count);
            int spawnIndex = availablePositions[randomIndex];
            availablePositions.RemoveAt(randomIndex);

            Vector3 spawnPosition = spawnPositions[spawnIndex].transform.position;
            Quaternion spawnRotation = Quaternion.identity;

            // Prefab'i Instantiate et
            PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation, 0);
            Debug.Log("Player instantiated at random position: " + spawnIndex);
        }
        else
        {
            Debug.LogError("Not enough spawn positions available or playerPrefab is not assigned!");
        }
    }

    private void AssignRelativePlayerPositions()
    {
        Player[] players = PhotonNetwork.PlayerList;  // Odaya katılan tüm oyuncuları alıyoruz
        int localPlayerIndex = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer);  // Yerel oyuncunun odaya giriş sırasını bul

        if (localPlayerIndex == -1)
        {
            Debug.LogError("Local player not found in the player list!");
            return;
        }

        // Her oyuncuyu göreceli pozisyona yerleştiriyoruz
        for (int i = 0; i < players.Length; i++)
        {
            // Göreceli sıralama hesaplama
            int relativeIndex = (i - localPlayerIndex + players.Length) % players.Length;

            // Kendi ismini her zaman 1. pozisyona yaz
            if (players[i] == PhotonNetwork.LocalPlayer)
            {
                playerNameTexts[0].text = players[i].NickName;
                Debug.Log("Local player sees themselves at position 1: " + players[i].NickName);
            }
            else
            {
                // Diğer oyuncuları göreceli sıraya göre yaz
                if (relativeIndex < playerNameTexts.Length)
                {
                    playerNameTexts[relativeIndex].text = players[i].NickName;
                    Debug.Log("Local player sees " + players[i].NickName + " at relative position " + (relativeIndex + 1));
                }
                else
                {
                    Debug.LogError("Relative index out of range! Index: " + relativeIndex);
                }
            }
        }
    }
}

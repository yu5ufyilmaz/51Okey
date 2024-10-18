using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;
using System.Linq;

public class OkeyGameManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private RoomManager roomManager;
    public GameObject playerPrefab;  // Oyuncu prefab'i
    public RectTransform[] spawnPositions;  // UI'daki oyuncu pozisyonları
    public TileManager tileManager;  // TileManager scriptine referans
    public TextMeshProUGUI[] playerNameTexts; // Oyuncu isimlerinin yazdırılacağı UI elemanları
    public Transform[] playerTileContainers; // Oyuncuların taşlarının yerleştirileceği Placeholder'lar

    [SerializeField] private List<int> availablePositions = new List<int>();
    int spawnIndex = 0;



    public override void OnJoinedRoom()
    {
        AssignRandomPositionAndInstantiate();  // Oyuncuyu rastgele bir pozisyona yerleştir ve instantiate et
        AssignRelativePlayerPositions();       // Oyunculara göreceli pozisyonlarını belirle

        /*     if (PhotonNetwork.IsMasterClient)  // Sadece MasterClient taşları dağıtır
           {

              if (tileManager == null)
                {
                    Debug.LogError("TileManager is not assigned in OkeyGameManager!");
                    return;
                }

                tileManager.DistributeTiles();  // Taşları dağıt (parametresiz)
           }*/

    }

    public override void OnLeftRoom()
    {
        PlayerLeftRoom();
        UpdatePlayerPositions();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AssignRelativePlayerPositions();  // Yeni bir oyuncu katıldığında oyuncuların göreceli pozisyonlarını güncelle
    }

    private int CheckEmptyPlace()
    {
        if (availablePositions.Count <= 0)
            return -1; // Tüm pozisyonlar doluysa -1 döndür.

        int minValue = availablePositions[0]; // İlk elemanı en küçük kabul et

        // Listedeki diğer elemanlarla karşılaştır
        for (int i = 1; i < availablePositions.Count; i++)
        {
            if (availablePositions[i] < minValue)
            {
                minValue = availablePositions[i];
            }
        }

        return minValue; // En küçük değeri döndür
    }

    private void AssignRandomPositionAndInstantiate()
    {

        if (availablePositions.Count > 0 && playerPrefab != null)
        {

            spawnIndex = CheckEmptyPlace();
            Debug.Log("Spawn index: " + CheckEmptyPlace());
            availablePositions.Remove(spawnIndex);

            if (spawnIndex != -1)
            {

                // Pozisyonu kullanılabilir listesinden kaldır.

                Vector3 spawnPosition = spawnPositions[spawnIndex].position;
                Quaternion spawnRotation = Quaternion.identity;

                // Oyuncuyu belirlenen pozisyona yerleştir.
                PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation, 0);
                Debug.Log("Player instantiated at position: " + spawnIndex);
            }
            else
            {
                Debug.LogError("No available spawn positions found!");
            }
        }
        else
        {
            Debug.LogError("Not enough spawn positions available or playerPrefab is not assigned!");
        }
    }

    private void AssignRelativePlayerPositions()
    {
        Player[] players = PhotonNetwork.PlayerList;
        int localPlayerIndex = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer);

        if (localPlayerIndex == -1)
        {
            Debug.LogError("Local player not found in the player list!");
            return;
        }

        for (int i = 0; i < players.Length; i++)
        {
            // Her oyuncu için relativeIndex, kendisini sıfırıncı indexte görmeli ve diğerlerini göreceli olarak sıralamalıdır
            int relativeIndex = (i - localPlayerIndex + players.Length) % players.Length;

            Debug.Log("Local player sees " + players[i].NickName + " at relative position " + (relativeIndex + 1));

            if (relativeIndex < playerNameTexts.Length)
            {
                playerNameTexts[relativeIndex].text = players[i].NickName;
            }
        }
    }

    private void PlayerLeftRoom()
    {
        Player[] players = PhotonNetwork.PlayerList;
        int localPlayerIndex = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer);

        // Tüm UI isim metinlerini "Oyuncu Bekleniyor" olarak ayarla
        for (int i = 0; i < playerNameTexts.Length; i++)
        {
            playerNameTexts[i].text = "Oyuncu Bekleniyor";
        }

        // Mevcut oyuncuların isimlerini doğru pozisyonlara yerleştir
        for (int i = 0; i < players.Length; i++)
        {
            int relativeIndex = (i - localPlayerIndex + players.Length) % players.Length;
            if (relativeIndex < playerNameTexts.Length)
            {
                playerNameTexts[relativeIndex].text = players[i].NickName;
            }
        }
    }

    private void UpdatePlayerPositions()
    {
        Player[] players = PhotonNetwork.PlayerList;

        // Tüm UI isim metinlerini önce "Oyuncu Bekleniyor" olarak ayarla
        for (int i = 0; i < playerNameTexts.Length; i++)
        {
            playerNameTexts[i].text = "Oyuncu Bekleniyor";
        }

        // Mevcut oyuncular için doğru isimleri yerleştir
        for (int i = 0; i < players.Length; i++)
        {
            if (i < playerNameTexts.Length)
            {
                playerNameTexts[i].text = players[i].NickName;
            }
        }
    }
}

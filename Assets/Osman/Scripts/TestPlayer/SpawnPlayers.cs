using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SpawnPlayers : MonoBehaviourPunCallbacks
{
    public GameObject playerPrefab;  // Oyuncu prefab'i
    public RectTransform[] spawnPositions;  // UI'daki oyuncu pozisyonları (fiziksel spawn yerleri)
    public static Dictionary<int, int> playerSpawnPositions = new Dictionary<int, int>();  // Oyuncu ID ve pozisyon eşlemesi

    private List<int> availablePositions = new List<int>();  // Kullanılabilir pozisyonlar

    public override void OnJoinedRoom()
    {
        AssignRandomPositionAndInstantiate();
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

            // Prefab'i Instantiate et ve oyuncu pozisyonunu kaydet
            GameObject playerInstance = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation, 0);
            playerSpawnPositions[PhotonNetwork.LocalPlayer.ActorNumber] = spawnIndex;
            Debug.Log("Player instantiated at random position: " + spawnIndex);
        }
        else
        {
            Debug.LogError("Not enough spawn positions available or playerPrefab is not assigned!");
        }
    }
}
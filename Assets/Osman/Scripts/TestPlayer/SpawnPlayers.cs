using Photon.Pun;
using TMPro;
using UnityEngine;

public class SpawnPlayers : MonoBehaviourPunCallbacks
{
    public GameObject playerPrefab;  
    public RectTransform[] spawnPositions;  
    public TextMeshProUGUI[] playerNameTexts;  

    public override void OnJoinedRoom()
    {
        AssignPlayerPosition(); 
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        AssignPlayerPosition(); 
    }

    private void AssignPlayerPosition()
    {
        if (spawnPositions == null || spawnPositions.Length == 0)
        {
            Debug.LogError("Spawn positions not set! Make sure to assign them in the inspector.");
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned! Make sure to assign the prefab in the inspector.");
            return;
        }
        
        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;
        Debug.Log("Total players in the room: " + players.Length);

        
        for (int i = 0; i < players.Length; i++)
        {
            if (i < spawnPositions.Length)
            {
                Vector3 spawnPosition = spawnPositions[i].transform.position;
                Quaternion spawnRotation = Quaternion.identity;
                
                GameObject playerInstance = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation, 0);
                
                playerNameTexts[i].text = players[i].NickName; 

                Debug.Log("Player instantiated at UI position: " + i);
            }
            else
            {
                Debug.LogError("Player index is out of bounds! Index: " + i + " SpawnPositions Length: " + spawnPositions.Length);
            }
        }
    }
}

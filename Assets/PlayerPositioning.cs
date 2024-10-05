using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PlayerPositioning : MonoBehaviourPunCallbacks
{
    public TextMeshProUGUI[] playerNameTexts;  

    public override void OnJoinedRoom()
    {
        SetPlayerPosition();  
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        SetPlayerPosition();  
    }

    private void SetPlayerPosition()
    {
        Player[] players = PhotonNetwork.PlayerList;  
        int localPlayerIndex = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer); 
        
        for (int i = 0; i < players.Length; i++)
        {
            int adjustedIndex = (i - localPlayerIndex + players.Length) % players.Length;
            
            playerNameTexts[adjustedIndex].text = players[i].NickName;
            Debug.Log("Player name written to UI: " + players[i].NickName + " at index: " + adjustedIndex);
        }
    }
}
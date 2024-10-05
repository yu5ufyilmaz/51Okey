using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PlayerPositioning : MonoBehaviourPunCallbacks
{
    public TextMeshProUGUI[] playerNameTexts;  // UI Text alanları (her oyuncu için)

    public override void OnJoinedRoom()
    {
        UpdatePlayerUI();  // Odaya katıldığında UI'ı güncelle
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerUI();  // Yeni bir oyuncu katıldığında UI'ı güncelle
    }

    private void UpdatePlayerUI()
    {
        Player[] players = PhotonNetwork.PlayerList;
        int localPlayerIndex = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer);

        if (localPlayerIndex == -1)
        {
            Debug.LogError("Local player not found in the player list!");
            return;
        }

        // Her oyuncunun UI sıralamasını yapıyoruz (her oyuncu kendisini 1. sırada görecek)
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == PhotonNetwork.LocalPlayer)
            {
                // Yerel oyuncu kendisini 1. sırada görür
                playerNameTexts[0].text = players[i].NickName;
                Debug.Log("Local player sees themselves at position 1: " + players[i].NickName);
            }
            else
            {
                // Diğer oyuncular için göreceli pozisyon
                // Yerel oyuncunun bakış açısından diğer oyuncuların sırasını kaydırarak belirliyoruz
                int relativeIndex = (i - localPlayerIndex + players.Length) % players.Length;

                // Kendimizi 1. sırada gördüğümüz için diğer oyuncular 1'den başlar
                int uiIndex = (relativeIndex + 1) % playerNameTexts.Length;

                if (uiIndex < playerNameTexts.Length)
                {
                    playerNameTexts[uiIndex].text = players[i].NickName;
                    Debug.Log("Local player sees " + players[i].NickName + " at relative position " + (uiIndex + 1));
                }
                else
                {
                    Debug.LogError("Player index out of bounds! Index: " + uiIndex + " PlayerNameTexts Length: " + playerNameTexts.Length);
                }
            }
        }
    }
}

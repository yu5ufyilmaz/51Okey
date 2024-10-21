using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerSetup : MonoBehaviourPunCallbacks
{
    public string playerName;
    public int playerQueue;
    //private TextMeshProUGUI nickNameText;



    [PunRPC]
    public void SetPlayerName(string _playerName)
    {
        playerName = _playerName;

        /*if (nickNameText != null)
        {
            nickNameText.text = playerName;  
        }
        else
        {
            Debug.LogError("NickNameText is not assigned! Please assign it in the inspector.");
        }*/
    }

    [PunRPC]
    public void SetPlayerQueue(int _playerQueue)
    {
        playerQueue = _playerQueue;
    }

    [PunRPC]
    public void SetPlayerSeat(Vector3 newPosition)
    {
        transform.position = newPosition;
    }
}
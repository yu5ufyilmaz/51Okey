using Photon.Pun;
using TMPro;
using UnityEngine;

public class PlayerSetup : MonoBehaviourPunCallbacks
{
    public string playerName;
    public int playerQueue;
    private TextMeshProUGUI nickNameText;

    
    void Start()
    { 
        if (nickNameText != null && photonView.IsMine)
        {
            nickNameText.text = playerName;
        }
    }

    [PunRPC]
    public void SetPlayerName(string _playerName)
    {
        playerName = _playerName;
        
        if (nickNameText != null)
        {
            nickNameText.text = playerName;  
        }
        else
        {
            Debug.LogError("NickNameText is not assigned! Please assign it in the inspector.");
        }
    }

    [PunRPC]
    public void SetPlayerQueue(int _playerQueue)
    {
        playerQueue = _playerQueue;
    }
}
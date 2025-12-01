using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    static PhotonManager instance = null;

    void Start()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
        // Arka planda çalışmayı zorla
        // 1. Oyun arka planda (alt-tab yapınca) çalışmaya devam etsin, kopmasın.
        Application.runInBackground = true;

        // 2. Bağlantı kopma süresini uzat (Zaten yapmışsın, kalsın)
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 30000;
        PhotonNetwork.KeepAliveInBackground = 60000;

        // 3. [YENİ] Veri gönderim sıklığını ayarla (RPC trafiğini rahatlatır)
        // Varsayılan değerler bazen çok sık veri yollar (saniyede 20-30 kez).
        // Bunu biraz düşürmek bant genişliğini rahatlatır.
        PhotonNetwork.SendRate = 20; // Saniyede 20 paket (Varsayılan 30 olabilir)
        PhotonNetwork.SerializationRate = 10; // OnPhotonSerializeView hızı

        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Server");
        PhotonNetwork.JoinLobby();
    }

    public override void OnLeftLobby()
    {
        Debug.Log("Left Lobby");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to join room");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to join random room");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to create room");
    }
}

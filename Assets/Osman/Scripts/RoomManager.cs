using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoomManager : MonoBehaviourPunCallbacks
{

    [Tooltip("Oda boş kaldığında ne kadar süre sonra otomatik olarak kapanacağını ayarlayan milisaniye cinsinden bir değerdir.")]
    [SerializeField] private int emptyRoomTtl = 0;

    [Tooltip("Bir oyuncu odadan ayrıldığında, o oyuncunun oyunla ilgili bilgileri (RPC çağrıları gibi) temizlenir.")]
    [SerializeField] private bool cleanupCacheOnLeave = true;


    [Tooltip("Oda aktif olacak mı?")]
    [SerializeField] bool isOpen = true;

    [Tooltip("Oda Gizlensin mi?")]
    [SerializeField] bool isVisible = true;
    private int maxPlayers = 4;

    void Start()
    {
        //Bu Scriptte oluşturduğum Fonksiyonları diğer scriptlerde çağırmak için bu fonksiyonları kullanabiliriz.
        EventDispatcher.RegisterFunction("CreateRoom", CreateRoom);
        EventDispatcher.RegisterFunction<string>("JoinRoom", JoinRoom);
        EventDispatcher.RegisterFunction<int>("JoinRandomRoomOrCreate", JoinRandomRoomOrCreate);
    }

    private void Update()
    {
      /*  if (PhotonNetwork.)
        {
            isOpen = false;
            isVisible = false;
        }*/
    }
    //Create Game butonuna bastığımızda çalışır. 
    //Burada ki Oda ayarları Oyuncular tarafından değiştirilebilir olması gerekir EKLENECEK.
    public void CreateRoom()
    {
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = maxPlayers;
        roomOptions.CleanupCacheOnLeave = cleanupCacheOnLeave;
        roomOptions.EmptyRoomTtl = emptyRoomTtl;
        roomOptions.IsOpen = isOpen;
        roomOptions.IsVisible = isVisible;
        PhotonNetwork.CreateRoom(PhotonNetwork.NickName, roomOptions, TypedLobby.Default);
        SceneChangeManager.Instance.ChangeScene("Table");
    }


    //Açılmış odalardan istediğimize tıkladığımız vakit O odaya gircektir.
    public void JoinRoom(string _roomName)
    {
        PhotonNetwork.JoinRoom(_roomName);
        SceneChangeManager.Instance.ChangeScene("Table");
    }


    //Join Game Butonuna bastığımızda eğer ki hiç oda kurulmamışsa oda oluşturacak kurulu odalar varsa rastgele birini seçecek ve ona katılacak
    public void JoinRandomRoomOrCreate(int roomCount)
    {
        Debug.Log(roomCount);
        if (roomCount > 0)
        {

            PhotonNetwork.JoinRandomRoom();
            SceneChangeManager.Instance.ChangeScene("Table");
        }
        else
        {
            EventDispatcher.SummonEvent("CreateRoom");
        }
    }



}

using UnityEngine;
using UnityEngine.Networking;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    // Taşın yerleştirildiğini diğer oyunculara bildir
    public void NotifyTilePlaced(string playerId, Vector3 position)
    {
        // Burada, taşın yerleştirildiği bilgiyi diğer oyunculara gönderecek kodu yazmalısınız.
        // Örneğin, Photon kullanıyorsanız, RPC (Remote Procedure Call) kullanabilirsiniz.
        Debug.Log($"Player {playerId} placed a tile at {position}");
    }
}
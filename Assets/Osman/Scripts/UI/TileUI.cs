using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class TileUI : MonoBehaviourPunCallbacks, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] Tiles tileDataInfo;
    [SerializeField] List<Tiles> playerTiles;
    public Transform middleTileContainer; // Orta taş havuzu
    public Transform rightTileContainer; // Sağ taş alanı
    public Transform leftTileContainer; // Sol taş alanı
    public Transform playerTileContainer; // Oyuncu taşı bölmesi
    public TileDistrubite tileDistrubite;
    private TurnManager turnManager;
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    string spritePath = "Sprites/Tiles";
    string spriteName;
    public Image tileImage;
    public float moveSpeed = 5f; // Hareket hızı ayarı
    public bool isIndicatorTile = false; // Gösterge taşı kontrolü
    [SerializeField] bool inMiddle = false;
    [SerializeField] bool inLeft = false;

    #region Awake ve Start
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        originalParent = transform.parent;
    }

    private void Start()
    {
        middleTileContainer = GameObject.Find("MiddleTileContainer").transform;
        rightTileContainer = GameObject.Find("RightTileContainer").transform;
        leftTileContainer = GameObject.FindWithTag("LeftTileContainer").transform;
        playerTileContainer = GameObject.Find("PlayerTileContainer").transform;
        if (gameObject.transform.parent == middleTileContainer)
        {
            inMiddle = true;
        }
        turnManager = GameObject.Find("TurnManager").GetComponent<TurnManager>();
        tileDistrubite = GameObject.Find("TileManager(Clone)").GetComponent<TileDistrubite>();
        Player player = PhotonNetwork.LocalPlayer;

        // Oyuncunun taş sayısını kontrol et
        playerTiles = null;

        // Oyuncunun hangi taş listesini kullandığını belirleyin
        int playerQueue = tileDistrubite.GetQueueNumberOfPlayer(player);
        switch (playerQueue)
        {
            case 1:
                playerTiles = tileDistrubite.playerTiles1;
                break;
            case 2:
                playerTiles = tileDistrubite.playerTiles2;
                break;
            case 3:
                playerTiles = tileDistrubite.playerTiles3;
                break;
            case 4:
                playerTiles = tileDistrubite.playerTiles4;
                break;
        }
    }
    #endregion

    #region Tile Set UI
    public void SetTileData(Tiles tileData)
    {
        if (tileData != null && tileImage != null)
        {
            tileDataInfo = tileData;
            // Sprite adını oluştur
            if (tileData.type == TileType.FakeJoker)
                spriteName = "FakeJoker";
            else
                spriteName = tileData.color.ToString() + "_" + tileData.number.ToString();

            // Sprite'ı Resources klasöründen yükle
            Sprite loadedSprite = Resources.Load<Sprite>($"{spritePath}/{spriteName}");

            // Eğer sprite başarıyla yüklendiyse, Image bileşenine ekle
            if (loadedSprite != null)
            {
                tileImage.sprite = loadedSprite;
            }
            else
            {
                Debug.LogError($"Sprite bulunamadı: {spritePath}/{spriteName}");
            }
        }
        else
        {
            Debug.LogError("TileData veya TileImage eksik!");
        }
    }
    #endregion

    #region Drag İşlemleri

    private (bool canDrop, bool canDraw) CanMoveTile()
    {
        if (isIndicatorTile)
        {
            Debug.LogWarning("Gösterge taşını hareket ettiremezsiniz!");
            return (false, false);
        }

        if (!turnManager.IsPlayerTurn())
        {
            if (gameObject.transform.parent == middleTileContainer)
            {
                Debug.LogWarning("Şu an taşı çekemezsiniz!");
                return (false, false);
            }
            else
                return (true, true);
        }
        else
        {
            if (gameObject.transform.parent == middleTileContainer || gameObject.transform.parent == leftTileContainer)
            {
                if (playerTiles.Count >= 15)
                {
                    Debug.LogWarning("Şu an taşı çekemezsiniz!");
                    return (false, false);
                }
                else
                    return (true, true);
            }
            else
                return (true, true); // Taş atılabilir, taş çekilebilir
        }
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDraw)
        {
            Debug.LogWarning("Taş çekilemez!"); // Hata ayıklama logu
            return; // Taş atılamıyorsa çık
        }
        if (gameObject.transform.parent.tag == "OtherSideTileContainer")
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!"); // Hata ayıklama logu
            return;
        }
        if (isIndicatorTile || gameObject.transform.parent == rightTileContainer)
        {

            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!"); // Hata ayıklama logu
            return;
        }

        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true);
    }
    public void OnDrag(PointerEventData eventData)
    {
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDrop)
        {
            Debug.LogWarning("Taş atılamaz!"); // Hata ayıklama logu
            return; // Taş atılamıyorsa çık
        }
        if (!canDraw)
        {
            Debug.LogWarning("Taş çekilemez!"); // Hata ayıklama logu
            return; // Taş atılamıyorsa çık
        }
        if (gameObject.transform.parent.tag == "OtherSideTileContainer")
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!"); // Hata ayıklama logu
            return;
        }

        // Taş çekilebilir durumda ise
        if (isIndicatorTile || gameObject.transform.parent == rightTileContainer)
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!"); // Hata ayıklama logu
            return;
        }
        transform.position = Input.mousePosition;

    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (gameObject.transform.parent.tag == "OtherSideTileContainer" || isIndicatorTile)
        {
            StartCoroutine(SmoothMove(transform, originalParent));
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!"); // Hata ayıklama logu
            return;
        }

        canvasGroup.blocksRaycasts = true;

        Transform parentContainer = playerTileContainer;
        Transform closestPlaceholder = null;
        float closestDistance = float.MaxValue;

        foreach (Transform placeholder in parentContainer)
        {
            if (placeholder.CompareTag("Placeholder"))
            {

                float distance = Vector3.Distance(placeholder.position, transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlaceholder = placeholder;
                }
            }
        }




        // Eğer en yakın placeholder boşsa ve taş oraya bırakılabiliyorsa
        if (closestPlaceholder != null && closestDistance < 25f)
        {
            if (turnManager.IsPlayerTurn() == true)
            {
                if (playerTiles.Count < 15)
                {
                    if (inMiddle == true)
                    {
                        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);

                        SetTileData(tileDistrubite.allTiles[0]);
                        tileDistrubite.photonView.RPC("AddTileFromMiddlePlayerList", RpcTarget.AllBuffered, queueValue);

                        StartCoroutine(SmoothMove(transform, closestPlaceholder));

                        inMiddle = false;
                        Debug.Log("Taş çekme işlemi gerçekleştirildi");
                    }
                    else if (gameObject.transform.parent == leftTileContainer)
                    {
                        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
                        tileDistrubite.photonView.RPC("AddTileFromMiddlePlayerList", RpcTarget.AllBuffered, queueValue);
                        StartCoroutine(SmoothMove(transform, closestPlaceholder));
                    }
                    else
                    {
                        if (closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight == true)
                        {
                            Debug.LogWarning("Şu an taş atamazsın 14 taşın var");
                            StartCoroutine(SmoothMove(transform, originalParent));
                            return;
                        }
                        else
                        {
                            StartCoroutine(SmoothMove(transform, closestPlaceholder));
                        }
                    }
                }
                else
                {
                    if (gameObject.transform.parent == middleTileContainer || gameObject.transform.parent == leftTileContainer)
                    {
                        Debug.LogWarning("Şu an taş çekemezsin 15 taşın var");
                        StartCoroutine(SmoothMove(transform, originalParent));
                        return;
                    }
                    else
                    {
                        if (closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight == true)
                        {
                            Debug.Log("Taşı attın sıra diğer oyuncuda");
                            NextTurnEvents();
                        }
                        else
                        {
                            StartCoroutine(SmoothMove(transform, closestPlaceholder));
                        }
                    }
                }
            }
            else
            {
                if (gameObject.transform.parent == middleTileContainer || gameObject.transform.parent == leftTileContainer)
                {
                    StartCoroutine(SmoothMove(transform, originalParent));
                    Debug.LogWarning("Sıra Sende değil taş çekemezsin");
                    return;
                }
                else
                {
                    if (closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight != true)
                        StartCoroutine(SmoothMove(transform, closestPlaceholder));
                    else
                    {
                        StartCoroutine(SmoothMove(transform, originalParent));
                        Debug.Log("Sıra sende değil Taş atamazsın");
                        return;
                    }
                }
            }
            int targetIndex = closestPlaceholder.GetSiblingIndex();

            if (closestPlaceholder.childCount > 1)
            {
                Transform displacedTile = closestPlaceholder.GetChild(0);

                if (targetIndex < originalParent.GetSiblingIndex())
                {
                    ShiftTilesLeft(playerTileContainer, displacedTile, targetIndex - 1);
                }
                else
                {
                    ShiftTilesRight(playerTileContainer, displacedTile, targetIndex + 1);
                }
            }
        }
        else
        {
            StartCoroutine(SmoothMove(transform, originalParent)); // Taşı orijinal konumuna geri döndür
        }
    }
    void NextTurnEvents()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        StartCoroutine(SmoothMove(transform, rightTileContainer)); // Taşı en yakın boş placeholder'a yerleştir
        int tileIndex = playerTiles.IndexOf(tileDataInfo);
        
        // RemoveTileFromPlayerList();
        tileDistrubite.photonView.RPC("RemoveTileFromPlayerList", RpcTarget.AllBuffered, queueValue, tileIndex);
        // Sıra diğer oyuncuya geçsin
        turnManager.photonView.RPC("NextTurn", RpcTarget.AllBuffered);
    }

    #endregion

    #region Shift Taşlar
    private void ShiftTilesRight(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        for (int i = startIndex; i < parentContainer.childCount; i++)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            // Eğer sağ alan boşsa, taş buraya taşınabilir
            if (currentPlaceholder == rightTileContainer && rightTileContainer.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, rightTileContainer));
                return;
            }

            if (currentPlaceholder.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                return;
            }
            else
            {
                Transform nextTileToShift = currentPlaceholder.GetChild(0);
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                tileToShift = nextTileToShift;
            }
        }
    }

    private void ShiftTilesLeft(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        for (int i = startIndex; i >= 0; i--)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            // Eğer sol alan doluysa, taş buraya taşınamaz
            /*
            if (currentPlaceholder == leftTileContainer)
            {
                Debug.LogWarning("Sol alana taş taşınamaz!");
                return;
            }*/

            if (currentPlaceholder.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                return;
            }
            else
            {
                Transform nextTileToShift = currentPlaceholder.GetChild(0);
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                tileToShift = nextTileToShift;
            }
        }
    }
    #endregion

    #region SmoothMove
    private IEnumerator SmoothMove(Transform tile, Transform targetPlaceholder)
    {
        Vector3 startPos = tile.position;
        Vector3 targetPos = targetPlaceholder.position;
        float elapsedTime = 0f;
        float journeyTime = 0.5f / moveSpeed;

        while (elapsedTime < journeyTime)
        {
            tile.position = Vector3.Lerp(startPos, targetPos, (elapsedTime / journeyTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        tile.SetParent(targetPlaceholder, false);
        tile.localPosition = Vector3.zero;
        tile.localScale = Vector3.one;
    }
    #endregion
}

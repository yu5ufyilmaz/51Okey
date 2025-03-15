using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class TileUI : MonoBehaviourPunCallbacks, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    #region Serialized Fields

    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private List<Tiles> playerTiles;
    [SerializeField] private Image tileImage;

    [SerializeField] private float moveSpeed = 5f; // Hareket hızı ayarı
    #endregion

    #region Private Fields
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    private TurnManager turnManager;
    private TileDistrubite tileDistrubite;

    // Container Transformları
    public Transform middleTileContainer; // Orta taş havuzu
    public Transform rightTileContainer; // Sağ taş alanı
    public Transform leftTileContainer; // Sol taş alanı
    public Transform playerTileContainer; // Oyuncu taşı bölmesi
    private Transform playerMeldContainer;

    public int tileRow;
    public int tileColumn;

    // Durum Değişkenleri
    public Tiles tileDataInfo;
    public int tilePlaceInt;
    private string spritePath = "Sprites/Tiles";
    private string spriteName;
    private bool inMiddle = false;
    private bool fromLeftContainer = false;
    public bool isIndicatorTile = false;
    #endregion
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
        playerMeldContainer = GameObject.Find("PlayerMeldContainer").transform;
        CheckPlace();
        if (gameObject.transform.parent == middleTileContainer)
        {
            inMiddle = true;
        }
        turnManager = GameObject.Find("TurnManager").GetComponent<TurnManager>();
        tileDistrubite = GameObject.Find("TileManager(Clone)").GetComponent<TileDistrubite>();
        scoreManager = GameObject.Find("ScoreManager(Clone)").GetComponent<ScoreManager>();
        playerTiles = null;
        playerTiles = tileDistrubite.GetPlayerTiles();
        tileDistrubite.RegisterTileUI(this);
        scoreManager.CheckForPer();
    }
    void CheckPlace()
    {
        if (transform.parent.parent == playerTileContainer)
        {
            tilePlaceInt = transform.parent.GetSiblingIndex();
        }
        else
        {

            Debug.LogWarning("Current transform is not a child of playerTileContainer");
        }
    }

    public void CheckRowColoumn(int rowIndex, int columnIndex)
    {
        tileRow = rowIndex;
        tileColumn = columnIndex;
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
            else if (tileData.type == TileType.Joker)
                spriteName = "Empty";
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

    #region DRAG_HANDLERS

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
    #region On Begin Drag
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
        if (gameObject.transform.parent == leftTileContainer)
        {
            fromLeftContainer = true;
        }
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true);
    }
    #endregion
    #region On Drag
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
    #endregion
    #region On End Drag
    public void OnEndDrag(PointerEventData eventData)
    {
        if (gameObject.transform.parent.tag == "OtherSideTileContainer" || isIndicatorTile)
        {
            StartCoroutine(SmoothMove(transform, originalParent));
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!"); // Hata ayıklama logu
            return;
        }
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
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
        if (closestPlaceholder != null && closestDistance < 40f)
        {
            if (turnManager.IsPlayerTurn() == true)
            {
                if (turnManager.canDrop == false)
                {
                    if (inMiddle == true)
                    {
                        SetTileData(tileDistrubite.allTiles[0]);
                        tileDistrubite.photonView.RPC("AddTileFromMiddlePlayerList", RpcTarget.AllBuffered, queueValue);
                        turnManager.canDrop = true;
                        StartCoroutine(SmoothMove(transform, closestPlaceholder));

                        inMiddle = false;

                        Debug.Log("Taş çekme işlemi gerçekleştirildi");
                    }
                    else if (fromLeftContainer == true)
                    {
                        //PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
                        StartCoroutine(SmoothMove(transform, closestPlaceholder));

                        Debug.Log("Soldan taş çekme işlemi gerçekleştirildi");
                        tileDistrubite.photonView.RPC("AddTileFromDropPlayerList", RpcTarget.AllBuffered, queueValue);
                        turnManager.canDrop = true;
                        tileDistrubite.dropTile = this.tileDataInfo;
                        fromLeftContainer = false;
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
                    if (gameObject.transform.parent == middleTileContainer || fromLeftContainer == true)
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
                        else if (closestPlaceholder.gameObject.GetComponent<Placeholder>().available == true)
                        {
                            if (closestPlaceholder.gameObject.GetComponent<Placeholder>().AvailableTileInfo == tileDataInfo)
                            {
                                StartCoroutine(SmoothMove(transform, closestPlaceholder));
                                ActiveStoneEvents();

                            }
                            else
                            {
                                Debug.Log("Yanlış taşı işlemeye çalışıyrosun");
                            }
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
                    {
                        StartCoroutine(SmoothMove(transform, closestPlaceholder));
                    }
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
                if (closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight == false)
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
        }
        else
        {
            StartCoroutine(SmoothMove(transform, originalParent)); // Taşı orijinal konumuna geri döndür
        }
    }
    #endregion
    void NextTurnEvents()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        StartCoroutine(SmoothMove(transform, rightTileContainer)); // Taşı en yakın boş placeholder'a yerleştir
        int tileIndex = playerTiles.IndexOf(tileDataInfo);

        tileDistrubite.photonView.RPC("RemoveTileFromPlayerList", RpcTarget.AllBuffered, queueValue, tileIndex);

        scoreManager.RemoveMeldedTiles();
        tileDistrubite.photonView.RPC("CheckForAvailableTiles", RpcTarget.AllBuffered, queueValue);
        Destroy(gameObject);

        turnManager.canDrop = false;
        // Sıra diğer oyuncuya geçsin

        turnManager.photonView.RPC("NextTurn", RpcTarget.AllBuffered);

    }
    void ActiveStoneEvents()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int tileIndex = playerTiles.IndexOf(tileDataInfo);
        tileDistrubite.photonView.RPC("RemoveActiveTileFromPlayerList", RpcTarget.AllBuffered, queueValue, tileIndex);
        Debug.Log("Taşı aktif edildi");
        Destroy(gameObject);

    }
    #endregion

    #region Shift_Tiles
    private void ShiftTilesRight(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        if (gameObject.transform.parent == middleTileContainer) return;
        if (gameObject.transform.parent.tag == "MeldPlaceholder") return;
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
        if (gameObject.transform.parent == middleTileContainer) return;
        if (gameObject.transform.parent.tag == "MeldPlaceholder") return;
        for (int i = startIndex; i >= 0; i--)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);
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
        CheckPlace();
        scoreManager.CheckForPer();
        tile.localPosition = Vector3.zero;
        tile.localScale = Vector3.one;
    }
    #endregion
}

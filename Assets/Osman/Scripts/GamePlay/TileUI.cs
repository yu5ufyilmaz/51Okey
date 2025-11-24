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

    [SerializeField]
    private ScoreManager scoreManager;

    [SerializeField]
    private List<Tiles> playerTiles;

    [SerializeField]
    private Image tileImage;

    [SerializeField]
    private float moveSpeed = 5f; // Hareket hızı ayarı
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
        playerMeldContainer = GameObject.Find("TileMeldPlace").transform;
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
            if (
                gameObject.transform.parent == middleTileContainer
                || gameObject.transform.parent == leftTileContainer
            )
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
        // 1. Hareket İzni Kontrolü (TurnManager vb.)
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDraw)
        {
            Debug.LogWarning("Bu taşı şu an hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }

        // 2. Rakip Alan ve Gösterge Kontrolü
        if (
            gameObject.transform.parent.tag == "OtherSideTileContainer"
            || isIndicatorTile
            || gameObject.transform.parent == rightTileContainer
        )
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }

        // --- 3. KRİTİK DÜZELTME: MASADAKİ (MELD) TAŞLARI KİLİTLEME ---

        Transform currentParent = transform.parent; // Taşın içinde olduğu Placeholder
        Transform grandParent = currentParent.parent; // Placeholder'ın bağlı olduğu Container

        // Eğer taşın "Büyük Babası" (GrandParent) Istaka (PlayerTileContainer) DEĞİLSE...
        // (Yani taş ıstakada durmuyorsa)
        if (grandParent != playerTileContainer)
        {
            // Ve taş Orta, Sol veya Sağ atma alanında da değilse...
            // (Yani çekilebilir bir taş da değilse)
            if (
                currentParent != middleTileContainer
                && currentParent != leftTileContainer
                && currentParent != rightTileContainer
            )
            {
                // O ZAMAN BU TAŞ MASAYA AÇILMIŞ BİR TAŞTIR! DOKUNMA!
                Debug.LogWarning("Masaya işlenmiş taşları hareket ettiremezsiniz!");
                eventData.pointerDrag = null; // Sürüklemeyi anında iptal et
                return;
            }
        }
        // --------------------------------------------------------------

        // 4. Sol Konteyner Kontrolü
        if (gameObject.transform.parent == leftTileContainer)
        {
            fromLeftContainer = true;
        }

        // --- SÜRÜKLEME BAŞLATILIYOR ---
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
                        tileDistrubite.photonView.RPC(
                            "AddTileFromMiddlePlayerList",
                            RpcTarget.AllBuffered,
                            queueValue
                        );
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
                        tileDistrubite.photonView.RPC(
                            "AddTileFromDropPlayerList",
                            RpcTarget.AllBuffered,
                            queueValue
                        );
                        turnManager.canDrop = true;
                        tileDistrubite.dropTile = this.tileDataInfo;
                        fromLeftContainer = false;
                    }
                    else
                    {
                        if (
                            closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight
                            == true
                        )
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
                    if (
                        gameObject.transform.parent == middleTileContainer
                        || fromLeftContainer == true
                    )
                    {
                        Debug.LogWarning("Şu an taş çekemezsin 15 taşın var");
                        StartCoroutine(SmoothMove(transform, originalParent));
                        return;
                    }
                    else
                    {
                        if (
                            closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight
                            == true
                        )
                        {
                            Debug.Log("Taşı attın sıra diğer oyuncuda");
                            NextTurnEvents();
                        }
                        else if (
                            closestPlaceholder.gameObject.GetComponent<Placeholder>().available
                            == true
                        )
                        {
                            if (
                                closestPlaceholder
                                    .gameObject.GetComponent<Placeholder>()
                                    .AvailableTileInfo == tileDataInfo
                            )
                            {
                                StartCoroutine(SmoothMove(transform, closestPlaceholder));
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
                if (
                    gameObject.transform.parent == middleTileContainer
                    || gameObject.transform.parent == leftTileContainer
                )
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

            // ... (Buradaki sıra kontrolü, taş atma, taş çekme kodların AYNI KALSIN) ...

            // ---------------------------------------------------------------
            // --- KAYDIRMA (SHIFT) MANTIĞI - DÜZELTME BURADA ---
            // ---------------------------------------------------------------

            // KRİTİK KONTROL: Kaydırma işlemi SADECE hedef yer "Oyuncunun Istakası" ise yapılmalı.
            // Eğer hedef yer masadaki meld alanı, orta veya çöp ise KAYDIRMA YAPMA.
            if (closestPlaceholder.parent == playerTileContainer)
            {
                // Eğer o kutuda zaten bir taş varsa (yani childCount > 1 olduysa, çünkü biz de oraya gittik)
                // Not: Drop işlemi gerçekleştiyse childCount 2 olabilir (eski taş + yeni taş)
                if (closestPlaceholder.childCount > 1)
                {
                    if (closestPlaceholder.parent != playerTileContainer)
                    {
                        Debug.LogWarning(
                            "Masadaki taşların üzerine taş koyamazsın veya kaydıramazsın!"
                        );
                        StartCoroutine(SmoothMove(transform, originalParent)); // Taşı eski yerine yolla
                        return; // Çıkış
                    }
                    if (closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight == false)
                    {
                        // Kaydırılacak olan "eski" taş (ilk çocuk)
                        Transform displacedTile = closestPlaceholder.GetChild(0);

                        // Eğer yeni gelen taş (transform), eskisinden sonraysa (sağdan geldiyse) -> Sola kaydır
                        // Eğer yeni gelen taş, eskisinden önceyse (soldan geldiyse) -> Sağa kaydır
                        // (Not: originalParent mantığına göre yönü belirliyoruz)

                        if (targetIndex < originalParent.GetSiblingIndex())
                        {
                            ShiftTilesRight(playerTileContainer, displacedTile, targetIndex + 1);
                        }
                        else
                        {
                            ShiftTilesLeft(playerTileContainer, displacedTile, targetIndex - 1);
                        }
                    }
                }
            }
            // ---------------------------------------------------------------
        }
        else
        {
            StartCoroutine(SmoothMove(transform, originalParent));
        }
    }
    #endregion
    // TileUI.cs -> NextTurnEvents() metodunun güncel hali

    void NextTurnEvents()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        StartCoroutine(SmoothMove(transform, rightTileContainer));
        int tileIndex = playerTiles.IndexOf(tileDataInfo);

        tileDistrubite.photonView.RPC(
            "RemoveTileFromPlayerList",
            RpcTarget.AllBuffered,
            queueValue,
            tileIndex
        );

        // ESKİ ÇAĞRIYI YORUMA AL:
        tileDistrubite.photonView.RPC("CheckForAvailableTiles", RpcTarget.AllBuffered, queueValue);
        scoreManager.CommitAndStoreMelds();
        scoreManager.CommitJokerTransactions();
        // YENİ, DAHA GÜÇLÜ ÇAĞRI:
        List<ActiveTilePlacementInfo> placements =
            scoreManager.GetAndClearPendingActivePlacements();
        if (placements != null && placements.Count > 0)
        {
            // --- YENİ EKLENEN KOD BAŞLANGICI ---
            // İşlenen her bir taşı, işleyen oyuncunun (yani mevcut oyuncunun)
            // veri listesinden silmek için RPC çağır.
            int playerQue = (int)queueValue;
            foreach (var placement in placements)
            {
                // Bu RPC, TileDistrubite.cs içinde zaten mevcut ve doğru çalışıyor.
                // Onu burada çağırmamız yeterli.
                tileDistrubite.photonView.RPC(
                    "RemoveActiveTileFromPlayerList",
                    RpcTarget.AllBuffered,
                    playerQue,
                    placement.tileData
                );
            }
            // --- YENİ EKLENEN KOD SONU ---

            // TileDistrubite'a bu yerleşimleri tüm client'larda oluşturması için RPC gönder
            tileDistrubite.photonView.RPC(
                "InstantiateActiveTiles",
                RpcTarget.AllBuffered,
                placements.ToArray()
            );
        }

        turnManager.canDrop = false;
        turnManager.photonView.RPC("NextTurn", RpcTarget.AllBuffered);
        Destroy(gameObject);
    }

    #endregion

    #region Shift_Tiles
    private void ShiftTilesRight(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        if (parentContainer != playerTileContainer)
            return;
        if (gameObject.transform.parent == middleTileContainer)
            return;
        if (gameObject.transform.parent.tag == "MeldPlaceholder")
            return;

        TileUI tileUI = tileToShift.GetComponent<TileUI>();
        if (tileUI != null && tileUI.isIndicatorTile)
        {
            Debug.LogWarning("Gösterge taşını kaydıramazsın!");
            return; // Metottan çık, kaydırma yapma.
        }
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
        if (parentContainer != playerTileContainer)
        {
            return;
        }
        if (gameObject.transform.parent == middleTileContainer)
            return;
        if (gameObject.transform.parent.tag == "MeldPlaceholder")
            return;
        TileUI tileUI = tileToShift.GetComponent<TileUI>();
        if (tileUI != null && tileUI.isIndicatorTile)
        {
            Debug.LogWarning("Gösterge taşını kaydıramazsın!");
            return; // Metottan çık, kaydırma yapma.
        }
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

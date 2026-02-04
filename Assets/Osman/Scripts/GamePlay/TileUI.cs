using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SocialPlatforms.Impl;
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
    private Coroutine currentMoveCoroutine;

    private Vector3 _dragOffset; // Tıkladığın yer ile taşın merkezi arasındaki fark
    private bool _isAnimating = false; // OPTİMİZASYON: Sadece hareket varken true olur
    private Vector3 _currentVelocity; // SmoothDamp için
    private float _targetRotationZ;

    [Header("Game Feel")]
    [SerializeField]
    private float dragDamping = 0.01f; // Yumuşak takip (0.03 - 0.05 ideal)

    [SerializeField]
    private float slideDuration = 0.3f;

    [SerializeField]
    private float tiltAmount = 0.8f; // Eğilme katsayısı

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

    [Header("Scaling Settings")]
    private Vector3 _originalScale; // Taşın ilk boyutu
    private RectTransform _parentRect; // Referans düzlemimiz
    private Vector3 _targetScale; // Olması gereken boyut
    public float scaleSpeed = 15f; // Büyüme/Küçülme hızı
    private List<Placeholder> allMeldPlaceholders = new List<Placeholder>();

    #region YENİ GÖRSEL AYARLAR (Burayı ekle)
    [Header("Game Feel - Hissiyat Ayarları")]
    [SerializeField]
    private float tiltStrength = 4f; // Sürüklerken ne kadar eğilsin?

    [SerializeField]
    private float tiltSpeed = 10f; // Eğilme hızı

    [SerializeField]
    private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Kayma eğrisi

    // Drag Hesaplamaları için private değişkenler
    private Quaternion _defaultRotation; // Orijinal rotasyon
    #endregion
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
        FitToParent();
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
        Placeholder[] allPh = FindObjectsOfType<Placeholder>();

        // Sadece 'isMeldArea' kutucuğu işaretli olanları listemize ekle
        foreach (Placeholder ph in allPh)
        {
            if (ph.isMeldArea)
            {
                allMeldPlaceholders.Add(ph);
            }
        }
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

    // TileUI.cs içine eklenecek TEK VE SON metod:

    // TileUI.cs içine:

    public void FitToParent()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            // 1. Pivotu Ortala
            rt.pivot = new Vector2(0.5f, 0.5f);

            // 2. Anchors'ı "Stretch" (Tam Kapla) yap
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;

            // 3. SIFIR NOKTASI: Kenar boşluklarını ve boyutu sıfırla
            // (Ebeveyn ne kadarsa o kadar ol demektir)
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 4. SCALE ZORLAMASI: En önemli kısım burası!
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
        }
    }
    #endregion

    #region DRAG_HANDLERS
    void Update()
    {
        // OPTİMİZASYON KİLİDİ: Eğer taş sürüklenmiyorsa VE boyutu/yeri oturmuşsa çalışma!
        if (!_isAnimating && canvasGroup.blocksRaycasts)
            return;

        bool isMoving = false; // Hala hareket var mı kontrolü

        // 1. SCALE ANIMASYONU
        if (Vector3.Distance(transform.localScale, _targetScale) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                _targetScale,
                Time.deltaTime * scaleSpeed
            );
            isMoving = true;
        }
        else
        {
            transform.localScale = _targetScale; // Küsuratı temizle
        }

        // 2. TILT (EĞİLME) EFEKTİ (Sadece sürüklerken)
        if (!canvasGroup.blocksRaycasts) // Sürükleniyor demektir
        {
            // Hedef rotasyona yumuşak geçiş
            Quaternion targetRot = Quaternion.Euler(0, 0, _targetRotationZ);
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * 15f
            );
            isMoving = true;
        }
        else if (transform.rotation != Quaternion.identity) // Sürükleme bitti, düzeliyor
        {
            // Eski haline (düz) dön
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.identity,
                Time.deltaTime * 15f
            );

            // Eğer çok yaklaştıysa tam düzelt ve işlemi bitir
            if (Quaternion.Angle(transform.rotation, Quaternion.identity) < 0.5f)
            {
                transform.rotation = Quaternion.identity;
            }
            else
            {
                isMoving = true; // Hala düzelmeye çalışıyor
            }
        }

        // Eğer hiçbir hareket kalmadıysa Update döngüsünü boşa çalıştırma
        _isAnimating = isMoving;
    }

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
    #region On Begin Drag
    #region On Begin Drag
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentMoveCoroutine != null)
        {
            StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = null;
        }
        _targetScale = _originalScale;

        // --- 1. SOL TARAFTAN ÇEKME KONTROLÜ ---
        if (transform.parent == leftTileContainer)
        {
            // A) SIRA SENDE Mİ?
            if (!turnManager.IsPlayerTurn())
            {
                Debug.LogWarning("Sıra sende değil! Yandaki taşa dokunamazsın.");
                eventData.pointerDrag = null;
                return;
            }

            // B) ZATEN TAŞ ÇEKTİN Mİ? (canDrop true ise çekmişsindir)
            if (turnManager.canDrop == true)
            {
                Debug.LogWarning("Zaten taş çektiniz! Yandan alamazsınız.");
                eventData.pointerDrag = null;
                return;
            }

            // C) AÇTIN MI / İŞLEDİN Mİ? (Yandan almak da bir çekme işlemidir)
            if (turnManager.hasOpenedThisTurn || turnManager.hasProcessedThisTurn)
            {
                Debug.LogWarning("El açtınız veya işlediniz, yandan taş alamazsınız!");
                eventData.pointerDrag = null;
                return;
            }

            // Şartlar uygunsa işaretle
            fromLeftContainer = true;
        }
        // --- 2. ORTA TAŞLARDAN ÇEKME KONTROLÜ (DÜZELTİLDİ) ---
        else if (
            transform.parent == middleTileContainer
            || transform.parent.CompareTag("MiddleTileContainer")
        )
        {
            // A) SIRA SENDE Mİ?
            if (!turnManager.IsPlayerTurn())
            {
                Debug.LogWarning("Sıra sende değil!");
                eventData.pointerDrag = null;
                return;
            }

            // B) KURAL İHLALİ KONTROLLERİ
            // - Zaten taş çektiyse (canDrop = true) -> DÜZELTİLEN KISIM BURASI
            // - Bu tur elini açtıysa (hasOpenedThisTurn)
            // - Bu tur yere taş işlediyse (hasProcessedThisTurn)
            if (
                turnManager.canDrop
                || turnManager.hasOpenedThisTurn
                || turnManager.hasProcessedThisTurn
            )
            {
                Debug.LogWarning(
                    "Hamle yaptınız (Çektiniz/Açtınız/İşlediniz), tekrar taş çekemezsiniz!"
                );
                eventData.pointerDrag = null;
                return;
            }

            // C) EL KAPASİTESİ KONTROLÜ (15 taş kuralı)
            if (playerTiles.Count >= 15)
            {
                Debug.LogWarning("Eliniz dolu (15 Taş), yeni taş çekemezsiniz!");
                eventData.pointerDrag = null;
                return;
            }
        }

        // --- 3. GENEL HAREKET İZNİ (CanMoveTile) ---
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDraw)
        {
            Debug.LogWarning("Bu taşı şu an hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }

        // --- 4. YASAKLI ALANLAR ---
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

        // --- 5. MASADAKİ (MELD) TAŞLARI KİLİTLEME ---
        Transform currentParent = transform.parent;
        Transform grandParent = currentParent.parent;

        if (
            grandParent != playerTileContainer
            && currentParent != middleTileContainer
            && currentParent != leftTileContainer
            && currentParent != rightTileContainer
        )
        {
            Debug.LogWarning("Masaya işlenmiş taşları hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }
        _parentRect = transform.parent.GetComponent<RectTransform>();

        Vector3 worldPoint;
        // Tıklanan ekran noktasını, kutucuğun dünyadaki düzlemine çevir
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            _parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out worldPoint
        );

        // Aradaki farkı kaydet (Offset)
        _dragOffset = transform.position - worldPoint;

        // Diğer ayarlar
        _isAnimating = true;
        _currentVelocity = Vector3.zero;
        _targetRotationZ = 0f;
        // --- SÜRÜKLEME BAŞLATILIYOR ---
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true);
        _currentVelocity = Vector3.zero; // Hızı sıfırla
        _targetRotationZ = 0f;
    }
    #endregion
    #endregion
    #endregion
    #region On Drag
    [Header("Scaling Settings")]
    [SerializeField]
    private Vector3 shrinkScale = new Vector3(0.3f, 0.3f, 0.3f); // Küçülme boyutu

    public void OnDrag(PointerEventData eventData)
    {
        // --- 1. GÜVENLİK KONTROLLERİ (SENİN ORİJİNAL KODLARIN) ---
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDrop)
        {
            Debug.LogWarning("Taş atılamaz!");
            return;
        }
        if (!canDraw)
        {
            Debug.LogWarning("Taş çekilemez!");
            return;
        }
        if (gameObject.transform.parent.tag == "OtherSideTileContainer")
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!");
            return;
        }

        if (isIndicatorTile || gameObject.transform.parent == rightTileContainer)
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!");
            return;
        }
        // --- HAREKET ---
        Vector3 globalMousePos;

        // Yine parentRect referansına göre hesapla (Taş hareket etse de parent sabit!)
        if (
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out globalMousePos
            )
        )
        {
            // Hedef = Mouse'un Gerçek Yeri + İlk Tuttuğumuz Fark
            Vector3 targetPos = globalMousePos + _dragOffset;

            // Smooth Takip
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref _currentVelocity,
                dragDamping
            );

            // Tilt (Eğilme) Efekti
            float deltaX = eventData.delta.x;
            _targetRotationZ = Mathf.Clamp(-deltaX * tiltAmount, -10f, 10f);
        }

        _isAnimating = true;

        // --- SCALING MANTIĞI ---
        // Artık hem senin hem rakiplerin meld alanlarını algılar
        Placeholder visualTarget = GetVisualClosestPlaceholder(100f);

        if (visualTarget != null)
        {
            // Mesafe içindeyiz, küçül
            if (_targetScale != shrinkScale)
            {
                _targetScale = shrinkScale;
            }
        }
        else
        {
            // Mesafe dışındayız, büyü
            if (_targetScale != _originalScale)
            {
                _targetScale = _originalScale;
            }
        }
    }
    #endregion
    #region On End Drag
    [Header("Drag Settings")]
    [SerializeField]
    private float dropDistanceThreshold = 100f;

    public void OnEndDrag(PointerEventData eventData)
    {
        // --- 1. TEMEL GÜVENLİK KONTROLLERİ ---
        if (transform.parent.CompareTag("OtherSideTileContainer") || isIndicatorTile)
        {
            _targetScale = _originalScale;
            StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }
        _targetRotationZ = 0f;
        _isAnimating = true;
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int localQueInt = (int)queueValue;
        canvasGroup.blocksRaycasts = true;

        // --- 2. HEDEF TESPİTİ ---
        Placeholder bestTarget = null;
        float closestDistance = float.MaxValue;

        // A) Kendi Istakamız
        foreach (Transform t in playerTileContainer)
        {
            float dist = Vector2.Distance(t.position, transform.position);
            if (dist < 100f && dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = t.GetComponent<Placeholder>();
            }
        }

        // B) Masa / Atma Alanı
        if (bestTarget == null)
        {
            Placeholder[] allPlaceholders = FindObjectsOfType<Placeholder>();
            foreach (Placeholder ph in allPlaceholders)
            {
                if (ph.isMeldArea || ph.isRight)
                {
                    float dist = Vector2.Distance(ph.transform.position, transform.position);
                    if (dist < 100f && dist < closestDistance)
                    {
                        closestDistance = dist;
                        bestTarget = ph;
                    }
                }
            }
        }

        // Hedef yoksa eve dön
        if (bestTarget == null)
        {
            _targetScale = _originalScale;
            StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }

        // --- 3. AKSİYONLAR ---

        // DURUM A: TAŞ ATMA (SAĞ TARAF)
        if (bestTarget.isRight)
        {
            if (turnManager.IsPlayerTurn() && turnManager.canDrop)
            {
                if (!turnManager.CanFinishTurn())
                {
                    Debug.LogWarning("KURAL HATASI: Açmadan bitiremezsiniz.");
                    GameManager.Instance.HandleFailedSidePick(this.tileDataInfo);
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, originalParent));
                    return;
                }

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.photonView.RPC(
                        "CheckPenaltyRPC",
                        RpcTarget.MasterClient,
                        localQueInt,
                        this.tileDataInfo
                    );
                }
                ExecuteNextTurn();
            }
            else
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
            }
        }
        // DURUM B: KENDİ ISTAKAMIZ (SWAP MANTIĞI BURADA)
        else if (bestTarget.transform.parent == playerTileContainer)
        {
            // -- Taş Çekme Kontrolleri (Aynen Kalsın) --
            if (turnManager.IsPlayerTurn() && !turnManager.canDrop)
            {
                if (inMiddle)
                {
                    SetTileData(tileDistrubite.allTiles[0]);
                    tileDistrubite.photonView.RPC(
                        "AddTileFromMiddlePlayerList",
                        RpcTarget.AllBuffered,
                        localQueInt
                    );
                    turnManager.canDrop = true;
                    inMiddle = false;
                }
                else if (fromLeftContainer)
                {
                    turnManager.hasPickedFromSide = true;
                    EventDispatcher.SummonEvent("OnSideTilePicked", this.tileDataInfo);
                    tileDistrubite.photonView.RPC(
                        "AddTileFromDropPlayerList",
                        RpcTarget.AllBuffered,
                        localQueInt
                    );
                    turnManager.canDrop = true;
                    fromLeftContainer = false;
                }
            }

            // --- HİBRİT YERLEŞTİRME MANTIĞI ---

            // Hedef kutu doluysa ve orası benim eski yerim değilse bir aksiyon lazım
            if (
                bestTarget.transform.childCount > 0
                && bestTarget != originalParent.GetComponent<Placeholder>()
            )
            {
                int targetIndex = bestTarget.transform.GetSiblingIndex();

                // Farenin/Parmağın taşın neresinde olduğuna bakıyoruz (Local fark)
                // Eğer taşı kutunun soluna yakın bıraktıysak (fark < 0), sağa itmeye çalış.
                // Eğer taşı kutunun sağına yakın bıraktıysak (fark > 0), sola itmeye çalış.
                float differenceX = transform.position.x - bestTarget.transform.position.x;

                bool shiftSuccess = false;

                if (differenceX < 0)
                {
                    // Sola yakın bıraktım -> Mevcut taşı SAĞA ötele
                    shiftSuccess = TryShiftRight(playerTileContainer, targetIndex);
                }
                else
                {
                    // Sağa yakın bıraktım -> Mevcut taşı SOLA ötele
                    shiftSuccess = TryShiftLeft(playerTileContainer, targetIndex);
                }

                // --- SWAP (FALLBACK) ---
                // Eğer kaydırma başarısız olduysa (yer yoksa), eski usül SWAP yap.
                if (!shiftSuccess)
                {
                    Transform residentTile = bestTarget.transform.GetChild(0);
                    TileUI residentUI = residentTile.GetComponent<TileUI>();
                    if (residentUI != null)
                    {
                        // Kiracıyı benim eski yerime gönder
                        residentUI.StartCoroutine(
                            residentUI.SmoothMove(residentTile, originalParent)
                        );
                    }
                }
            }

            // Ben her türlü o kutuya gidiyorum (Ya boşaldı, ya da takas ettik)
            _targetScale = _originalScale;
            StartCoroutine(SmoothMove(transform, bestTarget.transform));
        }
        // DURUM C: MASAYA İŞLEME (Aynı kalıyor)
        else
        {
            if (
                !turnManager.IsPlayerTurn()
                || !turnManager.canDrop
                || (!scoreManager.hasOpenedSeries && !scoreManager.hasOpenedPairs)
            )
            {
                if (!turnManager.canDrop)
                    Debug.LogWarning("Önce taş çekmelisiniz!");
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
                return;
            }

            bool success = scoreManager.ProcessManualDrop(
                this.tileDataInfo,
                bestTarget.transform,
                localQueInt
            );

            if (success)
                gameObject.SetActive(false);
            else
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
            }
        }
    }
    #endregion
    #endregion

    #region Next Turn Event
    // 1. BU METODU ÇAĞIRACAKSIN (Eski NextTurnEvents yerine)
    private void ExecuteNextTurn()
    {
        if (GameManager.Instance.scoreManager != null)
        {
            PenaltySystem.Instance.CommitAllTurnPenalties();
            // Tur boyunca yaptığım işlemelerden doğan cezaları şimdi sunucuya gönderiyorum.
            GameManager.Instance.scoreManager.CommitFinalTableLimit();
        }
        // Coroutine başlatıyoruz ki işlemleri zamana yayabilelim
        StartCoroutine(NextTurnRoutine());
    }

    // TileUI.cs içindeki NextTurnRoutine metodunu bununla değiştir:

    private IEnumerator NextTurnRoutine()
    {
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;

        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int playerQueInt = (int)queueValue;

        // 1. ELDEN TAŞ DÜŞME MANTIĞI
        List<Tiles> realTimePlayerTiles = tileDistrubite.GetPlayerTiles();

        // Listeyi kopyalıyoruz ki orijinal veri bozulmadan simülasyon yapabilelim
        List<Tiles> currentHandForCheck = new List<Tiles>(realTimePlayerTiles);

        // --- [DÜZELTME 1] ID KONTROLÜ İLE TAŞI BUL ---
        // Eski yöntem (Renk/Sayı) yerine doğrudan benzersiz ID ile arıyoruz.
        var tileThrown = currentHandForCheck.FirstOrDefault(t => t.id == tileDataInfo.id);

        if (tileThrown != null)
        {
            currentHandForCheck.Remove(tileThrown);
        }
        else
        {
            Debug.LogError(
                $"[NextTurnRoutine] HATA: Atılan taş ID ({tileDataInfo.id}) listede bulunamadı!"
            );
        }

        // Yeni açılan perleri (henüz sunucuya gitmemiş olanları) de simülasyon listesinden düş
        if (scoreManager != null)
        {
            List<Tiles> pendingTiles = scoreManager.GetAllTilesPendingCommit();
            foreach (var pt in pendingTiles)
            {
                // Not: Perdeki taşlar için de ID kontrolü yapmak en sağlıklısıdır,
                // ancak ScoreManager yapına göre burada ID eşleşmesi yoksa şimdilik değer kontrolü kalabilir.
                // Eğer pendingTiles içindeki taşların ID'leri korunuyorsa burayı da ID'ye çevirebilirsin.
                var it = currentHandForCheck.FirstOrDefault(t =>
                    t.color == pt.color && t.number == pt.number && t.type == pt.type
                );
                // EĞER ID SİSTEMİN ScoreManager'DA TAM OTURDUYSA YUKARIDAKİ YERİNE ŞUNU KULLAN:
                // var it = currentHandForCheck.FirstOrDefault(t => t.id == pt.id);

                if (it != null)
                    currentHandForCheck.Remove(it);
            }
        }

        // El tamamen bitti mi? (0 taş kaldıysa bitmiştir)
        bool isGameReallyOver = (currentHandForCheck.Count == 0);

        // GameManager'a Bildir
        if (tileDistrubite != null)
        {
            HandData data = new HandData();
            data.actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            // Eğer oyun bittiyse boş el gönder, bitmediyse göstermelik bir taş koy (kontrol için)
            data.handTiles = isGameReallyOver
                ? new List<Tiles>()
                : new List<Tiles> { new Tiles(TileColor.black, 1, TileType.Number) };

            EventDispatcher.SummonEvent("OnPlayerMoveFinished", data);
        }

        if (isGameReallyOver)
        {
            Destroy(gameObject);
            yield break;
        }

        // --- GÖRSEL HAREKET ---
        StartCoroutine(SmoothMove(transform, rightTileContainer));
        yield return new WaitForSeconds(0.05f);

        // --- [DÜZELTME 2] VERİTABANINDAN SİLME (ATILAN TAŞ) ---
        // TileDistrubite üzerindeki gerçek listeden hangi indeksi sileceğimizi ID ile buluyoruz.
        int indexToRemove = -1;
        List<Tiles> actualPlayerTiles = tileDistrubite.GetPlayerTiles();

        for (int i = 0; i < actualPlayerTiles.Count; i++)
        {
            // ESKİ KOD: Renk ve Sayı kontrolü (Kimlik karmaşası yaratıyordu)
            // YENİ KOD: Sadece ID kontrolü
            if (actualPlayerTiles[i].id == tileDataInfo.id)
            {
                indexToRemove = i;
                break; // Doğru taşı bulduk, döngüden çık
            }
        }

        if (indexToRemove != -1)
        {
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerList",
                RpcTarget.All, // Herkeste silinsin
                playerQueInt,
                indexToRemove
            );
        }
        else
        {
            Debug.LogError(
                $"[KRİTİK HATA] Silinecek taşın ID'si ({tileDataInfo.id}) gerçek oyuncu listesinde bulunamadı!"
            );
        }

        yield return new WaitForSeconds(0.05f);

        // --- MASA GÜNCELLEMELERİ VE SENKRONİZASYON ---
        // Diğer oyuncular için "işlek taş" (available tiles) kontrolünü yenile
        tileDistrubite.photonView.RPC("CheckForAvailableTiles", RpcTarget.All, playerQueInt);

        scoreManager.CommitAndStoreMelds();
        scoreManager.CommitJokerTransactions();

        // ** KRİTİK NOKTA: BEKLEYEN İŞLEMELERİ ŞİMDİ GÖNDER **
        scoreManager.ExecutePendingSyncs();

        yield return new WaitForSeconds(0.05f);

        // --- OYUN BİTİŞ / SIRA DEVRETME ---
        // Ortada taş kalmadıysa beraberlik
        if (tileDistrubite.allTiles.Count == 0)
        {
            if (GameManager.Instance != null && !GameManager.Instance.isGameEnded)
                GameManager.Instance.photonView.RPC(
                    "FinishGameRPC",
                    RpcTarget.All,
                    -1,
                    false,
                    false
                );
            Destroy(gameObject);
            yield break;
        }

        // Turu bitir, bayrakları sıfırla ve sırayı devret
        turnManager.canDrop = false;
        turnManager.photonView.RPC("NextTurn", RpcTarget.AllBuffered);

        // Görsel nesneyi yok et
        Destroy(gameObject);
    }
    #endregion
    #region Shift_Tiles
    private void ShiftTilesRight(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        // 1. GÜVENLİK KONTROLLERİ
        if (parentContainer != playerTileContainer)
            return;
        if (gameObject.transform.parent == middleTileContainer)
            return;

        // [DÜZELTME 1]: Tag kontrolünü sildik.
        // Zaten yukarıda parentContainer != playerTileContainer kontrolü var.
        // Eğer oyuncu ıstakasında değilsek çalışmaz. Tag'e gerek yok.

        // Gösterge taşı kaydırılamaz
        TileUI tileUI = tileToShift.GetComponent<TileUI>();
        if (tileUI != null && tileUI.isIndicatorTile)
            return;

        // --- 2. DUVAR (SINIR) TESPİTİ ---
        int wallIndex = parentContainer.childCount;
        for (int i = startIndex; i < parentContainer.childCount; i++)
        {
            Placeholder ph = parentContainer.GetChild(i).GetComponent<Placeholder>();
            // Atma alanı (isRight) veya kilitli bir yer varsa orası duvardır
            if (ph != null && ph.isRight)
            {
                wallIndex = i;
                break;
            }
        }

        // --- 3. YER VAR MI KONTROLÜ ---
        bool hasSpace = false;
        for (int i = startIndex; i < wallIndex; i++)
        {
            if (parentContainer.GetChild(i).childCount == 0)
            {
                hasSpace = true;
                break;
            }
        }

        // Yer yoksa işlemi iptal et ve taşı geri gönder (Eğer sürüklenen taşsa)
        if (!hasSpace)
        {
            Debug.LogWarning("Sağ taraf dolu, kaydırma yapılamaz.");
            if (tileToShift == transform)
                StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }

        // --- 4. ZİNCİRLEME KAYDIRMA (DOMİNO ETKİSİ) ---
        for (int i = startIndex; i < wallIndex; i++)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            // A) KUTU BOŞ MU?
            if (currentPlaceholder.childCount == 0)
            {
                // Boşsa taşı buraya gönder ve döngüyü bitir.
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                return;
            }
            // B) KUTU DOLU MU?
            else
            {
                // Kutudaki taşı (kiracıyı) hafızaya al
                Transform residentTile = currentPlaceholder.GetChild(0);

                // Elimdeki taşı bu kutuya yolla (Animasyon başlasın)
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));

                // Artık elimdeki taş, az önce yerinden ettiğim taş oldu.
                // Bir sonraki döngüde bunu bir yan kutuya taşıyacağız.
                tileToShift = residentTile;
            }
        }
    }

    private void ShiftTilesLeft(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        if (parentContainer != playerTileContainer)
            return;
        if (gameObject.transform.parent == middleTileContainer)
            return;
        // Tag kontrolünü burada da kaldırdık.

        TileUI tileUI = tileToShift.GetComponent<TileUI>();
        if (tileUI != null && tileUI.isIndicatorTile)
            return;

        // --- 1. SOL TARAFTA BOŞLUK VAR MI? ---
        bool hasSpace = false;
        for (int i = startIndex; i >= 0; i--)
        {
            if (parentContainer.GetChild(i).childCount == 0)
            {
                hasSpace = true;
                break;
            }
        }

        if (!hasSpace)
        {
            Debug.LogWarning("Sol taraf dolu, kaydırma yapılamaz.");
            if (tileToShift == transform)
                StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }

        // --- 2. ZİNCİRLEME KAYDIRMA ---
        for (int i = startIndex; i >= 0; i--)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            Placeholder ph = currentPlaceholder.GetComponent<Placeholder>();
            if (ph != null && ph.isRight)
                return; // Güvenlik

            if (currentPlaceholder.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                return;
            }
            else
            {
                Transform residentTile = currentPlaceholder.GetChild(0);
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                tileToShift = residentTile;
            }
        }
    }
    #endregion
    #region Smart Shift Logic (Akıllı Kaydırma)
    // Sağa kaydırmayı dener. Başarılı olursa true, yer yoksa false döner.
    private bool TryShiftRight(Transform container, int startIndex)
    {
        // 1. Duvarı (Boşluğu) Bul
        int emptySlotIndex = -1;

        // StartIndex'ten sağa doğru boş yer ara
        for (int i = startIndex; i < container.childCount; i++)
        {
            Placeholder ph = container.GetChild(i).GetComponent<Placeholder>();
            // Eğer atma alanıysa veya kilitliyse dur (Duvar)
            if (ph.isRight)
                break;

            if (container.GetChild(i).childCount == 0)
            {
                emptySlotIndex = i;
                break;
            }
        }

        // Eğer boş yer yoksa veya çok uzaktaysa (Opsiyonel: sadece yan yana olanları kaydır) başarısız dön
        if (emptySlotIndex == -1)
            return false;

        // 2. Kaydırma İşlemi (Ters Döngü)
        // Boşluktan geriye doğru gelerek taşları birer sağa itiyoruz
        // Örn: [Dolu1][Dolu2][BOŞ] -> [Dolu1][BOŞ][Dolu2] -> [BOŞ][Dolu1][Dolu2]
        for (int i = emptySlotIndex; i > startIndex; i--)
        {
            Transform targetSlot = container.GetChild(i); // Boş olan (veya boşalacak olan)
            Transform sourceSlot = container.GetChild(i - 1); // Oraya gelecek olan

            if (sourceSlot.childCount > 0)
            {
                Transform tileToMove = sourceSlot.GetChild(0);
                TileUI tileUI = tileToMove.GetComponent<TileUI>();
                if (tileUI != null)
                {
                    tileUI.StartCoroutine(tileUI.SmoothMove(tileToMove, targetSlot));
                }
            }
        }

        return true; // Kaydırma işlemi başladı
    }

    // Sola kaydırmayı dener.
    private bool TryShiftLeft(Transform container, int startIndex)
    {
        // 1. Sol tarafta boşluk ara
        int emptySlotIndex = -1;

        for (int i = startIndex; i >= 0; i--)
        {
            if (container.GetChild(i).childCount == 0)
            {
                emptySlotIndex = i;
                break;
            }
        }

        if (emptySlotIndex == -1)
            return false;

        // 2. Kaydırma İşlemi (Düz Döngü)
        // Boşluktan hedefe doğru gelerek taşları sola çekiyoruz
        for (int i = emptySlotIndex; i < startIndex; i++)
        {
            Transform targetSlot = container.GetChild(i); // Boş olan
            Transform sourceSlot = container.GetChild(i + 1); // Taşın olduğu yer

            if (sourceSlot.childCount > 0)
            {
                Transform tileToMove = sourceSlot.GetChild(0);
                TileUI tileUI = tileToMove.GetComponent<TileUI>();
                if (tileUI != null)
                {
                    tileUI.StartCoroutine(tileUI.SmoothMove(tileToMove, targetSlot));
                }
            }
        }

        return true;
    }
    #endregion
    #region SmoothMove
    // Private yerine PUBLIC yapıyoruz ki Swap sırasında diğer taşa erişebilelim
    public IEnumerator SmoothMove(Transform tile, Transform targetPlaceholder)
    {
        Vector3 startPos = tile.position;
        Vector3 targetPos = targetPlaceholder.position;

        float elapsedTime = 0f;
        float duration = slideDuration > 0 ? slideDuration : 0.25f;

        while (elapsedTime < duration)
        {
            if (tile == null)
                yield break;

            float t = elapsedTime / duration;
            // Eğri varsa kullan
            float curvedT =
                (slideCurve != null && slideCurve.length > 0)
                    ? slideCurve.Evaluate(t)
                    : Mathf.SmoothStep(0, 1, t);

            tile.position = Vector3.Lerp(startPos, targetPos, curvedT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (tile != null)
        {
            // [ÇÖZÜM]: true parametresi dünya pozisyonunu korur, zıplamayı önler.
            tile.SetParent(targetPlaceholder, true);

            // Görsel olarak zaten oradayız, şimdi verileri eşitleyelim.
            tile.localPosition = Vector3.zero;
            tile.localScale = Vector3.one;
            tile.localRotation = Quaternion.identity;

            FitToParent();
            CheckPlace();

            if (scoreManager != null)
                scoreManager.CheckForPer();
        }
    }
    #endregion

    // TileUI.cs dosyasının en altındaki metodu bununla değiştir:
    private Placeholder GetVisualClosestPlaceholder(float detectionRadius = 100f)
    {
        Placeholder bestTarget = null;
        float closestDistanceSqr = detectionRadius * detectionRadius; // Karesini alıyoruz (Performans için)
        Vector3 currentPos = transform.position;

        // Start'ta doldurduğumuz "isMeldArea" listesini tara
        foreach (Placeholder ph in allMeldPlaceholders)
        {
            if (ph == null)
                continue;
            // Eğer placeholder kapalıysa (inactive) hesaplama
            if (!ph.gameObject.activeInHierarchy)
                continue;

            // Mesafeye bak
            float dSqr = (ph.transform.position - currentPos).sqrMagnitude;

            if (dSqr < closestDistanceSqr)
            {
                closestDistanceSqr = dSqr;
                bestTarget = ph;
            }
        }

        return bestTarget;
    }
}

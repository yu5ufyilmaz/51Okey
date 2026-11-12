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

    // Atılan taş atma yerine kayarken başka taş oynatılmasın (çift atma / işleme).
    private static float discardLockUntil;

    // Elde tutulan taş. Tutulurken yuvası boş görünür; süre dolumu hamlesi oraya taş koyup üst üste bindirmesin.
    private static TileUI held;
    public static bool Busy => held != null || Time.time < discardLockUntil;

    // SmoothMove'un gittiği yuva; varınca yuva doluysa ve oradaki taş gitmiyorsa boş yuvaya sapılır.
    private Transform movingTo;

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
    // Sürükle-bırakı koddan yapar (süre dolunca otomatik hamle, test oyuncuları); insanla aynı kural yolundan geçer.
    // Overlay canvas: ekran noktası için kamera yok. OnBeginDrag hamleyi reddederse pointerDrag'i null yapar.
    public static bool SimulateDrag(TileUI tile, Transform target)
    {
        var e = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, tile.transform.position),
            pointerDrag = tile.gameObject,
        };
        e.pressPosition = e.position;
        tile.OnBeginDrag(e);
        if (e.pointerDrag == null)
            return false;
        tile.transform.position = target.position;
        tile.OnEndDrag(e);
        return true;
    }

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

        if (Time.time < discardLockUntil)
            return (false, false);

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
        held = this;
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true);
        _currentVelocity = Vector3.zero; // Hızı sıfırla
        _targetRotationZ = 0f;

        // Istakadaki taş: işlenebileceği yuvaları parlat (sıra, çekiş ve açmış olma ProcessAssist'te kontrol edilir).
        if (grandParent == playerTileContainer)
            ProcessAssist.Begin(this);
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
            // Mesafe içindeyiz: gideceği yuvanın gerçek boyuna küçül (işleme yardımı alanı büyüttüyse büyük yuvaya).
            // Sabit 0.3 ölçek yuvanın ~yarısı kalıyordu; işlenen taş masadakilerden küçük görünüyordu.
            _targetScale = ScaleToFit(visualTarget.transform, out Vector3 fit) ? fit : shrinkScale;
        }
        else
        {
            // Mesafe dışındayız, büyü
            if (_targetScale != _originalScale)
            {
                _targetScale = _originalScale;
            }
        }

        ProcessAssist.Drag(this);
    }
    #endregion
    #region On End Drag
    [Header("Drag Settings")]
    [SerializeField]
    private float dropDistanceThreshold = 100f;

   public void OnEndDrag(PointerEventData eventData)
    {
        if (held == this)
            held = null;
        // Parlayan hedef yuva (varsa); parıltı ve büyütme her durumda burada kapanır.
        Placeholder assisted = ProcessAssist.End(this);

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

        // B) Parlayan işleme yuvası: büyümüş alanda gösterilen yere düşer.
        if (bestTarget == null)
            bestTarget = assisted;

        // C) Masa / Atma Alanı
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
            // Kilit: önceki atış hâlâ kayarken ikinci parmakla/otomatik hamleyle ikinci taş atılmasın.
            if (turnManager.IsPlayerTurn() && turnManager.canDrop && Time.time >= discardLockUntil)
            {
                if (!turnManager.CanFinishTurn())
                {
                    Debug.LogWarning("KURAL HATASI: Açmadan bitiremezsiniz.");
                    Tiles returned = GameManager.Instance.CurrentSidePickTile ?? tileDataInfo;
                    GameManager.Instance.HandleFailedSidePick(this.tileDataInfo);
                    // Atılmaya çalışılan taş yandan alınanın kendisiyse yana iade edildi (yeni görseli orada);
                    // bu görsel ıstakaya geri dönerse verisi olmayan hayalet taş olur.
                    if (returned.id == tileDataInfo.id)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, originalParent));
                    return;
                }

                // Atma cezası (okey/işlek) NextTurnRoutine'de: bitiren atış cezalanmaz.
                ExecuteNextTurn();
            }
            else
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
            }
        }
        // DURUM B: KENDİ ISTAKAMIZ (SWAP ve AKILLI KAYDIRMA)
        else if (bestTarget.transform.parent == playerTileContainer)
        {
            // Ortadaki/yandaki taş ancak gerçekten çekilebiliyorsa ıstakaya girer; yoksa verisiz hayalet taş olurdu.
            if ((inMiddle || fromLeftContainer) && !(turnManager.IsPlayerTurn() && !turnManager.canDrop))
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
                return;
            }

            // -- Taş Çekme Kontrolleri --
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

            // --- AKILLI KAYDIRMA VE SWAP İŞLEMİ ---

            // Bıraktığımız yer DOLU MU? ve orası zaten benim ESKİ YERİM DEĞİL Mİ?
            if (bestTarget.transform.childCount > 0 && bestTarget != originalParent.GetComponent<Placeholder>())
            {
                Transform residentTile = bestTarget.transform.GetChild(0);
                TileUI residentUI = residentTile.GetComponent<TileUI>();

                // Gösterge taşı kilitlidir, yerinden oynatılamaz.
                if (residentUI != null && residentUI.isIndicatorTile)
                {
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, originalParent));
                    return;
                }

                // 1. ADIM: AKILLI BOŞLUK ARAMA
                Placeholder smartEmptySpot = FindSmartEmptyPlaceholder(
                    bestTarget.transform.parent, 
                    bestTarget.transform.GetSiblingIndex()
                );

                // Eğer uygun ve güvenli bir boşluk bulunduysa oraya kaydır
                if (smartEmptySpot != null)
                {
                    residentUI.StartCoroutine(residentUI.SmoothMove(residentTile, smartEmptySpot.transform));
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, bestTarget.transform));
                }
                // Boşluk yoksa mecburen TAKAS (SWAP) yap
                else
                {
                    residentUI.StartCoroutine(residentUI.SmoothMove(residentTile, originalParent));
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, bestTarget.transform));
                }
            }
            // Bıraktığımız yer BOŞSA -> Direkt yerleş
            else
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, bestTarget.transform));
            }
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
        // Atış başladı: tur devredilene kadar yeni sürükleme, ikinci atış ve süre dolumu hamlesi yok.
        discardLockUntil = Time.time + slideDuration + 0.5f;
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

        // Bu tur açılan/işlenen taşlar açıldıkları anda veriden düşüyor (RemoveTileFromPlayerListByValue).
        // Burada bir daha renk/sayıyla düşmek eldeki ikizlerini de siliyor, el bitmeden "bitti" sayıyordu.

        // El tamamen bitti mi? (0 taş kaldıysa bitmiştir)
        bool isGameReallyOver = (currentHandForCheck.Count == 0);

        // Atış cezası (okey/işlek) aşağıda, bu turun açma/işlemeleri gönderildikten sonra istenir.

        // Yandan alan turu açarak ya da işleyerek tamamladı: taşı atan önceki oyuncu taş sayısı × 10 yer.
        // Atışta yazılır ki geri almalar sayılmasın ve seri+çift açınca iki kez yazılmasın.
        if (
            turnManager.hasPickedFromSide
            && (turnManager.hasOpenedThisTurn || turnManager.hasProcessedThisTurn)
            && GameManager.Instance != null
            && GameManager.Instance.CurrentSidePickTile != null
        )
        {
            GameManager.Instance.photonView.RPC(
                "ApplySidePickSuccessPenaltyRPC",
                RpcTarget.MasterClient,
                playerQueInt,
                GameManager.Instance.CurrentSidePickTile.number
            );
        }

        // Bitiren turda açılan/işlenen taşlar da bitişten önce herkese gitsin; aşağıdaki erken çıkış
        // bunları göndermeden bitiriyordu, diğer ekranlarda son per/işleme eksik kalıyordu.
        if (isGameReallyOver)
        {
            scoreManager.CommitAndStoreMelds();
            scoreManager.CommitJokerTransactions();
            scoreManager.ExecutePendingSyncs();
        }

        // GameManager'a Bildir
        if (tileDistrubite != null)
        {
            HandData data = new HandData();
            data.actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            data.okeyFinish = isGameReallyOver && tileDataInfo.type == TileType.Joker;
            // Kafa: masada kimse açmamışken aynı turda kemiksiz açıp bitmek.
            data.kafa =
                isGameReallyOver
                && scoreManager != null
                && scoreManager.FirstOpenThisTurn
                && scoreManager.playersWhoOpened.All(q => q == playerQueInt);
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
        // Taş, yana atılan taşın çıkacağı yere tam varır; yenisi ancak o zaman çıkar ve bu taş aynı karede gizlenir.
        // Önceden yenisi 0.05 sn'de çıkıyor, bu taş yarı yoldayken siliniyordu.
        GameObject dropSpot = FindObjectOfType<SeatManager>()?.tiledropOffset[0];
        Transform target = dropSpot != null ? dropSpot.transform : rightTileContainer;
        // Kayarken atma yerinin boyuna küçülsün ki yer değişiminde boyut zıplamasın (Update ölçeği _targetScale'e taşır).
        if (ScaleToFit(target, out Vector3 fit))
            _targetScale = fit;
        yield return Slide(transform, target.position);

        // --- [DÜZELTME 2] VERİTABANINDAN SİLME (ATILAN TAŞ) ---
        // Herkes kendi listesinde taşı ID ile bulur.
        if (tileDistrubite.GetPlayerTiles().Any(t => t.id == tileDataInfo.id))
        {
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerList",
                RpcTarget.All, // Herkeste silinsin
                playerQueInt,
                tileDataInfo.id
            );
            // RPC yerelde hemen çalıştı, yana atılan taş tam bu noktada oluştu.
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }
        else
        {
            Debug.LogError(
                $"[KRİTİK HATA] Silinecek taşın ID'si ({tileDataInfo.id}) gerçek oyuncu listesinde bulunamadı!"
            );
        }

        yield return new WaitForSeconds(0.05f);

        // --- MASA GÜNCELLEMELERİ VE SENKRONİZASYON ---
        // İşlek listesi NextTurn'de herkeste masadaki gerçek taşlardan yeniden kurulur (RecalculateAllAvailableSlots).
        // Burada eskiden açılıştaki perlerden ekleme yapan bir RPC vardı; işlenen taşları bilmediği ve geç ulaştığı için
        // artık işlek olmayan taşı (ör. işlenmiş b8) geri ekliyor, sonraki atışta haksız 250 yazdırıyordu.

        scoreManager.CommitAndStoreMelds();
        scoreManager.CommitJokerTransactions();

        // ** KRİTİK NOKTA: BEKLEYEN İŞLEMELERİ ŞİMDİ GÖNDER **
        scoreManager.ExecutePendingSyncs();
        scoreManager.ClearTurnHistory();

        // Atış cezası master'da bu turun perleri ve işlemeleri masaya oturduktan sonra bakılır (RPC sırası korunur).
        // Önce bakılınca, aynı turda y10'u perin ucuna işleyip öbür y10'u atan oyuncu eski masaya göre "işlek attı" yiyordu.
        // Bitiren atış yukarıda çıktı: kural kitabına göre bitiren atışa okey/işlek cezası yazılmaz.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.photonView.RPC(
                "CheckPenaltyRPC",
                RpcTarget.MasterClient,
                playerQueInt,
                tileDataInfo
            );
        }
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
    #region SmoothMove
    // Private yerine PUBLIC yapıyoruz ki Swap sırasında diğer taşa erişebilelim
    public IEnumerator SmoothMove(Transform tile, Transform targetPlaceholder)
    {
        movingTo = targetPlaceholder;
        yield return Slide(tile, targetPlaceholder.position);
        // Kayarken oyuncu taşı yakaladıysa yeri artık sürükleme belirler.
        if (held == this)
        {
            movingTo = null;
            yield break;
        }

        // Yuva yalnızca varışta alınır: kayarken başka taş oraya oturduysa (hızlı iki bırakış, otomatik çekiş,
        // yana itilen taş) üst üste binmesin; alttaki görünmez olur.
        while (tile != null && targetPlaceholder.parent == playerTileContainer && SlotTaken(targetPlaceholder, tile))
        {
            Placeholder free = FindSmartEmptyPlaceholder(playerTileContainer, targetPlaceholder.GetSiblingIndex());
            if (free == null)
                break;
            targetPlaceholder = free.transform;
            movingTo = targetPlaceholder;
            yield return Slide(tile, targetPlaceholder.position);
            if (held == this)
            {
                movingTo = null;
                yield break;
            }
        }
        movingTo = null;

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

    // Bu taşı hedef yuvayla aynı ekran boyuna getiren localScale (taşın kendi ölçeği 1 iken boyu esas alınır).
    bool ScaleToFit(Transform target, out Vector3 scale)
    {
        scale = Vector3.one;
        if (!(target is RectTransform to) || !(transform is RectTransform me) || me.localScale.x <= 0 || me.localScale.y <= 0)
            return false;
        float unitW = me.rect.width * me.lossyScale.x / me.localScale.x;
        float unitH = me.rect.height * me.lossyScale.y / me.localScale.y;
        if (unitW <= 0 || unitH <= 0)
            return false;
        scale = new Vector3(to.rect.width * to.lossyScale.x / unitW, to.rect.height * to.lossyScale.y / unitH, 1f);
        return true;
    }

    // Destroy kare sonunu bekler; yuvadan hemen çıkarılmazsa aynı karede işlek hesabı ve kontroller eski taşı da görür.
    public static void DestroyNow(GameObject go)
    {
        go.transform.SetParent(null, false);
        Destroy(go);
    }

    // Yuvada başka bir taş var ve o taş başka yere gitmiyor.
    static bool SlotTaken(Transform slot, Transform me)
    {
        foreach (Transform child in slot)
        {
            if (child == me)
                continue;
            TileUI other = child.GetComponent<TileUI>();
            if (other == null || other.movingTo == null || other.movingTo == slot)
                return true;
        }
        return false;
    }

    IEnumerator Slide(Transform tile, Vector3 targetPos)
    {
        Vector3 startPos = tile.position;

        float elapsedTime = 0f;
        float duration = slideDuration > 0 ? slideDuration : 0.25f;

        while (elapsedTime < duration)
        {
            if (tile == null || (held != null && held.transform == tile))
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
            tile.position = targetPos;
    }
    #endregion
    /// <summary>
    /// Belirtilen hedef index'in (targetIndex) uygun ve boş olup olmadığını kontrol eder.
    /// Uygunsa residentUI taşını oraya gönderir ve true döner.
    /// Değilse (Doluysa veya Duvarsa) hiçbir şey yapmaz ve false döner.
    /// </summary>
    private bool CheckAndMoveNeighbor(Transform container, TileUI residentUI, int targetIndex)
    {
        // 1. Sınır Kontrolü (Index var mı?)
        if (targetIndex < 0 || targetIndex >= container.childCount)
            return false;

        Transform neighborPlaceholder = container.GetChild(targetIndex);
        Placeholder phScript = neighborPlaceholder.GetComponent<Placeholder>();

        // 2. Duvar Kontrolü (Atma alanı mı?)
        if (phScript != null && phScript.isRight)
            return false;

        // 3. Doluluk Kontrolü (Senin isteğin: Sadece boşsa kaydır, doluysa kaydırma)
        if (neighborPlaceholder.childCount == 0)
        {
            // BOŞ! O zaman taşı oraya kaydır.
            residentUI.StartCoroutine(
                residentUI.SmoothMove(residentUI.transform, neighborPlaceholder)
            );
            return true; // Başarılı, kaydırdık.
        }
        else
        {
            // DOLU! Kaydırma yapma, false dön (Böylece Swap devreye girecek)
            return false;
        }
    }

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
    /// <summary>
    /// Istaka içinde, verilen başlangıç noktasından itibaren (önce sağ, sonra sol)
    /// taşın kaydırılabileceği EN UYGUN ve BOŞ yeri bulur.
    /// Duvarlara (isRight) çarpınca durur.
    /// </summary>
    private Placeholder FindSmartEmptyPlaceholder(Transform container, int startIndex)
    {
        int maxIndex = container.childCount - 1;

        // --- 1. SAĞ TARAFI TARA ---
        for (int i = startIndex + 1; i <= maxIndex; i++)
        {
            Transform sibling = container.GetChild(i);
            Placeholder ph = sibling.GetComponent<Placeholder>();

            // GÜVENLİK: Script yoksa geç
            if (ph == null) continue;

            // KRİTİK DUVAR KONTROLÜ: 
            // Eğer baktığımız yer "Atma Alanı" (isRight) ise, DUR!
            // Buradan ötesine (veya buraya) taş kayamaz.
            if (ph.isRight) 
            {
                break; 
            }

            // Orta alana veya meld alanına kaymayı engelle (Ekstra önlem)
            if (ph.isMeldArea || ph.isDrop) continue;

            // BOŞ MU?
            if (sibling.childCount == 0)
            {
                return ph; // Bulduk!
            }
        }

        // --- 2. SOL TARAFI TARA (Eğer sağda yer yoksa) ---
        for (int i = startIndex - 1; i >= 0; i--)
        {
            Transform sibling = container.GetChild(i);
            Placeholder ph = sibling.GetComponent<Placeholder>();

            if (ph == null) continue;

            // Sol tarafta "isRight" (Atma Alanı) olması beklenmez ama yine de kontrol edelim
            if (ph.isRight) continue;

            if (ph.isMeldArea || ph.isDrop) continue;

            // BOŞ MU?
            if (sibling.childCount == 0)
            {
                return ph; // Bulduk!
            }
        }

        // Hiçbir yer yoksa null döner
        return null;
    }
}

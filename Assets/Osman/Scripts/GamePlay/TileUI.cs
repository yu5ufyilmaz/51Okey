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
    private TileDistribute _tileDistribute;

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
        _tileDistribute = GameObject.Find("TileManager(Clone)").GetComponent<TileDistribute>();
        scoreManager = GameObject.Find("ScoreManager(Clone)").GetComponent<ScoreManager>();
        playerTiles = null;
        playerTiles = _tileDistribute.GetPlayerTiles();
        _tileDistribute.RegisterTileUI(this);
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
    // Can't move indicator tiles
    if (isIndicatorTile)
    {
        Debug.LogWarning("Cannot move indicator tiles!");
        return (false, false);
    }

    bool isMiddleTile = gameObject.transform.parent == middleTileContainer;
    bool isLeftTile = gameObject.transform.parent == leftTileContainer;
    bool isPlayerTurn = turnManager.IsPlayerTurn();
    int playerTileCount = playerTiles.Count;

    // If it's not the player's turn
    if (!isPlayerTurn)
    {
        // Can't draw tiles when it's not your turn
        if (isMiddleTile)
        {
            Debug.LogWarning("Cannot draw tiles when it's not your turn!");
            return (false, false);
        }
        return (true, true); // Can move placed tiles
    }
    
    // If it's the player's turn
    if (isMiddleTile || isLeftTile)
    {
        // Check if player already has max tiles
        if (playerTileCount >= 15)
        {
            Debug.LogWarning("Cannot draw more tiles. You already have 15 tiles!");
            return (false, false);
        }
        return (true, true); // Can draw tiles
    }
    
    return (true, true); // Can move placed tiles
}
    #region On Begin Drag
    public void OnBeginDrag(PointerEventData eventData)
    {
        var (canDrop, canDraw) = CanMoveTile();
    
        // Early exit conditions
        if (!canDraw) return;
        if (gameObject.transform.parent.tag == "OtherSideTileContainer") return;
        if (isIndicatorTile || gameObject.transform.parent == rightTileContainer) return;
    
        // Set flag if dragging from left container
        fromLeftContainer = (gameObject.transform.parent == leftTileContainer);
    
        // Prepare for dragging
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true);
    }
    #endregion
    #region On Drag
    public void OnDrag(PointerEventData eventData)
    {
        var (canDrop, canDraw) = CanMoveTile();
    
        // Early exit conditions
        if (!canDrop || !canDraw) return;
        if (gameObject.transform.parent.tag == "OtherSideTileContainer") return;
        if (isIndicatorTile || gameObject.transform.parent == rightTileContainer) return;
    
        transform.position = Input.mousePosition;
    }
    #endregion
    #region On End Drag
    public void OnEndDrag(PointerEventData eventData)
    {
        // Early exit conditions
        if (gameObject.transform.parent.tag == "OtherSideTileContainer" || isIndicatorTile)
        {
            StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }
    
        // Re-enable ray casting
        canvasGroup.blocksRaycasts = true;
    
        // Find closest placeholder
        Transform closestPlaceholder = FindClosestPlaceholder();
    
        // If no suitable placeholder found or too far, return to original position
        if (closestPlaceholder == null)
        {
            StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }
    
        // Handle tile movement based on game state
        if (turnManager.IsPlayerTurn())
        {
            HandleTilePlacementDuringTurn(closestPlaceholder);
        }
        else
        {
            HandleTilePlacementOutsideTurn(closestPlaceholder);
        }
    }
    
    private Transform FindClosestPlaceholder()
{
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
    
    return (closestDistance < 40f) ? closestPlaceholder : null;
}

// Helper to handle tile placement during player's turn
private void HandleTilePlacementDuringTurn(Transform closestPlaceholder)
{
    bool isPlaceholderRight = closestPlaceholder.GetComponent<Placeholder>().isRight;
    PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
    
    if (!turnManager.canDrop)
    {
        // Drawing a tile
        if (inMiddle)
        {
            // Draw from middle
            SetTileData(_tileDistribute.allTiles[0]);
            _tileDistribute.photonView.RPC("AddTileFromMiddlePlayerList", RpcTarget.AllBuffered, queueValue);
            turnManager.canDrop = true;
            StartCoroutine(SmoothMove(transform, closestPlaceholder));
            inMiddle = false;
        }
        else if (fromLeftContainer)
        {
            // Draw from left container
            StartCoroutine(SmoothMove(transform, closestPlaceholder));
            _tileDistribute.photonView.RPC("AddTileFromDropPlayerList", RpcTarget.AllBuffered, queueValue);
            turnManager.canDrop = true;
            _tileDistribute.dropTile = this.tileDataInfo;
            fromLeftContainer = false;
        }
        else if (isPlaceholderRight)
        {
            // Cannot drop a tile when canDrop is false and 14 tiles
            Debug.LogWarning("Cannot drop a tile now. You have 14 tiles.");
            StartCoroutine(SmoothMove(transform, originalParent));
        }
        else
        {
            // Move tile to placeholder
            StartCoroutine(SmoothMove(transform, closestPlaceholder));
        }
    }
    else
    {
        // Dropping a tile
        if (inMiddle || fromLeftContainer)
        {
            // Cannot draw when already has 15 tiles
            Debug.LogWarning("Cannot draw more tiles. You already have 15 tiles!");
            StartCoroutine(SmoothMove(transform, originalParent));
        }
        else if (isPlaceholderRight)
        {
            // Dropping tile to end turn
            Debug.Log("Tile dropped. Next player's turn.");
            NextTurnEvents();
        }
        else if (closestPlaceholder.GetComponent<Placeholder>().available)
        {
            // Check if placing on an available slot with matching tile
            if (closestPlaceholder.GetComponent<Placeholder>().AvailableTileInfo == tileDataInfo)
            {
                StartCoroutine(SmoothMove(transform, closestPlaceholder));
            }
            else
            {
                Debug.Log("Incorrect tile for this position");
                StartCoroutine(SmoothMove(transform, originalParent));
            }
        }
        else
        {
            // Move tile to placeholder
            StartCoroutine(SmoothMove(transform, closestPlaceholder));
        }
    }
    
    // Handle tile displacement if needed
    HandleTileDisplacement(closestPlaceholder);
}

// Helper to handle tile placement outside player's turn
private void HandleTilePlacementOutsideTurn(Transform closestPlaceholder)
{
    bool isPlaceholderRight = closestPlaceholder.GetComponent<Placeholder>().isRight;
    
    if (inMiddle || fromLeftContainer)
    {
        // Cannot draw when it's not your turn
        Debug.LogWarning("Cannot draw tiles when it's not your turn!");
        StartCoroutine(SmoothMove(transform, originalParent));
    }
    else if (isPlaceholderRight)
    {
        // Cannot drop when it's not your turn
        Debug.Log("Cannot drop tiles when it's not your turn!");
        StartCoroutine(SmoothMove(transform, originalParent));
    }
    else
    {
        // Just reordering tiles
        StartCoroutine(SmoothMove(transform, closestPlaceholder));
    }
}

// Helper to handle tile displacement
private void HandleTileDisplacement(Transform closestPlaceholder)
{
    if (closestPlaceholder.childCount <= 1) return;
    if (closestPlaceholder.GetComponent<Placeholder>().isRight) return;

    int targetIndex = closestPlaceholder.GetSiblingIndex();
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
    #endregion
    void NextTurnEvents()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int playerQue = (int)queueValue;
    
        // Get the tile index
        int tileIndex = playerTiles.IndexOf(tileDataInfo);
    
        // First remove the tile from player list (this will also instantiate it for others)
        _tileDistribute.photonView.RPC("RemoveTileFromPlayerList", RpcTarget.AllBuffered, playerQue, tileIndex);
    
        // Clean up melded tiles
        scoreManager.RemoveMeldedTiles();
    
        // Check for available tiles
        _tileDistribute.photonView.RPC("CheckForAvailableTiles", RpcTarget.AllBuffered, playerQue);
    
        // Destroy the local GameObject (it's already shown for others via the network)
        Destroy(gameObject);
    
        // Update turn state
        turnManager.canDrop = false;
    
        // Move to next player's turn
        turnManager.photonView.RPC("NextTurn", RpcTarget.AllBuffered);
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

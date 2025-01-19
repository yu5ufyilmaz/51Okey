using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class TileUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
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
    public bool isInMiddleTileContainer = false;

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
        leftTileContainer = GameObject.Find("LeftTileContainer").transform;
        playerTileContainer = GameObject.Find("PlayerTileContainer").transform;
        turnManager = GameObject.Find("TurnManager").GetComponent<TurnManager>();
        tileDistrubite = GameObject.Find("TileManager(Clone)").GetComponent<TileDistrubite>();
        if (gameObject.transform.parent == middleTileContainer)
        {
            isInMiddleTileContainer = true;
        }
    }
    #endregion

    #region Tile Set UI
    public void SetTileData(Tiles tileData)
    {
        if (tileData != null && tileImage != null)
        {
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
    void QueueCheck()
    {
        if (turnManager.IsPlayerTurn() == false)
        {
            if (isInMiddleTileContainer == true)
            {
                Debug.LogWarning("Orta taşını hareket ettiremezsiniz!");
                return;
            }
            else if (gameObject.transform.parent == leftTileContainer)
            {
                Debug.LogWarning("Oyuncu taşını hareket ettiremezsiniz!");
                return;
            }

        }
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isIndicatorTile || gameObject.transform.parent == rightTileContainer)
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!");
            return;
        }

        QueueCheck();



        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isIndicatorTile) return;
        QueueCheck();

        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isIndicatorTile)
        {
            Debug.LogWarning("Gösterge taşını hareket ettiremezsiniz!");
            return;
        }
        QueueCheck();

        canvasGroup.blocksRaycasts = true;

        Transform parentContainer = originalParent.parent;
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

        if (closestPlaceholder != null && closestDistance < 100f)
        {
            StartCoroutine(SmoothMove(transform, closestPlaceholder));
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
            StartCoroutine(SmoothMove(transform, originalParent));
        }
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
            if (currentPlaceholder == leftTileContainer)
            {
                Debug.LogWarning("Sol alana taş taşınamaz!");
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
        tile.localPosition = Vector3.zero;
        tile.localScale = Vector3.one;
    }
    #endregion
}

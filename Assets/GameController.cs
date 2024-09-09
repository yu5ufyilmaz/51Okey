using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public GameObject placeHoldersParent;
    public static List<GameObject> allPlaceHolders;
    public GameObject blankBlockPrefab;

    public List<Sprite> sprites;
    public List<Sprite> player1Blocks, player2Blocks, player3Blocks, player4Blocks;

    public static GameController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject); // Ensure singleton behavior
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        allPlaceHolders = new List<GameObject>(GameObject.FindGameObjectsWithTag("PlaceHolder"));
    }

   

    // Start is called before the first frame update
    void Start()
    {
        sprites.AddRange(sprites);
        StartGame();
    }

    public void StartGame()
    {
        for (int i = 0; i < 14; i++)
        {
            player1Blocks.Add(sprites[Random.Range(0, sprites.Count)]);
            player2Blocks.Add(sprites[Random.Range(0, sprites.Count)]);
            player3Blocks.Add(sprites[Random.Range(0, sprites.Count)]);
            player4Blocks.Add(sprites[Random.Range(0, sprites.Count)]);
        }

        player1Blocks.Add(sprites[Random.Range(0, sprites.Count)]);

        for (int i = 0; i < player1Blocks.Count; i++)
        {
            Transform parent = placeHoldersParent.transform.GetChild(i);
            GameObject gameObject = Instantiate(blankBlockPrefab, Vector3.zero, Quaternion.identity);
            gameObject.GetComponent<OkeyBlock>().SetBlockSprite(player1Blocks[i]);
            gameObject.transform.parent = parent;
        }

    }

 public void ReorderBlocks()
{
    bool moreChangesNeeded;
    do {
        moreChangesNeeded = false;
        for (int i = 0; i < placeHoldersParent.transform.childCount; i++)
        {
            Transform child = placeHoldersParent.transform.GetChild(i);
            if (child.childCount > 1)
            {
                moreChangesNeeded = true;
                Transform nextParent = placeHoldersParent.transform.GetChild((i + 1) % placeHoldersParent.transform.childCount);
                child.GetChild(1).parent = nextParent;
            }
        }
    } while (moreChangesNeeded);
}

}

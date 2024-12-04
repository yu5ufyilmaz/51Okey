using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteManager : MonoBehaviour
{
    public static SpriteManager Instance;

    public Sprite[] tileSprites;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // ID'ye göre Sprite alma fonksiyonu
    public Sprite GetSpriteById(int id)
    {
        if (id >= 0 && id < tileSprites.Length)
            return tileSprites[id];
        else
            return null;
    }
}


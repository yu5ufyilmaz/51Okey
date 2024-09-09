using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OkeyBlock : MonoBehaviour
{

    public string type = "";

    public string number = "";

    public bool isJoker = false;
    public bool isDraggable;
    public float smoothDamping =5f;

    private SpriteRenderer spriteRenderer;
    private Camera mainCam;

    private bool _isMoveWithMouse = false;
    // Start is called before the first frame update
    private void Awake()
    {
        mainCam = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        
    }

    private void OnMouseDown()
    {
        if (isDraggable)
        {
            _isMoveWithMouse = true;
        }
        
    }

    private void OnMouseUp()
    {
        if (isDraggable)
        {
            _isMoveWithMouse = false;
            this.transform.parent = FindNearestPlaceHolder().transform;
            GameController.Instance.ReorderBlocks();
        }
    }

    public GameObject FindNearestPlaceHolder()
    {
        List<(float, Transform)> distanceTransformList = new List<(float, Transform)>();
        foreach (var item in GameController.allPlaceHolders)
        {
            float distance = Vector3.Distance(item.transform.position, this.transform.position);
            distanceTransformList.Add((distance,item.transform));
        }

        return distanceTransformList.OrderBy(x => x.Item1).FirstOrDefault().Item2.gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        if (_isMoveWithMouse)
        this.transform.position = Vector3.Lerp(this.transform.position,mainCam.ScreenPointToRay(Input.mousePosition).GetPoint(5),Time.deltaTime*2.6f);
        else 
        
        if (transform.parent)
        {
            this.transform.position = Vector3.Lerp(this.transform.position,this.transform.parent.position,Time.deltaTime*2.6f);

        }
    }

    public void SetBlockSprite(Sprite sprite)
    {
        spriteRenderer.sprite = sprite;
        type = sprite.name.Split('_')[0];
        number = sprite.name.Split('_')[1];
    }
}

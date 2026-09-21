using UnityEngine;

public partial class Match3Game : MonoBehaviour
{
    GameObject boardGridRoot;


    void DrawBoardGrid()
    {
        if (boardGridRoot != null) Destroy(boardGridRoot);
        boardGridRoot = null;
        if (!showBoardGrid || rows <= 0 || cols <= 0 || cellSize <= 0f) return;

        Sprite lineSprite = squareSprite != null ? squareSprite : CreateSquareSprite();
        float W = cols * cellSize, H = rows * cellSize; 
        float thinW = Mathf.Max(gridLineThickness * cellSize, 0.01f);
        float borderW = Mathf.Max(gridBorderThickness * cellSize, 0.02f);

        var root = new GameObject("M3_BoardGrid");
        root.transform.SetParent(boardShakeRoot != null ? boardShakeRoot : managerTransform, false);
        boardGridRoot = root;

        Color thinC = gridLineColor;
       
        Color frameC = new Color(gridLineColor.r, gridLineColor.g, gridLineColor.b, Mathf.Clamp01(gridLineColor.a * 1.8f));

        
        for (int i = 0; i <= cols; i++)
        {
            bool border = i == 0 || i == cols;
            AddGridLine(root, lineSprite, new Vector3(-W / 2f + i * cellSize, 0f, 0f), border ? borderW : thinW, H, border ? frameC : thinC);
        }

        
        for (int j = 0; j <= rows; j++)
        {
            bool border = j == 0 || j == rows;
            float len = W;
            AddGridLine(root, lineSprite, new Vector3(0f, H / 2f - j * cellSize, 0f), len, border ? borderW : thinW, border ? frameC : thinC);
        }
    }

    
    void AddGridLine(GameObject root, Sprite s, Vector3 center, float width, float height, Color col)
    {
        var go = new GameObject("GridLine");
        go.transform.SetParent(root.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; 
        sr.color = col;
        sr.sortingOrder = -5; 
        float worldW = SpriteWorldWidth(s); 
        go.transform.localPosition = center;
        go.transform.localScale = new Vector3(width / worldW, height / worldW, 1f);
    }
}

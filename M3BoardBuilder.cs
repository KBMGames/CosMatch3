using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class Match3Game : MonoBehaviour
{

    Sprite CreateCircleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center + 0.5f);
                float dy = (y - center + 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, d <= radius ? new Color(1, 1, 1, 1) : new Color(0, 0, 0, 0));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    Sprite CreateSquareSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1, 1, 1, 1));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }


    void CreateSpecialSprites()
    {
        if (yellowCircleSprite == null) yellowCircleSprite = CreateOutlinedCircleSprite(new Color(0.98f, 0.78f, 0.25f));
        if (purpleSymbolIdle == null) purpleSymbolIdle = CreateOutlinedCircleSprite(new Color(0.62f, 0.42f, 0.90f));
        if (purpleSymbolActive0 == null) purpleSymbolActive0 = CreateOutlinedCircleSprite(new Color(0.62f, 0.42f, 0.90f));
        if (purpleSymbolActive1 == null) purpleSymbolActive1 = CreateOutlinedCircleSprite(new Color(0.62f, 0.42f, 0.90f));
        if (purpleSymbolActive2 == null) purpleSymbolActive2 = CreateOutlinedCircleSprite(new Color(0.62f, 0.42f, 0.90f));
        if (purpleSymbolActive3 == null) purpleSymbolActive3 = CreateOutlinedCircleSprite(new Color(0.62f, 0.42f, 0.90f));
        if (purpleSymbolActive4 == null) purpleSymbolActive4 = CreateOutlinedCircleSprite(new Color(0.62f, 0.42f, 0.90f));
    }


    Sprite CreateOutlinedCircleSprite(Color fill)
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f - 0.5f;
        float radius = size * 0.48f;
        float outlineT = radius * 0.30f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                Color col = d <= radius ? (d >= radius - outlineT ? new Color(0f, 0f, 0f, 1f) : fill) : new Color(0f, 0f, 0f, 0f);
                tex.SetPixel(x, y, col);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    Vector3 LocalPos(int row, int col)
    {
        return new Vector3((col - (cols - 1) / 2f) * cellSize, ((rows - 1) / 2f - row) * cellSize, 0);
    }

    void CreateHoleBlocks()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!shape[r, c])
                {
                    var go = new GameObject("HoleBlock");
                    go.transform.SetParent(boardShakeRoot != null ? boardShakeRoot : managerTransform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    Sprite s = pickedHoleBlock != null ? pickedHoleBlock : (holeBlockSprite != null ? holeBlockSprite : squareSprite);
                    sr.sprite = s;
                    sr.color = (pickedHoleBlock != null || holeBlockSprite != null) ? Color.white : new Color(0.93f, 0.94f, 0.96f, 0.35f);
                    sr.sortingOrder = 0;
                    go.transform.localPosition = LocalPos(r, c);
                    go.transform.localScale = ScaleToWidth(s, cellSize * 0.9f * holeBlockScale);
                    holeBlocks.Add(go);
                }
    }

void BuildBoard()
    {
        ClearBoardObjects();

        var shakeRootGo = new GameObject("BoardShakeRoot");
        shakeRootGo.transform.SetParent(managerTransform, false);
        boardShakeRoot = shakeRootGo.transform;
        setupProtectedCells.Clear();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (tiles[r, c] != null) Destroy(tiles[r, c]);
                tiles[r, c] = null;
                colorGrid[r, c] = -1;
            }

        int attempts = 0;
        while (attempts < 40 && CountStoneCandidates() < 6)
        {
            GenerateShape();
            attempts++;
        }

        if (currentLevel <= holeFreeLevels)
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    shape[r, c] = true;

        CreateHoleBlocks();
        CreateBoardBackground();
        CreateBoardTintBackdrop();
        GenerateBestOpening();
        SeedGuaranteedSetups();

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (shape[r, c]) tiles[r, c] = CreateTileObject(r, c, colorGrid[r, c]);

        PlaceStones();
        initialStoneCount = CountStones();
        availableMoves = CountPlayableMoves();
        cellSizeAtLoad = cellSize;
        DrawBoardGrid();
    }


    HashSet<Vector2Int> setupProtectedCells = new HashSet<Vector2Int>();

    static Vector2Int SetupCellKey(int row, int col) => new Vector2Int(row, col);
    static string SetupRegionName(int region) => region == 0 ? "Left" : region == 1 ? "Right" : "Middle";

    void SeedGuaranteedSetups()
    {
        var m = Mech();
        bool any5 = m.yellowTarget || m.greenHeal || m.purpleTransmuter;

        var targets = new List<(int k, int region)>();
        if (any5) { targets.Add((5, 0)); targets.Add((5, 1)); targets.Add((4, 2)); }
        else if (m.match4EndSpawn) { targets.Add((4, 0)); targets.Add((4, 1)); }
        else return;

        var consumed = new HashSet<Vector2Int>();
        foreach (var t in targets)
            SeedOneSetup(t.k, t.region, consumed);
    }

    void SeedOneSetup(int k, int region, HashSet<Vector2Int> consumed)
    {
        const int exactBudget = 28;
        const int fallbackBudget = 12;

        for (int pass = 0; pass < 2; pass++)
        {
            bool exact = pass == 0;
            int budget = exact ? exactBudget : fallbackBudget;
            for (int attempt = 0; attempt < budget; attempt++)
                if (TrySeedSetup(k, region, consumed, exact)) return;
        }
        Debug.LogWarning("Match3 setup L" + currentLevel + ": could not plant a " + k + "-swap in the " + SetupRegionName(region) +
            " half after retry budget - continuing without it (guarantee degraded to best-effort).");
    }

    bool TrySeedSetup(int k, int region, HashSet<Vector2Int> consumed, bool exact)
    {
        bool horizontal = Random.value < 0.5f;
        int lr, lc0;
        if (!PickSetupLine(k, region, horizontal, consumed, out lr, out lc0)) return false;

        int dIdx = Random.Range(Mathf.Max(0, k - 3), Mathf.Min(k - 1, 2) + 1);

        int cColor = RandomEnabledColor();
        int dColor; do { dColor = RandomEnabledColor(cColor); } while (dColor == cColor && EnabledColorCount() > 1);

        int dr = horizontal ? lr : lr + dIdx;
        int dc = horizontal ? lc0 + dIdx : lc0;
        var pCands = new List<int[]>();
        foreach (var off in new[] { new int[] { -1, 0 }, new int[] { 1, 0 }, new int[] { 0, -1 }, new int[] { 0, 1 } })
        {
            int pr = dr + off[0], pc = dc + off[1];
            if (pr < 0 || pr >= rows || pc < 0 || pc >= cols) continue;
            if (!shape[pr, pc]) continue;
            if (OnSetupLine(horizontal, lr, lc0, k, pr, pc)) continue;
            if (consumed.Contains(SetupCellKey(pr, pc))) continue;
            pCands.Add(new int[] { pr, pc });
        }
        if (pCands.Count == 0) return false;
        var p = pCands[Random.Range(0, pCands.Count)];

        var scratch = (int[,])colorGrid.Clone();
        for (int i = 0; i < k; i++) SetSetupLineCell(scratch, horizontal, lr, lc0, i, i == dIdx ? dColor : cColor);
        scratch[p[0], p[1]] = cColor;

        if (BoardHasMatch(scratch)) return false;

        scratch[dr, dc] = cColor;
        scratch[p[0], p[1]] = dColor;
        int runLen = CountSetupRun(scratch, horizontal, lr, lc0, k, cColor);
        if (!(exact ? runLen == k : runLen >= k)) return false;

        for (int i = 0; i < k; i++) SetSetupLineCell(colorGrid, horizontal, lr, lc0, i, i == dIdx ? dColor : cColor);
        colorGrid[p[0], p[1]] = cColor;
        for (int i = 0; i < k; i++)
        {
            int r = horizontal ? lr : lr + i;
            int c = horizontal ? lc0 + i : lc0;
            consumed.Add(SetupCellKey(r, c));
            setupProtectedCells.Add(SetupCellKey(r, c));
        }
        consumed.Add(SetupCellKey(p[0], p[1]));
        setupProtectedCells.Add(SetupCellKey(p[0], p[1]));

        string lineDesc = horizontal ? ("row r=" + lr + " c=" + lc0 + ".." + (lc0 + k - 1)) : ("col c=" + lc0 + " r=" + lr + ".." + (lr + k - 1));
        Debug.Log("Match3 setup L" + currentLevel + ": planted " + (exact ? "EXACT-" : ">=") + k + "-swap in " + SetupRegionName(region) + " (" + lineDesc + ") - run color " + cColor + ", intruder color " + dColor +
            " at (" + dr + "," + dc + "), partner color " + cColor + " at (" + p[0] + "," + p[1] + "); swap those two cells to complete it.");
        return true;
    }

    bool PickSetupLine(int k, int region, bool horizontal, HashSet<Vector2Int> consumed, out int lr, out int lc0)
    {
        lr = 0; lc0 = 0;
        int cMin = 0, cMax = cols - 1, rMin = 0, rMax = rows - 1;
        if (region == 0) cMax = cols / 2 - 1;
        else if (region == 1) cMin = (cols + 1) / 2;
        else { cMin = Mathf.CeilToInt(cols * 0.3f); cMax = Mathf.FloorToInt(cols * 0.7f); rMin = Mathf.CeilToInt(rows * 0.3f); rMax = Mathf.FloorToInt(rows * 0.7f); }

        if (horizontal)
        {
            if (cMax - cMin + 1 < k) return false;
            lr = Random.Range(rMin, rMax + 1);
            lc0 = Random.Range(cMin, cMax - k + 2);
        }
        else
        {
            if (rMax - rMin + 1 < k) return false;
            lc0 = Random.Range(cMin, cMax + 1);
            lr = Random.Range(rMin, rMax - k + 2);
        }

        for (int i = 0; i < k; i++)
        {
            int r = horizontal ? lr : lr + i;
            int c = horizontal ? lc0 + i : lc0;
            if (!shape[r, c]) return false;
            if (consumed.Contains(SetupCellKey(r, c))) return false;
        }
        return true;
    }

    bool OnSetupLine(bool horizontal, int lr, int lc0, int k, int r, int c)
    {
        if (horizontal) return r == lr && c >= lc0 && c < lc0 + k;
        return c == lc0 && r >= lr && r < lr + k;
    }

    void SetSetupLineCell(int[,] g, bool horizontal, int lr, int lc0, int i, int value)
    {
        if (horizontal) g[lr, lc0 + i] = value; else g[lr + i, lc0] = value;
    }

    int CountSetupRun(int[,] g, bool horizontal, int lr, int lc0, int k, int color)
    {
        int len = k;
        if (horizontal)
        {
            for (int c = lc0 - 1; c >= 0 && g[lr, c] == color; c--) len++;
            for (int c = lc0 + k; c < cols && g[lr, c] == color; c++) len++;
        }
        else
        {
            for (int r = lr - 1; r >= 0 && g[r, lc0] == color; r--) len++;
            for (int r = lr + k; r < rows && g[r, lc0] == color; r++) len++;
        }
        return len;
    }

    GameObject CreateTileObject(int row, int col, int colorIdx)
    {
        var go = new GameObject("Tile_" + row + "_" + col);
        go.transform.SetParent(boardShakeRoot != null ? boardShakeRoot : managerTransform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        Sprite s = GetTileSprite(colorIdx);
        sr.sprite = s;
        bool custom = tileSprites != null && colorIdx >= 0 && colorIdx < tileSprites.Length && tileSprites[colorIdx] != null;
        sr.color = (custom || IsSpecial(colorIdx)) ? Color.white : tileColors[colorIdx];
        sr.sortingOrder = 1;
        go.transform.localPosition = LocalPos(row, col);
        go.transform.localScale = TileScale(colorIdx);
        return go;
    }


    void GenerateShape()
    {
        int attempts = 0;
        while (true)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols / 2; c++)
                {
                    bool on = Random.value < shapeDensity;
                    shape[r, c] = on;
                    shape[r, cols - 1 - c] = on;
                }
            if (cols % 2 == 1)
                for (int r = 0; r < rows; r++)
                    shape[r, cols / 2] = Random.value < shapeDensity;

            int cellCount = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (shape[r, c]) cellCount++;

            if (cellCount >= (rows * cols) / 2 || attempts >= 10) break;
            attempts++;
        }
    }


    void GenerateBestOpening()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                colorGrid[r, c] = shape[r, c] ? RandomEnabledColor() : -1;

        int repairPasses = 0;
        while (BoardHasMatch(colorGrid) && repairPasses < 200)
        {
            repairPasses++;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (!shape[r, c] || !CreatesMatchAt(colorGrid, r, c)) continue;
                    int orig = colorGrid[r, c];
                    for (int t = 0; t < tileColors.Length * 3; t++)
                    {
                        int col = RandomEnabledColor();
                        if (col == orig) continue;
                        colorGrid[r, c] = col;
                        if (!CreatesMatchAt(colorGrid, r, c)) break;
                        colorGrid[r, c] = orig;
                    }
                }
        }

        if (BoardHasMatch(colorGrid))
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    colorGrid[r, c] = shape[r, c] ? (r + c) % tileColors.Length : -1;

        TuneToTarget();

        openingMoveCount = CountValidMoves(colorGrid);
        Debug.Log("Match3: starting board has " + openingMoveCount + " legal moves (target ~" + targetOpeningMoves + ")");
    }

    void TuneToTarget()
    {
        int iter = 0;
        bool improved = true;
        while (improved && iter < localRepairCap)
        {
            improved = false;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (!shape[r, c]) continue;
                    if (CountValidMoves(colorGrid) == targetOpeningMoves) break;

                    int orig = colorGrid[r, c];
                    int bestScore = Mathf.Abs(CountValidMoves(colorGrid) - targetOpeningMoves);
                    int bestColor = orig;

                    for (int col = 0; col < tileColors.Length; col++)
                    {
                        if (col == orig) continue;
                        colorGrid[r, c] = col;
                        if (!BoardHasMatch(colorGrid))
                        {
                            int nv = CountValidMoves(colorGrid);
                            int sc = Mathf.Abs(nv - targetOpeningMoves);
                            if (sc < bestScore) { bestScore = sc; bestColor = col; }
                        }
                        colorGrid[r, c] = orig;
                    }

                    if (bestColor != orig) { colorGrid[r, c] = bestColor; improved = true; }
                }
            iter++;
        }
    }

    bool BoardHasMatch(int[,] g)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (CreatesMatchAt(g, r, c)) return true;
        return false;
    }

    int CountValidMoves(int[,] g)
    {
        int count = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (g[r, c] == -1) continue;
                if (c + 1 < cols && g[r, c + 1] != -1 && SwapCreatesMatch(g, r, c, r, c + 1)) count++;
                if (r + 1 < rows && g[r + 1, c] != -1 && SwapCreatesMatch(g, r, c, r + 1, c)) count++;
            }
        return count;
    }

    bool SwapCreatesMatch(int[,] g, int r1, int c1, int r2, int c2)
    {
        int tmp = g[r1, c1]; g[r1, c1] = g[r2, c2]; g[r2, c2] = tmp;
        bool m = CreatesMatchAt(g, r1, c1) || CreatesMatchAt(g, r2, c2);
        int t2 = g[r1, c1]; g[r1, c1] = g[r2, c2]; g[r2, c2] = t2;
        return m;
    }

    bool CreatesMatchAt(int[,] g, int r, int c)
    {
        int col = g[r, c];
        if (col == -1 || IsSpecial(col)) return false;
        int cnt = 1;
        for (int i = c - 1; i >= 0 && g[r, i] == col; i--) cnt++;
        for (int i = c + 1; i < cols && g[r, i] == col; i++) cnt++;
        if (cnt >= 3) return true;
        cnt = 1;
        for (int i = r - 1; i >= 0 && g[i, c] == col; i--) cnt++;
        for (int i = r + 1; i < rows && g[i, c] == col; i++) cnt++;
        return cnt >= 3;
    }

    int ScoreForRun(int len)
    {
        if (len >= 5) return 100;
        if (len == 4) return 50;
        return 30;
    }

    Sprite GetTileSprite(int colorIdx)
    {
        if (colorIdx == SpecialYellowCircle) return yellowCircleSprite != null ? yellowCircleSprite : circleSprite;
        if (colorIdx == SpecialPurpleTransmuter) return purpleSymbolIdle != null ? purpleSymbolIdle : circleSprite;
        if (pickedTiles != null && colorIdx >= 0 && colorIdx < pickedTiles.Length && pickedTiles[colorIdx] != null) return pickedTiles[colorIdx];
        if (tileSprites != null && colorIdx >= 0 && colorIdx < tileSprites.Length && tileSprites[colorIdx] != null)
            return tileSprites[colorIdx];
        return circleSprite;
    }

    float TileScaleMult(int colorIdx)
    {
        return tileSpriteScales != null && colorIdx >= 0 && colorIdx < tileSpriteScales.Length ? tileSpriteScales[colorIdx] : 1f;
    }


    float SpriteWorldWidth(Sprite s)
    {
        return s.rect.width / (float)s.pixelsPerUnit;
    }

    Vector3 ScaleToWidth(Sprite s, float targetWorldW)
    {
        float w = SpriteWorldWidth(s);
        float k = w > 0f ? targetWorldW / w : 1f;
        return new Vector3(k, k, 1f);
    }

    Vector3 TileScale(int colorIdx)
    {
        return ScaleToWidth(GetTileSprite(colorIdx), cellSize * 0.85f * TileScaleMult(colorIdx));
    }

    Sprite[] bgSpriteCache = new Sprite[totalLevels];
    float boardBgUserMult = 1f;

    void CreateBoardBackground()
    {
        Texture2D tex = pickedBgTex != null && currentLevel >= 1 && currentLevel <= pickedBgTex.Length ? pickedBgTex[currentLevel - 1] : null;
        if (tex == null && levelBackgrounds != null && currentLevel >= 1 && currentLevel <= levelBackgrounds.Length) tex = levelBackgrounds[currentLevel - 1];
        if (tex == null) return;
        var go = new GameObject("BoardBackground");
        go.transform.SetParent(managerTransform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        int bgIdx = currentLevel - 1;
        if (bgSpriteCache[bgIdx] == null || bgSpriteCache[bgIdx].texture != tex)
            bgSpriteCache[bgIdx] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        sr.sprite = bgSpriteCache[bgIdx];
        sr.sortingOrder = -10;
        go.transform.localPosition = new Vector3(cam != null ? cam.transform.position.x : 0f, cam != null ? cam.transform.position.y : 0f, 0f);
        float bgScale = levelBackgroundScales != null && currentLevel - 1 < levelBackgroundScales.Length ? levelBackgroundScales[currentLevel - 1] : 1f;
        boardBgUserMult = bgScale;
        float worldW = sr.sprite.rect.width / (float)sr.sprite.pixelsPerUnit;
        float worldH = sr.sprite.rect.height / (float)sr.sprite.pixelsPerUnit;
        float screenH = cam != null ? 2f * cam.orthographicSize : rows * cellSize;
        float screenW = cam != null ? screenH * Mathf.Max(0.1f, cam.aspect) : cols * cellSize;
        float hugScale = Mathf.Max(screenH / Mathf.Max(0.01f, worldH), screenW / Mathf.Max(0.01f, worldW));
        go.transform.localScale = new Vector3(hugScale * bgScale, hugScale * bgScale, 1f);
    }

    Sprite boardTintBase = null;

    void CreateBoardTintBackdrop()
    {
        if (!boardTintEnabled) return;
        Color c = levelBackdropColors != null && currentLevel >= 1 && currentLevel <= levelBackdropColors.Length
            ? levelBackdropColors[currentLevel - 1] : Color.black;
        c.a = Mathf.Clamp01(boardTintAlpha);

        if (boardTintBase == null)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            boardTintBase = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        var go = new GameObject("BoardTint");
        go.transform.SetParent(managerTransform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boardTintBase;
        sr.color = c;
        sr.sortingOrder = -6;
        float w = SpriteWorldWidth(sr.sprite);
        float h = sr.sprite.rect.height / (float)sr.sprite.pixelsPerUnit;
        go.transform.localScale = new Vector3((cols * cellSize) / w, (rows * cellSize) / h, 1f);
        go.transform.localPosition = Vector3.zero;
    }

    int boardGen = 0;
    float flyInEndsAt = -1f;


    void ClearBoardObjects()
    {
        boardGen++;
        for (int i = managerTransform.childCount - 1; i >= 0; i--)
            Destroy(managerTransform.GetChild(i).gameObject);
        BuildSfxPool();
        damagedThisPass.Clear();
        stoneHits.Clear(); stonesDestroyedLastResolve = 0;
        purpleStoredColor.Clear();

        scorePops.Clear();
        resolveWaveDepth = 0;
        StopBoardShakeSnap();
        holeBlocks.Clear();
        shakeLocks.Clear();
    }


    struct TileFly { public Transform t; public Vector3 from, to; public float delay, dur; }

    IEnumerator TileFlyInRoutine()
    {
        var flies = new List<TileFly>();
        float orthoH = cam != null ? cam.orthographicSize : 10f;
        float aspect = (cam != null && cam.aspect > 0) ? cam.aspect : ((float)Screen.width / Screen.height);
        float spawnDist = Mathf.Sqrt(orthoH * orthoH + (orthoH * aspect) * (orthoH * aspect)) * 1.15f;

        float viewCx = cam != null ? managerTransform.InverseTransformPoint(new Vector3(cam.transform.position.x, 0f, 0f)).x : 0f;
        float diagView = Mathf.Sqrt(orthoH * orthoH + (orthoH * aspect) * (orthoH * aspect));
        int n = 0, i = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (shape[r, c] && tiles[r, c] != null) n++;

        float staggerWindow = 1.4f;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (shape[r, c] && tiles[r, c] != null)
                {
                    Vector3 to = LocalPos(r, c);
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    var tf = new TileFly();
                    tf.t = tiles[r, c].transform;
                    tf.to = to;
                    tf.from = to + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * spawnDist;
                    if (tf.from.magnitude < minSpawnDist)
                    {
                        Vector3 dirOut = tf.from.sqrMagnitude > 1e-4f ? tf.from.normalized : new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                        tf.from = dirOut * minSpawnDist;
                    }

                    if (cam != null && cam.transform.position.x != 0f)
                    {
                        Vector2 rel = new Vector2(tf.from.x - viewCx, tf.from.y);
                        float guardR = diagView * 1.05f + cellSize;
                        if (rel.sqrMagnitude > 1e-6f && rel.sqrMagnitude <= guardR * guardR)
                            tf.from = new Vector3(viewCx + rel.normalized.x * guardR, rel.normalized.y * guardR, 0f);
                    }
                    tf.delay = (n > 1 ? i / (float)(n - 1) : 0f) * staggerWindow;
                    tf.dur = 0.9f + Random.Range(0f, 0.5f);
                    flies.Add(tf);
                    tiles[r, c].transform.localPosition = tf.from;
                    i++;
                }

        busy = true;
        int myGen = boardGen;
        float elapsed = 0f, endT = 0f;
        foreach (var f in flies) endT = Mathf.Max(endT, f.delay + f.dur);
        flyInEndsAt = Time.time + endT;

        while (elapsed < endT)
        {
            if (boardGen != myGen) { busy = false; yield break; }
            for (int k = 0; k < flies.Count; k++)
            {
                var f = flies[k];
                if (elapsed <= f.delay) continue;
                float t = Mathf.Clamp01((elapsed - f.delay) / f.dur);
                float kk = EaseOutBack(t);
                f.t.localPosition = Vector3.Lerp(f.from, f.to, kk);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (boardGen != myGen) { busy = false; yield break; }
        for (int k = 0; k < flies.Count; k++) flies[k].t.localPosition = flies[k].to;
        busy = false;
    }
}

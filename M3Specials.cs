using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public partial class Match3Game : MonoBehaviour
{


    const int SpecialYellowCircle = -2;
    const int SpecialPurpleTransmuter = -3;

    bool IsSpecial(int v) { return v <= -2; }
    bool IsColorCode(int v) { return v >= 0 && v < tileColors.Length; }


    const int ColorRed = 0;
    const int ColorBlue = 2;
    const int ColorGreen = 1;
    const int ColorYellow = 3;
    const int ColorPurple = 4;

    const int BeamBlastScorePerBubble = 10;
    const int SpecialBeamStoneHits = 3;
    const int YellowCircleScorePerBubble = 10;
    const float SpecialClearSpeedup = 3f;


    List<Vector2Int> FindColorCells(int colorIdx)
    {
        var list = new List<Vector2Int>();
        if (!IsColorCode(colorIdx)) return list;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!CellHasStone(r, c) && colorGrid[r, c] == colorIdx) list.Add(new Vector2Int(c, r));
        return list;
    }

    void PlaceSpecialEntity(Vector2Int cell, int code)
    {
        if (!IsSpecial(code)) return;
        if (cell.y < 0 || cell.y >= rows || cell.x < 0 || cell.x >= cols) return;
        if (!autoDemo) DailyTrackSpecialSpawn();
        colorGrid[cell.y, cell.x] = code;
        tiles[cell.y, cell.x] = CreateTileObject(cell.y, cell.x, code);
    }


    IEnumerator OnMatchRunFound(List<Vector2Int> runCells, int colorIdx)
    {
        int len = runCells.Count;
        if (len == 4)
        {
            if (!MechMatch4()) yield break;
            GetRunEndCells(runCells, out Vector2Int e0, out Vector2Int e1);
            QueueEndSpawn(e0, EndSpawnColor(e0, e1, colorIdx));
            QueueEndSpawn(e1, EndSpawnColor(e1, e0, colorIdx));
        }
        else if (len >= 5)
        {
            if (RunBecomesBeam(colorIdx, len))
                yield return BeamZapRoutine(runCells, colorIdx);
            else if (colorIdx == ColorYellow && MechYellowTarget())
            {
                Vector2Int anchor = runCells[Mathf.Min(len / 2, len - 1)];
                PlaceSpecialEntity(anchor, SpecialYellowCircle);
                var go = tiles[anchor.y, anchor.x];
                if (go != null) StartCoroutine(SpawnPopIn(go, TileScale(SpecialYellowCircle)));
            }
            else if (colorIdx == ColorGreen && MechGreenHeal())
                RefillHpNow(runCells[Mathf.Min(len / 2, len - 1)]);
            else if (colorIdx == ColorPurple && MechPurpleTransmuter())
            {
                Vector2Int anchor = runCells[Mathf.Min(len / 2, len - 1)];
                PlaceSpecialEntity(anchor, SpecialPurpleTransmuter);
                var go = tiles[anchor.y, anchor.x];
                if (go != null) StartCoroutine(SpawnPopIn(go, TileScale(SpecialPurpleTransmuter)));
            }
        }
    }


    struct ZapExtra { public Vector2Int cell; public int dist; public int color; }
    IEnumerator BeamZapRoutine(List<Vector2Int> runCells, int colorIdx)
    {
        bool horizontal = runCells[0].x != runCells[runCells.Count - 1].x;

        Vector2Int anchor = runCells[Mathf.Min(runCells.Count / 2, runCells.Count - 1)];
        int r0 = anchor.y;
        int c0 = anchor.x;
        var runSet = new HashSet<Vector2Int>(runCells);

        var extras = new List<ZapExtra>();
        for (int dir = -1; dir <= 1; dir += 2)
            for (int i = 1; ; i++)
            {
                int r = horizontal ? r0 : r0 + dir * i;
                int c = horizontal ? c0 + dir * i : c0;
                if (r < 0 || r >= rows || c < 0 || c >= cols) break;
                var cell = new Vector2Int(c, r);
                if (runSet.Contains(cell)) continue;
                if (CellHasStone(r, c)) break;
                int v = colorGrid[r, c];
                if (v == -1) break;
                if (IsSpecial(v)) continue;
                if (IsColorCode(v) && tiles[r, c] != null)
                    extras.Add(new ZapExtra { cell = cell, dist = i, color = v });
            }

        int totalDestroyed = runCells.Count + extras.Count;

        score -= ScoreForRun(runCells.Count);
        score += BeamBlastScorePerBubble * totalDestroyed;

        foreach (var rc in runCells) SpawnScorePop(rc, colorIdx, BeamBlastScorePerBubble, false);
        foreach (var cell in runSet) beamBlastCells.Add(cell);
        foreach (var e in extras) beamBlastCells.Add(e.cell);

        float leftFar = 0f, rightFar = 0f;
        foreach (var cell in runSet)
        {
            int off = horizontal ? cell.x - c0 : cell.y - r0;
            if (off < 0) leftFar = Mathf.Max(leftFar, -off); else rightFar = Mathf.Max(rightFar, off);
        }
        foreach (var e in extras)
        {
            bool onLeft = horizontal ? e.cell.x < c0 : e.cell.y < r0;
            if (onLeft) leftFar = Mathf.Max(leftFar, e.dist); else rightFar = Mathf.Max(rightFar, e.dist);
        }

        foreach (var e in extras)
        {
            var go = tiles[e.cell.y, e.cell.x];
            colorGrid[e.cell.y, e.cell.x] = -1;
            tiles[e.cell.y, e.cell.x] = null;
            bool onLeft = horizontal ? e.cell.x < c0 : e.cell.y < r0;
            float sideFar = Mathf.Max(0.01f, onLeft ? leftFar : rightFar);
            float popAt = zapTravelTime * e.dist / sideFar;
            if (go != null) StartCoroutine(PopTile(go, popAt));
            StartCoroutine(ZapBubbleGold(e.cell, e.color, popAt));
        }

        var beamGo = new GameObject("M3_ZapBeam");
        beamGo.transform.SetParent(managerTransform, false);
        var sr = beamGo.AddComponent<SpriteRenderer>();
        Sprite s = squareSprite != null ? squareSprite : CreateSquareSprite();
        sr.sprite = s;
        Color tint = tileColors[colorIdx];
        sr.color = new Color(tint.r, tint.g, tint.b, 0.85f);
        sr.sortingOrder = 3;

        PlaySfx(SfxSlot.Zap, managerTransform.TransformPoint(LocalPos(r0, c0)));

        Vector3 bSize = s.bounds.size;
        float thick = Mathf.Max(0.05f, cellSize * beamLineThickness);
        float travel = Mathf.Max(0.01f, zapTravelTime);

        float t = 0f;
        while (t < travel)
        {
            if (beamGo == null || managerTransform == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / travel);
            SetZapBeamExtent(beamGo, bSize, thick, horizontal, r0, c0, leftFar * k, rightFar * k);
            yield return null;
        }
        if (beamGo == null || managerTransform == null) yield break;
        SetZapBeamExtent(beamGo, bSize, thick, horizontal, r0, c0, leftFar, rightFar);
        yield return BeamFlashFade(beamGo);
    }

    IEnumerator ZapBubbleGold(Vector2Int cell, int colorIdx, float delay)
    {
        if (delay > 0f) yield return WaitGameSeconds(delay);
        SpawnScorePop(cell, colorIdx, BeamBlastScorePerBubble, false);
    }

    void SetZapBeamExtent(GameObject beamGo, Vector3 bSize, float thick, bool horizontal, int r0, int c0, float leftCells, float rightCells)
    {
        Vector3 pA = LocalPos(r0, c0);
        Vector3 pMin = horizontal ? pA - new Vector3(leftCells * cellSize, 0f, 0f) : pA + new Vector3(0f, leftCells * cellSize, 0f);
        Vector3 pMax = horizontal ? pA + new Vector3(rightCells * cellSize, 0f, 0f) : pA - new Vector3(0f, rightCells * cellSize, 0f);
        beamGo.transform.localPosition = (pMin + pMax) * 0.5f;
        float lineLen = Mathf.Max(0.01f, Vector3.Distance(pMin, pMax)) + cellSize;
        beamGo.transform.localScale = new Vector3(horizontal ? lineLen / bSize.x : thick / bSize.x, horizontal ? thick / bSize.y : lineLen / bSize.y, 1f);
    }

    IEnumerator BeamFlashFade(GameObject go)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        Color c0 = sr.color;
        float t = 0f;
        while (t < beamFlashDur && go != null)
        {
            if (go == null || sr == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / beamFlashDur);
            sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - k));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    static void GetRunEndCells(List<Vector2Int> cells, out Vector2Int e0, out Vector2Int e1)
    {
        if (cells.Count < 2) { e0 = cells[0]; e1 = cells[0]; return; }
        bool vertical = cells[0].x == cells[cells.Count - 1].x;
        if (vertical)
        {
            Vector2Int top = cells[0], bottom = cells[0];
            foreach (var c in cells) { if (c.y < top.y) top = c; if (c.y > bottom.y) bottom = c; }
            e0 = top; e1 = bottom;
        }
        else
        {
            Vector2Int left = cells[0], right = cells[0];
            foreach (var c in cells) { if (c.x < left.x) left = c; if (c.x > right.x) right = c; }
            e0 = left; e1 = right;
        }
    }

    int EndSpawnColor(Vector2Int endCell, Vector2Int otherEnd, int ownColor)
    {
        bool horizontal = endCell.y == otherEnd.y;
        var n = horizontal ? new Vector2Int(endCell.x + (endCell.x < otherEnd.x ? -1 : 1), endCell.y)
                           : new Vector2Int(endCell.x, endCell.y + (endCell.y < otherEnd.y ? -1 : 1));
        if (n.y < 0 || n.y >= rows || n.x < 0 || n.x >= cols) return ownColor;
        int v = colorGrid[n.y, n.x];
        return IsColorCode(v) ? v : ownColor;
    }


    struct M3EndSpawn { public Vector2Int cell; public int colorIdx; }
    List<M3EndSpawn> pendingEndSpawns = new List<M3EndSpawn>();

    HashSet<Vector2Int> beamBlastCells = new HashSet<Vector2Int>();

    void QueueEndSpawn(Vector2Int cell, int colorIdx)
    {
        if (cell.y < 0 || cell.y >= rows || cell.x < 0 || cell.x >= cols) return;
        if (CellHasStone(cell.y, cell.x)) return;
        if (colorGrid[cell.y, cell.x] != -1) return;
        foreach (var p in pendingEndSpawns) if (p.cell == cell) return;
        pendingEndSpawns.Add(new M3EndSpawn { cell = cell, colorIdx = colorIdx });
    }

    IEnumerator FlushRunSpawns()
    {
        if (pendingEndSpawns.Count == 0) yield break;

        var gos = new List<GameObject>();
        var fullScales = new List<Vector3>();
        foreach (var sp in pendingEndSpawns)
        {
            colorGrid[sp.cell.y, sp.cell.x] = sp.colorIdx;
            var go = CreateTileObject(sp.cell.y, sp.cell.x, sp.colorIdx);
            tiles[sp.cell.y, sp.cell.x] = go;
            gos.Add(go);
            fullScales.Add(TileScale(sp.colorIdx));
        }

        float t = 0f;
        while (t < specialSpawnIn)
        {
            bool anyAlive = false;
            for (int i = 0; i < gos.Count; i++)
                if (gos[i] != null)
                {
                    anyAlive = true;
                    float k = Mathf.Clamp01(t / specialSpawnIn);
                    float e = k * k * (3f - 2f * k);
                    gos[i].transform.localScale = fullScales[i] * Mathf.Lerp(0.02f, 1f, e);
                }
            if (!anyAlive) break;
            t += Time.deltaTime;
            yield return null;
        }
        for (int i = 0; i < gos.Count; i++) if (gos[i] != null) gos[i].transform.localScale = fullScales[i];
        pendingEndSpawns.Clear();
    }

    IEnumerator DropOnSpecialRoutine(Vector2Int a, Vector2Int b)
    {
        int va = colorGrid[a.y, a.x];
        int vb = colorGrid[b.y, b.x];
        if (IsSpecial(va) && IsSpecial(vb)) { Deselect(); yield break; }

        Vector2Int specialCell = IsSpecial(va) ? a : b;
        Vector2Int draggedCell = IsSpecial(va) ? b : a;
        int draggedColor = IsSpecial(va) ? vb : va;

        busy = true; Deselect(); idleTimer = 0f; hintRepeating = false;
        stonesDestroyedLastResolve = 0; swapGenAtStart = boardGen;
        if (!autoDemo) movesMade++;

        pendingSpecialClears.Clear(); pendingSpecialPops.Clear(); pendingSuckGos.Clear(); pendingSuckOrigins.Clear(); pendingSuckScales.Clear(); pendingSuckActive = false; suckEntityGo = null;
        OnTileDroppedOnSpecial(specialCell, draggedCell, draggedColor);

        if (pendingSpecialClears.Count == 0)
        {
            if (!autoDemo) movesMade--;
            busy = false;
            yield break;
        }
        TriggerBoardShakeSpecial();

        if (pendingSuckActive && pendingSuckGos.Count > 0)
            yield return SuckIntoCircleRoutine();

        if (pendingSpecialPops.Count > 0)
        {
            float step = popDelay / SpecialClearSpeedup;
            for (int i = 0; i < pendingSpecialPops.Count; i++)
                StartCoroutine(PopTile(pendingSpecialPops[i], step * Mathf.Min(i, maxStaggeredPops)));
            yield return WaitGameSeconds(step * Mathf.Min(Mathf.Max(1, pendingSpecialPops.Count) - 1, maxStaggeredPops) + clearTime);
        }

        BeginDamagePass();
        var hitStones = CollectAdjacentStones(new HashSet<Vector2Int>(pendingSpecialClears));
        if (hitStones.Count > 0)
        {
            yield return WaitGameSeconds(stoneBreakDelay);
            foreach (var s in hitStones) DamageStone(s);
        }

        var preSettle = ApplyGravityAndRefill();
        yield return AnimateFalls(preSettle);

        yield return ResolveCascades(specialCell);

        busy = false;
        ApplyMoveHp(); CheckWin(); availableMoves = CountPlayableMoves();
        resolveWaveDepth = 0;
    }

    void OnTileDroppedOnSpecial(Vector2Int specialCell, Vector2Int draggedCell, int draggedColor)
    {
        int code = colorGrid[specialCell.y, specialCell.x];
        if (code == SpecialYellowCircle) YellowTargetEffect(specialCell, draggedColor);
        else PurpleTransmuterEffect(specialCell, draggedCell, draggedColor);
    }

    void YellowTargetEffect(Vector2Int specialCell, int draggedColor)
    {
        var targets = FindColorCells(draggedColor);
        if (targets.Count < 2)
        {
            StartCoroutine(ShakeTile(specialCell, false));
            return;
        }

        pendingSuckActive = true;
        suckCenterLocal = LocalPos(specialCell.y, specialCell.x);
        foreach (var t in targets)
        {
            var go = tiles[t.y, t.x];
            if (go != null && !pendingSuckGos.Contains(go))
            {
                pendingSuckGos.Add(go);
                pendingSuckOrigins.Add(go.transform.localPosition);
                pendingSuckScales.Add(go.transform.localScale);
            }
        }
        suckEntityGo = tiles[specialCell.y, specialCell.x];
        PlaySfx(SfxSlot.BlackHoleSuck, managerTransform.TransformPoint(suckCenterLocal));

        ClearForSpecial(specialCell);
        foreach (var t in targets) ClearForSpecial(t);
        score += YellowCircleScorePerBubble * targets.Count;

        foreach (var t in targets) SpawnScorePop(t, draggedColor, YellowCircleScorePerBubble, true, false);
    }

    IEnumerator SuckIntoCircleRoutine()
    {
        if (!autoDemo) DailyTrackBlackHoleUse();
        float dur = Mathf.Max(0.1f, suckInTime);
        Vector3 center = suckCenterLocal;

        var delays = new List<float>();
        float maxDist = 0.001f, maxDelay = 0f;
        foreach (var o in pendingSuckOrigins) maxDist = Mathf.Max(maxDist, Vector2.Distance(o, center));
        for (int i = 0; i < pendingSuckGos.Count; i++)
        {
            float d = Vector2.Distance(pendingSuckOrigins[i], center) / maxDist * Mathf.Max(0f, suckStaggerSpan);
            delays.Add(d);
            if (d > maxDelay) maxDelay = d;
        }

        float t = 0f, total = dur + maxDelay;
        while (t < total && managerTransform != null)
        {
            bool anyAlive = false;
            for (int i = 0; i < pendingSuckGos.Count; i++)
            {
                var go = pendingSuckGos[i];
                if (go == null) continue;
                float ti = t - delays[i];
                if (ti <= 0f) { anyAlive = true; continue; }
                float k = Mathf.Clamp01(ti / dur);
                if (k >= 1f)
                {
                    go.transform.localPosition = center;
                    pendingSpecialPops.Remove(go);
                    Destroy(go);
                    continue;
                }
                anyAlive = true;
                float e = k * k;
                go.transform.localPosition = Vector3.Lerp(pendingSuckOrigins[i], center, e);
            }
            if (!anyAlive) break;
            t += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < pendingSuckGos.Count; i++)
        {
            var go = pendingSuckGos[i];
            if (go != null)
            {
                go.transform.localPosition = center;
                pendingSpecialPops.Remove(go);
                Destroy(go);
            }
        }

        if (suckEntityGo != null) { pendingSpecialPops.Remove(suckEntityGo); Destroy(suckEntityGo); }
        suckEntityGo = null;
        pendingSuckGos.Clear(); pendingSuckOrigins.Clear(); pendingSuckScales.Clear(); pendingSuckActive = false;
    }

    List<Vector2Int> pendingSpecialClears = new List<Vector2Int>();
    List<GameObject> pendingSpecialPops = new List<GameObject>();
    Vector3 suckCenterLocal;
    List<GameObject> pendingSuckGos = new List<GameObject>();
    List<Vector3> pendingSuckOrigins = new List<Vector3>();
    List<Vector3> pendingSuckScales = new List<Vector3>();
    bool pendingSuckActive;
    GameObject suckEntityGo;

    void ClearForSpecial(Vector2Int cell)
    {
        if (cell.y < 0 || cell.y >= rows || cell.x < 0 || cell.x >= cols) return;
        if (colorGrid[cell.y, cell.x] == -1) return;
        var go = tiles[cell.y, cell.x];
        colorGrid[cell.y, cell.x] = -1;
        tiles[cell.y, cell.x] = null;
        pendingSpecialClears.Add(cell);
        if (go != null && !pendingSpecialPops.Contains(go)) pendingSpecialPops.Add(go);
    }

    void RefillHpNow(Vector2Int anchor)
    {
        if (autoDemo) return;
        greenHealFiredLastResolve = true;
        int missing = Mathf.Max(0, maxHp - hp);
        if (missing <= 0) return;

        var emptyRects = GetEmptyLifeSegmentRects();
        int n = Mathf.Min(missing, emptyRects.Count);
        if (n <= 0) return;

        Vector3 originLocal = LocalPos(anchor.y, anchor.x);
        PlaySfx(SfxSlot.GreenHeal, managerTransform.TransformPoint(originLocal));
        for (int i = 0; i < n; i++)
            StartCoroutine(HealBlockRoutine(originLocal, emptyRects[i], i));
    }

    IEnumerator HealBlockRoutine(Vector3 originLocal, Rect targetRect, int index)
    {
        var go = new GameObject("M3_HealBlock");
        go.transform.SetParent(managerTransform, false);
        if (go == null || managerTransform == null) yield break;
        Sprite s = healBlockSprite != null ? healBlockSprite : (squareSprite != null ? squareSprite : CreateSquareSprite());
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = 6;
        Vector3 fullScale = ScaleToWidth(s, cellSize * 0.7f);

        go.transform.localPosition = originLocal;
        StartCoroutine(SpawnPopIn(go, fullScale));

        float delay = index * Mathf.Max(0f, healBlockLaunchGap);
        if (delay > 0f) yield return WaitGameSeconds(delay);

        var c = cam != null ? cam : Camera.main;
        Vector3 targetLocal = originLocal;
        if (c != null && cellSize > 0f)
        {
            Vector3 w = c.ScreenToWorldPoint(new Vector3(targetRect.x + targetRect.width * 0.5f, Screen.height - (targetRect.y + targetRect.height * 0.5f)));
            var l = managerTransform.InverseTransformPoint(w);
            targetLocal = new Vector3(l.x, l.y, 0f);
        }

        float flyDur = Mathf.Max(0.05f, healBlockFlyTime);
        float t = 0f;
        while (t < flyDur && go != null)
        {
            if (go == null || managerTransform == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flyDur);
            float e = k * k * (3f - 2f * k);
            go.transform.localPosition = Vector3.Lerp(originLocal, targetLocal, e);
            yield return null;
        }
        if (go == null) yield break;

        GainHpLive(1);
        if (!autoDemo) DailyTrackHealLand();
        PlaySfx(SfxSlot.HealLand, managerTransform.TransformPoint(targetLocal));

        float pt = 0f; const float popDur = 0.18f;
        Vector3 curScale = go.transform.localScale;
        while (pt < popDur && go != null)
        {
            if (go == null || managerTransform == null) yield break;
            pt += Time.deltaTime;
            float k = Mathf.Clamp01(pt / popDur);
            go.transform.localScale = curScale * Mathf.Lerp(1f, 0.02f, k * k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    IEnumerator SpawnPopIn(GameObject go, Vector3 fullScale)
    {
        if (go == null) yield break;
        go.transform.localScale = fullScale * 0.02f;
        float t = 0f;
        while (t < specialSpawnIn && go != null)
        {
            if (go == null) yield break;
            float k = Mathf.Clamp01(t / specialSpawnIn);
            float e = k * k * (3f - 2f * k);
            go.transform.localScale = fullScale * Mathf.Lerp(0.02f, 1f, e);
            t += Time.deltaTime;
            yield return null;
        }
        if (go != null) go.transform.localScale = fullScale;
    }


    Dictionary<GameObject, int> purpleStoredColor = new Dictionary<GameObject, int>();

    void PurpleTransmuterEffect(Vector2Int specialCell, Vector2Int draggedCell, int draggedColor)
    {
        var entGo = tiles[specialCell.y, specialCell.x];
        if (entGo == null) { ClearForSpecial(specialCell); return; }

        int stored = 0;
        bool armed = purpleStoredColor.TryGetValue(entGo, out stored);
        if (!armed)
        {
            purpleStoredColor[entGo] = draggedColor;
            SetTransmuterSprite(entGo, draggedColor);
            PlaySfx(SfxSlot.PurpleArm, entGo.transform.position);
            ClearForSpecial(draggedCell);
        }
        else if (stored == draggedColor)
        {
            StartCoroutine(ShakeTile(draggedCell, false));
        }
        else
        {
            if (!autoDemo) DailyTrackTransmute();
            purpleStoredColor.Remove(entGo);
            PlaySfx(SfxSlot.PurpleTransmute, entGo.transform.position);

            Vector3 newScale = TileScale(draggedColor);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (!CellHasStone(r, c) && colorGrid[r, c] == stored)
                    {
                        var go = tiles[r, c];
                        colorGrid[r, c] = draggedColor;
                        if (go != null)
                        {
                            var sr = go.GetComponent<SpriteRenderer>();
                            if (sr != null)
                            {
                                sr.sprite = GetTileSprite(draggedColor);
                                bool custom = tileSprites != null && draggedColor >= 0 && draggedColor < tileSprites.Length && tileSprites[draggedColor] != null;
                                sr.color = custom ? Color.white : tileColors[draggedColor];
                            }
                            go.transform.localScale = newScale;
                            StartCoroutine(TransmutePulse(go, newScale));
                        }
                    }

            ClearForSpecial(draggedCell);
            ConsumeEntityNoDamage(specialCell);
        }
    }

    Sprite TransmuterActiveSprite(int colorIdx)
    {
        switch (colorIdx)
        {
            case 0: return purpleSymbolActive0;
            case 1: return purpleSymbolActive1;
            case 2: return purpleSymbolActive2;
            case 3: return purpleSymbolActive3;
            default: return purpleSymbolActive4;
        }
    }

    void SetTransmuterSprite(GameObject entGo, int heldColor)
    {
        var sr = entGo.GetComponent<SpriteRenderer>();
        if (sr == null || !IsColorCode(heldColor)) return;
        Sprite s = TransmuterActiveSprite(heldColor);
        if (s != null) sr.sprite = s;
        entGo.transform.localScale = ScaleToWidth(s != null ? s : purpleSymbolIdle, cellSize * 0.85f);
    }

    void ConsumeEntityNoDamage(Vector2Int cell)
    {
        if (cell.y < 0 || cell.y >= rows || cell.x < 0 || cell.x >= cols) return;
        var go = tiles[cell.y, cell.x];
        colorGrid[cell.y, cell.x] = -1;
        tiles[cell.y, cell.x] = null;
        if (go != null && !pendingSpecialPops.Contains(go)) pendingSpecialPops.Add(go);
    }

    IEnumerator TransmutePulse(GameObject go, Vector3 fullScale)
    {
        const float dur = 0.25f;
        float t = 0f;
        while (t < dur && go != null)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float bump = 1f + 0.35f * Mathf.Sin(Mathf.PI * k);
            go.transform.localScale = fullScale * bump;
            yield return null;
        }
        if (go != null) go.transform.localScale = fullScale;
    }

    struct BlastHit { public Vector2Int cell; public int dist; public int color; }

    void FireItemBlast(Vector2Int center, bool mega)
    {
        if (busy || state != GameState.Playing || paused || won || lost) return;
        int r = center.y, c = center.x;
        if (r < 0 || r >= rows || c < 0 || c >= cols || colorGrid[r, c] == -1) return;
        if (!autoDemo) DailyTrackItemBlast();

        if (mega) megaCrossBlasts = Mathf.Max(0, megaCrossBlasts - 1); else crossBlasts = Mathf.Max(0, crossBlasts - 1);
        SaveCharges();
        TriggerBoardShakeSpecial();

        busy = true; Deselect(); idleTimer = 0f; hintRepeating = false;
        stonesDestroyedLastResolve = 0; swapGenAtStart = boardGen;
        if (!autoDemo) movesMade++;

        PlaySfx(mega ? SfxSlot.MegaCrossBlast : SfxSlot.CrossBlast, managerTransform != null ? managerTransform.TransformPoint(LocalPos(r, c)) : Vector3.zero);
        StartCoroutine(ItemBlastRoutine(center, mega));
    }

    IEnumerator ItemBlastRoutine(Vector2Int center, bool mega)
    {
        int r0 = center.y, c0 = center.x;
        var hits = new List<BlastHit>();

        var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        if (mega) dirs = new[] { dirs[0], dirs[1], dirs[2], dirs[3], new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) };

        int maxDist = 0;
        foreach (var d in dirs)
            for (int i = 1; ; i++)
            {
                int r = r0 + d.y * i, c = c0 + d.x * i;
                if (r < 0 || r >= rows || c < 0 || c >= cols) break;
                var stoneGo = GetStoneOnTile(tiles[r, c]);
                if (stoneGo != null)
                {
                    if (!DestroyStonebySpecial) break;
                    bool broken = DamageStoneDirect(stoneGo, SpecialBeamStoneHits);
                    if (!broken) continue;
                }
                int v = colorGrid[r, c];
                if (v == -1) break;
                if (IsSpecial(v)) continue;
                hits.Add(new BlastHit { cell = new Vector2Int(c, r), dist = i, color = v });
                maxDist = Mathf.Max(maxDist, i);
            }

        int cv = colorGrid[r0, c0];
        var centerStone = GetStoneOnTile(tiles[r0, c0]);
        if (centerStone != null)
        {
            if (DestroyStonebySpecial && DamageStoneDirect(centerStone, SpecialBeamStoneHits) && IsColorCode(cv))
                hits.Add(new BlastHit { cell = new Vector2Int(c0, r0), dist = 0, color = cv });
        }
        else if (cv != -1 && !IsSpecial(cv))
            hits.Add(new BlastHit { cell = new Vector2Int(c0, r0), dist = 0, color = cv });

        score += BeamBlastScorePerBubble * hits.Count;

        foreach (var h in hits)
        {
            var go = tiles[h.cell.y, h.cell.x];
            colorGrid[h.cell.y, h.cell.x] = -1;
            tiles[h.cell.y, h.cell.x] = null;
            if (go != null) StartCoroutine(PopTile(go, Mathf.Max(0f, blastPopStagger) * h.dist));
        }
        foreach (var h in hits) SpawnScorePop(h.cell, h.color, BeamBlastScorePerBubble, false);

        yield return WaitGameSeconds(Mathf.Max(0f, blastPopStagger) * maxDist + clearTime);

        BeginDamagePass();
        var clearedSet = new HashSet<Vector2Int>();
        foreach (var h in hits) clearedSet.Add(h.cell);
        var hitStones = CollectAdjacentStones(clearedSet);
        if (hitStones.Count > 0)
        {
            yield return WaitGameSeconds(stoneBreakDelay);
            foreach (var s in hitStones) DamageStone(s);
        }

        var preSettle = ApplyGravityAndRefill();
        yield return AnimateFalls(preSettle);

        yield return ResolveCascades(center);

        busy = false;
        ApplyMoveHp(); CheckWin(); availableMoves = CountPlayableMoves();
        resolveWaveDepth = 0;
    }
}

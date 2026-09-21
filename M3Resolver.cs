using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public partial class Match3Game : MonoBehaviour
{


    bool ScanMatches(out HashSet<Vector2Int> cells, out int gained)
    {
        var runs = ScanRuns(out cells, out gained);
        return runs.Count > 0;
    }

    IEnumerator WaitGameSeconds(float s)
    {
        float t = 0f;
        while (t < s) { t += Time.deltaTime; yield return null; }
    }

    int CountPlayableMoves()
    {
        int count = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (colorGrid[r, c] == -1) continue;
                if (c + 1 < cols && WouldCreateMatch(new Vector2Int(c, r), new Vector2Int(c + 1, r))) count++;
                if (r + 1 < rows && WouldCreateMatch(new Vector2Int(c, r), new Vector2Int(c, r + 1))) count++;
            }
        return count;
    }


    void AttemptSwap(Vector2Int a, Vector2Int b)
    {
        if (b.x < 0 || b.x >= cols || b.y < 0 || b.y >= rows) return;
        if (!IsAdjacent(a, b)) return;
        if (colorGrid[a.y, a.x] == -1 || colorGrid[b.y, b.x] == -1) return;

        if (CellHasStone(a.y, a.x) || CellHasStone(b.y, b.x)) {
            bool vertical = Mathf.Abs(b.y - a.y) > Mathf.Abs(b.x - a.x);
            if (CellHasStone(a.y, a.x)) StartCoroutine(ShakeTile(a, vertical));
            if (CellHasStone(b.y, b.x)) StartCoroutine(ShakeTile(b, vertical));
            return;
        }

        if (IsSpecial(colorGrid[a.y, a.x]) || IsSpecial(colorGrid[b.y, b.x]))
        {
            StartCoroutine(DropOnSpecialRoutine(a, b));
            return;
        }

        stonesDestroyedLastResolve = 0;
        greenHealFiredLastResolve = false;
        swapGenAtStart = boardGen;
        busy = true;
        if (!autoDemo) movesMade++;
        StartCoroutine(SwapRoutine(a, b));
    }

IEnumerator SwapRoutine(Vector2Int a, Vector2Int b)
    {
        Deselect();
        SwapData(a, b);
        yield return AnimateSlide(tiles[a.y, a.x], LocalPos(a.y, a.x), tiles[b.y, b.x], LocalPos(b.y, b.x));

        HashSet<Vector2Int> cells; int gained;
        if (ScanMatches(out cells, out gained))
        {
            idleTimer = 0f;
            hintRepeating = false;
        PlaySfx(SfxSlot.Swap, managerTransform.TransformPoint(LocalPos(b.y, b.x)));
            yield return WaitGameSeconds(matchPopDelay);
            StartCoroutine(ResolveBoard(b));
        }
        else
        {
            PlaySfx(SfxSlot.Blocked, managerTransform.TransformPoint(LocalPos(b.y, b.x)));
            SwapData(a, b);
            yield return AnimateSlide(tiles[a.y, a.x], LocalPos(a.y, a.x), tiles[b.y, b.x], LocalPos(b.y, b.x));
            busy = false;
            hp += 0;
        }
    }

    void SwapData(Vector2Int a, Vector2Int b)
    {
        int tc = colorGrid[a.y, a.x]; colorGrid[a.y, a.x] = colorGrid[b.y, b.x]; colorGrid[b.y, b.x] = tc;
        GameObject tt = tiles[a.y, a.x]; tiles[a.y, a.x] = tiles[b.y, b.x]; tiles[b.y, b.x] = tt;
    }

    IEnumerator AnimateSlide(GameObject goA, Vector3 targetA, GameObject goB, Vector3 targetB)
    {
        if (goA == null || goB == null) yield break;
        Vector3 startA = goA.transform.localPosition;
        Vector3 startB = goB.transform.localPosition;
        float t = 0f;
        while (t < swapTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / swapTime);
            float e = k * k * (3f - 2f * k);
            goA.transform.localPosition = Vector3.Lerp(startA, targetA, e);
            goB.transform.localPosition = Vector3.Lerp(startB, targetB, e);
            yield return null;
        }
        goA.transform.localPosition = targetA;
        goB.transform.localPosition = targetB;
    }

    void GainHpLive(int amount)
    {
        if (autoDemo) return;
        int before = hp;
        hp = Mathf.Clamp(hp + amount, 0, maxHp);
        if (hp != before) hpFlashTime = Time.unscaledTime;
    }

    void ApplyMoveHp()
    {
        if (autoDemo || boardGen != swapGenAtStart) return;
        int before = hp;
        if (stonesDestroyedLastResolve == 0 && !greenHealFiredLastResolve) hp -= 1;
        hp = Mathf.Clamp(hp, 0, maxHp);
        if (hp != before) hpFlashTime = Time.unscaledTime;
        if (hp <= 0) { lost = true; CloseHelpIfOpen(); }
    }


    void TryAutoSwap()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (c + 1 < cols)
                {
                    var a = new Vector2Int(c, r);
                    var b = new Vector2Int(c + 1, r);
                    if (WouldCreateMatch(a, b)) { AttemptSwap(a, b); return; }
                }
                if (r + 1 < rows)
                {
                    var a = new Vector2Int(c, r);
                    var b = new Vector2Int(c, r + 1);
                    if (WouldCreateMatch(a, b)) { AttemptSwap(a, b); return; }
                }
            }
    }

    bool WouldCreateMatch(Vector2Int a, Vector2Int b)
    {
        if (b.x < 0 || b.x >= cols || b.y < 0 || b.y >= rows) return false;
        if (colorGrid[a.y, a.x] == -1 || colorGrid[b.y, b.x] == -1) return false;
        if (CellHasStone(a.y, a.x) || CellHasStone(b.y, b.x)) return false;
        if (IsSpecial(colorGrid[a.y, a.x]) || IsSpecial(colorGrid[b.y, b.x])) return false;
        int ca = colorGrid[a.y, a.x];
        int cb = colorGrid[b.y, b.x];
        colorGrid[a.y, a.x] = cb; colorGrid[b.y, b.x] = ca;
        HashSet<Vector2Int> cells; int gained;
        bool ok = ScanMatches(out cells, out gained);
        colorGrid[a.y, a.x] = ca; colorGrid[b.y, b.x] = cb;
        return ok;
    }


    IEnumerator ResolveBoard(Vector2Int playerOrigin)
    {
        busy = true;
        Deselect();
        yield return ResolveCascades(playerOrigin);
        busy = false;
        ApplyMoveHp();
        CheckWin();
        availableMoves = CountPlayableMoves();
        resolveWaveDepth = 0;
    }

    IEnumerator ResolveCascades(Vector2Int origin)
    {
        int waves = 0;
        while (true)
        {
            resolveWaveDepth = waves;
            var runs = ScanRuns(out HashSet<Vector2Int> cells, out int gained);
            if (runs.Count == 0) break;
            if (++waves > maxCascadeWaves) { Debug.LogWarning("Match3: cascade wave cap (" + maxCascadeWaves + ") hit - forcing settle"); break; }
            if (!autoDemo && resolveWaveDepth >= 1)
                PlaySfx(resolveWaveDepth == 1 ? SfxSlot.Chain2 : (resolveWaveDepth == 2 ? SfxSlot.Chain3 : SfxSlot.Chain4), managerTransform.TransformPoint(Vector3.zero));
            if (!autoDemo && resolveWaveDepth >= 1)
                TriggerBoardShakeCascade();

            score += gained;
            yield return ClearTiles(cells, origin);
            origin = new Vector2Int(-1, -1);

            foreach (var run in runs)
            {
                if (!RunBecomesBeam(run.colorIdx, run.cells.Count))
                    SpawnScorePopsForRun(run);
                if (run.cells.Count >= 4) yield return OnMatchRunFound(run.cells, run.colorIdx);
            }
            yield return FlushRunSpawns();

            foreach (var bc in beamBlastCells) cells.Add(bc);
            beamBlastCells.Clear();

            BeginDamagePass();
            var hitStones = CollectAdjacentStones(cells);
            if (hitStones.Count > 0)
            {
                yield return WaitGameSeconds(stoneBreakDelay);
                foreach (var s in hitStones) DamageStone(s);
            }

            List<M3Fall> falls = ApplyGravityAndRefill();
            yield return AnimateFalls(falls);
        }
    }


    struct ScorePop { public Vector3 worldPos; public Color color; public string text; public float age; public float dur; public bool isChain; public Vector2 screenOff; }
    List<ScorePop> scorePops = new List<ScorePop>();
    int resolveWaveDepth = 0;

    void SpawnScorePop(Vector2Int cell, int colorIdx, int amount, bool special) => SpawnScorePop(cell, colorIdx, amount, special, true);

    void SpawnScorePop(Vector2Int cell, int colorIdx, int amount, bool special, bool coinSound)
    {
        if (managerTransform == null || rows <= 0 || cols <= 0) return;
        float chainFactor = 1f + (chainGoldStepPercent / 100f) * resolveWaveDepth;
        bool isChain = !autoDemo && chainFactor > 1f;
        if (isChain && !autoDemo) DailyTrackChainWave();
        int pay = Mathf.RoundToInt(amount * chainFactor);
        if (!autoDemo)
        {
            levelGold += pay;
            DailyTrackGoldPopped(pay);
            hiddenGold += pay;
            if (coinSound) PlaySfx(SfxSlot.CoinPop, managerTransform.TransformPoint(LocalPos(cell.y, cell.x)));
        }
        var c = tileColors != null && colorIdx >= 0 && colorIdx < tileColors.Length ? tileColors[colorIdx] : Color.white;
        string label = isChain ? "CHAIN +" + pay : amount.ToString();
        Vector3 wp = managerTransform.TransformPoint(LocalPos(cell.y, cell.x));
        scorePops.Add(new ScorePop
        {
            worldPos = wp,
            color = c,
            text = label,
            age = 0f,
            dur = special ? Mathf.Max(0.05f, scorePopSpecialDur) : Mathf.Max(0.1f, scorePopFadeDur),
            isChain = isChain,
            screenOff = ResolveScorePopOffset(wp, label)
        });
        if (scorePops.Count > 300) scorePops.RemoveAt(0);
    }

    Vector2 ResolveScorePopOffset(Vector3 worldPos, string text)
    {
        if (cam == null || scorePops.Count == 0) return Vector2.zero;
        float fs = Mathf.Max(8f, scorePopSize);
        float estW = Mathf.Max(fs * 1.4f, fs * 0.7f * text.Length);
        float h = fs * 1.6f;
        float m = Mathf.Max(3f, fs * 0.12f);

        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        Vector2 baseC = new Vector2(sp.x, Screen.height - sp.y);

        int n = scorePops.Count;
        var occX = new float[n]; var occY = new float[n]; var occW = new float[n];
        for (int i = 0; i < n; i++)
        {
            var p = scorePops[i];
            Vector3 s2 = cam.WorldToScreenPoint(p.worldPos);
            occX[i] = s2.x + p.screenOff.x;
            occY[i] = Screen.height - s2.y + scorePopRisePx * Mathf.Clamp01(p.age / p.dur) + p.screenOff.y;
            occW[i] = Mathf.Max(fs * 1.4f, fs * 0.7f * p.text.Length);
        }

        float stepY = h + m * 2f;
        float sideDx = estW + m * 2f;
        Vector2 lastCand = baseC;
        for (int up = 0; up <= 5; up++)
            for (int si = 0; si < 3; si++)
            {
                float dx = si == 0 ? 0f : (si == 1 ? sideDx : -sideDx);
                Vector2 cand = baseC + new Vector2(dx, -up * stepY);
                lastCand = cand;
                bool hit = false;
                for (int i = 0; i < n && !hit; i++)
                    if (Mathf.Abs(cand.x - occX[i]) < (estW + occW[i]) * 0.5f + m && Mathf.Abs(cand.y - occY[i]) < h + m) hit = true;
                if (!hit) return cand - baseC;
            }
        return lastCand - baseC;
    }


    void SpawnScorePopsForRun(M3MatchRun run)
    {
        var mid = run.cells[Mathf.Min(run.cells.Count / 2, run.cells.Count - 1)];
        SpawnScorePop(mid, run.colorIdx, ScoreForRun(run.cells.Count), false);
    }

    void AgeScorePops()
    {
        float dt = Time.deltaTime;
        for (int i = scorePops.Count - 1; i >= 0; i--)
        {
            var p = scorePops[i];
            p.age += dt;
            if (p.age >= p.dur) scorePops.RemoveAt(i);
            else scorePops[i] = p;
        }
    }

IEnumerator ClearTiles(HashSet<Vector2Int> cells, Vector2Int origin)
    {
        var entries = new List<KeyValuePair<Vector2Int, GameObject>>();
        foreach (var c in cells)
        {
            if (tiles[c.y, c.x] != null) entries.Add(new KeyValuePair<Vector2Int, GameObject>(c, tiles[c.y, c.x]));
            colorGrid[c.y, c.x] = -1;
            tiles[c.y, c.x] = null;
        }
        if (entries.Count == 0) yield break;

        entries.Sort((p, q) =>
        {
            if (q.Key.y != p.Key.y) return q.Key.y.CompareTo(p.Key.y);
            return p.Key.x.CompareTo(q.Key.x);
        });

        for (int i = 0; i < entries.Count; i++)
            StartCoroutine(PopTile(entries[i].Value, popDelay * Mathf.Min(i, maxStaggeredPops)));

        yield return WaitGameSeconds(popDelay * Mathf.Min(entries.Count - 1, maxStaggeredPops) + clearTime);
    }



    IEnumerator PopTile(GameObject go, float delay)
    {
        if (!autoDemo) DailyTrackPop();
        if (delay > 0f) yield return WaitGameSeconds(delay);
        if (go == null) yield break;
        PlaySfx(SfxSlot.Pop, go.transform.position);
        Vector3 s0 = go.transform.localScale;
        float t = 0f;
        while (t < clearTime)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / clearTime);
            float s = Mathf.Max(0.01f, s0.x * (1f - k));
            go.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    List<M3Fall> ApplyGravityAndRefill()
    {
        var falls = new List<M3Fall>();
        for (int col = 0; col < cols; col++)
        {
            var slotRows = new List<int>();
            for (int r = rows - 1; r >= 0; r--)
                if (shape[r, col]) slotRows.Add(r);
            int n = slotRows.Count;
            if (n == 0) continue;

            bool[] fixedSlot = new bool[n];
            for (int i = 0; i < n; i++)
                fixedSlot[i] = CellHasStone(slotRows[i], col);

            var survivors = new List<int>();
            for (int i = 0; i < n; i++)
                if (!fixedSlot[i] && colorGrid[slotRows[i], col] != -1)
                    survivors.Add(i);

            var freeSlots = new List<int>();
            for (int i = 0; i < n; i++)
                if (!fixedSlot[i]) freeSlots.Add(i);

            for (int k = 0; k < survivors.Count; k++)
            {
                int srcRow = slotRows[survivors[k]];
                int destRow = slotRows[freeSlots[k]];
                if (destRow != srcRow)
                {
                    colorGrid[destRow, col] = colorGrid[srcRow, col];
                    tiles[destRow, col] = tiles[srcRow, col];
                    colorGrid[srcRow, col] = -1;
                    tiles[srcRow, col] = null;
                    falls.Add(new M3Fall(tiles[destRow, col], LocalPos(destRow, col)));
                }
            }

            int topRow = slotRows[n - 1];
            for (int k = survivors.Count; k < freeSlots.Count; k++)
            {
                int r = slotRows[freeSlots[k]];
                int ci = RandomEnabledColor();
                colorGrid[r, col] = ci;
                var go = CreateTileObject(r, col, ci);
                tiles[r, col] = go;

                float startY = LocalPos(topRow, col).y + (k - survivors.Count + 1) * cellSize;
                go.transform.localPosition = new Vector3(LocalPos(r, col).x, startY, 0);
                falls.Add(new M3Fall(go, LocalPos(r, col)));
            }
        }
        return falls;
    }

IEnumerator AnimateFalls(List<M3Fall> falls)
    {
        bool done = false;
        while (!done)
        {
            done = true;
            int airborne = 0;
            foreach (var f in falls)
            {
                if (f.go == null) continue;
                Vector3 cur = f.go.transform.localPosition;
                float ty = f.target.y;
                if (cur.y > ty + 0.01f)
                {
                    airborne++;
                    f.vel += gravity * Time.deltaTime;
                    float newY = cur.y - f.vel * Time.deltaTime;
                    if (newY <= ty)
                    {
                        f.go.transform.localPosition = new Vector3(f.target.x, ty, 0);
                        f.vel = 0f;
                    }
                    else
                    {
                        f.go.transform.localPosition = new Vector3(f.target.x, newY, 0);
                        done = false;
                    }
                }
                else if (cur != f.target)
                {
                    f.go.transform.localPosition = f.target;
                }
            }
            UpdateFallWhoosh(airborne);
            yield return null;
        }
    }




    class M3Fall
    {
        public GameObject go;
        public Vector3 target;
        public float vel;
        public M3Fall(GameObject g, Vector3 t) { go = g; target = t; }
    }



    bool FindSmallestMatchMove(out Vector2Int a, out Vector2Int b)
    {
        a = new Vector2Int(-1, -1);
        b = new Vector2Int(-1, -1);
        int bestLen = int.MaxValue;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (colorGrid[r, c] == -1 || CellHasStone(r, c)) continue;

                if (c + 1 < cols && colorGrid[r, c + 1] != -1 && !CellHasStone(r, c + 1))
                {
                    int len = SwapMaxRun(new Vector2Int(c, r), new Vector2Int(c + 1, r));
                    if (len >= 3 && len < bestLen) { bestLen = len; a = new Vector2Int(c, r); b = new Vector2Int(c + 1, r); }
                }
                if (r + 1 < rows && colorGrid[r + 1, c] != -1 && !CellHasStone(r + 1, c))
                {
                    int len = SwapMaxRun(new Vector2Int(c, r), new Vector2Int(c, r + 1));
                    if (len >= 3 && len < bestLen) { bestLen = len; a = new Vector2Int(c, r); b = new Vector2Int(c, r + 1); }
                }
            }
        return a.x != -1;
    }


    int SwapMaxRun(Vector2Int a, Vector2Int b)
    {
        int ca = colorGrid[a.y, a.x]; int cb = colorGrid[b.y, b.x];
        if (IsSpecial(ca) || IsSpecial(cb)) return 0;
        colorGrid[a.y, a.x] = cb; colorGrid[b.y, b.x] = ca;

        int best = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (IsMatchable(r, c))
                {
                    int col = colorGrid[r, c];
                    int len = 1;
                    while (c + len < cols && IsMatchable(r, c + len) && colorGrid[r, c + len] == col) len++;
                    best = Mathf.Max(best, len);
                }
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                if (IsMatchable(r, c))
                {
                    int col = colorGrid[r, c];
                    int len = 1;
                    while (r + len < rows && IsMatchable(r + len, c) && colorGrid[r + len, c] == col) len++;
                    best = Mathf.Max(best, len);
                }

        colorGrid[a.y, a.x] = ca; colorGrid[b.y, b.x] = cb;
        return best;
    }


    IEnumerator HintNudge(Vector2Int a, Vector2Int b)
    {
        var t = tiles[a.y, a.x];
        if (t == null || shakeLocks.Contains(a)) yield break;
        shakeLocks.Add(a);
        PlaySfx(SfxSlot.Hint, t.transform.position);

        Vector3 dir = (LocalPos(b.y, b.x) - LocalPos(a.y, a.x)).normalized;
        Vector3 home = LocalPos(a.y, a.x);
        float reach = hintNudgeReach;
        float elapsed = 0f;
        float dur = 0.45f;
        while (elapsed < dur)
        {
            if (t == null || !IsTileInCell(t, a)) break;
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / dur);
            t.transform.localPosition = home + dir * (Mathf.Sin(k * Mathf.PI) * reach);
            yield return null;
        }
        if (t != null && IsTileInCell(t, a)) t.transform.localPosition = home;
        shakeLocks.Remove(a);
    }

    bool IsTileInCell(GameObject tile, Vector2Int cell)
    {
        return cell.y >= 0 && cell.y < rows && cell.x >= 0 && cell.x < cols && tiles[cell.y, cell.x] == tile;
    }



    class M3MatchRun { public List<Vector2Int> cells = new List<Vector2Int>(); public int colorIdx; }

    const int maxCascadeWaves = 64;

    List<M3MatchRun> ScanRuns(out HashSet<Vector2Int> allCells, out int gained)
    {
        var runs = new List<M3MatchRun>();
        allCells = new HashSet<Vector2Int>();
        gained = 0;

        for (int r = 0; r < rows; r++)
        {
            int c = 0;
            while (c < cols)
            {
                if (!IsMatchable(r, c)) { c++; continue; }
                int col = colorGrid[r, c];
                int len = 1;
                while (c + len < cols && IsMatchable(r, c + len) && colorGrid[r, c + len] == col) len++;
                if (len >= 3)
                {
                    var run = new M3MatchRun { colorIdx = col };
                    for (int i = 0; i < len; i++)
                    {
                        var cell = new Vector2Int(c + i, r);
                        allCells.Add(cell);
                        run.cells.Add(cell);
                    }
                    runs.Add(run);
                    gained += ScoreForRun(len);
                }
                c += len;
            }
        }

        for (int c = 0; c < cols; c++)
        {
            int r = 0;
            while (r < rows)
            {
                if (!IsMatchable(r, c)) { r++; continue; }
                int col = colorGrid[r, c];
                int len = 1;
                while (r + len < rows && IsMatchable(r + len, c) && colorGrid[r + len, c] == col) len++;
                if (len >= 3)
                {
                    var run = new M3MatchRun { colorIdx = col };
                    for (int i = 0; i < len; i++)
                    {
                        var cell = new Vector2Int(c, r + i);
                        allCells.Add(cell);
                        run.cells.Add(cell);
                    }
                    runs.Add(run);
                    gained += ScoreForRun(len);
                }
                r += len;
            }
        }

        return runs;
    }
}

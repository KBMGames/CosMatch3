using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public partial class Match3Game : MonoBehaviour
{

    Dictionary<GameObject, int> stoneHits = new Dictionary<GameObject, int>();


    bool IsMatchable(int r, int c)
    {
        return !IsSpecial(colorGrid[r, c]) && colorGrid[r, c] != -1 && !CellHasStone(r, c);
    }

    IEnumerator ShakeTile(Vector2Int cell, bool vertical)
    {
        var t = tiles[cell.y, cell.x];
        if (t == null || shakeLocks.Contains(cell)) yield break;
        shakeLocks.Add(cell);
        PlaySfx(SfxSlot.StoneWiggle, t.transform.position);

        Vector3 home = LocalPos(cell.y, cell.x);
        float elapsed = 0f;
        float dur = 0.3f;
        while (elapsed < dur)
        {
            if (t == null) break;
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / dur);
            float amp = cellSize * 0.15f * (1f - k);
            float off = Mathf.Sin(k * Mathf.PI * 6f) * amp;
            t.transform.localPosition = home + (vertical ? new Vector3(0f, off, 0f) : new Vector3(off, 0f, 0f));
            yield return null;
        }
        if (t != null) t.transform.localPosition = home;
        shakeLocks.Remove(cell);
    }


    GameObject GetStoneOnTile(GameObject tile)
    {
        if (tile == null || tile.transform.childCount == 0) return null;
        var t = tile.transform.GetChild(0);
        return t.name == "Stone" ? t.gameObject : null;
    }

    bool StoneAllowed(int r, int c)
    {
        if (!shape[r, c]) return false;
        if (setupProtectedCells.Contains(SetupCellKey(r, c))) return false;
        if (r == 0 || r == rows - 1 || c == 0 || c == cols - 1) return false;
        if (!shape[r - 1, c] || !shape[r + 1, c] || !shape[r, c - 1] || !shape[r, c + 1]) return false;
        return true;
    }

    int CountStoneCandidates()
    {
        int n = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (StoneAllowed(r, c)) n++;
        return n;
    }

    bool CellHasStone(int r, int c)
    {
        var t = tiles[r, c];
        return t != null && GetStoneOnTile(t) != null;
    }

    void PlaceStones()
    {
        var candidates = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (StoneAllowed(r, c)) candidates.Add(new Vector2Int(c, r));

        int targetCount = Mathf.RoundToInt(candidates.Count * stoneChance);
        int attempts = 0;
        while (attempts < 25)
        {
            attempts++;
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
            }

            var placed = new List<GameObject>();
            for (int i = 0; i < targetCount; i++)
            {
                var stoneTile = tiles[candidates[i].y, candidates[i].x];
                placed.Add(CreateStoneOnTile(stoneTile, candidates[i].y, candidates[i].x));
            }

            if (CountPlayableMoves() >= 1) break;
            foreach (var s in placed) { stoneHits.Remove(s); Destroy(s); }
        }
        Debug.Log("Match3: level " + currentLevel + ", " + targetCount + " stones placed, " + CountPlayableMoves() + " legal moves remain");
    }

    GameObject CreateStoneOnTile(GameObject tile, int row, int col)
    {
        var go = new GameObject("Stone");
        go.transform.SetParent(tile.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        Sprite s = StoneArtFor(0);
        sr.sprite = s;
        bool customArt = s != squareSprite;
        Color baseCol = customArt ? new Color(1f, 1f, 1f, stoneAlphaNew) : new Color(0.45f, 0.47f, 0.50f, stoneAlphaNew);
        sr.color = baseCol;
        sr.sortingOrder = 5;
        go.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        float tileWorldW = tile.transform.localScale.x * SpriteWorldWidth(GetTileSprite(colorGrid[row, col]));
        float k = (tileWorldW * stoneSize) / (tile.transform.localScale.x * SpriteWorldWidth(s));
        go.transform.localScale = new Vector3(k, k, 1f);
        stoneHits[go] = 0;
        return go;
    }


    void BeginDamagePass()
    {
        damagedThisPass.Clear();
    }

    float StoneAlphaFor(int hits)
    {
        if (hits <= 0) return stoneAlphaNew;
        if (hits == 1) return stoneAlphaHit1;
        return stoneAlphaHit2;
    }

    Sprite StoneBaseArt()
    {
        return pickedStone != null ? pickedStone : (stoneSprite != null ? stoneSprite : squareSprite);
    }


    Sprite StoneArtFor(int hits)
    {
        Sprite dedicated = hits <= 0 ? stoneArtNew : (hits == 1 ? stoneArtHit1 : stoneArtHit2);
        return dedicated != null ? dedicated : StoneBaseArt();
    }


    void DamageStone(GameObject stone)
    {
        if (stone == null || damagedThisPass.Contains(stone)) return;
        damagedThisPass.Add(stone);

        int hits = 0;
        stoneHits.TryGetValue(stone, out hits);
        hits++;

        var sr = stone.GetComponent<SpriteRenderer>();
        if (hits < stoneMaxHits)
        {
            stoneHits[stone] = hits;
            if (sr != null)
            {
                sr.sprite = StoneArtFor(hits);
                Color c = sr.color; c.a = StoneAlphaFor(hits); sr.color = c;
            }
            PlaySfx(SfxSlot.StoneCrack, stone.transform.position);
            StartCoroutine(CrackPulse(stone));
        }
        else
        {
            stoneHits.Remove(stone);
            StartCoroutine(BreakStoneFull(stone));
        }
    }

    bool DamageStoneDirect(GameObject stone, int amount)
    {
        if (stone == null || amount <= 0) return false;
        int hits = 0;
        stoneHits.TryGetValue(stone, out hits);
        hits += amount;

        var sr = stone.GetComponent<SpriteRenderer>();
        if (hits < stoneMaxHits)
        {
            stoneHits[stone] = hits;
            if (sr != null)
            {
                sr.sprite = StoneArtFor(hits);
                Color c = sr.color; c.a = StoneAlphaFor(hits); sr.color = c;
            }
            PlaySfx(SfxSlot.StoneCrack, stone.transform.position);
            StartCoroutine(CrackPulse(stone));
            return false;
        }

        stoneHits.Remove(stone);
        StartCoroutine(BreakStoneFull(stone));
        return true;
    }

    IEnumerator CrackPulse(GameObject stone)
    {
        Vector3 baseScale = stone.transform.localScale;
        float t = 0f;
        const float dur = 0.1f;
        while (t < dur && stone != null)
        {
            if (stone == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            stone.transform.localScale = baseScale * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        if (stone != null) stone.transform.localScale = baseScale;
    }

    IEnumerator BreakStoneFull(GameObject stone)
    {
        if (!autoDemo) DailyTrackStoneBreak();
        stonesDestroyedLastResolve++;
        TriggerBoardShakeStoneBreak();
        GainHpLive(1);
        stoneHits.Remove(stone);
        PlaySfx(SfxSlot.StoneBreak, stone.transform.position);

        Vector3 startPos = stone.transform.position;
        Quaternion startRot = stone.transform.rotation;
        Vector3 boardWorldS = stone.transform.lossyScale;
        stone.transform.SetParent(managerTransform, false);
        Vector3 mgrS = managerTransform != null ? managerTransform.lossyScale : Vector3.one;
        stone.transform.localScale = new Vector3(
            boardWorldS.x * stoneFallScale / Mathf.Max(mgrS.x, 0.001f),
            boardWorldS.y * stoneFallScale / Mathf.Max(mgrS.y, 0.001f),
            boardWorldS.z);

        float targetY;
        if (cam != null && cam.orthographic)
            targetY = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 1f)).y - cellSize;
        else
            targetY = startPos.y - rows * cellSize;

        const float fallDur = 0.5f;
        const float spinRad = 1.2f;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, spinRad);

        float t = 0f;
        while (t < fallDur && stone != null)
        {
            if (stone == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fallDur);
            float e = k * k;
            stone.transform.position = new Vector3(startPos.x, startPos.y - (startPos.y - targetY) * e, startPos.z);
            stone.transform.rotation = Quaternion.Slerp(startRot, endRot, k);
            yield return null;
        }
        if (stone != null) Destroy(stone);
    }

    List<GameObject> CollectAdjacentStones(HashSet<Vector2Int> cleared)
    {
        var stones = new List<GameObject>();
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };
        foreach (var cell in cleared)
            for (int k = 0; k < 4; k++)
            {
                int nr = cell.y + dr[k];
                int nc = cell.x + dc[k];
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                var s = GetStoneOnTile(tiles[nr, nc]);
                if (s != null && !damagedThisPass.Contains(s)) stones.Add(s);
            }
        return stones;
    }

}

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
public partial class Match3Game : MonoBehaviour
{
    bool hasSel = false;
    Vector2Int sel = new Vector2Int(-1, -1);
    bool isDragging = false;
    Vector2Int dragStartCell = new Vector2Int(-1, -1);
    Vector3 dragStartWorld;
    bool blastDragging = false;
    bool blastDragIsMega = false;
    bool blastDragByTouch = false;
    Vector2 blastDragScreenPos;

    bool GetCellFromScreen(Vector2 screenPos, out int row, out int col)
    {
        row = 0; col = 0;
        if (cam == null) return false;
        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        Vector3 local = managerTransform.InverseTransformPoint(world);
        int cc = Mathf.RoundToInt(local.x / cellSize + (cols - 1) / 2f);
        int rr = Mathf.RoundToInt((rows - 1) / 2f - local.y / cellSize);
        if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) return false;
        row = rr; col = cc;
        return true;
    }


    void HandlePointer()
    {
        var mouse = Mouse.current;
        var ts = Touchscreen.current;
        bool blastPressConsumed = false;
        if (!blastDragging)
        {
            Vector2? downPos = null; bool byTouch = false;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) downPos = mouse.position.ReadValue();
            else if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { downPos = ts.primaryTouch.position.ReadValue(); byTouch = true; }

            if (downPos.HasValue)
            {
                Vector2 p = downPos.Value;
                Vector2 gp = new Vector2(p.x, Screen.height - p.y);
                bool overCross = itemCrossSqR.Contains(gp);
                bool overMega = itemMegaSqR.Contains(gp);
                if (overCross || overMega)
                {
                    blastPressConsumed = true;
                    int owned = overMega ? megaCrossBlasts : crossBlasts;
                    if (owned <= 0)
                    {
                        itemSqShakeT = Time.unscaledTime; itemSqShakeIsMega = overMega;
                        PlaySfx(SfxSlot.PurchaseDeny, cam != null ? cam.transform.position : Vector3.zero);
                    }
                    else
                    {
                        blastDragging = true; blastDragIsMega = overMega; blastDragByTouch = byTouch; blastDragScreenPos = p;
                    }
                }
            }
        }
        else
        {
            Vector2 curPos = blastDragScreenPos; bool released = false, stale = false;
            if (blastDragByTouch)
            {
                var touch0 = ts != null ? ts.primaryTouch : null;
                if (touch0 == null) { blastDragging = false; return; }
                curPos = touch0.position.ReadValue();
                bool pressed = touch0.press.isPressed;
                if (!pressed) { released = touch0.press.wasReleasedThisFrame; stale = !released; }
            }
            else
            {
                if (mouse == null) { blastDragging = false; return; }
                curPos = mouse.position.ReadValue();
                bool down = mouse.leftButton.isPressed;
                if (!down) { released = mouse.leftButton.wasReleasedThisFrame; stale = !released; }
            }

            blastDragScreenPos = curPos;
            if (stale) { blastDragging = false; return; }
            if (!released) return;

            bool isMega = blastDragIsMega;
            blastDragging = false;
            int r, c;
            if (GetCellFromScreen(curPos, out r, out c) && colorGrid[r, c] != -1)
                FireItemBlast(new Vector2Int(c, r), isMega);
            return;
        }

        if (blastDragging || blastPressConsumed) return;

        if (!isDragging)
        {
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 sp = mouse.position.ReadValue();
                int r, c;
                if (GetCellFromScreen(sp, out r, out c) && colorGrid[r, c] != -1 && !CellHasStone(r, c))
                {
                    isDragging = true; dragByTouch = false;
                    dragStartCell = new Vector2Int(c, r);
                    dragStartWorld = cam.ScreenToWorldPoint(sp);
                    Select(dragStartCell);
                }
            }
            if (!isDragging && ts != null)
            {
                var touch0 = ts.primaryTouch;
                if (touch0.press.wasPressedThisFrame)
                {
                    Vector2 sp = touch0.position.ReadValue();
                    int r, c;
                    if (GetCellFromScreen(sp, out r, out c) && colorGrid[r, c] != -1 && !CellHasStone(r, c))
                    {
                        isDragging = true; dragByTouch = true;
                        dragStartCell = new Vector2Int(c, r);
                        dragStartWorld = cam.ScreenToWorldPoint(sp);
                        Select(dragStartCell);
                    }
                }
            }
        }
        else
        {
            bool released = false;
            Vector3 curWorld;

            if (dragByTouch)
            {
                if (ts == null) return;
                var touch0 = ts.primaryTouch;
                curWorld = cam.ScreenToWorldPoint(touch0.position.ReadValue());
                released = !touch0.press.isPressed;
            }
            else
            {
                if (mouse == null) return;
                curWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                released = mouse.leftButton.wasReleasedThisFrame;
            }

            Vector3 delta = curWorld - dragStartWorld;

            if (delta.magnitude > cellSize * 0.4f)
            {
                Vector2Int target;
                if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                {
                    int dirX = delta.x > 0 ? 1 : -1;
                    target = new Vector2Int(dragStartCell.x + dirX, dragStartCell.y);
                }
                else
                {
                    int dirRow = delta.y > 0 ? -1 : 1;
                    target = new Vector2Int(dragStartCell.x, dragStartCell.y + dirRow);
                }

                Deselect();
                isDragging = false;
                AttemptSwap(dragStartCell, target);
            }
            else if (released)
            {
                Deselect();
                isDragging = false;
            }
        }
    }

    bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    void Select(Vector2Int cell)
    {
        hasSel = true; sel = cell;
        var t = tiles[cell.y, cell.x];
        if (t != null && colorGrid[cell.y, cell.x] != -1)
            t.transform.localScale = TileScale(colorGrid[cell.y, cell.x]) * 1.2f;
    }

    void Deselect()
    {
        if (hasSel && sel.y >= 0 && sel.y < rows && sel.x >= 0 && sel.x < cols)
        {
            var t = tiles[sel.y, sel.x];
            if (t != null && colorGrid[sel.y, sel.x] != -1)
                t.transform.localScale = TileScale(colorGrid[sel.y, sel.x]);
        }
        hasSel = false;
    }
}

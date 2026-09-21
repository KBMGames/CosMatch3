using UnityEngine;
using System.Collections;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public partial class Match3Game : MonoBehaviour
{
    Texture2D menuBgTex;
    Texture2D panelBgTex;

void DrawPauseOverlay()
    {
        var p = MakeUiPanel(420f, 380f, 0.70f, 0.62f);
        Rect box;
        float sx = p.sx, sy = p.sy, sF = p.sF; uiScaleCur = sF;

        bool dbgRow = showTesterPops && Application.isPlaying;
        bool cheatRow = showCheatGoldButton;
        float musicTopP = showPauseStoreButton ? 280f : 220f;

        bool rowAOn = showOptionsButton || cheatRow || showTestPassLevelButton;
        float dailyTopP = musicTopP + 50f;   
        float yA = (showDailiesButton ? dailyTopP : musicTopP) + 50f; 
        float dbgBase = musicTopP + 42f;     
        if (showDailiesButton) dbgBase = dailyTopP + 42f;
        if (rowAOn) dbgBase = yA + 42f;
        float dbgTopDesign = dbgBase + 46f; 

        {
            float pBot = musicTopP + 36f;    
            if (rowAOn) pBot = Mathf.Max(pBot, yA + 42f);
                        if (showDailiesButton) pBot = Mathf.Max(pBot, dailyTopP + 42f); 
            if (dbgRow) pBot = Mathf.Max(pBot, dbgTopDesign + 30f); 
            pBot += 46f; 
            float bh = sy * (pBot + 45f);
            box = new Rect(p.rect.x, (Screen.height - bh) / 2f, p.rect.width, bh);
        }

        float pDur = Mathf.Max(0.1f, overlayFlyDur);
        bool risingP = pauseRiseT >= 0f && pauseRiseT < pDur;
        float pk = pauseRiseT < 0f ? 1f : EaseOutBack(Mathf.Clamp01(pauseRiseT / pDur)); 
        Vector2 poff = new Vector2(0f, Screen.height * (1f - pk));
        var pm = GUI.matrix; bool pen = GUI.enabled;
        if (risingP) GUI.enabled = false; 
        GUI.matrix = Matrix4x4.TRS(poff, Quaternion.identity, Vector3.one);

        if (panelBgTex != null) GUI.DrawTexture(new Rect(box.x, box.y, box.width, box.height), panelBgTex); 

        var title = UiStyle(GUI.skin.label);
        title.fontSize = Mathf.Max(16, (int)(40 * sF));
        title.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, box.y + 30 * sy, Screen.width, 60 * sy), uiTextPausedTitle, title);

        float bx = box.x + box.width * 0.1f;
        float bwd = box.width * 0.8f;
        if (UiButton(new Rect(bx, box.y + 95 * sy, bwd, 54 * sy), uiTextMainMenu))
        {
            SetPaused(false); 
            BeginLoadThenMenu();   
        }

        if (UiButton(new Rect(bx, box.y + 160 * sy, bwd, 54 * sy), uiTextLevelSelect))
        {
            SetPaused(false); 
            levelResultsCached = false; 
            levelSelectFromGame = true;  
            TransitionTo(GameState.LevelSelect); 
        }

        if (showPauseStoreButton)
        {
            if (UiButton(new Rect(bx, box.y + 220f * sy, bwd, 54 * sy), uiTextStore))
            {
                storeFromPause = true; 
                storeFromHud = false; 
                TransitionTo(GameState.Store, true); 
            }
        }

        if (UiButton(DialRect(new Rect(bx, box.y + musicTopP * sy, bwd, 42f * sy), pauseMusicOffX, pauseMusicOffY, pauseMusicScale), uiTextMusic)) musicDrawerOpen = !musicDrawerOpen; 

        Rect DialRect(Rect snap, float offX, float offY, float scale)
        {
            float k = Mathf.Max(0.1f, scale); 
            float w = snap.width * k, h = snap.height * k;
            return new Rect(snap.center.x + offX * sy - w / 2f, snap.center.y + offY * sy - h / 2f, w, h);
        }

        if (rowAOn)
        {
            float slotW = bwd / 3f;
            float w3 = slotW - 8f * sF; 
            if (showOptionsButton && UiButton(DialRect(new Rect(bx + 4f * sF, box.y + yA * sy, w3, 42 * sy), pauseOptOffX, pauseOptOffY, pauseOptScale), uiTextOptions))
            {
                optionsFromPause = true;
                TransitionTo(GameState.Options); 
            }
            if (cheatRow && UiButton(DialRect(new Rect(bx + slotW + 4f * sF, box.y + yA * sy, w3, 42 * sy), pauseCheatOffX, pauseCheatOffY, pauseCheatScale), uiTextCheatGold)) CheatAddGold(10000f); 
            if (showTestPassLevelButton && UiButton(DialRect(new Rect(bx + 2f * slotW + 4f * sF, box.y + yA * sy, w3, 42 * sy), pauseTestPassOffX, pauseTestPassOffY, pauseTestPassScale), uiTextTestPassLevel)) TestPassLevel();
        }

        if (showDailiesButton && UiButton(DialRect(new Rect(bx, box.y + dailyTopP * sy, bwd, 42f * sy), pauseDailyOffX, pauseDailyOffY, pauseDailyScale), uiTextDailiesHeader)) dailiesOpen = !dailiesOpen; 

        if (dbgRow)
        {
            string[] labels = { "4", "R5", "B5", "Y5", "G5", "P5" };
            int[] dbgColors = { -1, 0, 2, 3, 1, 4 }; 
            int[] dbgLens = { 4, 5, 5, 5, 5, 5 };
            float bw = bwd / labels.Length;
            for (int i = 0; i < labels.Length; i++)
                if (UiButton(DialRect(new Rect(bx + i * bw, box.y + dbgTopDesign * sy, Mathf.Max(24f, bw - 6f), 30 * sy), pauseDbgOffX, pauseDbgOffY, pauseDbgScale), labels[i]))
                {
                    SetPaused(false); 
                    DebugForceRun(dbgColors[i], dbgLens[i]);
                }
        }

        {
            float qaLabelW = 96f * sy;
            float qaGapX = 6f * sy;
            float qaToggleBoxW = (54f + 32f + 8f * 2f) * sy; 
            float qaTotalW = qaLabelW + qaGapX + qaToggleBoxW;
            DrawQaDebugToggle(new Rect(box.xMax - 12f * sy - qaTotalW, box.yMax - 40f * sy, qaTotalW, 34f * sy));
        }
        DrawOverlayChrome(box, pauseSplashTex); 
        GUI.matrix = pm; GUI.enabled = pen;
    }


    void DebugForceRun(int colorIdx, int len)
    {
        if (busy || state != GameState.Playing || rows <= 0 || cols <= 0 || colorGrid == null) return;
        if (colorGrid.GetLength(0) != rows || colorGrid.GetLength(1) != cols || tiles == null || tiles.GetLength(0) != rows || tiles.GetLength(1) != cols) return;

        var scratch = new int[rows, cols]; 
        for (int a = 0; a < 60; a++)
        {
            int col = colorIdx < 0 ? RandomEnabledColor() : colorIdx; 
            bool horiz = cols >= len && (rows < len || UnityEngine.Random.value > 0.5f); 
            if (!horiz && rows < len) continue;

            int r0 = UnityEngine.Random.Range(0, rows - (horiz ? 1 : len) + 1);
            int c0 = UnityEngine.Random.Range(0, cols - (horiz ? len : 1) + 1);

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++) scratch[r, c] = colorGrid[r, c];

            bool ok = true;
            for (int i = 0; i < len && ok; i++) 
            {
                int r = horiz ? r0 : r0 + i, c = horiz ? c0 + i : c0;
                if (tiles[r, c] == null || colorGrid[r, c] < 0 || CellHasStone(r, c)) ok = false;
            }
            if (!ok) continue;

            for (int i = 0; i < len; i++)
            {
                int r = horiz ? r0 : r0 + i, c = horiz ? c0 + i : c0;
                scratch[r, c] = col;
            }

            ok = true;
            for (int i = 0; i < len && ok; i++) 
            {
                int r = horiz ? r0 : r0 + i, c = horiz ? c0 + i : c0;
                int along = 1, cross = 1;
                if (horiz)
                {
                    for (int x = c - 1; x >= 0 && scratch[r, x] == col; x--) along++;
                    for (int x = c + 1; x < cols && scratch[r, x] == col; x++) along++;
                    for (int y = r - 1; y >= 0 && scratch[y, c] == col; y--) cross++;
                    for (int y = r + 1; y < rows && scratch[y, c] == col; y++) cross++;
                }
                else
                {
                    for (int y = r - 1; y >= 0 && scratch[y, c] == col; y--) along++;
                    for (int y = r + 1; y < rows && scratch[y, c] == col; y++) along++;
                    for (int x = c - 1; x >= 0 && scratch[r, x] == col; x--) cross++;
                    for (int x = c + 1; x < cols && scratch[r, x] == col; x++) cross++;
                }
                if (along != len || cross > 2) ok = false; 
            }

            if (ok)
            {
                for (int i = 0; i < len; i++) 
                {
                    int r = horiz ? r0 : r0 + i, c = horiz ? c0 + i : c0;
                    scratch[r, c] = -1;
                }
                if (BoardHasMatch(scratch)) ok = false;
            }
            if (!ok) continue;

            
            for (int i = 0; i < len; i++)
            {
                int r = horiz ? r0 : r0 + i, c = horiz ? c0 + i : c0;
                colorGrid[r, c] = col;
                var go = tiles[r, c];
                if (go != null)
                {
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.sprite = GetTileSprite(col);
                        bool custom = tileSprites != null && col >= 0 && col < tileSprites.Length && tileSprites[col] != null; 
                        sr.color = custom ? Color.white : tileColors[col];
                    }
                    go.transform.localScale = TileScale(col);
                }
            }

            stonesDestroyedLastResolve = 0; 
            swapGenAtStart = boardGen;
            StartCoroutine(ResolveBoard(new Vector2Int(c0, r0))); 
            return;
        }

        Debug.Log("Match3 DEBUG: force-run placement failed after 60 tries (board too crowded?)");
    }

    
    Texture2D _whitePx; 
    void EnsureWhitePx() { if (_whitePx == null) { _whitePx = new Texture2D(1, 1); _whitePx.SetPixel(0, 0, Color.white); _whitePx.Apply(); } }


    void DrawMusicDrawer()
    {
        if (musicSlideT <= 0f) return;

        float s = UiGlobalScale();
        EnsureWhitePx();
        float ease = musicSlideT * musicSlideT * (3f - 2f * musicSlideT); 
        bool ready = musicSlideT > 0.9f;                            

        float pw = Mathf.Min(680f * s, Screen.width * 92f / 100f);   
        float ph = 256f * s;                                          

        float dropY = showDailiesButton ? (128f + 324f / 2f + 10f) * s : 0f; 
        Rect box = new Rect(Screen.width - ease * pw, (Screen.height - ph) / 2f + dropY, pw, ph); 
        GUI.Box(box, "");

        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(13, (int)(20 * s));
        st.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(box.x + 8f * s, box.y + 6f * s, pw - 84f * s, 34f * s), uiTextMusic, st);
        bool oldEn = GUI.enabled; GUI.enabled = ready;
        if (UiButton(new Rect(box.x + pw - 70f * s, box.y + 6f * s, 64f * s, 34f * s), uiTextBack)) musicDrawerOpen = false; 

        float bw = 2f * s;
        Color borderColor = new Color(0.15f, 0.15f, 0.15f, 0.65f);
        void DrawBorder(Rect r)
        {
            Color oldC = GUI.color;
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, bw), _whitePx);                    
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - bw, r.width, bw), _whitePx);   
            GUI.DrawTexture(new Rect(r.x, r.y, bw, r.height), _whitePx);                  
            GUI.DrawTexture(new Rect(r.x + r.width - bw, r.y, bw, r.height), _whitePx);  
            GUI.color = oldC;
        }

        int FitBoxFont(float maxWd, float maxHt, string text)
        {
            var fs = UiStyle(GUI.skin.label);
            fs.fontSize = Mathf.Max(12, (int)(maxHt * 0.6f));
            Vector2 tsz = fs.CalcSize(new GUIContent(text));
            float mw = maxWd - bw * 4f;
            if (tsz.x > mw || tsz.y > maxHt)
                fs.fontSize = Mathf.Max(10, (int)(fs.fontSize * Mathf.Min(mw / tsz.x, maxHt / tsz.y)));
            return fs.fontSize;
        }

        if (ready) 
        {
            float padX = 12f * s, labelW = 76f * s, gapX = 8f * s;
            float rowH = 54f * s;
            float r1y = box.y + (8f + 40f + 10f) * s; 
            float r2y = r1y + (54f + 10f) * s;
            float r3y = r2y + (54f + 10f) * s;

            
            Rect row1Box = new Rect(box.x + padX, r1y, pw - padX * 2f, rowH);
            DrawBorder(row1Box);

            float trackW = 54f * s;
            float trackH = 26f * s;
            float knobSize = trackH + 6f * s;
            float trackX = row1Box.x + (row1Box.width - trackW) / 2f;
            float trackY = row1Box.y + (row1Box.height - trackH) / 2f;

            Color savedColor = GUI.color;
            if (musicEnabled)
                GUI.color = new Color(0.25f, 0.8f, 0.35f, 1f);
            else
                GUI.color = new Color(0.85f, 0.25f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(trackX, trackY, trackW, trackH), _whitePx);

            
            float knobX = musicEnabled ? (trackX + trackW - knobSize / 2f) : (trackX - knobSize / 2f);
            float knobY = trackY + trackH / 2f - knobSize / 2f;
            GUI.color = musicEnabled ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f);
            if (circleSprite != null)
                GUI.DrawTexture(new Rect(knobX, knobY, knobSize, knobSize), circleSprite.texture, ScaleMode.ScaleAndCrop);

            
            float hitW = trackW + knobSize;
            Rect toggleHit = new Rect(trackX - knobSize / 2f, r1y, hitW, rowH);
            if (Event.current.type == EventType.MouseDown && toggleHit.Contains(Event.current.mousePosition))
            {
                musicEnabled = !musicEnabled;
                PlayerPrefs.SetInt("M3_opt_musicOn", musicEnabled ? 1 : 0);
                PlaySfx(SfxSlot.UiClick, cam.transform.position);
                if (musicEnabled) StartBgm(); else StopBgm();
                ReapplyCurrentBgm(); 
                Event.current.Use();
            }

            
            Rect volLabelBox = new Rect(box.x + padX, r2y, labelW, rowH);
            DrawBorder(volLabelBox);

            var volLblStyle = UiStyle(GUI.skin.label);
            volLblStyle.fontSize = FitBoxFont(labelW, rowH, uiTextVolume);
            volLblStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(volLabelBox, uiTextVolume, volLblStyle);

            Rect sliderBox = new Rect(box.x + padX + labelW + gapX, r2y, pw - padX * 2f - labelW - gapX, rowH); 
            DrawBorder(sliderBox);

            float slPad = 8f * s;
            float slX = sliderBox.x + slPad;
            float slW = sliderBox.width - slPad * 2f;
            float trackLineH = 6f * s;
            Rect volTrackRect = new Rect(slX, r2y + (rowH - trackLineH) / 2f, slW, trackLineH);

            if (!musicEnabled) GUI.color = Color.gray; else GUI.color = new Color(0.35f, 0.6f, 1f, 1f); 
            GUI.DrawTexture(volTrackRect, _whitePx);

            
            float fillRatio = Mathf.Clamp01((bgmVolume - VolumeMin) / (VolumeMax - VolumeMin));
            if (musicEnabled && fillRatio > 0.01f)
            {
                GUI.color = new Color(0.3f, 0.75f, 1f, 1f);
                GUI.DrawTexture(new Rect(slX, r2y + (rowH - trackLineH) / 2f, slW * fillRatio, trackLineH), _whitePx);
            }

            
            float bigKnobSize = 30f * s;
            float vKnobX = slX + (slW - bigKnobSize) * fillRatio;
            Rect vKnobRect = new Rect(vKnobX, r2y + (rowH - bigKnobSize) / 2f, bigKnobSize, bigKnobSize);

            GUI.color = musicEnabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            if (circleSprite != null)
                GUI.DrawTexture(vKnobRect, circleSprite.texture, ScaleMode.ScaleAndCrop);

            
            Rect volHitArea = new Rect(slX - bigKnobSize / 2f, r2y, slW + bigKnobSize, rowH);
            if (musicEnabled)
            {
                Event ev = Event.current;
                bool overSlider = volHitArea.Contains(ev.mousePosition);

                if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && overSlider)
                {
                    bgmVolume = Mathf.Lerp(VolumeMin, VolumeMax, Mathf.Clamp01((ev.mousePosition.x - slX) / slW)); 
                    PlayerPrefs.SetFloat("M3_opt_bgmVol", bgmVolume);
                    if (bgmSrc != null) bgmSrc.volume = Mathf.Clamp01(bgmVolume);
                    UpdateCtxBgmVolume(); 
                    ev.Use();
                }
            }

            
            Rect sfxLabelBox = new Rect(box.x + padX, r3y, labelW, rowH);
            DrawBorder(sfxLabelBox);

            var sfxLblStyle = UiStyle(GUI.skin.label);
            sfxLblStyle.fontSize = FitBoxFont(labelW, rowH, uiTextSfxVol);
            sfxLblStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(sfxLabelBox, uiTextSfxVol, sfxLblStyle);

            Rect sliderBox3 = new Rect(box.x + padX + labelW + gapX, r3y, pw - padX * 2f - labelW - gapX, rowH);
            DrawBorder(sliderBox3);

            float slX3 = sliderBox3.x + slPad;
            float slW3 = sliderBox3.width - slPad * 2f;
            Rect sfxTrackRect = new Rect(slX3, r3y + (rowH - trackLineH) / 2f, slW3, trackLineH);

            GUI.color = new Color(0.35f, 0.6f, 1f, 1f);
            GUI.DrawTexture(sfxTrackRect, _whitePx);

            
            float fillRatio3 = Mathf.Clamp01((sfxVolume - VolumeMin) / (VolumeMax - VolumeMin));
            if (fillRatio3 > 0.01f)
            {
                GUI.color = new Color(0.3f, 0.75f, 1f, 1f);
                GUI.DrawTexture(new Rect(slX3, r3y + (rowH - trackLineH) / 2f, slW3 * fillRatio3, trackLineH), _whitePx);
            }

            float sfxKnobX = slX3 + (slW3 - bigKnobSize) * fillRatio3;
            Rect sfxKnobRect = new Rect(sfxKnobX, r3y + (rowH - bigKnobSize) / 2f, bigKnobSize, bigKnobSize);

            GUI.color = Color.white;
            if (circleSprite != null)
                GUI.DrawTexture(sfxKnobRect, circleSprite.texture, ScaleMode.ScaleAndCrop);

            
            Rect sfxHitArea = new Rect(slX3 - bigKnobSize / 2f, r3y, slW3 + bigKnobSize, rowH);
            {
                Event ev = Event.current;
                if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && sfxHitArea.Contains(ev.mousePosition))
                {
                    sfxVolume = Mathf.Lerp(VolumeMin, VolumeMax, Mathf.Clamp01((ev.mousePosition.x - slX3) / slW3)); 
                    PlayerPrefs.SetFloat("M3_opt_sfxVolume", sfxVolume); 
                    ev.Use();
                }
            }

            GUI.color = savedColor;
        }
        GUI.enabled = oldEn;
    }


    void DrawQaDebugToggle(Rect area)
    {
        EnsureWhitePx();

        float s = area.height / 34f; 

        
        float bw = 2f * s;
        Color borderColor = new Color(0.15f, 0.15f, 0.15f, 0.65f);

        void DrawBorder(Rect r)
        {
            Color old = GUI.color;
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, bw), _whitePx);                    
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - bw, r.width, bw), _whitePx);   
            GUI.DrawTexture(new Rect(r.x, r.y, bw, r.height), _whitePx);                  
            GUI.DrawTexture(new Rect(r.x + r.width - bw, r.y, bw, r.height), _whitePx);  
            GUI.color = old;
        }

        int FitBoxFont(float maxW, float maxH, string text)
        {
            var st = UiStyle(GUI.skin.label);
            st.fontSize = Mathf.Max(12, (int)(maxH * 0.6f));
            Vector2 ts = st.CalcSize(new GUIContent(text));
            float maxWd = maxW - bw * 4f;
            if (ts.x > maxWd || ts.y > maxH)
                st.fontSize = Mathf.Max(10, (int)(st.fontSize * Mathf.Min(maxWd / ts.x, maxH / ts.y)));
            return st.fontSize;
        }

        
        float gapX = 6f * s;
        float padIn = 8f * s;
        float trackW = 54f * s;
        float trackH = 26f * s;
        float knobSize = trackH + 6f * s;
        float toggleBoxW = trackW + knobSize + padIn * 2f;
        float labelW = area.width - gapX - toggleBoxW; 

        Rect labelBox = new Rect(area.x, area.y, labelW, area.height);
        DrawBorder(labelBox);

        var lblStyle = UiStyle(GUI.skin.label);
        lblStyle.fontSize = FitBoxFont(labelW, area.height, uiTextQaDebugOptions);
        lblStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(labelBox, uiTextQaDebugOptions, lblStyle);

        Rect toggleBox = new Rect(area.x + labelW + gapX, area.y, toggleBoxW, area.height);
        DrawBorder(toggleBox);

        
        float trackX = toggleBox.x + (toggleBox.width - trackW) / 2f;
        float trackY = toggleBox.y + (area.height - trackH) / 2f;

        Color savedColor = GUI.color;
        if (qaDebugOptions)
            GUI.color = new Color(0.25f, 0.8f, 0.35f, 1f);
        else
            GUI.color = new Color(0.85f, 0.25f, 0.25f, 1f);
        GUI.DrawTexture(new Rect(trackX, trackY, trackW, trackH), _whitePx);

        
        float knobX = qaDebugOptions ? (trackX + trackW - knobSize / 2f) : (trackX - knobSize / 2f);
        float knobY = trackY + trackH / 2f - knobSize / 2f;
        GUI.color = qaDebugOptions ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f);
        if (circleSprite != null)
            GUI.DrawTexture(new Rect(knobX, knobY, knobSize, knobSize), circleSprite.texture, ScaleMode.ScaleAndCrop);

        
        float hitW = trackW + knobSize;
        Rect toggleHit = new Rect(trackX - knobSize / 2f, area.y, hitW, area.height);
        if (Event.current.type == EventType.MouseDown && toggleHit.Contains(Event.current.mousePosition))
        {
            qaDebugOptions = !qaDebugOptions;
            ApplyQaDebugOptions();
            PlayerPrefs.SetInt("M3_opt_qaDebug", qaDebugOptions ? 1 : 0);
            PlayerPrefs.Save();
            PlaySfx(SfxSlot.UiClick, cam.transform.position);
            Event.current.Use();
        }

        GUI.color = savedColor; 
    }


    GUIStyle UiStyle(GUIStyle baseStyle) { var s = new GUIStyle(baseStyle); if (uiFontSlot != null) s.font = uiFontSlot; return s; }

    void FitButton(Rect r, string label, bool snapWidth, out Rect fitRect, out GUIStyle style)
    {
        var s = UiStyle(GUI.skin.button);
        s.fontSize = Mathf.Max(14, (int)(r.height * 0.6f)); 
        Vector2 ts = s.CalcSize(new GUIContent(label));
        if (!snapWidth && ts.x > r.width * 0.8f)
            s.fontSize = Mathf.Max(14, (int)(s.fontSize * (r.width * 0.8f) / ts.x)); 
        if (!snapWidth) ts = s.CalcSize(new GUIContent(label));
        float w = snapWidth ? ts.x * 1.25f : r.width; 
        fitRect = new Rect(r.center.x - w / 2f, r.y, w, r.height);
        style = s;
    }


    int FitFontSize(float baseFont, string label, float maxW)
    {
        var s = UiStyle(GUI.skin.button);
        s.fontSize = Mathf.Max(14, (int)baseFont);
        Vector2 ts = s.CalcSize(new GUIContent(label));
        if (ts.x > maxW * 0.8f) return Mathf.Max(14, (int)(s.fontSize * (maxW * 0.8f) / ts.x));
        return s.fontSize;
    }

bool UiButton(Rect r, string label)
    {
        Rect fr; GUIStyle st;
        FitButton(r, label, true, out fr, out st); 
        bool pressed = GUI.Button(fr, label, st);
        if (pressed) PlaySfx(SfxSlot.UiClick, cam != null ? cam.transform.position : Vector3.zero);
        return pressed;
   }

    struct UiPanel { public Rect rect; public float sx; public float sy; public float sF; }

    UiPanel MakeUiPanel(float designW, float designH, float fracW, float fracH)
    {
        float bw = Mathf.Min(Mathf.Max(320f, Screen.width * fracW), Mathf.Max(100f, Screen.width - 16f));
        float bh = Mathf.Min(Mathf.Max(320f, Screen.height * fracH), Mathf.Max(100f, Screen.height - 16f));
        var r = new Rect((Screen.width - bw) / 2f, (Screen.height - bh) / 2f, bw, bh);
        float sx = bw / designW;
        float sy = bh / designH;
        return new UiPanel { rect = r, sx = sx, sy = sy, sF = Mathf.Min(sx, sy) };
    }


    float UiGlobalScale() { return Mathf.Min(Screen.width / 720f, Screen.height / 1280f); }

    float uiScaleCur = 1f; 

    void OnGUI()
    {
        
        if (state == GameState.Menu || state == GameState.Loading || (state == GameState.Options && !optionsFromPause) || state == GameState.Sprites || state == GameState.Sounds || (state == GameState.Store && !storeFromPause && !storeFromHud)) 
        {
            Texture2D mb = MenuBackdrop();
            if (mb != null) DrawHugBg(new Rect(0, 0, Screen.width, Screen.height), mb); 
        }
        else if (state == GameState.LevelSelect && !levelSelectFromGame)   
        {
            Texture2D lsMb = levelSelectBgTex != null ? levelSelectBgTex : MenuBackdrop();
            if (lsMb != null) DrawHugBg(new Rect(0, 0, Screen.width, Screen.height), lsMb);
        }

        if (ghostActive) DrawGhostScreen();

        switch (state)
        {
            case GameState.Menu: WithEntryAnim(DrawMenu); break;
            case GameState.LevelSelect: WithEntryAnim(DrawLevelSelect); break;
            case GameState.Options: WithEntryAnim(DrawOptions); break;
            case GameState.Sprites: WithEntryAnim(DrawSprites); break;
            case GameState.Sounds: WithEntryAnim(DrawSounds); break;
            case GameState.SpritePick: DrawSpritePick(); break;   
            case GameState.ImageBrowse: DrawImageBrowse(); break;
            case GameState.SoundPick: DrawSoundPick(); break;
            case GameState.AudioBrowse: DrawAudioBrowse(); break;
            case GameState.Intro: DrawIntro(); break;             
            case GameState.StartGate: DrawStartGate(); break;     
            case GameState.Loading: DrawLoading(); break;         
            case GameState.Store: WithEntryAnim(DrawStore); break; 
            case GameState.Playing:
                DrawGameHUD();
                if (helpOpen) DrawHelpScreen();   
                else if (won) DrawWinOverlay();
                else if (lost) DrawLoseOverlay(); 
                break;
        }
        if (state == GameState.Menu) DrawTestCheatDrawer(); 
        if (state == GameState.Menu || paused) DrawDailiesDrawer(); 
        if (state == GameState.Menu || paused) DrawMusicDrawer(); 

        
        if (dailyNotices.Count > 0) { foreach (var n in dailyNotices) dailyToasts.Add(new DailyToast { title = n.title, reward = n.reward, age = 0f }); dailyNotices.Clear(); }
        DrawDailyToasts();   
        DrawCoinFlights();   
    }

    
    float screenEntryT = 99f;      
    float currentEntryDur = 1f;    
    Vector2 screenEnterFrom;       

    bool ghostActive = false;      
    GameState ghostState;          
    float ghostT;                  
    Vector2 ghostExitDir = Vector2.left;

    static float EaseOutBack(float t)   
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }


    void WithEntryAnim(System.Action draw)
    {
        if (screenEntryT >= currentEntryDur) { draw(); return; }   
        float k = EaseOutBack(Mathf.Clamp01(screenEntryT / currentEntryDur));
        Vector2 off = screenEnterFrom * (1f - k);                  
        var m = GUI.matrix; bool en = GUI.enabled;
        GUI.enabled = false;                                       
        GUI.matrix = Matrix4x4.TRS(off, Quaternion.identity, Vector3.one);
        draw();
        GUI.matrix = m; GUI.enabled = en;
    }

    void DrawGhostScreen()
    {
        float exitDur = Mathf.Max(0.1f, slideTransitionDur * 0.45f);
        float g = Mathf.Clamp01(ghostT / exitDur);
        float eg = g * g; 
        Vector2 off = ghostExitDir * (eg * (Mathf.Max(Screen.width, Screen.height) + 80f));
        var m = GUI.matrix; bool en = GUI.enabled;
        GUI.enabled = false;                                       
        GUI.matrix = Matrix4x4.TRS(off, Quaternion.identity, Vector3.one);
        switch (ghostState)
        {
            case GameState.Menu: DrawMenu(); break;
            case GameState.LevelSelect: DrawLevelSelect(); break;
            case GameState.Options: DrawOptions(); break;
            case GameState.Sprites: DrawSprites(); break;
            case GameState.Sounds: DrawSounds(); break;
            case GameState.Store: DrawStore(); break;             
        }
        GUI.matrix = m; GUI.enabled = en;
    }


    void TransitionTo(GameState target, bool mirror = false)
    {
        ghostActive = true; ghostT = 0f; ghostState = state;
        ghostExitDir = mirror ? Vector2.right : Vector2.left;   
        screenEnterFrom = mirror ? new Vector2(-Screen.width, 0) : new Vector2(Screen.width, 0);
        currentEntryDur = Mathf.Max(0.1f, slideTransitionDur);
        screenEntryT = 0f;
        if (target == GameState.LevelSelect) BeginLevelFlyIn(); 
        state = target;
        if (target == GameState.Menu) BgmForScreen(0);               
    }

    void BeginMenuSpawn()   
    {
        ghostActive = false;
        screenEnterFrom = new Vector2(0f, Screen.height);
        currentEntryDur = Mathf.Max(0.1f, menuFlyInDur);
        screenEntryT = 0f;
        state = GameState.Menu;
        BgmForScreen(0); 
    }


    
    float loadT, loadDur, loadingAngle, introWatchdog;
    bool loadToMenu; int loadLevel;

    void BeginLoadThenLevel(int lvl) { state = GameState.Loading; loadToMenu = false; ResetLoadingTimer(); loadLevel = lvl; }
void BeginLoadThenMenu()         { state = GameState.Loading; loadToMenu = true;  ResetLoadingTimer(); ClearBoardObjects(); } 
    void ResetLoadingTimer()
    {
        ghostActive = false;
        loadT = 0f; loadingAngle = 0f;
        loadDur = Random.Range(Mathf.Min(loadMinSec, loadMaxSec), Mathf.Max(loadMinSec, loadMaxSec)); 
    }
    void FinishLoading()
    {
        if (loadToMenu) BeginMenuSpawn();   
        else LoadLevel(loadLevel);          
    }


    
    bool lvlFlyActive = false; float lvlFlyT = 0f; int lvlFlyCount = 0; 
    struct LvlFly { public Vector2 from; public float delay, dur; }
    LvlFly[] lvlFlies = new LvlFly[10];

    void BeginLevelFlyIn()
    {
        levelScroll = Vector2.zero; 
        int flyCount = Mathf.Clamp(flyingLevelCount, 0, totalLevels);   
        lvlFlyCount = flyCount;
        if (!enableLevelFlyAnim || flyCount <= 0) { lvlFlyActive = false; return; } 
        lvlFlyActive = true; lvlFlyT = 0f;
        float stagger = (levelTileFlyDur - 1.4f) / flyCount;            
        for (int i = 0; i < flyCount; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(700f, 1500f);                    
            lvlFlies[i].from = new Vector2(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist);
            lvlFlies[i].delay = i * stagger;
            lvlFlies[i].dur = 1.0f + Random.Range(0f, 0.4f);
        }
    }

    Texture2D LevelIconTexture()
    {
        if (levelIconTex != null) return levelIconTex;
        if (tileSprites != null && tileSprites.Length > 0 && tileSprites[0] != null) return tileSprites[0].texture;
        return circleSprite != null ? circleSprite.texture : null;     
    }

    void DrawFlyingLevelTile(int lvl, Rect face)
    {
        var tex = LevelIconTexture(); if (tex == null) return;
        float t = (lvlFlyT - lvlFlies[lvl - 1].delay) / Mathf.Max(0.05f, lvlFlies[lvl - 1].dur);
        float k = EaseOutBack(Mathf.Clamp01(t));                       
        Vector2 center = new Vector2(face.x + face.width / 2f, face.y + face.height / 2f);
        Vector2 pos = center + lvlFlies[lvl - 1].from * (1f - k);
        float s = face.width;
        DrawHugBg(new Rect(pos.x - s / 2f, pos.y - s / 2f, s, s), tex); 
        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(14, (int)(s * 0.35f));
        st.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(pos.x - s / 2f, pos.y - s / 2f, s, s), lvl.ToString(), st);
    }


    
    float winRiseT = -1f, pauseRiseT = -1f, loseRiseT = -1f; bool prevWon, prevPaused, prevLost;

    float helpRiseT = -1f, helpCloseT = -1f; bool helpClosing = false; bool prevHelpOpen, prevPauseOverlayOpen;


    
    void UpdateCinematic()
    {
        float dt = Time.unscaledDeltaTime;
        TrackPointer();

        if (won && !prevWon) winRiseT = 0f;      
        if (lost && !prevLost) loseRiseT = 0f;   
        if (paused && pauseOverlayOpen && (!prevPaused || !prevPauseOverlayOpen)) pauseRiseT = 0f; 
        if (helpOpen && !prevHelpOpen) helpRiseT = 0f;          
        prevWon = won; prevLost = lost; prevPaused = paused; prevHelpOpen = helpOpen; prevPauseOverlayOpen = pauseOverlayOpen;
        if (winRiseT >= 0f) winRiseT += dt;
        if (loseRiseT >= 0f) loseRiseT += dt;
        if (pauseRiseT >= 0f) pauseRiseT += dt;
        if (helpRiseT >= 0f && !helpClosing) helpRiseT += dt;
        if (helpCloseT >= 0f) 
        {
            float hDur = Mathf.Max(0.1f, overlayFlyDur);
            helpCloseT += dt;
            if (helpCloseT >= hDur) { helpOpen = false; helpClosing = false; helpRiseT = -1f; helpCloseT = -1f; SetPaused(false); }
        }

        switch (state)
        {
            case GameState.StartGate:
                {
                    bool gateTapped = false;
                    var gM = Mouse.current; if (gM != null && gM.leftButton.wasPressedThisFrame) gateTapped = true;
                    var gT = Touchscreen.current; if (!gateTapped && gT != null && gT.primaryTouch.press.wasPressedThisFrame) gateTapped = true;
                    if (gateTapped) ProceedFromStartGate(); 
                }
                break;

            case GameState.Intro:
                introWatchdog += dt;
                bool tapped = false;
                var m2 = Mouse.current; if (m2 != null && m2.leftButton.wasPressedThisFrame) tapped = true;
                var t2 = Touchscreen.current; if (!tapped && t2 != null && t2.primaryTouch.press.wasPressedThisFrame) tapped = true;
                bool done = false;
                if (introIsGif && gifFrames != null && gifFrames.Count > 0)
                {
                    
                    gifAccum += dt;
                    while (gifAccum >= gifFrames[gifFrameIdx].delaySec)
                    {
                        gifAccum -= gifFrames[gifFrameIdx].delaySec;
                        gifFrameIdx++;
                        if (gifFrameIdx >= gifFrames.Count) { gifFrameIdx = 0; gifLoopDone = true; }   
                    }
                    if (!gifLoopDone && introRi != null) introRi.texture = gifFrames[gifFrameIdx].tex;
                    done = gifLoopDone;
                }
                else
                {

                    if (!introEnded && introVp != null && !introPlaying && introVp.isPrepared) { introPlaying = true; introVp.Play(); }
                    else if (introPlaying && !introEnded && introVp != null && !introVp.isPlaying && introVp.clockTime < 0.1f) introVp.Play(); 
                bool clipDone = introPlaying && !introEnded && introVp != null && introVp.clockTime > 0.1f && (!introVp.isPlaying || (introVideoClip != null && introVideoClip.length > 0.1f && introVp.clockTime >= introVideoClip.length - 0.25f)); 
                    done = clipDone;
                }
                float watchLimit = introIsGif ? Mathf.Max(15f, gifTotalDur + 3f) : 15f; 
                if ((showIntroSkipHint && tapped) || done || introWatchdog > watchLimit) EndIntro(); 
                break;

            case GameState.Loading:
                loadT += dt;
                float spinSpeed = 200f + Mathf.Sin(loadT * 1.7f) * 90f;   
                loadingAngle += spinSpeed * dt;
                if (loadT >= loadDur) FinishLoading();
                break;

            default:
                screenEntryT += dt;
                if (state == GameState.Menu) tcSlideT = Mathf.MoveTowards(tcSlideT, testCheatOpen ? 1f : 0f, dt / 0.28f); 
                else if (testCheatOpen || tcSlideT > 0f) { testCheatOpen = false; tcSlideT = Mathf.MoveTowards(tcSlideT, 0f, dt / 0.28f); } 
                bool dailiesHosted = state == GameState.Menu || (paused && pauseOverlayOpen); 
                if (!dailiesHosted && dailiesOpen) dailiesOpen = false; 
                dailiesSlideT = Mathf.MoveTowards(dailiesSlideT, (dailiesHosted && dailiesOpen) ? 1f : 0f, dt / 0.28f); 
                if (!dailiesHosted && musicDrawerOpen) musicDrawerOpen = false; 
                musicSlideT = Mathf.MoveTowards(musicSlideT, (dailiesHosted && musicDrawerOpen) ? 1f : 0f, dt / 0.28f); 

                
                for (int i = dailyToasts.Count - 1; i >= 0; i--)
                {
                    DailyToast tst = dailyToasts[i]; 
                    tst.age += dt;
                    if (tst.age >= Mathf.Max(0.1f, dailyToastDur)) 
                    {
                        float sFly = UiGlobalScale(); 
                        coinFlights.Add(new CoinFlight { from = new Vector2(Screen.width - dailyToastOffX * sFly, dailyToastOffY * sFly), t = 0f, reward = tst.reward });
                        dailyToasts.RemoveAt(i);
                    }
                    else dailyToasts[i] = tst; 
                }
                for (int i = coinFlights.Count - 1; i >= 0; i--)
                {
                    CoinFlight cf = coinFlights[i];
                    cf.t += dt / Mathf.Max(0.05f, dailyFlyTime); 
                    if (cf.t >= 1f) 
                    { PlaySfx(SfxSlot.CoinPop, cam != null ? cam.ScreenToWorldPoint(new Vector3(hudBankRect.center.x, hudBankRect.center.y, 0f)) : Vector3.zero); coinFlights.RemoveAt(i); } 
                    else coinFlights[i] = cf; 
                }
                if (ghostActive) { ghostT += dt; if (ghostT > slideTransitionDur * 0.45f) ghostActive = false; }
                if (lvlFlyActive && state == GameState.LevelSelect) { lvlFlyT += dt; if (lvlFlyT >= levelTileFlyDur) lvlFlyActive = false; }
                break;
        }
    }


    
void DrawMenu()
    {
        
        var p = MakeUiPanel(420f, 620f, 0.70f, 0.80f);
        Rect panel;
        float sx = p.sx, sy = p.sy, sF = p.sF; uiScaleCur = sF;

        
        {
            float musicRowM = 388f + (showTestCheatButton ? 78f : 0f) + (showDailiesButton ? 78f : 0f); 
            float mBot = musicRowM + 64f + 2f; 
            float oy = musicRowM + 64f + 14f;   
            if (showOptionsButton) { mBot = oy + 64f; oy += 85f; } 
            
            if (showDeleteProgressButton) mBot = oy + 40f;
            mBot += 46f; 
            float bh = sy * (mBot + 25f);
            panel = new Rect(p.rect.x, (Screen.height - bh) / 2f, p.rect.width, bh);
        }
        GUI.Box(panel, "");

        var title = UiStyle(GUI.skin.label);
        title.fontSize = Mathf.Clamp((int)(panel.width * 0.14f), (int)(34 * sF), (int)(64 * sF));
        title.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, panel.y + 50 * sy, Screen.width, 80 * sy), uiTextMenuTitle, title);

        
        bool menuSettled = screenEntryT >= currentEntryDur;
        if (!menuSettled) GUI.enabled = false;

        if (UiButton(new Rect(panel.x + panel.width * 0.2f, panel.y + 150 * sy, panel.width * 0.6f, 64 * sy), uiTextPlayGame))
            BeginLoadThenLevel(unlockedLevel); 

        if (UiButton(new Rect(panel.x + panel.width * 0.2f, panel.y + 230 * sy, panel.width * 0.6f, 64 * sy), uiTextLevelSelect))
        {
            levelResultsCached = false; 
            levelSelectFromGame = false;
            TransitionTo(GameState.LevelSelect);
        }

        
        if (UiButton(new Rect(panel.x + panel.width * 0.2f, panel.y + 310f * sy, panel.width * 0.6f, 64 * sy), uiTextStore))
        {
            storeFromPause = false; 
            storeFromHud = false; 
            TransitionTo(GameState.Store);
        }

        

        
        if (showTestCheatButton && UiButton(new Rect(panel.x + panel.width * 0.2f, panel.y + 388f * sy, panel.width * 0.6f, 64 * sy), uiTextTestCheat))
            testCheatOpen = !testCheatOpen; 
        
        {
            Rect DialRect(Rect snap, float offX, float offY, float scale) 
            {
                float k = Mathf.Max(0.1f, scale); 
                float w = snap.width * k, h = snap.height * k;
                return new Rect(snap.center.x + offX * sy - w / 2f, snap.center.y + offY * sy - h / 2f, w, h);
            }
            if (UiButton(DialRect(new Rect(panel.x + panel.width * 0.2f, panel.y + (388f + (showTestCheatButton ? 78f : 0f)) * sy, panel.width * 0.6f, 64 * sy), dailyMenuRowOffX, dailyMenuRowOffY, dailyMenuRowScale), uiTextDailiesHeader))
                dailiesOpen = !dailiesOpen; 
        }
        
        {
            float kM = Mathf.Max(0.1f, musicMenuRowScale); 
            Rect mSnap = new Rect(panel.x + panel.width * 0.2f, panel.y + (388f + (showTestCheatButton ? 78f : 0f) + (showDailiesButton ? 78f : 0f)) * sy, panel.width * 0.6f, 64f * sy);
            if (UiButton(new Rect(mSnap.center.x + musicMenuRowOffX * sy - mSnap.width * kM / 2f, mSnap.center.y + musicMenuRowOffY * sy - mSnap.height * kM / 2f, mSnap.width * kM, mSnap.height * kM), uiTextMusic))
                musicDrawerOpen = !musicDrawerOpen; 
        }

        
        float optY = 466f + (showTestCheatButton ? 78f : 0f) + (showDailiesButton ? 78f : 0f); 

        if (showOptionsButton)
        {
            if (UiButton(new Rect(panel.x + panel.width * 0.2f, panel.y + optY * sy, panel.width * 0.6f, 64 * sy), uiTextOptions))
            {
                optionsFromPause = false;
                TransitionTo(GameState.Options);
            }
            optY += 85f; 
        }

        

        
        if (showDeleteProgressButton)
        {
            if (!deleteProgressArmed)
            {
                if (UiButton(new Rect(panel.x + panel.width * 0.2f, panel.y + optY * sy, panel.width * 0.6f, 40 * sy), uiTextDeleteProgress))
                    deleteProgressArmed = true;
            }
            else
            {
                if (UiButton(new Rect(panel.x + panel.width * 0.2f, panel.y + optY * sy, panel.width * 0.35f, 40 * sy), uiTextConfirmDelete))
                    DeleteAllProgress();
                if (UiButton(new Rect(panel.x + panel.width * 0.45f, panel.y + optY * sy, panel.width * 0.35f, 40 * sy), uiTextCancel))
                    deleteProgressArmed = false;
            }
        }

        
        {
            float qaLabelW = 96f * sy;
            float qaGapX = 6f * sy;
            float qaToggleBoxW = (54f + 32f + 8f * 2f) * sy; 
            float qaTotalW = qaLabelW + qaGapX + qaToggleBoxW;
            DrawQaDebugToggle(new Rect(panel.xMax - 12f * sy - qaTotalW, panel.yMax - 40f * sy, qaTotalW, 34f * sy));
        }
        GUI.enabled = true; 

        
        {
            float s = UiGlobalScale();
#if UNITY_ANDROID
            float botMargin = showOrientationButton ? 54f * s : 6f * s; 
#else
            float botMargin = 6f * s;
#endif
            var cs = UiStyle(GUI.skin.label);
            if (creditsFont != null) cs.font = creditsFont; 
            cs.fontSize = Mathf.Max(11, (int)(13 * s));
            cs.wordWrap = true; 
            float mgn = 10f * s, halfW = Screen.width / 2f;
            float chh = Mathf.Max(64f * s, 42f); 
            float cy = Screen.height - botMargin - chh;
            var leftS = new GUIStyle(cs); leftS.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(mgn, cy, halfW - mgn * 2f, chh), uiTextCreditsProject, leftS);
            var rightS = new GUIStyle(cs); rightS.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(halfW + mgn, cy, Screen.width - halfW - mgn * 2f, chh), uiTextCreditsMusic, rightS);
        }
        #if UNITY_ANDROID
        
        if (showOrientationButton)
        {
            float s = UiGlobalScale();
            bool wantLandscape = PlayerPrefs.GetInt("M3_opt_orient", 0) == 1;
            if (UiButton(new Rect(Screen.width - 158f * s, Screen.height - 46f * s, 146f * s, 36f * s), wantLandscape ? uiTextPortrait : uiTextWidescreen))
            {
                wantLandscape = !wantLandscape; 
                PlayerPrefs.SetInt("M3_opt_orient", wantLandscape ? 1 : 0);
                PlayerPrefs.Save();
                Screen.orientation = wantLandscape ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            }
        }
#endif
 DrawMenuChrome(menuSplashTex); 
    }


    
    

    bool levelListPointerDown = false; 
    Vector2 levelListDownPos;          
    Vector2 levelListLastPos;          
    bool levelListDragActive = false;  
    float levelListFlickVel = 0f;      
    float levelListBarW = 15f;      
    float levelScrollTargetY = -1f;   

    
    struct LevelListLayout
    {
        public Rect panel;      
        public float sx, sy, sF;
        public Rect viewport;   
        public float availW;    
        public float side;      
        public float faceH;     
        public float infoTop, infoH, rowGap, cellH;
        public int rows;
        public float contentH;  
    }

    LevelListLayout GetLevelListLayout()
    {
        var p = MakeUiPanel(560f, 700f, 0.80f, 0.85f);


        
        float areaW = p.rect.width - 20f * p.sx;
        Rect scrollRect = new Rect(p.rect.x + 10 * p.sx, p.rect.y + 70 * p.sy, areaW, p.rect.height - 130 * p.sy);

        float sbW = levelListBarW; 
        sbW += 4f; 
        float availW = scrollRect.width - sbW;            

        float marginX = 8f * p.sx;
        float colGap = 14f * p.sx;
        float side = (availW - 2f * marginX - colGap) / 2f; 
        float faceH = side * 9f / 16f;                  
        float infoTop = 0f;
        float infoH = 0f;    
        float rowGap = 18f * p.sy;
        float cellH = faceH;
        int rows = (totalLevels + 1) / 2;
        float contentH = 4f * p.sy + rows * cellH + (rows - 1) * rowGap;

        return new LevelListLayout { panel = p.rect, sx = p.sx, sy = p.sy, sF = p.sF, viewport = scrollRect, availW = availW, side = side, faceH = faceH, infoTop = infoTop, infoH = infoH, rowGap = rowGap, cellH = cellH, rows = rows, contentH = contentH };
    }

    float LevelListMaxScroll(LevelListLayout l)
    {
        return Mathf.Max(0f, l.contentH - l.viewport.height); 
    }


    
    void HandleLevelListScroll()
    {
        var l = GetLevelListLayout();
        float maxScroll = LevelListMaxScroll(l);

        
        if (levelScrollTargetY >= 0f)
        {
            float dt = Time.unscaledDeltaTime;
            levelScroll.y += (levelScrollTargetY - levelScroll.y) * (1f - Mathf.Exp(-dt / 0.12f));
            if (Mathf.Abs(levelScrollTargetY - levelScroll.y) < 0.5f) { levelScroll.y = levelScrollTargetY; levelScrollTargetY = -1f; } 
        }

        
        var wheelMouse = Mouse.current;
        if (wheelMouse != null && maxScroll > 0f)
        {
            float wheelY = -wheelMouse.scroll.ReadValue().y; 
            if (wheelY != 0f && l.viewport.Contains(wheelMouse.position.ReadValue()))
            {
                levelListFlickVel = 0f; 
                levelScrollTargetY = -1f; 
                float rowStep = l.cellH + l.rowGap; 
                levelScroll.y = Mathf.Clamp(levelScroll.y + wheelY * rowStep, 0f, maxScroll);
            }
        }
        
        if (levelListFlickVel != 0f)
        {
            levelScroll.y += levelListFlickVel * Time.unscaledDeltaTime;
            levelListFlickVel *= Mathf.Exp(-Time.unscaledDeltaTime / 0.15f); 
            if (levelScroll.y <= 0f || levelScroll.y >= maxScroll)
            {
                levelScroll.y = Mathf.Clamp(levelScroll.y, 0f, maxScroll);
                levelListFlickVel = 0f; 
            }
            else if (Mathf.Abs(levelListFlickVel) < 20f)
                levelListFlickVel = 0f;
        }

        var ts = Touchscreen.current;
        var mouse = Mouse.current;
        bool touchDown = ts != null && ts.primaryTouch.press.isPressed;
        Vector2 pos = touchDown ? ts.primaryTouch.position.ReadValue() : (mouse != null ? mouse.position.ReadValue() : Vector2.zero);
        bool pointerDown = touchDown || (mouse != null && mouse.leftButton.isPressed);

        if (pointerDown)
        {
            float barW = levelListBarW; 
            bool overBar = l.viewport.Contains(pos) && pos.x > l.viewport.xMax - Mathf.Max(barW, 15f);
            if (!levelListPointerDown && l.viewport.Contains(pos) && !overBar)
            {
                levelListPointerDown = true;
                levelListDragActive = false; 
                levelListDownPos = pos;
                levelListLastPos = pos;
                levelListFlickVel = 0f;      
                levelScrollTargetY = -1f;   
            }
            if (levelListPointerDown)
            {
                float dy = pos.y - levelListLastPos.y;
                if (!levelListDragActive && Vector2.Distance(pos, levelListDownPos) > 10f)
                    levelListDragActive = true; 
                if (levelListDragActive && maxScroll > 0f)
                {
                    levelScroll.y = Mathf.Clamp(levelScroll.y + dy, 0f, maxScroll);
                    float dt = Time.unscaledDeltaTime;
                    if (dt > 0.001f)
                        levelListFlickVel = Mathf.Lerp(levelListFlickVel, dy / dt, 1f - Mathf.Exp(-dt / 0.04f)); 
                }
                levelListLastPos = pos;
            }
        }
        else if (levelListPointerDown)
        {
            levelListPointerDown = false;
            levelListFlickVel = Mathf.Clamp(levelListFlickVel, -3000f, 3000f); 
        }
    }


    
    void NudgeLevelList(float dy)
    {
        var l = GetLevelListLayout();
        levelListFlickVel = 0f;
        levelScrollTargetY = Mathf.Clamp(levelScroll.y + dy, 0f, LevelListMaxScroll(l)); 
    }


    
    void GoBackFromLevelSelect()
    {
        if (levelSelectFromGame)
        {
            levelSelectFromGame = false;
            TransitionTo(GameState.Playing, true); 
        }
        else
            TransitionTo(GameState.Menu, true);   
    }

    
    

    bool levelSelectFromGame = false;

    bool levelResultsCached = false;
    int[] lvlScoreCache = new int[totalLevels + 1];
    int[] lvlMovesCache = new int[totalLevels + 1];
    float[] lvlTimeCache = new float[totalLevels + 1];
    bool[] lvlHasResult = new bool[totalLevels + 1];
    int[] lvlStarsCache = new int[totalLevels + 1]; 

void DrawLevelSelect()
    {
        if (!levelResultsCached)
        {
            for (int lvl = 1; lvl <= totalLevels; lvl++)
            {
                lvlHasResult[lvl] = PlayerPrefs.HasKey("M3_score_" + lvl);
                if (lvlHasResult[lvl])
                {
                    lvlScoreCache[lvl] = PlayerPrefs.GetInt("M3_score_" + lvl);
                    lvlMovesCache[lvl] = PlayerPrefs.GetInt("M3_moves_" + lvl);
                    lvlTimeCache[lvl] = PlayerPrefs.GetFloat("M3_time_" + lvl);
                    lvlStarsCache[lvl] = PlayerPrefs.GetInt("M3_stars_" + lvl); 
                }
            }
            levelResultsCached = true;
        }
        

        levelListBarW = GUI.skin.verticalScrollbar.fixedWidth; if (levelListBarW <= 0f) levelListBarW = 15f; 
        var l = GetLevelListLayout();
        
        if (Event.current.type == EventType.ScrollWheel && l.viewport.Contains(Event.current.mousePosition))
            Event.current.Use();
        Rect panel = l.panel; float sx = l.sx, sy = l.sy, sF = l.sF; uiScaleCur = sF;
        if (!levelSelectFromGame) GUI.Box(panel, ""); 
        else { Color oldC = GUI.color; GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(levelSelectBoxAlpha)); GUI.Box(panel, ""); GUI.color = oldC; } 

        var title = UiStyle(GUI.skin.label);
        title.fontSize = Mathf.Max(16, (int)(34 * sF));
        title.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, panel.y + 16 * sy, Screen.width, 50 * sy), uiTextSelectLevel, title);

        
        var oldVBar = GUI.skin.verticalScrollbar;
        var oldThumb = GUI.skin.verticalScrollbarThumb;
        var oldUpBtn = GUI.skin.verticalScrollbarUpButton;
        var oldDownBtn = GUI.skin.verticalScrollbarDownButton;
        if (oldVBar != null) { var h1 = new GUIStyle(oldVBar); h1.normal.background = null; h1.hover.background = null; h1.active.background = null; GUI.skin.verticalScrollbar = h1; }
        if (oldThumb != null) { var h2 = new GUIStyle(oldThumb); h2.normal.background = null; h2.hover.background = null; h2.active.background = null; GUI.skin.verticalScrollbarThumb = h2; }
        if (oldUpBtn != null) { var h3 = new GUIStyle(oldUpBtn); h3.normal.background = null; h3.hover.background = null; h3.active.background = null; GUI.skin.verticalScrollbarUpButton = h3; }
        if (oldDownBtn != null) { var h4 = new GUIStyle(oldDownBtn); h4.normal.background = null; h4.hover.background = null; h4.active.background = null; GUI.skin.verticalScrollbarDownButton = h4; }

        levelScroll = GUI.BeginScrollView(l.viewport, levelScroll, new Rect(0, 0, l.availW, l.contentH));
        
        float colGap = 14f * sx;
        float x0 = (l.availW - (2f * l.side + colGap)) / 2f; 

        var btnStyle = UiStyle(GUI.skin.button);
        btnStyle.fontSize = FitFontSize(l.side * 0.45f, "Stage 10", l.side);

        for (int row = 0; row < l.rows; row++)
        {
            float yTop = 4f * sy + row * (l.cellH + l.rowGap); 
            for (int col = 0; col < 2; col++)
            {
                int lvl = row * 2 + col + 1;
                bool unlocked = lvl <= unlockedLevel;
                Rect face = new Rect(x0 + col * (l.side + colGap), yTop, l.side, l.faceH);

                if (lvlFlyActive && lvl <= lvlFlyCount) continue; 

                if (!unlocked) GUI.enabled = false;
                bool start = GUI.Button(face, "", btnStyle); 
                if (!unlocked) GUI.enabled = true;
                
                if (start && unlocked && !levelListDragActive) BeginLoadThenLevel(lvl); 

                
                var nameSt = UiStyle(GUI.skin.label);
                nameSt.fontSize = Mathf.Max(12, (int)(stageNameFont * sF));
                nameSt.alignment = TextAnchor.MiddleCenter;
                float nmW = face.width * 0.9f;
                Rect nmR = new Rect(face.center.x + stageNameOffX * sF - nmW / 2f, face.y + stageNameOffY * sy, nmW, 30f * sy);
                GUI.Label(nmR, unlocked ? uiTextLevelTemplate + lvl : uiTextLocked, nameSt);

                
                if (lvlHasResult[lvl] && lvlStarsCache[lvl] > 0)
                {
                    float stSz = face.width * starsScaleF;
                    float stGap = face.width * 0.03f;
                    float stTotalW = 3f * stSz + 2f * stGap;
                    float stX0 = face.center.x + starsOffX * sF - stTotalW / 2f;
                    float stY = face.y + starsOffY * sy;
                    for (int si = 0; si < 3; si++)
                    {
                        var sspr = StarSpriteFor(si, lvlStarsCache[lvl]);
                        if (sspr != null) GUI.DrawTexture(new Rect(stX0 + si * (stSz + stGap), stY, stSz, stSz), sspr.texture);
                    }
                }

                
                var info = UiStyle(GUI.skin.label);
                info.fontSize = Mathf.Max(9, (int)(infoFont * sF));
                info.alignment = TextAnchor.MiddleCenter;
                float infW = face.width * 0.92f;
                Rect line1R = new Rect(face.center.x + info1OffX * sF - infW / 2f, face.y + info1OffY * sy, infW, 14f * sy);
                Rect line2R = new Rect(face.center.x + info2OffX * sF - infW / 2f, face.y + info2OffY * sy, infW, 14f * sy);
                if (lvlHasResult[lvl])
                {
                    GUI.Label(line1R, uiTextScorePrefix + lvlScoreCache[lvl] + "   " + uiTextMovesPrefix + lvlMovesCache[lvl], info);
                    GUI.Label(line2R, uiTextTimePrefix + FormatTime(lvlTimeCache[lvl]), info);
                }
                else if (unlocked)
                {
                    GUI.Label(new Rect(face.center.x - infW / 2f, face.y + info1OffY * sy, infW, 28f * sy), uiTextNotCleared, info);
                }
            }
        }
        GUI.EndScrollView(false);
        
        if (oldVBar != null) GUI.skin.verticalScrollbar = oldVBar;
        if (oldThumb != null) GUI.skin.verticalScrollbarThumb = oldThumb;
        if (oldUpBtn != null) GUI.skin.verticalScrollbarUpButton = oldUpBtn;
        if (oldDownBtn != null) GUI.skin.verticalScrollbarDownButton = oldDownBtn;

        if (lvlFlyActive)   
            for (int lvl = 1; lvl <= lvlFlyCount; lvl++)
                DrawFlyingLevelTile(lvl, LevelFaceScreenRect(l, lvl));

        
        float bsz = 56f * sy;   
        float browY = panel.y + panel.height - 10f * sy - bsz;
        var backStyle = UiStyle(GUI.skin.button);
        backStyle.fontSize = Mathf.Max(14, (int)(bsz * 0.55f));
        Rect backR = new Rect(panel.x + 10f * sx, browY, bsz, bsz);
        if (!lvlFlyActive && GUI.Button(backR, uiTextBackArrow, backStyle)) { PlaySfx(SfxSlot.UiClick, cam != null ? cam.transform.position : Vector3.zero); GoBackFromLevelSelect(); }

        float gap = 8f * sx;
        float uwd = bsz * 1.6f; 
        var uwStyle = UiStyle(GUI.skin.button);
        uwStyle.fontSize = Mathf.Max(12, (int)(bsz * 0.45f));
        Rect downR = new Rect(backR.xMax + gap, browY, uwd, bsz);
        if (!lvlFlyActive)
        {
            bool hitDown;
            if (downButtonSprite != null)
            {
                DrawHugBg(downR, downButtonSprite.texture); 
                hitDown = GUI.Button(downR, GUIContent.none);
            }
            else
                hitDown = GUI.Button(downR, uiTextListDown, uwStyle);
            if (hitDown) NudgeLevelList(l.cellH);   

            Rect upR = new Rect(downR.xMax + gap, browY, uwd, bsz); 
            bool hitUp;
            if (upButtonSprite != null)
            {
                DrawHugBg(upR, upButtonSprite.texture); 
                hitUp = GUI.Button(upR, GUIContent.none);
            }
            else
                hitUp = GUI.Button(upR, uiTextListUp, uwStyle);
            if (hitUp) NudgeLevelList(-l.cellH);  
        }

        DrawMenuChrome(levelSelectSplashTex); 
    }


    
    Rect LevelFaceScreenRect(LevelListLayout l, int lvl)
    {
        float colGap = 14f * l.sx;
        float x0 = (l.availW - (2f * l.side + colGap)) / 2f;
        int row = (lvl - 1) / 2, col = (lvl - 1) % 2;
        float yTop = 4f * l.sy + row * (l.cellH + l.rowGap); 
        return new Rect(l.viewport.x + x0 + col * (l.side + colGap), l.viewport.y + yTop, l.side, l.faceH);
    }


    

    
    static Texture2D hpStripTex = null;
    static void EnsureHpStripTex()
    {
        if (hpStripTex == null)
        {
            hpStripTex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            hpStripTex.SetPixels(px);
            hpStripTex.Apply();
        }
    }


    
    

    Rect ComputeLifeStripRect()
    {
        float s = UiGlobalScale();
        bool landscape = !showOrientationButton || Screen.width > Screen.height; 
        float kPix = cam != null ? Screen.height / (2f * Mathf.Max(0.1f, cam.orthographicSize)) : 1f; 
        float halfBh = rows * cellSize / 2f;
        float boardTopY = Screen.height / 2f + (cam != null ? cam.transform.position.y : 0f) * kPix - halfBh * kPix;   
        float tsz = Mathf.Clamp(timerSizeScale, 0.25f, 3f); 
        Rect tRect;
        if (!landscape)
            tRect = new Rect((Screen.width - 240f * s * tsz) / 2f + timerPosX * s, boardTopY - 68f * s * tsz - 50f * s + timerPosY * s, 240f * s * tsz, 68f * s * tsz);
        else
        {
            tRect = new Rect(wideLeftStrip * Screen.width / 2f - (240f * s * tsz) / 2f + timerPosX * s, 8f * s + timerPosY * s, 240f * s * tsz, 68f * s * tsz);
            tRect.x = Mathf.Clamp(tRect.x, 4f * s, Screen.width - tRect.width - 4f * s); 
        }
        float stripH = 36f * s; 
        float boardCXpx = Screen.width / 2f - (cam != null ? cam.transform.position.x : 0f) * kPix; 
        float stripW = cols * cellSize * kPix; 
        float y = tRect.yMax + 6f * s - 50f * s; 
        y = Mathf.Max(y, tRect.yMax + 4f * s);                    
        if (y > boardTopY - stripH - 2f * s) y = boardTopY - stripH - 2f * s; 
        y = Mathf.Max(y, 4f * s);                                  
        return new Rect(boardCXpx - stripW / 2f, y, stripW, stripH);
    }


    
    

    List<Rect> GetEmptyLifeSegmentRects()
    {
        var list = new List<Rect>();
        if (maxHp <= 0 || rows <= 0) return list; 
        int lives = Mathf.Clamp(maxHp > 0 ? hp : 0, 0, maxHp); 
        int slots = Mathf.Max(1, maxHp);                       
        if (lives >= slots) return list;                        
        float s = UiGlobalScale();
        Rect stripR = ComputeLifeStripRect();
        float border = 2f * s;                                  
        Rect inner = new Rect(stripR.x + border, stripR.y + border, stripR.width - 2f * border, stripR.height - 2f * border);
        if (inner.width <= 0f) return list;
        float pitch = inner.width / slots;                      
        float segW = pitch * 0.72f;                             
        for (int i = lives; i < slots; i++)
            list.Add(new Rect(inner.x + i * pitch + (pitch - segW) / 2f, inner.y, segW, inner.height));
        return list;
    }


    

    
    void TogglePauseOverlay()
    {
        if (!paused) { pauseOverlayOpen = true; SetPaused(true); }  
        else { pauseOverlayOpen = false; SetPaused(false); }         
    }


    
    void OpenHelpScreen()
    {
        helpClosing = false;
        helpOpen = true;      
        SetPaused(true);
    }


    
    void BeginHelpClose()
    {
        if (!helpOpen || helpClosing) return;
        helpClosing = true;
        helpCloseT = 0f;
    }


    

    void DrawGoldLine(Vector2 anchor, float sizeDial, float offXDial, float offYDial, string prefixText, float value, bool captureBankRect = false) 
    {
        float s = UiGlobalScale();
        int fs = Mathf.Max(10, (int)(sizeDial * s)); 
        var st = UiStyle(GUI.skin.label);
        st.fontSize = fs;
        string numStr = (prefixText ?? "") + Mathf.RoundToInt(value).ToString(); 
        Vector2 numSz = st.CalcSize(new GUIContent(numStr));
        float ch = fs * 0.9f; 
        float cw = goldCoinSprite != null ? ch * (goldCoinSprite.rect.width / Mathf.Max(1f, goldCoinSprite.rect.height)) : 0f; 
        float gap = fs * 0.25f; 
        Rect box = new Rect(anchor.x + offXDial * s - (cw + gap + numSz.x) / 2f, anchor.y + offYDial * s, cw + gap + numSz.x, fs * 1.4f); 
        if (captureBankRect) hudBankRect = box; 
        var stL = new GUIStyle(st); stL.alignment = TextAnchor.MiddleLeft;
        Rect coinR = new Rect(box.x, box.y + (box.height - ch) / 2f, cw, ch);
        Rect numR = new Rect(coinR.xMax + gap, box.y, numSz.x + 1f, box.height);
        var oc = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.65f); 
        if (goldCoinSprite != null) GUI.DrawTexture(new Rect(coinR.x + 2f * s, coinR.y + 2f * s, cw, ch), goldCoinSprite.texture);
        GUI.Label(new Rect(numR.x + 2f * s, numR.y + 2f * s, numSz.x + 1f, box.height), numStr, stL);
        if (goldCoinSprite != null) { GUI.color = Color.white; GUI.DrawTexture(coinR, goldCoinSprite.texture); } 
        GUI.color = new Color(1f, 0.93f, 0.75f); 
        GUI.Label(numR, numStr, stL);
        GUI.color = oc;
    }


    

    void DrawStarRow(Vector2 anchor, float offXDial, float offYDial)
    {
        float s = UiGlobalScale();
        int halfSteps = HalfStarSteps(); 
        float sz = starSize * s;
        float gap = starGap * s;
        float totalW = 3f * sz + 2f * gap;
        for (int i = 0; i < 3; i++)
        {
            var spr = StarSpriteFor(i, halfSteps);
            if (spr != null) GUI.DrawTexture(new Rect(anchor.x + offXDial * s - totalW / 2f + i * (sz + gap), anchor.y + offYDial * s, sz, sz), spr.texture); 
        }
    }


    
    Sprite StarSpriteFor(int starIndex, int halfSteps)
    {
        int lo = Mathf.Clamp(halfSteps - starIndex * 2, 0, 2); 
        return lo >= 2 ? starFullSprite : (lo == 1 ? starHalfSprite : starEmptySprite);
    }


    

    
    Rect itemCrossSqR = Rect.zero;   
    Rect itemMegaSqR = Rect.zero;    
    float itemSqShakeT = -99f;       
    bool itemSqShakeIsMega = false;  



    void DrawItemSquares(Vector2 anchor, bool landscape)
    {
        float s = UiGlobalScale();
        float sz = itemSquareSize * s; 
        float gap = 8f * s;            
        float offX = landscape ? itemSqWideX : itemSqPosX;   
        float offY = landscape ? itemSqWideY : itemSqPosY;
        float totalW = 2f * sz + gap;

        float x0 = anchor.x + offX * s - totalW / 2f, y0 = anchor.y + offY * s; 
        itemCrossSqR = new Rect(x0, y0, sz, sz);     
        itemMegaSqR = new Rect(x0 + sz + gap, y0, sz, sz);

        float stT = Time.unscaledTime - itemSqShakeT; 
        float shXc = (!itemSqShakeIsMega && stT >= 0f && stT < 0.35f) ? Mathf.Sin(stT * 42f) * 6f * s * (1f - stT / 0.35f) : 0f;
        float shXm = (itemSqShakeIsMega && stT >= 0f && stT < 0.35f) ? Mathf.Sin(stT * 42f) * 6f * s * (1f - stT / 0.35f) : 0f;
        DrawItemSquare(x0 + shXc, y0, sz, crossBlastIcon, crossBlasts, blastDragging && !blastDragIsMega); 
        DrawItemSquare(x0 + sz + gap + shXm, y0, sz, megaCrossIcon, megaCrossBlasts, blastDragging && blastDragIsMega);
    }

    void DrawItemSquare(float x, float y, float sz, Sprite icon, int owned, bool highlight)
    {
        EnsureWhitePx(); 
        var oc = GUI.color;
        if (icon != null)
        {
            if (owned <= 0) GUI.color = new Color(1f, 1f, 1f, 0.4f); 
            GUI.DrawTexture(new Rect(x, y, sz, sz), icon.texture);
        }
        else
        {
            GUI.color = new Color(0.5f, 0.52f, 0.58f, owned <= 0 ? 0.4f : 1f); 
            GUI.DrawTexture(new Rect(x, y, sz, sz), _whitePx);
        }
        if (owned <= 0) 

        {
            GUI.color = new Color(0.45f, 0.47f, 0.52f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, sz, sz), _whitePx);
        }
        GUI.color = oc;

        
        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(9, (int)(sz * 0.32f));
        st.alignment = TextAnchor.MiddleRight;
        string n = owned.ToString();
        Vector2 nsz = st.CalcSize(new GUIContent(n));
        var ocB = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.65f); 
        GUI.Label(new Rect(x + sz - nsz.x - 1f, y + sz - nsz.y - 1f, nsz.x, nsz.y), n, st);
        GUI.color = owned <= 0 ? new Color(1f, 1f, 1f, 0.4f) : new Color(1f, 0.93f, 0.75f); 
        GUI.Label(new Rect(x + sz - nsz.x, y + sz - nsz.y, nsz.x, nsz.y), n, st);
        GUI.color = ocB;
        if (highlight) 

        {
            EnsureWhitePx();
            var ocH = GUI.color;
            float bw = Mathf.Max(2f, sz * 0.06f); 
            GUI.color = new Color(1f, 0.93f, 0.75f, 0.95f); 
            GUI.DrawTexture(new Rect(x - bw, y - bw, sz + 2f * bw, bw), _whitePx);   
            GUI.DrawTexture(new Rect(x - bw, y + sz, sz + 2f * bw, bw), _whitePx);  
            GUI.DrawTexture(new Rect(x - bw, y, bw, sz), _whitePx);                 
            GUI.DrawTexture(new Rect(x + sz, y, bw, sz), _whitePx);                 
            GUI.color = ocH;
        }
    }


    

    
    void DrawCoinLine(Vector2 center, float fontPx, string prefixText, float value, bool captureBankRect = false) 
    {
        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(12, (int)fontPx);
        string numStr = Mathf.RoundToInt(value).ToString(); 
        Vector2 preSz = prefixText != null ? st.CalcSize(new GUIContent(prefixText)) : Vector2.zero;
        float ch = fontPx * 0.9f; 
        float cw = goldCoinSprite != null ? ch * (goldCoinSprite.rect.width / Mathf.Max(1f, goldCoinSprite.rect.height)) : 0f; 
        Vector2 numSz = st.CalcSize(new GUIContent(numStr));
        float gapPx = fontPx * 0.25f;
        bool hasPre = prefixText != null && preSz.x > 0f;
        bool hasCoin = goldCoinSprite != null;
        float totalW = (hasPre ? preSz.x + gapPx : 0f) + (hasCoin ? cw + gapPx : 0f) + numSz.x;
        var stL = new GUIStyle(st); stL.alignment = TextAnchor.MiddleLeft;
        float x = center.x - totalW / 2f, yTop = center.y - fontPx * 0.7f, boxH = fontPx * 1.4f; 
        if (captureBankRect) hudBankRect = new Rect(x, yTop, totalW, boxH); 
        var oc = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.65f); 
        float cxS = x; if (hasPre) { GUI.Label(new Rect(cxS + 2f, yTop + 2f, preSz.x + 1f, boxH), prefixText, stL); cxS += preSz.x + gapPx; }
        if (hasCoin) { GUI.DrawTexture(new Rect(cxS + 2f, yTop + (boxH - ch) / 2f + 2f, cw, ch), goldCoinSprite.texture); cxS += cw + gapPx; }
        GUI.Label(new Rect(cxS + 2f, yTop + 2f, numSz.x + 1f, boxH), numStr, stL);
        float cx = x; if (hasPre) { GUI.color = new Color(1f, 0.93f, 0.75f); GUI.Label(new Rect(cx, yTop, preSz.x + 1f, boxH), prefixText, stL); cx += preSz.x + gapPx; } 
        if (hasCoin) { GUI.color = Color.white; GUI.DrawTexture(new Rect(cx, yTop + (boxH - ch) / 2f, cw, ch), goldCoinSprite.texture); cx += cw + gapPx; } 
        GUI.color = new Color(1f, 0.93f, 0.75f);
        GUI.Label(new Rect(cx, yTop, numSz.x + 1f, boxH), numStr, stL);
        GUI.color = oc;
    }


    
    

    void DrawHiddenStrip(bool landscape)
    {
        float s = UiGlobalScale();
        int fs = Mathf.Max(10, (int)(hiddenStripSize * s)); 
        var st = UiStyle(GUI.skin.label); 
        st.fontSize = fs;

        string lvlStr = hiddenLevelLabel + GetHiddenLevel();
        string goldStr = Mathf.RoundToInt(hiddenGold).ToString(); 
        Vector2 lvlSz = st.CalcSize(new GUIContent(lvlStr));
        Vector2 numSz = st.CalcSize(new GUIContent(goldStr));
        float ch = fs * 0.9f;   
        float cw = goldCoinSprite != null ? ch * (goldCoinSprite.rect.width / Mathf.Max(1f, goldCoinSprite.rect.height)) : 0f; 
        float gap = fs * 0.25f; 

        float lineH = fs * 1.4f;    
        float stackGap = fs * 0.35f; 
        float marginX = 12f * s, marginY = 8f * s; 
        float offX = landscape ? hiddenStripWideX : hiddenStripPosX;   
        float offY = landscape ? hiddenStripWideY : hiddenStripPosY;

        float line2W = cw + gap + numSz.x;                    
        float rightEdge = Screen.width - marginX + offX * s;  
        float topY = marginY + offY * s;

        var stL = new GUIStyle(st); stL.alignment = TextAnchor.MiddleLeft;
        Rect lvlR = new Rect(rightEdge - lvlSz.x, topY, lvlSz.x, lineH);                          
        Rect num2R = new Rect(rightEdge - line2W + cw + gap, topY + lineH + stackGap, numSz.x, lineH); 
        Rect coin2R = new Rect(num2R.x - gap, num2R.y + (lineH - ch) / 2f, cw, ch);               

        var oc = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.65f); 
        GUI.Label(new Rect(lvlR.x + 2f * s, lvlR.y + 2f * s, lvlSz.x + 1f, lineH), lvlStr, stL);
        if (goldCoinSprite != null) GUI.DrawTexture(new Rect(coin2R.x + 2f * s, coin2R.y + 2f * s, cw, ch), goldCoinSprite.texture);
        GUI.Label(new Rect(num2R.x + 2f * s, num2R.y + 2f * s, numSz.x + 1f, lineH), goldStr, stL);
        if (goldCoinSprite != null) { GUI.color = Color.white; GUI.DrawTexture(coin2R, goldCoinSprite.texture); } 
        GUI.color = new Color(1f, 0.93f, 0.75f); 
        GUI.Label(lvlR, lvlStr, stL);
        GUI.Label(num2R, goldStr, stL);
                GUI.color = oc;
    }


    

    
    void DrawDailyToasts()
    {
        if (dailyToasts.Count == 0) return;
        float s = UiGlobalScale();
        int fs = Mathf.Max(10, (int)(26f * dailyToastScale * s)); 
        var st = UiStyle(GUI.skin.label);
        st.fontSize = fs;
        st.fontStyle = FontStyle.Bold;      
        EnsureWhitePx();
        float dur = Mathf.Max(0.1f, dailyToastDur);
        var ocT = GUI.color;
        float stackY = dailyToastOffY * s; 
        for (int i = 0; i < dailyToasts.Count; i++)
        {
            DailyToast t = dailyToasts[i];
            string txt = t.title ?? "";
            Vector2 tsz = st.CalcSize(new GUIContent(txt));
            float padX = 16f * s * dailyToastScale, padY = 10f * s * dailyToastScale; 
            float bw = tsz.x + 2f * padX, bh = tsz.y + 2f * padY;
            float inT = Mathf.Clamp01(t.age / 0.25f);                 
            float eIn = 1f - (1f - inT) * (1f - inT);                  
            float alpha = Mathf.Clamp01((dur - t.age) / 0.3f);         
            if (alpha <= 0f) { stackY += bh + 8f * s; continue; }      
            float bx = Screen.width - dailyToastOffX * s - bw + (1f - eIn) * (bw + 24f); 
            Rect boxR = new Rect(bx, stackY, bw, bh);
            if (dailyToastSplashArt != null) 

                { GUI.color = new Color(1f, 1f, 1f, alpha); GUI.DrawTexture(boxR, dailyToastSplashArt.texture); }
            else { GUI.color = new Color(0.07f, 0.08f, 0.10f, 0.80f * alpha); 
                GUI.DrawTexture(boxR, _whitePx); }
            float fw = Mathf.Max(2f, bh * 0.05f);                     
            GUI.color = new Color(1f, 0.84f, 0.35f, alpha);            
            GUI.DrawTexture(new Rect(boxR.x - fw, boxR.y - fw, boxR.width + 2f * fw, fw), _whitePx);    
            GUI.DrawTexture(new Rect(boxR.x - fw, boxR.yMax, boxR.width + 2f * fw, fw), _whitePx);       
            GUI.DrawTexture(new Rect(boxR.x - fw, boxR.y, fw, boxR.height), _whitePx);                   
            GUI.DrawTexture(new Rect(boxR.xMax, boxR.y, fw, boxR.height), _whitePx);                     
            var stC = new GUIStyle(st); stC.alignment = TextAnchor.MiddleCenter;
            GUI.color = new Color(1f, 0.93f, 0.75f, alpha);           
            GUI.Label(new Rect(boxR.x + padX * 0.25f, boxR.y, bw - padX * 0.5f, bh), txt, stC);
            stackY += bh + 8f * s; 
        }
        GUI.color = ocT;
    }

    void DrawCoinFlights()
    {
        if (coinFlights.Count == 0 || goldCoinSprite == null) return;
        float s = UiGlobalScale();
        Vector2 target = hudBankRect.center; 
        var stL = UiStyle(GUI.skin.label);
        int fs = Mathf.Max(10, (int)(dailyCoinSize * 0.52f * s)); 
        stL.fontSize = fs;
        for (int i = 0; i < coinFlights.Count; i++)
        {
            CoinFlight cf = coinFlights[i];
            float k = Mathf.Clamp01(cf.t);
            Vector2 c0 = (cf.from + target) * 0.5f + new Vector2(dailyFlyCurveX * s, dailyFlyCurveY * s); 
            float ik = 1f - k;
            Vector2 pos = ik * ik * cf.from + 2f * ik * k * c0 + k * k * target; 
            float pop = k > 0.8f ? 1f + (k - 0.8f) / 0.2f * 0.35f : 1f;          
            float chh = dailyCoinSize * s * pop, cww = chh * (goldCoinSprite.rect.width / Mathf.Max(1f, goldCoinSprite.rect.height)); 
            string lbl = "+" + Mathf.RoundToInt(cf.reward) + "g";                
            Vector2 lsz = stL.CalcSize(new GUIContent(lbl));
            var ocF = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.65f); 
            GUI.DrawTexture(new Rect(pos.x - cww / 2f + 2f * s, pos.y - chh / 2f + 2f * s, cww, chh), goldCoinSprite.texture);
            GUI.Label(new Rect(pos.x + cww * 0.6f + 2f * s, pos.y - lsz.y * 0.5f + 2f * s, lsz.x + 1f, lsz.y), lbl, stL);
            GUI.color = Color.white; 
            GUI.DrawTexture(new Rect(pos.x - cww / 2f, pos.y - chh / 2f, cww, chh), goldCoinSprite.texture);
            GUI.color = new Color(1f, 0.84f, 0.35f); 
            GUI.Label(new Rect(pos.x + cww * 0.6f, pos.y - lsz.y * 0.5f, lsz.x + 1f, lsz.y), lbl, stL);
            GUI.color = ocF;
        }
    }

void DrawGameHUD()
    {
        float s = UiGlobalScale(); 
        bool landscape = !showOrientationButton || Screen.width > Screen.height; 

        
        float kPix = cam != null ? Screen.height / (2f * Mathf.Max(0.1f, cam.orthographicSize)) : 1f; 
        float halfBh = rows * cellSize / 2f;
        float halfWd = cols * cellSize / 2f;
        float boardTopY = Screen.height / 2f + (cam != null ? cam.transform.position.y : 0f) * kPix - halfBh * kPix;   
        var lbl = UiStyle(GUI.skin.label);
        lbl.fontSize = Mathf.Max(13, (int)(15 * s));

        
        float textW = landscape ? Mathf.Max(60f, Screen.width / 2f - halfWd * kPix - 24f * s) : Screen.width * 0.6f;
        float infoTop = !landscape ? 0f : (68f + 24f) * s; 

        
        string topLine = null;
        if (showHudLevelText && showHudScoreText) topLine = uiTextLevelTemplate + currentLevel + "   Score: " + score;
        else if (showHudLevelText) topLine = uiTextLevelTemplate + currentLevel;
        else if (showHudScoreText) topLine = uiTextScorePrefix + score;
        if (topLine != null) GUI.Label(new Rect(12 * s, 8 * s + infoTop, textW, 30 * s), topLine, lbl);

        
        if (showHudLegalMovesText)
        {
            string moves = busy ? "Legal moves: ..." : (uiTextLegalMoves + availableMoves);
            GUI.Label(new Rect(12 * s, 34 * s + infoTop, textW, 30 * s), moves, lbl);
        }

        
        if (showHudHelpText)
        {
            var helpStyle = new GUIStyle(lbl); helpStyle.wordWrap = true;
            string help = autoDemo
                ? uiTextHudHelpAutoDemo
                : uiTextHudHelpNormal;
            GUI.Label(new Rect(12 * s, 60 * s + infoTop, textW, landscape ? 64f * s : 30f * s), help, helpStyle);
        }


        
        float d = cam != null && cellSize > 0f ? cellSize * kPix : 0f; 
        float tsz = Mathf.Clamp(timerSizeScale, 0.25f, 3f); 
        Rect tRect, pbr;
        {
            float tw = 240f * s * tsz; 
            float th = 68f * s * tsz;  
            if (!landscape)
            {
                tRect = new Rect((Screen.width - tw) / 2f + timerPosX * s, boardTopY - th - 50f * s + timerPosY * s, tw, th); 
                pbr = new Rect((Screen.width - 1.5f * d) / 2f + pauseBtnOffsetX * s, Screen.height - d - 12f * s + pauseBtnOffsetY * s, 1.5f * d, d); 
            }
            else
            {
                tRect = new Rect(wideLeftStrip * Screen.width / 2f - tw / 2f + timerPosX * s, 8f * s + timerPosY * s, tw, th); 
                tRect.x = Mathf.Clamp(tRect.x, 4f * s, Screen.width - tw - 4f * s);           
                pbr = new Rect(12f * s + pauseBtnOffsetX * s, Screen.height - d - 12f * s + pauseBtnOffsetY * s, 1.5f * d, d);            
            }
        }

        
        if (showStageName && tRect.width > 0f)
        {
            var snStyle = UiStyle(GUI.skin.label);
            int snFs = Mathf.Max(13, (int)(stageNameSize * s)); 
            snStyle.fontSize = snFs;
            string stageStr = string.Format(uiTextStageNamePattern, currentLevel.ToString());
            float lineH = snFs * 1.4f; 
            Rect snR = new Rect(12f * s + stageNamePosX * s, 8f * s + stageNamePosY * s, textW, lineH); 
            var ocSn = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.65f); 
            GUI.Label(new Rect(snR.x + 2f * s, snR.y + 2f * s, snR.width, snR.height), stageStr, snStyle);
            GUI.color = ocSn;
            GUI.Label(snR, stageStr, snStyle);
        }

        
        if (showHudTimeText && tRect.width > 0f)
        {
            var timeStyle = UiStyle(GUI.skin.label);
            timeStyle.fontSize = Mathf.Max(14, (int)(52f * s * tsz)); 
            timeStyle.alignment = TextAnchor.UpperCenter;
            if (timerFont != null) timeStyle.font = timerFont; 
            string tStr = FormatTime(levelTime);
            
            Vector2 need = timeStyle.CalcSize(new GUIContent(tStr));
            float maxW = Mathf.Max(10f, tRect.width - 4f * s), maxH = Mathf.Max(8f, tRect.height - 3f * s);
            if (need.x > maxW || need.y > maxH)
                timeStyle.fontSize = Mathf.Max(9, (int)(timeStyle.fontSize * Mathf.Min(maxW / need.x, maxH / need.y))); 
            var oc = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.65f); 
            GUI.Label(new Rect(tRect.x + 2f * s, tRect.y + 2f * s, tRect.width, tRect.height), tStr, timeStyle);
            GUI.color = Color.yellow; 
            GUI.Label(tRect, tStr, timeStyle);
            GUI.color = oc;
        }


        
        {
            Rect stripR = ComputeLifeStripRect(); 
            EnsureHpStripTex();
            var ocHp = GUI.color;
            int lives = Mathf.Clamp(maxHp > 0 ? hp : 0, 0, maxHp); 
            int slots = Mathf.Max(1, maxHp);                       
            float border = 2f * s;                                  
            Rect inner = new Rect(stripR.x + border, stripR.y + border, stripR.width - 2f * border, stripR.height - 2f * border);
            GUI.color = Color.black;                                
            GUI.DrawTexture(stripR, hpStripTex);
            GUI.color = new Color(0.13f, 0.15f, 0.18f);             
            GUI.DrawTexture(inner, hpStripTex);
            if (lives > 0)
            {
                float pitch = inner.width / slots;                  
                float segW = pitch * 0.72f;                         
                GUI.color = new Color(0.30f, 0.85f, 0.35f);          
                for (int i = 0; i < lives; i++)
                    GUI.DrawTexture(new Rect(inner.x + i * pitch + (pitch - segW) / 2f, inner.y, segW, inner.height), hpStripTex);
            }
            float ft = Time.unscaledTime - hpFlashTime; 
            if (ft >= 0f && ft < 0.2f)
            {
                GUI.color = new Color(1f, 1f, 1f, 1f - ft / 0.2f);
                GUI.DrawTexture(stripR, hpStripTex);
            }
            GUI.color = ocHp;
        }


        
        {
            Vector2 goldAnchor;
            if (!landscape)
            {
                Rect stR = ComputeLifeStripRect(); 
                goldAnchor = new Vector2(tRect.center.x, stR.yMax + 6f * s);
            }
            else
                goldAnchor = new Vector2(wideLeftStrip * Screen.width / 2f, tRect.yMax + 10f * s);
            DrawGoldLine(goldAnchor, lvlGoldSize, landscape ? lvlGoldWideX : lvlGoldPosX, landscape ? lvlGoldWideY : lvlGoldPosY, null, levelGold);       
            DrawGoldLine(goldAnchor, totGoldSize, landscape ? totGoldWideX : totGoldPosX, landscape ? totGoldWideY : totGoldPosY, uiTextTotalPrefix, totalGold, true); 
            DrawItemSquares(goldAnchor, landscape); 
            DrawShopBtn(goldAnchor, landscape); 
            DrawStarRow(goldAnchor, landscape ? starRowWideX : starRowPosX, landscape ? starRowWideY : starRowPosY); 
        }


        
        int movesCap = LevelMovesCap();
        if (movesCap > 0)
        {
            int remaining = Mathf.Max(0, movesCap - movesMade);
            var mvStyle = UiStyle(GUI.skin.label);
            mvStyle.fontSize = Mathf.Max(13, (int)(movesLeftSize * s)); 
            float mx = 12f * s + (landscape ? movesLeftWideX : movesLeftPosX) * s;   
            float my = !landscape ? (96f + movesLeftPosY) * s : tRect.yMax + (152f + movesLeftWideY) * s; 
            GUI.Label(new Rect(mx, my, textW, 30f * s), uiTextMovesLeftPrefix + remaining, mvStyle);
        }

        
        float resetY = !landscape ? Screen.height - 46f * s + resetBtnOffsetY * s : Screen.height - d - 12f * s - 36f * s - 10f * s + resetBtnOffsetY * s; 
        if (showResetButton && UiButton(new Rect(12 * s + resetBtnOffsetX * s, resetY, 130f * s, 36 * s), uiTextResetBtn))
            ResetBoard();


        
        if (!won && !lost && cam != null && cellSize > 0f) 

        {
            var spr = paused ? playButtonSprite : pauseButtonSprite; 
            bool dimPill = helpOpen; 
            if (spr != null)
            {
                Rect sprR = new Rect(pbr.xMax - d, pbr.y, d, d); 
                var ocSpr = GUI.color;
                if (dimPill) GUI.color = new Color(1f, 1f, 1f, 0.4f); 
                DrawHugBg(sprR, spr.texture); 
                GUI.color = ocSpr;
                if (!dimPill && GUI.Button(pbr, GUIContent.none)) { PlaySfx(SfxSlot.UiClick, cam.transform.position); TogglePauseOverlay(); }
            }
            else if (!dimPill) { if (UiButton(pbr, paused ? uiTextResumeBtn : uiTextPauseBtn)) TogglePauseOverlay(); }
            else 

            {
                Rect fr; GUIStyle st; FitButton(pbr, uiTextPausedPill, true, out fr, out st);
                var ocDim = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                GUI.Button(fr, uiTextPausedPill, st);
                GUI.color = ocDim;
            }
        }

        DrawScorePops(); 


        
        if (showHelpBtn && !won && !lost && cam != null && cellSize > 0f && !helpOpen) 

        {
            float hs = Mathf.Max(20f, helpBtnSize * s);
            float hY = !landscape ? Screen.height - 46f * s - 8f * s - hs : Screen.height - d - 12f * s - 36f * s - 10f * s - 10f * s - hs; 
            Rect hbr = new Rect(12f * s + helpBtnOffsetX * s, hY + helpBtnOffsetY * s, hs, hs); 
            if (!pauseOverlayOpen)
            {
                Rect fr; GUIStyle st; FitButton(hbr, uiTextHelpPill, false, out fr, out st); 
                if (GUI.Button(fr, uiTextHelpPill, st)) { PlaySfx(SfxSlot.UiClick, cam.transform.position); OpenHelpScreen(); }
            }
            else 

            {
                Rect fr; GUIStyle st; FitButton(hbr, uiTextHelpPill, false, out fr, out st);
                var ocDim = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                GUI.Button(fr, uiTextHelpPill, st);
                GUI.color = ocDim;
            }
        }

        if (showHiddenStrip) DrawHiddenStrip(landscape); 

        if (paused && pauseOverlayOpen) DrawPauseOverlay(); 


        
        if (blastDragging)
        {
            Sprite gIcon = blastDragIsMega ? megaCrossIcon : crossBlastIcon;
            float gsz = itemSquareSize * s; 
            Vector2 gp = new Vector2(blastDragScreenPos.x, Screen.height - blastDragScreenPos.y); 
            Rect gr = new Rect(gp.x - gsz / 2f, gp.y - gsz / 2f, gsz, gsz); 
            var ocG = GUI.color;
            if (gIcon != null) { GUI.color = new Color(1f, 1f, 1f, 0.75f); GUI.DrawTexture(gr, gIcon.texture); }
            else { EnsureWhitePx(); GUI.color = new Color(0.5f, 0.52f, 0.58f, 0.6f); GUI.DrawTexture(gr, _whitePx); } 
            GUI.color = ocG;
        }
    }


    

    
    void DrawHelpScreen()
    {
        float s = UiGlobalScale();
        float dur = Mathf.Max(0.1f, overlayFlyDur);

        
        bool inFlight = helpClosing ? (helpCloseT >= 0f && helpCloseT < dur) : (helpRiseT >= 0f && helpRiseT < dur);
        float kIn = EaseOutBack(Mathf.Clamp01(helpRiseT / dur));    
        float kOut = Mathf.Pow(Mathf.Clamp01(helpCloseT / dur), 2f); 
        Vector2 off = new Vector2(0f, Screen.height * (helpClosing ? kOut : (1f - kIn)));

        var m = GUI.matrix; bool en = GUI.enabled;
        if (inFlight) GUI.enabled = false; 
        GUI.matrix = Matrix4x4.TRS(off, Quaternion.identity, Vector3.one);

        
        EnsureHelpBackdropTex();
        var ocB = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), helpBackdropTex);
        GUI.color = ocB;

        
        Sprite spr = HelpImageForLevel();
        float maxDim = Mathf.Min(Screen.width, Screen.height) * 0.65f; 
        Rect imgR;
        if (spr != null)
        {
            var nat = new Vector2(Mathf.Max(1, spr.texture.width), Mathf.Max(1, spr.texture.height));
            float iw = maxDim, ih = maxDim * nat.y / nat.x;
            if (ih > maxDim) { ih = maxDim; iw = maxDim * nat.x / nat.y; }
            imgR = new Rect((Screen.width - iw) / 2f, (Screen.height - ih) / 2f, iw, ih);
            GUI.DrawTexture(imgR, spr.texture); 
        }
        else
        {
            EnsureHelpPanelTex(); 
            float pw = maxDim * 0.8f;
            float ph = Mathf.Min(maxDim * 0.5f, Screen.height * 0.4f);
            imgR = new Rect((Screen.width - pw) / 2f, (Screen.height - ph) / 2f, pw, ph);
            var ocP = GUI.color;
            GUI.color = new Color(0.16f, 0.18f, 0.24f); 
            GUI.DrawTexture(imgR, helpPanelTex);
            GUI.color = ocP;
            var tStyle = UiStyle(GUI.skin.label);
            tStyle.fontSize = Mathf.Max(20, (int)(64 * s));
            tStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(imgR.x, imgR.y + ph * 0.3f, pw, ph * 0.5f), "HELP", tStyle);
        }

        
        float bw = 130f * s;
        Rect backR = new Rect(Screen.width - bw - 24f * s, imgR.center.y - 26f * s, bw, 52f * s);
        if (!inFlight && UiButton(backR, uiTextBack)) BeginHelpClose();

        GUI.matrix = m; GUI.enabled = en;
    }


    
    static Texture2D helpBackdropTex = null;
    static void EnsureHelpBackdropTex()
    {
        if (helpBackdropTex == null)
        {
            helpBackdropTex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            helpBackdropTex.SetPixels(px);
            helpBackdropTex.Apply();
        }
    }

    static Texture2D helpPanelTex = null;
    static void EnsureHelpPanelTex()
    {
        if (helpPanelTex == null)
        {
            int w = 256, h = 160, rad = 34; 
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - (w - 1f) / 2f) - (w / 2f - 1f - rad), 0f); 
                    float dy = Mathf.Max(Mathf.Abs(y - (h - 1f) / 2f) - (h / 2f - 1f - rad), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy); 
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((rad + 1.5f) - dist)); 
                }
            helpPanelTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            helpPanelTex.SetPixels(px);
            helpPanelTex.Apply();
        }
    }


    
    

    void DrawScorePops()
    {
        if (scorePops.Count == 0 || cam == null) return;
        var style = UiStyle(GUI.skin.label);
        float fs = Mathf.Max(8f, scorePopSize); 
        style.fontSize = (int)fs;
        style.alignment = TextAnchor.MiddleCenter;
        if (scorePopFont != null) style.font = scorePopFont; 
        var numStyle = new GUIStyle(style); numStyle.alignment = TextAnchor.MiddleLeft; 
        float popCoinH = fs * 0.9f; 
        float popCoinW = goldCoinSprite != null ? popCoinH * (goldCoinSprite.rect.width / Mathf.Max(1f, goldCoinSprite.rect.height)) : 0f; 

        foreach (var p in scorePops)
        {
            Vector3 sp = cam.WorldToScreenPoint(p.worldPos);
            float k = Mathf.Clamp01(p.age / p.dur); 
            if (sp.x < -80f || sp.x > Screen.width + 80f) continue; 
            float x = sp.x + p.screenOff.x; 
            float y = Screen.height - sp.y + scorePopRisePx * k + p.screenOff.y; 

            Vector2 numSz = numStyle.CalcSize(new GUIContent(p.text)); 
            float gapPx = fs * 0.25f;                                  
            bool chain = p.isChain;                                    
            float w = chain ? numSz.x : popCoinW + gapPx + numSz.x, h = fs * 1.6f; 
            var oc = GUI.color;
            GUI.color = new Color(0, 0, 0, Mathf.Clamp01((1f - k) * 0.7f)); 
            if (!chain && goldCoinSprite != null) GUI.DrawTexture(new Rect(x - w / 2f + 2f, y - popCoinH / 2f + 2f, popCoinW, popCoinH), goldCoinSprite.texture); 
            GUI.Label(new Rect(x - w / 2f + (chain ? 0f : popCoinW + gapPx) + 2f, y - h / 2f + 2f, numSz.x + 1f, h), p.text, numStyle);
            if (!chain && goldCoinSprite != null) { GUI.color = Color.white; GUI.DrawTexture(new Rect(x - w / 2f, y - popCoinH / 2f, popCoinW, popCoinH), goldCoinSprite.texture); } 
            Color c = chain ? new Color(1f, 0.84f, 0.35f) : p.color;   
            c.a *= (1f - k); 
            GUI.color = c;
            GUI.Label(new Rect(x - w / 2f + (chain ? 0f : popCoinW + gapPx), y - h / 2f, numSz.x + 1f, h), p.text, numStyle);
            GUI.color = oc;
        }
    }


    

void DrawWinOverlay()
    {
        var p = MakeUiPanel(480f, 600f, 0.70f, 0.62f); 
        Rect box = p.rect; float sx = p.sx, sy = p.sy, sF = p.sF; uiScaleCur = sF;

        
        float wDur = Mathf.Max(0.1f, overlayFlyDur);
        bool risingW = winRiseT >= 0f && winRiseT < wDur;
        float wk = winRiseT < 0f ? 1f : EaseOutBack(Mathf.Clamp01(winRiseT / wDur)); 
        Vector2 woff = new Vector2(0f, Screen.height * (1f - wk));
        var wm = GUI.matrix; bool wen = GUI.enabled;
        if (risingW) GUI.enabled = false; 
        GUI.matrix = Matrix4x4.TRS(woff, Quaternion.identity, Vector3.one);

        if (panelBgTex != null) GUI.DrawTexture(new Rect(box.x, box.y, box.width, box.height), panelBgTex); 

        var big = UiStyle(GUI.skin.label);
        big.fontSize = Mathf.Max(18, (int)(46 * sF));
        big.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, box.y + 25 * sy, Screen.width, 70 * sy), uiTextYouWin, big);

        var mid = UiStyle(GUI.skin.label);
        mid.fontSize = Mathf.Max(14, (int)(26 * sF));
        mid.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, box.y + 100 * sy, Screen.width, 40 * sy), uiTextScorePrefix + score, mid);
        GUI.Label(new Rect(0, box.y + 140 * sy, Screen.width, 40 * sy), uiTextMovesMadePrefix + movesMade, mid);
        GUI.Label(new Rect(0, box.y + 180 * sy, Screen.width, 40 * sy), uiTextTimePrefix + FormatTime(levelTime), mid);

        if (showWinStars) 

        {
            int halfStepsW = HalfStarSteps();
            float ssz = starSize * sF; 
            float sgap = starGap * sF;
            float sw3 = 3f * ssz + 2f * sgap;
            for (int i = 0; i < 3; i++)
            {
                var spr = StarSpriteFor(i, halfStepsW);
                if (spr != null) GUI.DrawTexture(new Rect(box.center.x - sw3 / 2f + i * (ssz + sgap), box.y + 236 * sy, ssz, ssz), spr.texture); 
            }
        }
        DrawCoinLine(new Vector2(box.center.x, box.y + 300 * sy), 24f * sF, uiTextStarBonusPrefix, lastStarBonus); 
        DrawCoinLine(new Vector2(box.center.x, box.y + 340 * sy), 24f * sF, uiTextTotalGoldPrefix, totalGold, true); 

        float bx = box.x + box.width * 0.1f;
        float bwd = box.width * 0.8f;

        
        if (currentLevel < totalLevels)
        {
            if (UiButton(new Rect(bx, box.y + 376 * sy, bwd, 74 * sy), uiTextNextLevel))
                BeginLoadThenLevel(currentLevel + 1); 
        }
        else
        {
            var done = UiStyle(GUI.skin.label);
            done.fontSize = Mathf.Max(12, (int)(20 * sF));
            done.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, box.y + 400 * sy, Screen.width, 40 * sy), uiTextAllLevelsComplete, done);
        }

        if (UiButton(new Rect(bx, box.y + 468 * sy, bwd, 54 * sy), uiTextMainMenu))
            BeginLoadThenMenu(); 

        if (showResetButton && UiButton(new Rect(bx, box.y + box.height - 64 * sy, bwd, 54 * sy), uiTextResetStage))
            ResetBoard();
        DrawOverlayChrome(box, winSplashTex); 
        GUI.matrix = wm; GUI.enabled = wen;
    }

void DrawLoseOverlay()
    {
        var p = MakeUiPanel(480f, 600f, 0.70f, 0.62f); 
        Rect box = p.rect; float sy = p.sy, sF = p.sF; uiScaleCur = sF;

        
        float wDur = Mathf.Max(0.1f, overlayFlyDur);
        bool risingL = loseRiseT >= 0f && loseRiseT < wDur;
        float lk = loseRiseT < 0f ? 1f : EaseOutBack(Mathf.Clamp01(loseRiseT / wDur)); 
        Vector2 loff = new Vector2(0f, Screen.height * (1f - lk));
        var lm = GUI.matrix; bool len = GUI.enabled;
        if (risingL) GUI.enabled = false; 
        GUI.matrix = Matrix4x4.TRS(loff, Quaternion.identity, Vector3.one);

        if (panelBgTex != null) GUI.DrawTexture(new Rect(box.x, box.y, box.width, box.height), panelBgTex); 

        var big = UiStyle(GUI.skin.label);
        big.fontSize = Mathf.Max(18, (int)(46 * sF));
        big.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, box.y + 25 * sy, Screen.width, 70 * sy), uiTextYouLose, big);

        var mid = UiStyle(GUI.skin.label);
        mid.fontSize = Mathf.Max(14, (int)(26 * sF));
        mid.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, box.y + 100 * sy, Screen.width, 40 * sy), uiTextScorePrefix + score, mid);
        GUI.Label(new Rect(0, box.y + 140 * sy, Screen.width, 40 * sy), uiTextMovesMadePrefix + movesMade, mid);
        GUI.Label(new Rect(0, box.y + 180 * sy, Screen.width, 40 * sy), uiTextTimePrefix + FormatTime(levelTime), mid); 

        float bx = box.x + box.width * 0.1f;
        float bwd = box.width * 0.8f;

        if (UiButton(new Rect(bx, box.y + 283 * sy, bwd, 74 * sy), uiTextRetry))
            ResetBoard(); 

        if (UiButton(new Rect(bx, box.y + 420 * sy, bwd, 54 * sy), uiTextLevelSelect))
        {
            levelResultsCached = false; 
            levelSelectFromGame = true;  
            TransitionTo(GameState.LevelSelect); 
        }

        if (UiButton(new Rect(bx, box.y + box.height - 64 * sy, bwd, 54 * sy), uiTextMainMenu))
            BeginLoadThenMenu(); 

        DrawOverlayChrome(box, null); 
        GUI.matrix = lm; GUI.enabled = len;
    }


    

    Vector2 optionsScroll;
    bool optionsFromPause = false;   
    bool storeFromPause = false;     
    bool testCheatOpen = false;   
float tcSlideT = 0f;          
    bool dailiesOpen = false;   
    float dailiesSlideT = 0f;    
    bool musicDrawerOpen = false;   
    float musicSlideT = 0f;         

    struct DailyToast { public string title; public float reward; public float age; }
    List<DailyToast> dailyToasts = new List<DailyToast>();   

    struct CoinFlight { public Vector2 from; public float t; public float reward; }
    List<CoinFlight> coinFlights = new List<CoinFlight>();
    Rect hudBankRect = new Rect(24f, 18f, 0f, 0f);           
bool storeFromHud = false;      
    float cellSizeAtLoad = 2f;       

void DrawOptions()
    {
        
        var p = MakeUiPanel(560f, 720f, 0.80f, 0.85f);
        Rect panel = p.rect; float sx = p.sx, sy = p.sy, sF = p.sF; uiScaleCur = sF;

        
        if (optionsFromPause)
        {
            EnsureWhitePx();
            var ocV = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f); 
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whitePx);
            GUI.color = ocV;
        }

        GUI.Box(panel, "");

        var title = UiStyle(GUI.skin.label);
        title.fontSize = Mathf.Max(16, (int)(34 * sF));
        title.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, panel.y + 16 * sy, Screen.width, 50 * sy), uiTextOptions, title);

        bool mediaRow = showSpritesButton || showSoundsButton; 
        GUILayout.BeginArea(new Rect(panel.x + 10 * sx, panel.y + 70 * sy, panel.width - 20 * sx, panel.height - (mediaRow ? 184f : 130f) * sy));
        optionsScroll = GUILayout.BeginScrollView(optionsScroll);
        if (OptionSlider(uiTextOptSfxVolume, ref sfxVolume, VolumeMin, VolumeMax)) SaveOptions();
        if (OptionSlider(uiTextOptTileSize, ref cellSize, 0.5f, 4f)) SaveOptions();
        if (OptionSlider(uiTextOptGravity, ref gravity, 5f, 200f)) SaveOptions();
        if (OptionSlider(uiTextOptSwapTime, ref swapTime, 0.02f, 1f)) SaveOptions();
        if (OptionSlider(uiTextOptClearTime, ref clearTime, 0.02f, 1f)) SaveOptions();
        if (OptionSlider(uiTextOptPopDelay, ref popDelay, 0f, 1f)) SaveOptions();
        if (OptionSlider(uiTextOptShapeDensity, ref shapeDensity, 0.4f, 1f)) SaveOptions();
        if (OptionSlider(uiTextOptStoneChance, ref stoneChance, 0f, 0.5f)) SaveOptions();
        if (OptionSlider(uiTextOptDemoInterval, ref demoInterval, 0.1f, 5f)) SaveOptions();

        
        {
            GUILayout.BeginHorizontal();
            var gridLbl = UiStyle(GUI.skin.label);
            gridLbl.fontSize = Mathf.Max(12, (int)(18 * uiScaleCur));
            bool nv = GUILayout.Toggle(showBoardGrid, uiTextToggleGrid, gridLbl, GUILayout.MinWidth(240f * uiScaleCur));
            if (nv != showBoardGrid)
            {
                showBoardGrid = nv;
                PlayerPrefs.SetInt("M3_opt_gridOn", nv ? 1 : 0);
                if (state == GameState.Playing) DrawBoardGrid(); 
            }
            GUILayout.EndHorizontal();
        }

        
        {
            GUILayout.BeginHorizontal();
            var starsLbl = UiStyle(GUI.skin.label);
            starsLbl.fontSize = Mathf.Max(12, (int)(18 * uiScaleCur));
            bool ns = GUILayout.Toggle(showWinStars, uiTextToggleWinStars, starsLbl, GUILayout.MinWidth(240f * uiScaleCur));
            if (ns != showWinStars)
            {
                showWinStars = ns;
                PlayerPrefs.SetInt("M3_opt_winStarsOn", ns ? 1 : 0);
            }
            GUILayout.EndHorizontal();
        }

        
        {
            GUILayout.BeginHorizontal();
            var hsLbl = UiStyle(GUI.skin.label);
            hsLbl.fontSize = Mathf.Max(12, (int)(18 * uiScaleCur));
            bool nh = GUILayout.Toggle(showHiddenStrip, uiTextToggleHiddenStrip, hsLbl, GUILayout.MinWidth(240f * uiScaleCur));
            if (nh != showHiddenStrip)
            {
                showHiddenStrip = nh;
                PlayerPrefs.SetInt("M3_opt_hiddenStripOn", nh ? 1 : 0);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        
        if (mediaRow)
        {
            float w2 = (panel.width - 30f * sx) / 2f;
            if (showSpritesButton && UiButton(new Rect(panel.x + 10 * sx, panel.y + panel.height - 108 * sy, w2, 42 * sy), uiTextSprites)) TransitionTo(GameState.Sprites);
            if (showSoundsButton && UiButton(new Rect(panel.x + panel.width / 2f + 5f * sx, panel.y + panel.height - 108 * sy, w2, 42 * sy), uiTextSounds)) TransitionTo(GameState.Sounds);
        }

        float half = (panel.width - 30f * sx) / 2f;
        if (UiButton(new Rect(panel.x + 10 * sx, panel.y + panel.height - 56 * sy, half, 42 * sy), uiTextResetDefaults)) ResetOptionDefaults();
        if (UiButton(new Rect(panel.x + panel.width / 2f + 5f * sx, panel.y + panel.height - 56 * sy, half, 42 * sy), uiTextBack)) CloseOptions();

        if (!optionsFromPause) DrawMenuChrome(optionsSplashTex); 
    }

bool OptionSlider(string label, ref float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        var lbl = UiStyle(GUI.skin.label);
        lbl.fontSize = Mathf.Max(12, (int)(18 * uiScaleCur));
        GUILayout.Label(label + ": " + value.ToString("0.##"), lbl, GUILayout.MinWidth(240f * uiScaleCur));
        float nv = GUILayout.HorizontalSlider(value, min, max);
        bool changed = nv != value;
        if (changed) value = nv;
        GUILayout.EndHorizontal();
        return changed;
    }

void CloseOptions()
    {
        PlayerPrefs.Save(); 
        if (state == GameState.Sprites || state == GameState.Sounds)
        {
            TransitionTo(GameState.Options, true); 
        }
        else if (optionsFromPause)
        {
            state = GameState.Playing; 
            pauseRiseT = 0f;           
            SetupCamera();
            if (Mathf.Abs(cellSize - cellSizeAtLoad) > 0.01f) ResetBoard(); 
        }
        else
        {
            TransitionTo(GameState.Menu, true); 
        }
    }


    
    

    float storeFlashT = -99f;   
    float storeShakeT = -99f;   

    void DrawStore()
    {
        bool storeDrawer = storeFromPause || storeFromHud; 
        var p = MakeUiPanel(480f, 620f, 0.75f, 0.85f);
        Rect box; float sx = p.sx, sy = p.sy, sF = p.sF; uiScaleCur = sF;

        
        {
            float cBot = storeDrawer ? 488f : 424f; 
            float bh = sy * (cBot + 16f);
            box = new Rect(p.rect.x, (Screen.height - bh) / 2f, p.rect.width, bh);
            if (storeDrawer)
            {
                
                bool landscape = !showOrientationButton || Screen.width > Screen.height; 
                float kPix = cam != null && cam.orthographicSize > 0f ? Screen.height / (2f * cam.orthographicSize) : 1f;
                float gapW = landscape ? wideLeftStrip * Screen.width : Mathf.Max(0f, (Screen.width - cols * cellSize * kPix) / 2f); 
                box.x = 0f;
                box.width = Mathf.Min(box.width, gapW);
                sx = box.width / 480f;   
                sF = Mathf.Min(sx, sy);  
            }
        }

        
        float stT = Time.unscaledTime - storeShakeT;
        if (stT >= 0f && stT < 0.35f) box = new Rect(box.x + Mathf.Sin(stT * 42f) * 6f * sF * (1f - stT / 0.35f), box.y, box.width, box.height);


                { Color oldC = GUI.color; GUI.color = new Color(storeBoxColor.r, storeBoxColor.g, storeBoxColor.b, storeBoxAlpha); GUI.Box(box, ""); GUI.color = oldC; }

        var title = UiStyle(GUI.skin.label);
        title.fontSize = Mathf.Max(16, (int)(40 * sF));
        title.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(box.x, box.y + 28 * sy, box.width, 56 * sy), uiTextStoreTitle, title); 

        
        float flT = Time.unscaledTime - storeFlashT;
        if (flT >= 0f && flT < 0.45f)
        {
            EnsureWhitePx();
            var ocF = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.35f * (1f - flT / 0.45f)); 
            float bw2 = box.width * 0.86f;
            GUI.DrawTexture(new Rect(box.center.x - bw2 / 2f, box.y + 92 * sy, bw2, 36 * sy), _whitePx);
            GUI.color = ocF;
        }
        DrawCoinLine(new Vector2(box.center.x, box.y + 110 * sy), 24f * sF, "", totalGold);

        
        DrawStoreRow(box, sx, sy, crossBlastIcon, uiTextStoreItemCross, uiTextStoreItemCrossDesc, crossBlasts, crossBlastPrice, 158f, false, storeDrawer);
        DrawStoreRow(box, sx, sy, megaCrossIcon, uiTextStoreItemMega, uiTextStoreItemMegaDesc, megaCrossBlasts, megaCrossPrice, storeDrawer ? 298f : 264f, true, storeDrawer); 

        
        if (UiButton(new Rect(box.x + 15f * sx, box.y + box.height - 52 * sy, box.width - 30f * sx, 48 * sy), uiTextBack)) CloseStore();

        DrawMenuChrome(null); 
    }


    
    void DrawStoreRow(Rect box, float sx, float sy, Sprite icon, string itemName, string desc, int owned, float price, float yTopDesign, bool mega, bool inDrawer) 
    {
        EnsureWhitePx();
        float sF = Mathf.Min(sx, sy); 
        float sz = itemSquareSize * sF; 
        float isz = sz * 0.5f; 

        Rect slotR, nameR, descR;
        if (inDrawer)
        {
            
            float rightCol = 164f * sx;                     
            float zx = box.x + 10f * sx;                    
            float zw = box.width - (zx - box.x) - rightCol; 
            nameR = new Rect(zx, box.y + (yTopDesign + 2f) * sy, zw, 30f * sy);    
            descR = new Rect(zx, box.y + (yTopDesign + 34f) * sy, zw, 40f * sy);   
            float cx = zx + zw / 2f;                         
            slotR = new Rect(cx - sz / 2f, box.y + (yTopDesign + 78f) * sy + Mathf.Max(0f, (48f * sy - sz) / 2f), sz, sz);
        }
        else
        {
            
            slotR = new Rect(box.x + 20f * sx, box.y + yTopDesign * sy + (96f * sy - sz) / 2f, sz, sz); 
            float textX = slotR.xMax + 14f * sx; 
            nameR = new Rect(textX, box.y + yTopDesign * sy + 6f * sy, box.width - (textX - box.x) - 20f * sx, 30f * sy);
            descR = new Rect(textX, box.y + yTopDesign * sy + 36f * sy, box.width - (textX - box.x) - 150f * sx, 36f * sy); 
        }

        Rect iconR = new Rect(slotR.center.x - isz / 2f, slotR.center.y - isz / 2f, isz, isz); 
        if (inDrawer) 

        {
            float aOffX = (mega ? megaCrossArtOffsetX : crossBlastArtOffsetX) * sF;
            float aOffY = (mega ? megaCrossArtOffsetY : crossBlastArtOffsetY) * sF;
            float fSize = isz * Mathf.Max(0.05f, mega ? megaCrossArtScale : crossBlastArtScale);
            iconR = new Rect(slotR.center.x + aOffX - fSize / 2f, slotR.center.y + aOffY - fSize / 2f, fSize, fSize);
        }
        if (icon != null)
        {
            var ocI = GUI.color;
            if (owned <= 0) GUI.color = new Color(1f, 1f, 1f, 0.4f); 
            GUI.DrawTexture(iconR, icon.texture);
            GUI.color = ocI;
        }
        else
        {
            var ocP = GUI.color;
            GUI.color = new Color(0.5f, 0.52f, 0.58f, owned <= 0 ? 0.4f : 1f); 
            GUI.DrawTexture(iconR, _whitePx);
            GUI.color = ocP;
        }

        var nameSt = UiStyle(GUI.skin.label);
        nameSt.fontSize = Mathf.Max(12, (int)(24 * sF));
        nameSt.alignment = TextAnchor.MiddleLeft;
        GUI.Label(nameR, itemName, nameSt);

        var descSt = UiStyle(GUI.skin.label);
        descSt.fontSize = Mathf.Max(8, (int)(11 * sF)); 
        descSt.alignment = TextAnchor.MiddleLeft;
        GUI.color = new Color(0.85f, 0.87f, 0.92f, 0.9f); 
        GUI.Label(descR, desc, descSt);
        GUI.color = Color.white;

        
        var ownSt = UiStyle(GUI.skin.label);
        ownSt.fontSize = Mathf.Max(10, (int)(16 * sF));
        ownSt.alignment = TextAnchor.MiddleRight;
        string ownStr = uiTextOwnedPrefix + owned.ToString() + "/" + maxBlastCharges.ToString();
        float colX = box.x + box.width - 24f * sx; 
        float colW = 130f * sx;
        GUI.color = new Color(1f, 0.93f, 0.75f); 
        GUI.Label(new Rect(colX - colW, box.y + yTopDesign * sy + 8f * sy, colW, 24f * sy), ownStr, ownSt);
        DrawCoinLine(new Vector2(colX - colW / 2f, box.y + (yTopDesign + 50f) * sy), 18f * sF, "", price);

        
        Rect buyR = new Rect(colX - colW, box.y + (yTopDesign + 62f) * sy, colW, 30f * sy);
        if (owned >= maxBlastCharges)
        {
            var maxSt = UiStyle(GUI.skin.label);
            maxSt.fontSize = Mathf.Max(12, (int)(18 * sF));
            maxSt.alignment = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.75f, 0.78f, 0.85f); 
            GUI.Label(buyR, uiTextMaxLabel, maxSt);
        }
        else if (UiButton(buyR, uiTextBuyBtn)) TryBuyCharge(mega);
    }


    
    void TryBuyCharge(bool mega)
    {
        int owned = mega ? megaCrossBlasts : crossBlasts;
        float price = mega ? megaCrossPrice : crossBlastPrice;
        if (owned >= maxBlastCharges || totalGold < price)
        {
            storeShakeT = Time.unscaledTime; 
            PlaySfx(SfxSlot.PurchaseDeny, cam != null ? cam.transform.position : Vector3.zero);
            return;
        }
        if (mega) megaCrossBlasts++; else crossBlasts++;
        totalGold -= price; 
        PlayerPrefs.SetFloat("M3_totalGold", totalGold);
        SaveCharges(); 
        storeFlashT = Time.unscaledTime; 
        PlaySfx(SfxSlot.PurchaseOk, cam != null ? cam.transform.position : Vector3.zero);
    }


    
    void CheatAddGold(float amount)
    {
        totalGold += amount;
        PlayerPrefs.SetFloat("M3_totalGold", totalGold); 
        PlaySfx(SfxSlot.PurchaseOk, cam != null ? cam.transform.position : Vector3.zero); 
    }


    

    
    void DrawTestCheatDrawer()
    {
        if (!showTestCheatButton || tcSlideT <= 0f) return;
        float s = UiGlobalScale();
        float ease = tcSlideT * tcSlideT * (3f - 2f * tcSlideT); 
        bool ready = tcSlideT > 0.9f;                            
        float pw = Mathf.Min(Screen.width * 0.94f, 620f * s);
        float ph = 178f * s;
        Rect box = new Rect((Screen.width - pw) / 2f, -(1f - ease) * (ph + 4f), pw, ph); 
        GUI.Box(box, "");
        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(13, (int)(20 * s));
        st.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(box.x + 8f * s, box.y + 6f * s, pw - 84f * s, 34f * s), uiTextTestCheat, st);
        bool oldEn = GUI.enabled; GUI.enabled = ready;
        if (UiButton(new Rect(box.x + pw - 70f * s, box.y + 6f * s, 64f * s, 34f * s), uiTextBack)) testCheatOpen = false; 
        float by = box.y + 48f * s;
        if (UiButton(new Rect(box.x + 12f * s, by, pw - 24f * s, 50f * s), uiTextUnlockAllLevels)) CheatUnlockAll();
        if (UiButton(new Rect(box.x + 12f * s, by + 60f * s, pw - 24f * s, 50f * s), uiTextCheatGold)) CheatAddGold(10000f); 
        GUI.enabled = oldEn;
    }


    

    
    void DrawDailiesDrawer()
    {
        if (!showDailiesButton || dailiesSlideT <= 0f) return;
        if (dailyProg == null || dailyDone == null || activeDailyIds == null || dailyTargets == null || dailyRewards == null) return; 

        float s = UiGlobalScale();
        float ease = dailiesSlideT * dailiesSlideT * (3f - 2f * dailiesSlideT); 
        bool ready = dailiesSlideT > 0.9f;                            
        float pw = Mathf.Min(680f * s, Screen.width * 92f / 100f);    
        float ph = 324f * s;                                          
        Rect box = new Rect(Screen.width - ease * pw, (Screen.height - ph) / 2f, pw, ph); 
        GUI.Box(box, "");

        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(13, (int)(20 * s));
        st.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(box.x + 8f * s, box.y + 6f * s, pw - 84f * s, 34f * s), uiTextDailiesHeader, st);
        bool oldEn = GUI.enabled; GUI.enabled = ready;
        if (UiButton(new Rect(box.x + pw - 70f * s, box.y + 6f * s, 64f * s, 34f * s), uiTextBack)) dailiesOpen = false; 

        string[] titles = { uiTextDaily0, uiTextDaily1, uiTextDaily2, uiTextDaily3, uiTextDaily4, uiTextDaily5, uiTextDaily6, uiTextDaily7, uiTextDaily8, uiTextDaily9 };
        string[] descs = { uiTextDailyDesc0, uiTextDailyDesc1, uiTextDailyDesc2, uiTextDailyDesc3, uiTextDailyDesc4, uiTextDailyDesc5, uiTextDailyDesc6, uiTextDailyDesc7, uiTextDailyDesc8, uiTextDailyDesc9 };
        if (ready) 

        {
            for (int i = 0; i < 2 && i < activeDailyIds.Length; i++)
            {
                int id = activeDailyIds[i];
                bool okId = id >= 0 && id < titles.Length && id < descs.Length && id < dailyProg.Length && id < dailyDone.Length && id < dailyTargets.Length && id < dailyRewards.Length; 
                if (!okId) continue;

                float cy = box.y + (48f + i * 136f) * s;               
                bool done = dailyDone[id];
                var ocC = GUI.color;
                if (done) GUI.color = new Color(0.55f, 0.55f, 0.55f, 1f); 

                GUI.Box(new Rect(box.x + 8f * s, cy, pw - 16f * s, 128f * s), "");
                var ts = UiStyle(GUI.skin.label);
                ts.fontSize = Mathf.Max(12, (int)(17 * s));
                ts.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(box.x + 14f * s, cy + 6f * s, pw - 100f * s, 24f * s), titles[id], ts);

                var rs = UiStyle(GUI.skin.label);
                rs.fontSize = Mathf.Max(12, (int)(16 * s));
                rs.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(box.x + pw - 80f * s, cy + 6f * s, 70f * s, 24f * s), "+" + Mathf.RoundToInt(dailyRewards[id]) + "g", rs);

                var dsc = UiStyle(GUI.skin.label); 
                dsc.fontSize = Mathf.Max(10, (int)(13 * s));
                dsc.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(box.x + 14f * s, cy + 34f * s, pw - 28f * s, 38f * s), descs[id], dsc);

                if (done)
                {
                    var ds = UiStyle(GUI.skin.label);
                    ds.fontSize = Mathf.Max(13, (int)(18 * s));
                    ds.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(new Rect(box.x + 14f * s, cy + 78f * s, pw - 28f * s, 26f * s), uiTextDailyDone, ds);
                }
                else
                {
                    var ps = UiStyle(GUI.skin.label);
                    ps.fontSize = Mathf.Max(11, (int)(14 * s));
                    ps.alignment = TextAnchor.MiddleLeft;
                    GUI.Label(new Rect(box.x + 14f * s, cy + 80f * s, 90f * s, 20f * s), Mathf.FloorToInt(dailyProg[id]) + " / " + dailyTargets[id], ps);

                    
                    Rect bar = new Rect(box.x + 104f * s, cy + 82f * s, pw - 118f * s, 16f * s);
                    float frac = dailyTargets[id] > 0 ? Mathf.Clamp01(dailyProg[id] / (float)dailyTargets[id]) : 0f;
                    EnsureWhitePx();
                    var ocG = GUI.color;
                    GUI.Box(bar, "");
                    if (frac > 0.004f) { GUI.color = new Color(0.85f, 0.62f, 0.18f, 0.95f); GUI.DrawTexture(new Rect(bar.x + 1.5f * s, bar.y + 1.5f * s, Mathf.Max(2f, (bar.width - 3f * s) * frac), bar.height - 3f * s), _whitePx); }
                    GUI.color = ocG;
                }
                GUI.color = ocC;
            }
        }
        GUI.enabled = oldEn;
    }


    
    void CheatUnlockAll()
    {
        unlockedLevel = totalLevels;
        PlayerPrefs.SetInt("M3_unlocked", unlockedLevel);
        levelResultsCached = false; 
    }

    void CloseStore()
    {
        bool inGameDrawer = storeFromPause || storeFromHud; 
        if (storeFromPause)
        {
            state = GameState.Playing; 
            pauseRiseT = 0f;           
            SetupCamera();
        }
        else if (storeFromHud)
        {
            state = GameState.Playing; 
            SetPaused(false);          
            SetupCamera();
        }
        else
            TransitionTo(GameState.Menu, true); 

        if (inGameDrawer) { ghostActive = true; ghostT = 0f; ghostState = GameState.Store; ghostExitDir = Vector2.left; } 
        
    }


    
    void OpenStoreFromHud()
    {
        SetPaused(true);       
        storeFromPause = false;
        storeFromHud = true;   
        TransitionTo(GameState.Store, true); 
    }


    
    void DrawShopBtn(Vector2 anchor, bool landscape)
    {
        float s = UiGlobalScale();
        float sz = itemSquareSize * s;      
        float gap = 8f * s;                 
        float offX = landscape ? itemSqWideX : itemSqPosX;   
        float offY = landscape ? itemSqWideY : itemSqPosY;
        float pairW = 2f * sz + gap;
        float bh = Mathf.Max(16f, shopBtnSize * s);
        Rect r = new Rect(anchor.x + offX * s - pairW / 2f + shopBtnPosX * s, anchor.y + offY * s + sz + 4f * s + shopBtnPosY * s, pairW, bh); 
                if (showShopBtn && !paused && !pauseOverlayOpen && !helpOpen) 
            if (UiButton(r, uiTextStore)) { PlaySfx(SfxSlot.UiClick, cam != null ? cam.transform.position : Vector3.zero); OpenStoreFromHud(); }
    }

Rect MenuPanel(string title)
    {
        
        var p = MakeUiPanel(560f, 720f, 0.80f, 0.85f);
        uiScaleCur = p.sF;
        GUI.Box(p.rect, "");
        var titleStyle = UiStyle(GUI.skin.label);
        titleStyle.fontSize = Mathf.Max(16, (int)(34 * p.sF));
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, p.rect.y + 16 * p.sy, Screen.width, 50 * p.sy), title, titleStyle);
        return p.rect;
    }

void MenuButtons(Rect panel, bool withPick)
    {
        float sx = panel.width / 560f, sy = panel.height / 720f;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (withPick && UiButton(new Rect(panel.x + 10 * sx, panel.y + panel.height - 104 * sy, panel.width - 20f * sx, 42 * sy), uiTextPickFromPhone))
            state = withPick ? (state == GameState.Sounds ? GameState.SoundPick : GameState.SpritePick) : GameState.SpritePick;
#endif
        float half = (panel.width - 30f * sx) / 2f;
        if (UiButton(new Rect(panel.x + 10 * sx, panel.y + panel.height - 56 * sy, half, 42 * sy), uiTextResetDefaults)) ResetOptionDefaults();
        if (UiButton(new Rect(panel.x + panel.width / 2f + 5f * sx, panel.y + panel.height - 56 * sy, half, 42 * sy), uiTextBack)) CloseOptions();
    }


    
void DrawSprites()
    {
        Rect panel = MenuPanel(uiTextSprites);
        float sx = panel.width / 560f, sy = panel.height / 720f;
        GUILayout.BeginArea(new Rect(panel.x + 10 * sx, panel.y + 70 * sy, panel.width - 20 * sx, panel.height - 130 * sy));
        optionsScroll = GUILayout.BeginScrollView(optionsScroll);
        if (OptionSlider(uiTextTileRed, ref tileSpriteScales[0], 0.1f, 3f)) SaveOptions();
        if (OptionSlider(uiTextTileGreen, ref tileSpriteScales[1], 0.1f, 3f)) SaveOptions();
        if (OptionSlider(uiTextTileBlue, ref tileSpriteScales[2], 0.1f, 3f)) SaveOptions();
        if (OptionSlider(uiTextTileYellow, ref tileSpriteScales[3], 0.1f, 3f)) SaveOptions();
        if (OptionSlider(uiTextTilePurple, ref tileSpriteScales[4], 0.1f, 3f)) SaveOptions();
        if (OptionSlider(uiTextStoneSprite, ref stoneSize, 0.3f, 2f)) SaveOptions();
        if (OptionSlider(uiTextHoleBlockSprite, ref holeBlockScale, 0.1f, 3f)) SaveOptions();
        for (int i = 0; i < levelBackgroundScales.Length; i++)
            if (OptionSlider(string.Format(uiTextLevelBgPattern, i + 1, i), ref levelBackgroundScales[i], 0.5f, 2f)) SaveOptions();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        MenuButtons(panel, true);

        DrawMenuChrome(spritesSplashTex); 
    }


    
void DrawSounds()
    {
        Rect panel = MenuPanel(uiTextSounds);
        float sx = panel.width / 560f, sy = panel.height / 720f;
        GUILayout.BeginArea(new Rect(panel.x + 10 * sx, panel.y + 70 * sy, panel.width - 20 * sx, panel.height - 130 * sy));
        optionsScroll = GUILayout.BeginScrollView(optionsScroll);
        if (OptionSlider(uiTextMusic, ref bgmVolume, VolumeMin, VolumeMax)) SaveOptions(); 
        if (OptionSlider(uiTextMasterVolume, ref sfxVolume, VolumeMin, VolumeMax)) SaveOptions();
        if (OptionSlider(uiTextSndSwap, ref sfxSwapVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndPop, ref sfxPopVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndStoneBreak, ref sfxStoneBreakVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndBlockedShake, ref sfxBlockedVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndStoneWiggle, ref sfxStoneWiggleVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndHintShake, ref sfxHintVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndWin, ref sfxWinVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndLevelStart, ref sfxLevelStartVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndUiClick, ref sfxUiClickVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndFallWhoosh, ref sfxWhooshVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndStoneCrack, ref sfxStoneCrackVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndBeamBlast, ref sfxBeamVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndGreenHeal, ref sfxGreenHealVol, 0f, 3f)) SaveOptions();
        if (OptionSlider(uiTextSndPurpleArm, ref sfxPurpleArmVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndPurpleTransmute, ref sfxPurpleTransmuteVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndZapBeam, ref sfxZapVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndHealLand, ref sfxHealLandVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndCoinPop, ref sfxCoinPopVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndStarFill, ref sfxStarFillVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndCrossBlast, ref sfxCrossBlastVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndMegaCrossBlast, ref sfxMegaCrossBlastVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndPurchaseOk, ref sfxPurchaseOkVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndPurchaseDeny, ref sfxPurchaseDenyVol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndChain2, ref sfxChain2Vol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndChain3, ref sfxChain3Vol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndChain4, ref sfxChain4Vol, 0f, 3f)) SaveOptions(); 
        if (OptionSlider(uiTextSndBlackHoleSuck, ref sfxBlackHoleSuckVol, 0f, 3f)) SaveOptions(); 
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        MenuButtons(panel, true);

        DrawMenuChrome(soundsSplashTex); 
    }


    
    string FormatTime(float t)
    {
        int totalCc = Mathf.RoundToInt(t * 100f); 
        return (totalCc / 6000) + ":" + ((totalCc % 6000) / 100).ToString("D2") + "." + (totalCc % 100).ToString("D2");
    }


    
    

    void DrawHugBg(Rect area, Texture tex)
    {
        if (tex == null || tex.width <= 0 || tex.height <= 0) return;
        float sc = area.height / (float)tex.height; 
        if (tex.width * sc < area.width) sc = area.width / (float)tex.width; 
        GUI.DrawTexture(new Rect(area.x + (area.width - tex.width * sc) / 2f, area.y + (area.height - tex.height * sc) / 2f, tex.width * sc, tex.height * sc), tex);
    }

        Texture2D MenuBackdrop() { return mainMenuBgTex != null ? mainMenuBgTex : (uiBackgroundSprite != null ? uiBackgroundSprite.texture : menuBgTex); }


    
    Texture2D CreateMenuBackground()
    {
        int h = 256;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
        Color top = new Color(0.14f, 0.16f, 0.21f, 1f);
        Color bottom = new Color(0.25f, 0.28f, 0.35f, 1f);
        var px = new Color[h];
        for (int y = 0; y < h; y++)
            px[y] = Color.Lerp(bottom, top, y / (h - 1f)); 
        tex.SetPixels(px);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }




    

    
    void DrawOverlayChrome(Rect box, Texture2D splash)   

    {
        if (splash != null) DrawHugBg(box, splash); 
        DrawPointerGlow(splash);
    }

    void DrawMenuChrome(Texture2D splash)   

    {
        if (splash != null) DrawHugBg(new Rect(0, 0, Screen.width, Screen.height), splash); 
        DrawPointerGlow(splash);
    }

    Texture2D pointerGlowTex; Vector2 lastPointerPos; bool havePointer = false;

    void TrackPointer()   

    {
        var mouse = Mouse.current;
        if (mouse != null) { lastPointerPos = mouse.position.ReadValue(); havePointer = true; return; }
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed) { lastPointerPos = ts.primaryTouch.position.ReadValue(); havePointer = true; }
    }

    void DrawPointerGlow(Texture2D splash)   

    {
        if (splash == null || !pointerGlowOn || !havePointer || pointerGlowTex == null) return;
        float s = pointerGlowSize * UiGlobalScale();
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.DrawTexture(new Rect(lastPointerPos.x - s / 2f, lastPointerPos.y - s / 2f, s, s), pointerGlowTex);
        GUI.color = Color.white;
    }


    
    void CreateCinematicTextures()
    {
        int n = 256; float c = (n - 1f) / 2f; 
        var sp = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            Vector2 p = new Vector2(x - c, y - c); float r = p.magnitude / (n * 0.5f);
            
            float theta = Mathf.Atan2(p.y, p.x);
            float mid = 0.70f;                              
            float halfT = 0.13f + 0.15f * Mathf.Cos(theta); 
            sp[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Min(r - (mid - halfT), (mid + halfT) - r) / 0.025f));
        }
        spinnerTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        spinnerTex.SetPixels(sp); spinnerTex.Apply();

        var gl = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float r = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / (n * 0.5f);
            gl[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f)); 
        }
        pointerGlowTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        pointerGlowTex.SetPixels(gl); pointerGlowTex.Apply();
    }


    
    VideoPlayer introVp; GameObject introGo; RenderTexture introRT; bool introEnded = false; bool introPlaying = false;
    RawImage introRi; AudioSource introAudioSrc; 

    bool introIsGif = false; List<GifDecoder.Frame> gifFrames = null; int gifFrameIdx = 0; float gifAccum = 0f, gifTotalDur = 0f; bool gifLoopDone = false;

    void SetupIntro()   

    {
        var go = new GameObject("Intro"); 

        var cv = new GameObject("BootCanvas").AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.gameObject.AddComponent<GraphicRaycaster>();
        cv.transform.SetParent(go.transform, false); 

        void FullRect(GameObject g) { var rtf = g.GetComponent<RectTransform>(); if (rtf == null) rtf = g.AddComponent<RectTransform>(); rtf.anchorMin = Vector2.zero; rtf.anchorMax = Vector2.one; rtf.offsetMin = Vector2.zero; rtf.offsetMax = Vector2.zero; }
        var bg = new GameObject("BlackBg"); bg.transform.SetParent(cv.transform, false); FullRect(bg);
        bg.AddComponent<Image>().color = Color.black; 

        var art = new GameObject("IntroArt"); art.transform.SetParent(cv.transform, false); FullRect(art);
        introRi = art.AddComponent<RawImage>();

        if (introVideoClip != null)
        {
            var vp = go.AddComponent<VideoPlayer>();
            
            int vw = (int)introVideoClip.width;   
            if (vw <= 0) vw = Screen.width;
            int w = Mathf.Clamp(vw, 320, 1920);
            float aspect = Screen.width / (float)Screen.height;
            int h = Mathf.Clamp(Mathf.RoundToInt(w / aspect), 180, 1920);
            introRT = new RenderTexture(w, h, 24);
            vp.renderMode = VideoRenderMode.RenderTexture;   
            vp.targetTexture = introRT;
            #if UNITY_WEBGL && !UNITY_EDITOR
            
            if (!string.IsNullOrEmpty(webIntroFileName)) { vp.source = VideoSource.Url; string sa = Application.streamingAssetsPath; vp.url = (sa.EndsWith("/") ? sa : sa + "/") + webIntroFileName.Trim(); }
            else Debug.LogError("Match3: WebGL intro video needs 'webIntroFileName' set to the file inside Assets/StreamingAssets - embedded clips don't work in a browser build");
#else
            vp.source = VideoSource.VideoClip;
            vp.clip = introVideoClip;
#endif
            if (introAudioClip != null) { vp.audioOutputMode = VideoAudioOutputMode.None; }       
            else { vp.audioOutputMode = VideoAudioOutputMode.Direct; var asrc = go.AddComponent<AudioSource>(); vp.SetTargetAudioSource(0, asrc); } 
            introRi.texture = introRT; 
            vp.prepareCompleted += v => { if (!introEnded && state == GameState.Intro) { introPlaying = true; vp.Play(); } };
            
            vp.errorReceived += (sender, e) => { Debug.LogError("Match3: intro video failed to load or play (" + e + ") - ending the intro now"); if (!introEnded && state == GameState.Intro) EndIntro(); }; 
            introVp = vp;
        }
        else
        {
            byte[] bytes = introGifFile != null ? introGifFile.bytes : null;   
            List<GifDecoder.Frame> frames;
            if (bytes == null || !GifDecoder.TryDecode(bytes, out frames) || frames.Count == 0)
            {
                Debug.LogWarning("Match3: intro GIF missing or failed to decode - skipping the intro");
                Destroy(go); 
                if (musicEnabled) StartBgm(); BeginMenuSpawn();   
                return;
            }
            introIsGif = true;
            gifFrames = frames;
            foreach (var f in frames) gifTotalDur += f.delaySec;
            introRi.texture = frames[0].tex;    
        }

        if (introAudioClip != null)   

        {
            var ag = new GameObject("IntroAudio"); ag.transform.SetParent(go.transform, false);
            introAudioSrc = ag.AddComponent<AudioSource>();
            introAudioSrc.clip = introAudioClip;
            introAudioSrc.loop = true;          
            introAudioSrc.volume = Mathf.Clamp01(sfxVolume); 
            introAudioSrc.Play();
        }

        introGo = go;
        state = GameState.Intro;
        introWatchdog = 0f;
    }

    void EndIntro()
    {
        if (introEnded) return;
        introEnded = true;
        if (introVp != null) { introVp.Stop(); introVp.enabled = false; }
        if (introAudioSrc != null) introAudioSrc.Stop(); 
        Destroy(introGo); 
        if (introRT != null) { introRT.Release(); Destroy(introRT); }
        if (gifFrames != null) foreach (var f in gifFrames) if (f.tex != null) Destroy(f.tex); 
        gifFrames = null;
        if (musicEnabled) StartBgm();   
        BeginMenuSpawn(); 
    }

    void ProceedFromStartGate() 

    {
        if (introVideoClip != null || introGifFile != null) SetupIntro(); 
        else { if (musicEnabled) StartBgm(); BeginMenuSpawn(); }          
    }

    void DrawStartGate()   

    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); 
        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(8, (int)startGateTextSize);
        st.alignment = TextAnchor.MiddleCenter;
        if (startGateFont != null) st.font = startGateFont;
        GUI.color = startGateTextColor;
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), uiTextStartGame, st); 
        GUI.color = Color.white;
    }

    void DrawIntro()   

    {
        if (!showIntroSkipHint || introWatchdog < 0.6f) return;   
        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(14, (int)(Screen.width * 0.025f));
        st.alignment = TextAnchor.MiddleCenter;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((introWatchdog - 0.6f) / 0.8f) * 0.7f);
        GUI.Label(new Rect(0, Screen.height - 90f, Screen.width, 40f), uiTextTapToSkip, st);
        GUI.color = Color.white;
    }


    
    Texture2D spinnerTex;

    void DrawLoading()
    {
        Rect full = new Rect(0, 0, Screen.width, Screen.height);
        if (loadingBgTex != null) DrawHugBg(full, loadingBgTex); 
        else { Texture2D mb = MenuBackdrop(); if (mb != null) DrawHugBg(full, mb); } 

        float cx = Screen.width / 2f;
        float size = Mathf.Min(Screen.width, Screen.height) * 0.38f;   

        var st = UiStyle(GUI.skin.label);
        st.fontSize = Mathf.Max(16, (int)(Screen.width * 0.03f));
        st.alignment = TextAnchor.MiddleCenter;
        float textH = Mathf.Max(40f, st.CalcSize(new GUIContent(uiTextLoading)).y);
        float gap = Screen.height * 0.03f;                              

        
        float padX = size * 0.32f, padY = Mathf.Max(36f, size * 0.3f);
        float panelW = Mathf.Min(Screen.width - 16f, Mathf.Max(size + 2f * padX, st.CalcSize(new GUIContent(uiTextLoading)).x + 48f));
        float panelH = size + gap + textH + 2f * padY;
        Rect box = new Rect((Screen.width - panelW) / 2f, (Screen.height - panelH) / 2f, panelW, panelH);

        if (panelBgTex != null) GUI.DrawTexture(box, panelBgTex);      

        float cy = box.y + padY + size / 2f;                           
        Rect logoRect = new Rect(cx - size / 2f, cy - size / 2f, size, size);

        var gm = GUI.matrix;                                           
        GUIUtility.RotateAroundPivot(loadingAngle % 360f, new Vector2(cx, cy)); 
        if (loadingLogoTex != null) DrawHugBg(logoRect, loadingLogoTex); 
        else GUI.DrawTexture(logoRect, spinnerTex);                    
        GUI.matrix = gm;

        GUI.Label(new Rect(box.x, cy + size / 2f + gap, panelW, textH), uiTextLoading, st); 
    }

}

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class Match3Game : MonoBehaviour
{

    
    Sprite[] pickedTiles = new Sprite[5];
    Sprite pickedStone;
    Sprite pickedHoleBlock;
    Texture2D[] pickedBgTex = new Texture2D[10]; 
        AudioClip[] pickedSfx = new AudioClip[27];     


    static readonly string[] spriteSlotKeys = { "tile_0", "tile_1", "tile_2", "tile_3", "tile_4", "stone", "holeblock", "bg_1", "bg_2", "bg_3", "bg_4", "bg_5", "bg_6", "bg_7", "bg_8", "bg_9", "bg_10" };
        static readonly string[] soundSlotKeys = { "swap", "pop", "stonebreak", "blocked", "hint", "win", "levelstart", "uiclick", "whoosh", "stonewiggle", "stonecrack", "beam", "greenheal", "purplearm", "purptransmute", "zap", "healland", "coinpop", "starfill", "crossblast", "megacross", "purchaseok", "purchasedeny", "chain2", "chain3", "chain4" };

    int pickSlot;                                  
    List<AndroidMediaPicker.MediaItem> browseItems = new List<AndroidMediaPicker.MediaItem>();
    string pickError = "";

    string SpriteSlotLabel(int i)
    {
        switch (i)
        {
            case 0: return uiTextTileRed;
            case 1: return uiTextTileGreen;
            case 2: return uiTextTileBlue;
            case 3: return uiTextTileYellow;
            case 4: return uiTextTilePurple;
            case 5: return uiTextStoneSprite;
            case 6: return uiTextHoleBlockSprite;
            default: return string.Format(uiTextLevelBgPattern, i - 6, i - 7);
        }
    }

string SoundSlotLabel(int i)
    {
        switch (i)
        {
            case 0: return uiTextSndSwap;
            case 1: return uiTextSndPop;
            case 2: return uiTextSndStoneBreak;
            case 3: return uiTextSndBlockedShake;
            case 4: return uiTextSndHintShake;
            case 5: return uiTextSndWin;
            case 6: return uiTextSndLevelStart;
            case 7: return uiTextSndUiClick;
            case 8: return uiTextSndFallWhoosh;
            case 9: return uiTextSndStoneWiggle;
            case 10: return uiTextSndStoneCrack;
            case 11: return uiTextSndBeamBlast;
            case 12: return uiTextSndGreenHeal;
            case 13: return uiTextSndPurpleArm;
            case 14: return uiTextSndPurpleTransmute;
            case 15: return uiTextSndZapBeam;
            case 16: return uiTextSndHealLand;
            case 17: return uiTextSndCoinPop;
            case 18: return uiTextSndStarFill;
            case 19: return uiTextSndCrossBlast;
            case 20: return uiTextSndMegaCrossBlast;
            case 21: return uiTextSndPurchaseOk;
            case 22: return uiTextSndPurchaseDeny;
            case 23: return uiTextSndChain2; 
            case 24: return uiTextSndChain3; 
            case 25: return uiTextSndChain4; 
            case 26: return uiTextSndBlackHoleSuck; 
            default: return "?";
        }
    }


    string PickDir()
    {
        var dir = System.IO.Path.Combine(Application.persistentDataPath, "Picked");
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    void SavePickedFile(string key, byte[] bytes)
    {
        string file = "M3_" + key + "_" + (browseItems.Count > pickSlot && pickSlot >= 0 ? SafeName(browseItems[pickSlot].name) : "file");
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(PickDir(), file), bytes);
        PlayerPrefs.SetString("M3_pickfile_" + key, file);
    }

    string SafeName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "file";
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s) if (!char.IsWhiteSpace(ch) && !char.IsPunctuation(ch) && char.IsLetterOrDigit(ch)) sb.Append(ch);
        string r = sb.ToString();
        return r.Length == 0 ? "file" : r;
    }

    void ClearPicked(string key)
    {
        PlayerPrefs.DeleteKey("M3_pickfile_" + key);
        PlayerPrefs.Save();
    }

   
    void LoadPickedAssets()
    {
        for (int i = 0; i < spriteSlotKeys.Length; i++)
        {
            string file = PlayerPrefs.GetString("M3_pickfile_" + spriteSlotKeys[i], "");
            if (file == "") continue;
            var bytes = System.IO.File.Exists(System.IO.Path.Combine(PickDir(), file)) ? System.IO.File.ReadAllBytes(System.IO.Path.Combine(PickDir(), file)) : null;
            if (bytes == null) continue;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) { Destroy(tex); continue; }
            var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            ApplyPickedSprite(i, spr, tex);
        }
        for (int i = 0; i < soundSlotKeys.Length; i++)
        {
            string file = PlayerPrefs.GetString("M3_pickfile_" + soundSlotKeys[i], "");
            if (file == "") continue;
            var bytes = System.IO.File.Exists(System.IO.Path.Combine(PickDir(), file)) ? System.IO.File.ReadAllBytes(System.IO.Path.Combine(PickDir(), file)) : null;
            if (bytes == null) continue;
            var clip = AudioClip.Create("picked_" + soundSlotKeys[i], 1, 1, 44100, false); 
            if (!LoadClipBytes(clip, bytes)) { Destroy(clip); continue; }
            pickedSfx[i] = clip;
        }
    }

    void ApplyPickedSprite(int slot, Sprite spr, Texture2D tex)
    {
        if (slot < 5) pickedTiles[slot] = spr;
        else if (slot == 5) pickedStone = spr;
        else if (slot == 6) pickedHoleBlock = spr;
        else if (slot >= 7 && slot <= 16) pickedBgTex[slot - 7] = tex;
    }


    void PickSpriteSlot(int i)
    {
        pickSlot = i;
        browseItems = AndroidMediaPicker.Query(true, 300);
        state = GameState.ImageBrowse;
    }

    
void DrawSpritePick()
    {
        Rect panel = MenuPanel("Pick Sprites");
        float sx = panel.width / 560f, sy = panel.height / 720f;
        GUILayout.BeginArea(new Rect(panel.x + 10 * sx, panel.y + 70 * sy, panel.width - 20 * sx, panel.height - 130 * sy));
        optionsScroll = GUILayout.BeginScrollView(optionsScroll);
        float labelW = (panel.width - 20f * sx) - 110f * uiScaleCur; 
        for (int i = 0; i < spriteSlotKeys.Length; i++)
            PickRow(SpriteSlotLabel(i), PlayerPrefs.GetString("M3_pickfile_" + spriteSlotKeys[i], ""), labelW, () => PickSpriteSlot(i));
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        if (UiButton(new Rect(panel.x + 10 * sx, panel.y + panel.height - 56 * sy, panel.width - 20f * sx, 42 * sy), uiTextBack)) state = GameState.Sprites;
    }

void DrawImageBrowse()
    {
        Rect panel = MenuPanel("Pick Image: " + SpriteSlotLabel(pickSlot));
        float sx = panel.width / 560f, sy = panel.height / 720f;
        if (pickError != "") GUI.Label(new Rect(panel.x + 10 * sx, panel.y + 62 * sy, panel.width - 20f * sx, 30 * sy), pickError);
        float listW = panel.width - 20f * sx; 

        GUILayout.BeginArea(new Rect(panel.x + 10 * sx, panel.y + 95 * sy, panel.width - 20 * sx, panel.height - 160 * sy));
        optionsScroll = GUILayout.BeginScrollView(optionsScroll);
        var defStyle = UiStyle(GUI.skin.button);
        defStyle.fontSize = FitFontSize(44f * uiScaleCur * 0.6f, "Use Default (clear)", listW);
        if (GUILayout.Button("Use Default (clear)", defStyle, GUILayout.Height(44f * uiScaleCur)))
        {
            ClearPicked(spriteSlotKeys[pickSlot]);
            if (pickSlot < 5) pickedTiles[pickSlot] = null;
            else if (pickSlot == 5) pickedStone = null;
            else if (pickSlot == 6) pickedHoleBlock = null;
            else if (pickSlot >= 7 && pickSlot <= 16) pickedBgTex[pickSlot - 7] = null;
            state = GameState.SpritePick;
        }
        for (int i = 0; i < browseItems.Count; i++)
        {
            var item = browseItems[i];
            var st = UiStyle(GUI.skin.button);
            st.fontSize = FitFontSize(44f * uiScaleCur * 0.6f, item.name, listW); 
            if (GUILayout.Button(item.name, st, GUILayout.Height(44f * uiScaleCur))) LoadPickedImage(i);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        if (UiButton(new Rect(panel.x + 10 * sx, panel.y + panel.height - 56 * sy, panel.width - 20f * sx, 42 * sy), uiTextBack)) state = GameState.SpritePick;
    }

    void LoadPickedImage(int itemIdx)
    {
        var bytes = AndroidMediaPicker.ReadBytes(true, browseItems[itemIdx].id);
        if (bytes == null || bytes.Length == 0) { pickError = "Could not read that file."; return; }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes))
        {
            Destroy(tex);
            pickError = "Unsupported image format - use PNG or JPEG.";
            return;
        }
        var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        ApplyPickedSprite(pickSlot, spr, tex);
        SavePickedFile(spriteSlotKeys[pickSlot], bytes);
        PlayerPrefs.Save();
        state = GameState.SpritePick;
    }


    void PickSoundSlot(int i)
    {
        pickError = "";
        pickSlot = i;
        browseItems = AndroidMediaPicker.Query(false, 300);
        state = GameState.AudioBrowse;
    }

    
void DrawSoundPick()
    {
        Rect panel = MenuPanel("Pick Sounds");
        float sx = panel.width / 560f, sy = panel.height / 720f;
        GUILayout.BeginArea(new Rect(panel.x + 10 * sx, panel.y + 70 * sy, panel.width - 20 * sx, panel.height - 130 * sy));
        optionsScroll = GUILayout.BeginScrollView(optionsScroll);
        float labelW = (panel.width - 20f * sx) - 110f * uiScaleCur; 
        for (int i = 0; i < soundSlotKeys.Length; i++)
            PickRow(SoundSlotLabel(i), PlayerPrefs.GetString("M3_pickfile_" + soundSlotKeys[i], ""), labelW, () => PickSoundSlot(i));
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        if (UiButton(new Rect(panel.x + 10 * sx, panel.y + panel.height - 56 * sy, panel.width - 20f * sx, 42 * sy), uiTextBack)) state = GameState.Sounds;
    }

void DrawAudioBrowse()
    {
        Rect panel = MenuPanel("Pick Sound: " + SoundSlotLabel(pickSlot));
        float sx = panel.width / 560f, sy = panel.height / 720f;
        if (pickError != "") GUI.Label(new Rect(panel.x + 10 * sx, panel.y + 62 * sy, panel.width - 20f * sx, 30 * sy), pickError);
        float listW = panel.width - 20f * sx; 

        GUILayout.BeginArea(new Rect(panel.x + 10 * sx, panel.y + 95 * sy, panel.width - 20 * sx, panel.height - 160 * sy));
        optionsScroll = GUILayout.BeginScrollView(optionsScroll);
        var defStyle = UiStyle(GUI.skin.button);
        defStyle.fontSize = FitFontSize(44f * uiScaleCur * 0.6f, "Use Default (clear)", listW);
        if (GUILayout.Button("Use Default (clear)", defStyle, GUILayout.Height(44f * uiScaleCur)))
        {
            ClearPicked(soundSlotKeys[pickSlot]);
            pickedSfx[pickSlot] = null;
            state = GameState.SoundPick;
        }
        for (int i = 0; i < browseItems.Count; i++)
        {
            var item = browseItems[i];
            var st = UiStyle(GUI.skin.button);
            st.fontSize = FitFontSize(44f * uiScaleCur * 0.6f, item.name, listW);
            if (GUILayout.Button(item.name, st, GUILayout.Height(44f * uiScaleCur))) LoadPickedSound(i);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        if (UiButton(new Rect(panel.x + 10 * sx, panel.y + panel.height - 56 * sy, panel.width - 20f * sx, 42 * sy), uiTextBack)) state = GameState.SoundPick;
    }

    void LoadPickedSound(int itemIdx)
    {
        var bytes = AndroidMediaPicker.ReadBytes(false, browseItems[itemIdx].id);
        var clip = AudioClip.Create("picked_" + soundSlotKeys[pickSlot], 1, 1, 44100, false); 
        if (!LoadClipBytes(clip, bytes))
        {
            Destroy(clip);
            pickError = "Unsupported audio format - use WAV or OGG.";
            return;
        }
        pickedSfx[pickSlot] = clip;
        SavePickedFile(soundSlotKeys[pickSlot], bytes);
        PlayerPrefs.Save();
        state = GameState.SoundPick;
    }

void PickRow(string label, string currentFile, float maxLabelW, System.Action browse)
    {
        GUILayout.BeginHorizontal();
        var lbl = UiStyle(GUI.skin.label);
        string full = label + (currentFile.Length > 0 ? " - " + currentFile : "");
        lbl.fontSize = Mathf.Max(12, FitFontSize(18f * uiScaleCur, full, maxLabelW));
        GUILayout.Label(full, lbl);
        var bStyle = UiStyle(GUI.skin.button);
        bStyle.fontSize = FitFontSize(36f * uiScaleCur * 0.6f, "Browse", 90f * uiScaleCur);
        if (GUILayout.Button("Browse", bStyle, GUILayout.Width(90f * uiScaleCur), GUILayout.Height(36f * uiScaleCur))) browse();
        GUILayout.EndHorizontal();
    }


    bool LoadClipBytes(AudioClip clip, byte[] bytes)
    {
        var fresh = new byte[bytes.Length];
        System.Array.Copy(bytes, fresh, bytes.Length);
        return clip.LoadAudioData();
    }

}

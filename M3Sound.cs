using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class Match3Game : MonoBehaviour
{
    AudioSource[] sfxPool;
    AudioSource fallWhooshSrc; 
    int sfxCursor;

    AudioSource bgmSrc;

    void SetupBgmSource()
    {
        if (bgmClip == null || bgmSrc != null) return;
        var go = new GameObject("M3_BGM");
        bgmSrc = go.AddComponent<AudioSource>();
        bgmSrc.loop = true;
        bgmSrc.clip = bgmClip;
    }

    void StartBgm()
    {
        if (bgmSrc == null) return;
        bgmSrc.volume = Mathf.Clamp01(bgmVolume);
        if (!bgmSrc.isPlaying) bgmSrc.Play();
    }

    void StopBgm()
    {
        if (bgmSrc == null) return;
        bgmSrc.Stop();
    }
       
    AudioSource ctxBgmSrc;      
    int bgmClaimedScreen = -1;  
    int ctxBgmScreen = -1;      
    float ctxBgmRelVol = 1f;    

    void BgmForScreen(int screen) 
    {
        bgmClaimedScreen = screen;
        if (!musicEnabled) { StopBgm(); StopCtxBgm(); return; } 

        AudioClip clip = null; float rel = 1f;
        if (screen == 0 && menuBgmClip != null) { clip = menuBgmClip; rel = Mathf.Clamp01(menuBgmVolume); }
        else if (screen >= 1 && screen <= levelBgmClips.Length && levelBgmClips[screen - 1] != null) { clip = levelBgmClips[screen - 1]; rel = Mathf.Clamp01(levelBgmVolumes[screen - 1]); }

        if (clip == null)
        {
            StopCtxBgm();
            StartBgm();
            return;
        }
        StopBgm();
        if (ctxBgmSrc == null)
        {
            var go = new GameObject("M3_BGM_Screen");
            ctxBgmSrc = go.AddComponent<AudioSource>();
            ctxBgmSrc.loop = true;
        }
        bool restart = !ctxBgmSrc.isPlaying || ctxBgmScreen != screen || ctxBgmSrc.clip != clip;
        if (restart) { ctxBgmScreen = screen; ctxBgmSrc.clip = clip; ctxBgmSrc.Play(); }
        ctxBgmRelVol = rel;
        ctxBgmSrc.volume = Mathf.Clamp01(rel * bgmVolume);
    }

    void StopCtxBgm() 
    {
        if (ctxBgmSrc != null && ctxBgmSrc.isPlaying) ctxBgmSrc.Stop();
    }

    void ReapplyCurrentBgm() 
    {
        int s = bgmClaimedScreen >= 0 ? bgmClaimedScreen : (state == GameState.Playing ? currentLevel : 0);
        BgmForScreen(s);
    }

    void UpdateCtxBgmVolume() 
    {
        if (ctxBgmSrc != null && ctxBgmSrc.isPlaying) ctxBgmSrc.volume = Mathf.Clamp01(ctxBgmRelVol * bgmVolume);
    }


    void UpdateFallWhoosh(int airborne)
    {
        if (fallWhooshSrc == null) return;
        var clip = GetSfxClip(SfxSlot.Whoosh);
        if (clip == null || airborne <= 0)
        {
            if (fallWhooshSrc.isPlaying) fallWhooshSrc.Stop();
            return;
        }
        float vol = airborne * fallWhooshPerCircle * sfxVolume * SfxMult(SfxSlot.Whoosh); 
        if (!fallWhooshSrc.isPlaying)
        {
            fallWhooshSrc.clip = clip;
            fallWhooshSrc.volume = vol;
            fallWhooshSrc.Play();
        }
        else
        {
            fallWhooshSrc.volume = vol;
        }
    }

  
void BuildSfxPool()
    {
        sfxPool = new AudioSource[8];
        for (int i = 0; i < sfxPool.Length; i++)
        {
            var go = new GameObject("SFX_" + i);
            go.transform.SetParent(managerTransform, false);
            go.transform.position = Vector3.zero;
            sfxPool[i] = go.AddComponent<AudioSource>();
        }
       
        var wg = new GameObject("SFX_Whoosh");
        wg.transform.SetParent(managerTransform, false);
        fallWhooshSrc = wg.AddComponent<AudioSource>();
        fallWhooshSrc.loop = true;
        sfxCursor = 0;
    }

   

void PlaySfx(SfxSlot s, Vector3 worldPos)
    {
        var clip = GetSfxClip(s);
        if (clip == null || sfxPool == null) return;
        var src = sfxPool[sfxCursor];
        sfxCursor = (sfxCursor + 1) % sfxPool.Length;
        if (src == null) return; 
        src.transform.position = worldPos;
        src.volume = sfxVolume * SfxMult(s);
        src.PlayOneShot(clip);
    }

float SfxMult(SfxSlot s)
    {
        switch (s)
        {
            case SfxSlot.Swap: return sfxSwapVol;
            case SfxSlot.Pop: return sfxPopVol;
            case SfxSlot.StoneBreak: return sfxStoneBreakVol;
            case SfxSlot.Blocked: return sfxBlockedVol;
            case SfxSlot.Hint: return sfxHintVol;
            case SfxSlot.Win: return sfxWinVol;
            case SfxSlot.LevelStart: return sfxLevelStartVol;
            case SfxSlot.Whoosh: return sfxWhooshVol;
            case SfxSlot.StoneWiggle: return sfxStoneWiggleVol;
            case SfxSlot.StoneCrack: return sfxStoneCrackVol;
            case SfxSlot.Beam: return sfxBeamVol;
            case SfxSlot.GreenHeal: return sfxGreenHealVol;
            case SfxSlot.PurpleArm: return sfxPurpleArmVol; 
            case SfxSlot.PurpleTransmute: return sfxPurpleTransmuteVol; 
            case SfxSlot.Zap: return sfxZapVol;
            case SfxSlot.HealLand: return sfxHealLandVol;
            case SfxSlot.CoinPop: return sfxCoinPopVol;
            case SfxSlot.StarFill: return sfxStarFillVol; 
            case SfxSlot.CrossBlast: return sfxCrossBlastVol;
            case SfxSlot.MegaCrossBlast: return sfxMegaCrossBlastVol; 
            case SfxSlot.PurchaseOk: return sfxPurchaseOkVol;
            case SfxSlot.PurchaseDeny: return sfxPurchaseDenyVol;
            case SfxSlot.Chain2: return sfxChain2Vol; 
            case SfxSlot.Chain3: return sfxChain3Vol; 
            case SfxSlot.Chain4: return sfxChain4Vol; 
            case SfxSlot.BlackHoleSuck: return sfxBlackHoleSuckVol;
            default: return sfxUiClickVol;
        }
    }


AudioClip GetSfxClip(SfxSlot s)
    {
        if (pickedSfx != null && pickedSfx[(int)s] != null) return pickedSfx[(int)s]; 
        switch (s)
        {
            case SfxSlot.Swap: return swapSwooshClip;
            case SfxSlot.Pop: return popClip;
            case SfxSlot.StoneBreak: return stoneBreakClip;
            case SfxSlot.Blocked: return blockedShakeClip;
            case SfxSlot.Hint: return hintShakeClip;
            case SfxSlot.Win: return winClip;
            case SfxSlot.LevelStart: return levelStartClip;
            case SfxSlot.Whoosh: return fallWhooshClip;
            case SfxSlot.StoneWiggle: return stoneWiggleClip != null ? stoneWiggleClip : blockedShakeClip; 
            case SfxSlot.StoneCrack: return stoneCrackClip;
            case SfxSlot.Beam: return beamBlastClip;
            case SfxSlot.GreenHeal: return greenHealClip;
            case SfxSlot.PurpleArm: return purpleArmClip; 
            case SfxSlot.PurpleTransmute: return purpleTransmuteClip;
            case SfxSlot.Zap: return zapBeamClip; 
            case SfxSlot.HealLand: return healLandClip;
            case SfxSlot.CoinPop: return coinPopClip;
            case SfxSlot.StarFill: return starFillClip; 
            case SfxSlot.CrossBlast: return crossBlastClip; 
            case SfxSlot.MegaCrossBlast: return megaCrossBlastClip;
            case SfxSlot.PurchaseOk: return purchaseOkClip; 
            case SfxSlot.PurchaseDeny: return purchaseDenyClip;
            case SfxSlot.Chain2: return chain2Clip;
            case SfxSlot.Chain3: return chain3Clip; 
            case SfxSlot.Chain4: return chain4Clip; 
            case SfxSlot.BlackHoleSuck: return blackHoleSuckClip;
            default: return uiClickClip;
        }
    }


        enum SfxSlot { Swap, Pop, StoneBreak, Blocked, Hint, Win, LevelStart, UiClick, Whoosh, StoneWiggle, StoneCrack, Beam, GreenHeal, PurpleArm, PurpleTransmute, Zap, HealLand, CoinPop, StarFill, CrossBlast, MegaCrossBlast, PurchaseOk, PurchaseDeny, Chain2, Chain3, Chain4, BlackHoleSuck }
}

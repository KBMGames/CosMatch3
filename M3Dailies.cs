using UnityEngine;
using System.Collections.Generic;
public partial class Match3Game : MonoBehaviour
{

    [Header("Daily challenges")]
    public bool showDailiesButton = true;
    public int[] dailyTargets = { 200, 5, 1, 3, 8, 5, 3, 6, 4, 1500 };
    public float[] dailyRewards = { 500f, 200f, 500f, 300f, 250f, 400f, 350f, 250f, 300f, 600f };
    public float dailyWinTimeLimitSec = 120f;
    public bool randomizeDailiesOnLaunch = true;
    public Sprite dailyToastSplashArt;

    enum DailyEvent { PopBubbles, UseBlackHole, BeatLevelUnderTime, Transmute, ChainWaves, BreakStones, FireItemBlasts, LandHealBlocks, SpawnSpecials, GoldPopped }

    float[] dailyProg;
    bool[] dailyDone;
    int[] activeDailyIds = new int[2];

    public struct DailyNotice { public string title; public float reward; }
    [System.NonSerialized]
    public List<DailyNotice> dailyNotices = new List<DailyNotice>();



    int NowDateInt() { var t = System.DateTime.Now; return t.Year * 10000 + t.Month * 100 + t.Day; }

    void InitDailies()
    {
        int n = Mathf.Max(1, dailyTargets != null ? dailyTargets.Length : 1);
        dailyProg = new float[n];
        dailyDone = new bool[n];

        int today = NowDateInt();
        bool freshRoll = randomizeDailiesOnLaunch || PlayerPrefs.GetInt("M3_daily_date", 0) != today;
        if (freshRoll)
        {
            activeDailyIds[0] = PickDailyId(n, -1);
            activeDailyIds[1] = n > 1 ? PickDailyId(n, activeDailyIds[0]) : activeDailyIds[0];
            PlayerPrefs.SetInt("M3_daily_date", today);
            PlayerPrefs.SetString("M3_daily_ids", activeDailyIds[0] + "," + activeDailyIds[1]);
            for (int i = 0; i < n; i++) PlayerPrefs.SetFloat("M3_dprog_" + i, 0f);
        }
        else
        {
            var parts = PlayerPrefs.GetString("M3_daily_ids", "").Split(',');
            for (int i = 0; i < 2; i++)
            {
                int id = -1;
                if (i < parts.Length) int.TryParse(parts[i], out id);
                if (id < 0 || id >= n || (i == 1 && id == activeDailyIds[0]))
                    id = PickDailyId(n, i == 1 ? activeDailyIds[0] : -1);
                activeDailyIds[i] = id;
            }
            for (int i = 0; i < n; i++) dailyProg[i] = PlayerPrefs.GetFloat("M3_dprog_" + i, 0f);
        }
    }

    public void ResetDailyProgress()
    {
        PlayerPrefs.DeleteKey("M3_daily_date");
        PlayerPrefs.DeleteKey("M3_daily_ids");
        int n = Mathf.Max(1, dailyTargets != null ? dailyTargets.Length : 1);
        for (int i = 0; i < n; i++) PlayerPrefs.DeleteKey("M3_dprog_" + i);
        if (dailyProg != null) { for (int i = 0; i < dailyProg.Length; i++) dailyProg[i] = 0f; }
        if (dailyDone != null) { for (int i = 0; i < dailyDone.Length; i++) dailyDone[i] = false; }
    }

    int PickDailyId(int n, int avoid) { if (n <= 1) return 0; int id; do { id = Random.Range(0, n); } while (id == avoid); return id; }

    void DailyTrack(DailyEvent e, float delta)
    {
        if (autoDemo || dailyProg == null) return;
        int id = (int)e;
        if (id < 0 || id >= dailyTargets.Length) return;
        bool active = false;
        for (int i = 0; i < activeDailyIds.Length; i++) { if (activeDailyIds[i] == id) { active = true; break; } }
        if (!active) return;
        if (dailyDone[id]) return;

        float before = dailyProg[id];
        float after = Mathf.Min(before + delta, (float)dailyTargets[id]);
        dailyProg[id] = after;
        PlayerPrefs.SetFloat("M3_dprog_" + id, after);

        if (before < dailyTargets[id] && after >= dailyTargets[id])
        {
            dailyDone[id] = true;
            PlayerPrefs.SetFloat("M3_dprog_" + id, after);
            float reward = dailyRewards != null && id < dailyRewards.Length ? dailyRewards[id] : 0f;
            CheatAddGold(reward);
            dailyNotices.Add(new DailyNotice { title = DailyTitle(id), reward = reward });
            Debug.Log("[Dailies] completed " + e.ToString() + " (+" + Mathf.RoundToInt(reward) + "g)");
        }
    }

    string DailyTitle(int id)
    {
        switch (id)
        {
            case 0: return uiTextDaily0; case 1: return uiTextDaily1; case 2: return uiTextDaily2; case 3: return uiTextDaily3; case 4: return uiTextDaily4;
            case 5: return uiTextDaily5; case 6: return uiTextDaily6; case 7: return uiTextDaily7; case 8: return uiTextDaily8; case 9: return uiTextDaily9;
            default: return "";
        }
    }

    public void DailyTrackPop() { DailyTrack(DailyEvent.PopBubbles, 1f); }
    public void DailyTrackBlackHoleUse() { DailyTrack(DailyEvent.UseBlackHole, 1f); }
    public void DailyTrackTransmute() { DailyTrack(DailyEvent.Transmute, 1f); }
    public void DailyTrackChainWave() { DailyTrack(DailyEvent.ChainWaves, 1f); }
    public void DailyTrackStoneBreak() { DailyTrack(DailyEvent.BreakStones, 1f); }
    public void DailyTrackItemBlast() { DailyTrack(DailyEvent.FireItemBlasts, 1f); }
    public void DailyTrackHealLand() { DailyTrack(DailyEvent.LandHealBlocks, 1f); }
    public void DailyTrackSpecialSpawn() { DailyTrack(DailyEvent.SpawnSpecials, 1f); }
    public void DailyTrackGoldPopped(float pay) { DailyTrack(DailyEvent.GoldPopped, pay); }
    public void DailyTrackLevelWon(float timeSec) { if (timeSec <= dailyWinTimeLimitSec) DailyTrack(DailyEvent.BeatLevelUnderTime, 1f); }
}

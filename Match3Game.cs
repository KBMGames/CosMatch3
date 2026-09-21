using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;

public partial class Match3Game : MonoBehaviour
{
    public int cols = 8;
    public int rows = 8;
    public float cellSize = 2f;
    public float gravity = 40f;
    public float swapTime = 0.08f;
    public float clearTime = 0.25f;
    [Header("Board size - grid dims per level (L0 base + level number, so each level adds one row AND one col; widescreen transposes)")]
    public float boardRowBase = 8f;
    public float boardColBase = 4f;

    public float shapeDensity = 0.9f;

    public int holeFreeLevels = 2;
    public float stoneChance = 0.5f;

    public bool autoDemo = false;
    public float demoInterval = 0.9f;

    public int targetOpeningMoves = 3;
    public int localRepairCap = 60;


    [System.Serializable]
    public struct LevelMechs
    {
        public bool match4EndSpawn;
        public bool lineBlasts;
        public bool yellowTarget;
        public bool greenHeal;
        public bool purpleTransmuter;
    }

    [Header("Per-level special combos (defaults: L1-3 none / L4-6 end-spawn+blasts / L7-10 all - override per level below)")]
    [SerializeField] LevelMechs[] levelToggles = new LevelMechs[]
    {
        new LevelMechs(),
        new LevelMechs(),
        new LevelMechs(),
        new LevelMechs { match4EndSpawn = true, lineBlasts = true },
        new LevelMechs { match4EndSpawn = true, lineBlasts = true },
        new LevelMechs { match4EndSpawn = true, lineBlasts = true },
        new LevelMechs { match4EndSpawn = true, lineBlasts = true, yellowTarget = true, greenHeal = true, purpleTransmuter = true },
        new LevelMechs { match4EndSpawn = true, lineBlasts = true, yellowTarget = true, greenHeal = true, purpleTransmuter = true },
        new LevelMechs { match4EndSpawn = true, lineBlasts = true, yellowTarget = true, greenHeal = true, purpleTransmuter = true },
        new LevelMechs { match4EndSpawn = true, lineBlasts = true, yellowTarget = true, greenHeal = true, purpleTransmuter = true }
    };
    [Header("Per-level tile colors (bit k = color index k allowed on that board; defaults L1-3 RGB / L4-6 +Y / L7-10 all - edit via the matrix block below)")]
    public int[] levelColorMasks = { 7, 7, 7, 15, 15, 15, 31, 31, 31, 31 };

    public int LevelColorMask() { return levelColorMasks == null || levelColorMasks.Length == 0 ? 31 : levelColorMasks[Mathf.Clamp(currentLevel - 1, 0, levelColorMasks.Length - 1)]; }
    public bool ColorEnabled(int c) { int m = LevelColorMask(); return m == 0 || (m & (1 << c)) != 0; }
    public int EnabledColorCount() { int n = 0; for (int i = 0; i < tileColors.Length; i++) if (ColorEnabled(i)) n++; return n; }
    public int RandomEnabledColor(int exclude = -1)
    {
        var pool = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tileColors.Length; i++) if (i != exclude && ColorEnabled(i)) pool.Add(i);
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : Random.Range(0, tileColors.Length);
    }
    public LevelMechs Mech() => levelToggles != null && levelToggles.Length > 0 ? levelToggles[Mathf.Clamp(currentLevel - 1, 0, levelToggles.Length - 1)] : default(LevelMechs);

    bool MechMatch4() { return Mech().match4EndSpawn; }
    bool MechLineBlasts() { return Mech().lineBlasts; }
    bool MechYellowTarget() { return Mech().yellowTarget; }
    bool MechGreenHeal() { return Mech().greenHeal; }
    bool MechPurpleTransmuter() { return Mech().purpleTransmuter; }

    bool RunBecomesBeam(int colorIdx, int len) { return len >= 5 && (colorIdx == ColorRed || colorIdx == ColorBlue) && MechLineBlasts(); }
    [Header("Sprites (optional - procedural defaults are used for any empty slot)")]
    public Sprite[] tileSprites;
    public Sprite stoneSprite;
    public Sprite holeBlockSprite;
    public Texture2D[] levelBackgrounds;
    public float stoneSize = 1f;
    [Header("Special entities - T5/T7/T8: spawned by 5-match, never match by color, persist until used (NO expiration)")]
    public Sprite yellowCircleSprite;
    public Sprite purpleSymbolIdle;
    public Sprite purpleSymbolActive0;
    public Sprite purpleSymbolActive1;
    public Sprite purpleSymbolActive2;
    public Sprite purpleSymbolActive3;
    public Sprite purpleSymbolActive4;
    [Header("Economy / HUD icons - gold economy, heal block, store items, three-star row (procedural fallbacks created at runtime)")]
    public Sprite goldCoinSprite;
    public Sprite healBlockSprite;
    public Sprite crossBlastIcon;
    public Sprite megaCrossIcon;
    public Sprite starEmptySprite;
    public Sprite starHalfSprite;
    public Sprite starFullSprite;
    [Header("Store - blast charge prices + cap (Inspector-only, NOT part of the Options pipeline)")]
    public float crossBlastPrice = 1000f;
    public float megaCrossPrice = 10000f;
    public const int maxBlastCharges = 10;

    [Header("Board backdrop tint - semi-transparent panel behind the tiles; per-level color keeps the board readable over any background image")]
    public bool boardTintEnabled = true;
    public float boardTintAlpha = 0.8f;
    public Color[] levelBackdropColors = {
        new Color(0f, 0f, 0f, 1f), new Color(0.05f, 0.12f, 0.28f, 1f), new Color(0.24f, 0.10f, 0.30f, 1f),
        new Color(0.02f, 0.20f, 0.16f, 1f), new Color(0.28f, 0.15f, 0.04f, 1f), new Color(0.30f, 0.04f, 0.10f, 1f),
        new Color(0.06f, 0.18f, 0.24f, 1f), new Color(0.16f, 0.10f, 0.24f, 1f), new Color(0.20f, 0.22f, 0.05f, 1f),
        new Color(0.30f, 0.28f, 0.06f, 1f)
    };
    [Header("Board grid (thin lines under the bubbles + thick frame on the 4 sides; toggle in Options)")]
    public bool showBoardGrid = true;
    public float gridLineThickness = 0.05f;
    public float gridBorderThickness = 0.14f;
    public Color gridLineColor = new Color(0.72f, 0.78f, 0.90f, 0.35f);

    [Header("Layout framing - widescreen dials apply ONLY when aspect > 1; portrait keeps its own centered fit")]
    public float wideBoardScale = 1f;
    public float wideLeftStrip = 0.30f;
    public float wideRightMargin = 0.05f;
    public float portraitTopSpace = 64f;

    [Header("HUD layout - timer + pause/reset buttons (design px scaled by UI scale; 0 offsets keep the default position)")]
    public float timerSizeScale = 1f;
    public float timerPosX = 0f;
    public float timerPosY = 0f;
    public float pauseBtnOffsetX = 0f;
    public float pauseBtnOffsetY = 0f;
    public float resetBtnOffsetX = 0f;
    public float resetBtnOffsetY = 0f;
    public float helpBtnSize = 44f;
    public float helpBtnOffsetX = 0f;
    public float helpBtnOffsetY = 0f;

    public float lvlGoldSize = 26f;
    public float lvlGoldPosX = 0f;
    public float lvlGoldPosY = 8f;
    public float lvlGoldWideX = 0f;
    public float lvlGoldWideY = 8f;
    public float totGoldSize = 20f;
    public float totGoldPosX = 0f;
    public float totGoldPosY = 40f;
    public float totGoldWideX = 0f;
    public float totGoldWideY = 36f;
    public float starSize = 34f;
    public float starGap = 6f;
    public float starRowPosX = 0f;
    public float starRowPosY = 134f;
    public float starRowWideX = 0f;
    public float starRowWideY = 116f;
    public float itemSquareSize = 48f;
    public float itemSqPosX = 0f;
    public float itemSqPosY = 72f;
    public float itemSqWideX = 0f;
    public float itemSqWideY = 52f;

    public float shopBtnSize = 28f;
    public float shopBtnPosX = 0f;
    public float shopBtnPosY = 6f;
    public float crossBlastArtOffsetX = 0f;
    public float crossBlastArtOffsetY = 0f;
    public float crossBlastArtScale   = 1f;
    public float megaCrossArtOffsetX  = 0f;
    public float megaCrossArtOffsetY  = 0f;
    public float megaCrossArtScale    = 1f;

    public float stageNameSize = 24f;
    public float stageNamePosX = 0f;
    public float stageNamePosY = 0f;

    public float hiddenLevelBase = 10000f;
    public float hiddenLevelStepMult = 1.1f;
    public string hiddenLevelLabel = "Level:";
    public float hiddenStripSize = 20f;
    public float hiddenStripPosX = 0f;
    public float hiddenStripPosY = 0f;
    public float hiddenStripWideX = 0f;
    public float hiddenStripWideY = 0f;

    public float movesLeftSize = 20f;
    public float movesLeftPosX = 0f;
    public float movesLeftPosY = 0f;
    public float movesLeftWideX = 0f;
    public float movesLeftWideY = 0f;
    [Header("Help screen - paused sub-state raised by the '?' pill (no new GameState)")]
    public Sprite helpImageSprite;
    public Sprite[] levelHelpImages = new Sprite[10];
    Sprite HelpImageForLevel() { var arr = levelHelpImages; if (arr != null && currentLevel >= 1 && currentLevel <= arr.Length && arr[currentLevel - 1] != null) return arr[currentLevel - 1]; return helpImageSprite; }
    [Header("Sounds (optional - any empty slot stays silent)")]
    public AudioClip swapSwooshClip;
    public AudioClip popClip;
    public AudioClip stoneBreakClip;
    public AudioClip blockedShakeClip;
    public AudioClip stoneWiggleClip;
    public AudioClip stoneCrackClip;
    public AudioClip hintShakeClip;
    public AudioClip winClip;
    public AudioClip levelStartClip;
    public AudioClip uiClickClip;
    public AudioClip fallWhooshClip;
    public AudioClip beamBlastClip;
     public AudioClip greenHealClip;
    public AudioClip purpleArmClip;
    public AudioClip purpleTransmuteClip;
    public AudioClip zapBeamClip;
    public AudioClip healLandClip;
    public AudioClip coinPopClip;
    public AudioClip starFillClip;
    public AudioClip crossBlastClip;
    public AudioClip megaCrossBlastClip;
    public AudioClip purchaseOkClip;
    public AudioClip purchaseDenyClip;
    public AudioClip chain2Clip;
    public AudioClip chain3Clip;
    public AudioClip chain4Clip;
    public AudioClip blackHoleSuckClip;

    [Header("Pop timing")]
    public float popDelay = 0.2f;
    public int maxStaggeredPops = 8;
    public float matchPopDelay = 0.1f;
    public float fallWhooshPerCircle = 0.1f;
    public float specialSpawnIn = 0.15f;
    public float beamFlashDur = 0.2f;
    public float suckInTime = 0.25f;
    public float suckStaggerSpan = 0.35f;
    public float zapTravelTime = 0.4f;
    public bool DestroyStonebySpecial = true;
    public float blastPopStagger = 0.05f;
    [Header("Score pop text (+N where bubbles pop)")]
    public float scorePopSize = 30f;
    public float scorePopRisePx = 200f;
    public float scorePopFadeDur = 1.4f;
    public float scorePopSpecialDur = 0.5f;
    public Font scorePopFont;
    [Header("Cascade chain bonus")]
    public float chainGoldStepPercent = 10f;
    [Header("Stones - hit points (each adjacent clear deals ONE hit per pass)")]
    public float beamLineThickness = 0.28f;
    public int stoneMaxHits = 3;
    public float stoneAlphaNew = 0.8f;
    public float stoneAlphaHit1 = 0.6f;
    public float stoneAlphaHit2 = 0.4f;
    public Sprite stoneArtNew;
    public Sprite stoneArtHit1;
    public Sprite stoneArtHit2;
    public float stoneFallScale = 1f;
    [Header("Stone break timing")]
    public float stoneBreakDelay = 0.2f;

    [Header("Board shake - board-root offset jolt per event; tiles/stones wobble, camera + backdrop stay put (Inspector is authoritative)")]
    public float boardShakeStoneAmp = 0.18f;
    public float boardShakeStoneDur = 0.18f;
    public float boardShakeStoneSpeed = 1f;
    public float boardShakeStoneAngleDeg = 45f;
    public float boardShakeCascadeAmp = 0.30f;
    public float boardShakeCascadeDur = 0.22f;
    public float boardShakeCascadeSpeed = 2f;
    public float boardShakeCascadeAngleDeg = 45f;
    public float boardShakeSpecialAmp = 0.45f;
    public float boardShakeSpecialDur = 0.30f;
    public float boardShakeSpecialSpeed = 2f;
    public float boardShakeSpecialAngleDeg = 45f;
    [Range(0, 1)] public float boardShakeDirRandomness = 1f;
    public float boardShakeDampPow = 1f;
    [Header("Options (in-game)")]

    public const float VolumeMin = 0.001f;
    public const float VolumeMax = 0.5f;
    [Range(VolumeMin, VolumeMax)] public float sfxVolume = VolumeMax;
    [Range(VolumeMin, VolumeMax)] public float bgmVolume = VolumeMax;
    public bool musicEnabled = true;
    [Header("Music UI positions (X offsets in design units, added to default layout)")]
    [Header("Main menu Music row - X/Y offset (design units) + size multiplier around its snap position")]
    public float musicMenuRowOffX = 0f;
    public float musicMenuRowOffY = 0f;
    public float musicMenuRowScale = 1f;
    [Header("Main menu Dailies row - X/Y offset (design units) + size multiplier around its snap position")]
    public float dailyMenuRowOffX = 0f;
    public float dailyMenuRowOffY = 0f;
    public float dailyMenuRowScale = 1f;
[Header("Pause overlay - compact row button dials (X/Y in design units, size multiplier around each button's snap position)")]
public float pauseOptOffX = 0f;
public float pauseOptOffY = 0f;
public float pauseOptScale = 1f;
public float pauseCheatOffX = 0f;
public float pauseCheatOffY = 0f;
public float pauseCheatScale = 1f;
public float pauseTestPassOffX = 0f;
public float pauseTestPassOffY = 0f;
public float pauseTestPassScale = 1f;
    [Header("Pause overlay - Dailies row dial (X/Y in design units, size multiplier around its snap position)")]
    public float pauseDailyOffX = 0f;
    public float pauseDailyOffY = 0f;
    public float pauseDailyScale = 1f;
    [Header("Pause overlay - Music row dial (X/Y in design units, size multiplier around its snap position)")]
    public float pauseMusicOffX = 0f;
    public float pauseMusicOffY = 0f;
    public float pauseMusicScale = 1f;
[Header("Pause overlay - force-run strip dials (applied to all six small buttons as one row)")]
    public float pauseDbgOffX = 0f;
    public float pauseDbgOffY = 0f;
    public float pauseDbgScale = 1f;
    public float holeBlockScale = 1f;
    public float[] tileSpriteScales = { 1f, 1f, 1f, 1f, 1f };
    public float[] levelBackgroundScales = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    [Header("Cinematic UI - intro video & music")]
    public VideoClip introVideoClip;
    public TextAsset introGifFile;
    public string webIntroFileName = "intro_vp8.webm";
    public AudioClip introAudioClip;

    public bool showIntroSkipHint = true;
    public bool alwaysShowStartGate = false;
    [Header("Cinematic UI - start gate appearance")]
    public float startGateTextSize = 64f;
    public Color startGateTextColor = Color.white;
    public Font startGateFont;
    public AudioClip bgmClip;

    [Header("Cinematic UI - per-screen BGM (optional loops; an EMPTY clip falls back to the overarching track above)")]
    public AudioClip menuBgmClip;
    [Range(0, 1)] public float menuBgmVolume = 1f;
    public AudioClip[] levelBgmClips = new AudioClip[10];
    [Range(0, 1)] public float[] levelBgmVolumes = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    [Header("Cinematic UI - splash art (optional; drawn OVER the GUI which stays as clickable backbone)")]
    public Texture2D menuSplashTex;
    public Texture2D levelSelectSplashTex;
    public Texture2D optionsSplashTex;
    public Texture2D spritesSplashTex;
    public Texture2D soundsSplashTex;
    public Texture2D winSplashTex;
    public Texture2D pauseSplashTex;
    public Texture2D loadingBgTex;
    public Texture2D loadingLogoTex;
    public Texture2D levelIconTex;
    public Sprite uiBackgroundSprite;
    public Texture2D mainMenuBgTex;
    public Texture2D levelSelectBgTex;
    public float levelSelectBoxAlpha = 0.9f;

    public Color storeBoxColor = new Color(0.25f, 0.28f, 0.35f);
    public float storeBoxAlpha = 0.9f;

    [Header("Stage Select - in-box text layout (design px offsets from face center-X / top-Y; same values apply to every stage)")]
    public float stageNameOffX = 0f;
    public float stageNameOffY = 18f;
    public float stageNameFont = 24f;

    public float starsOffX = 0f;
    public float starsOffY = 52f;
    public float starsScaleF = 0.28f;

    public float info1OffX = 0f;
    public float info1OffY = 76f;
    public float info2OffX = 0f;
    public float info2OffY = 92f;
    public float infoFont = 13f;
    [Header("Cinematic UI - transition tuning (unscaled seconds)")]
    public float menuFlyInDur = 1.0f;
    public float slideTransitionDur = 0.9f;
    public float levelTileFlyDur = 3.0f;
    public bool enableLevelFlyAnim = true;
    public int flyingLevelCount = 6;
    public bool boardTilesFlyIn = true;
    public float minSpawnDist = 10f;
    public float overlayFlyDur = 0.8f;
    public float loadMinSec = 2.3f, loadMaxSec = 2.7f;
    public bool pointerGlowOn = true;
    public float pointerGlowSize = 240f;

    [Header("UI visibility (uncheck to hide a button / HUD text everywhere it appears)")]
    public bool showSpritesButton = true;
    public bool showSoundsButton = true;
    public bool showOptionsButton = true;
    public bool showDeleteProgressButton = true;
    public bool showOrientationButton = true;
    public bool showTestPassLevelButton = true;
    public bool showTestCheatButton = true;
    public bool showCheatGoldButton = true;
    public bool showTesterPops = true;

    public bool qaDebugOptions = true;
    public bool showResetButton = true;
    public bool showShopBtn = true;
    public bool showPauseStoreButton = true;
    public bool showHelpBtn = true;
    public bool showHudLevelText = true;
    public bool showHudScoreText = true;
    public bool showHudLegalMovesText = true;
    public bool showHudHelpText = true;
    public bool showHudTimeText = true;
    public bool showStageName = true;
    public bool showWinStars = true;
    public bool showHiddenStrip = true;

    [Header("UI Style")]
    public Font uiFontSlot;
    public Color uiTextColor = new Color(0.92f, 0.87f, 0.75f);

    [Header("UI Text")]
    public string uiTextMainMenu = "Main Menu";
    public string uiTextLevelSelect = "Level Select";
    public string uiTextOptions = "Options";
    public string uiTextStore = "Store";
    public string uiTextPlayGame = "Play Game";
    public string uiTextSprites = "Sprites";
    public string uiTextSounds = "Sounds";

    public string uiTextQaDebugOptions = "QA DebugOptions";
    public string uiTextDeleteProgress = "Delete Progress";
    public string uiTextConfirmDelete = "Confirm Delete?";
    public string uiTextCancel = "Cancel";
    public string uiTextPortrait = "Portrait";
    public string uiTextWidescreen = "Widescreen";
    public string uiTextBack = "Back";
    public string uiTextPickFromPhone = "Pick From Phone";
    public string uiTextResetDefaults = "Reset Defaults";
    public string uiTextMenuTitle = "MATCH 3";
    public string uiTextCreditsProject = "KBMGames match3 game project";
    public string uiTextCreditsMusic = "Music: Upbeat Jazz by AurecTheme | Royalty-free Music";
    public string uiTextPausedTitle = "PAUSED";
    public string uiTextCheatGold = "CHEAT: +10000 Gold";
    public string uiTextTestCheat = "TestCheat";
    public string uiTextUnlockAllLevels = "Unlock All Levels";
    public string uiTextTestPassLevel = "TestPassLevel";
    public string[] uiTextTesterPopLabels = new string[] { "4", "R5", "B5", "Y5", "G5", "P5" };
    public string uiTextSelectLevel = "Stage Select";
    public string uiTextLevelTemplate = "Stage ";
    public string uiTextLocked = "Locked";
    public string uiTextBackArrow = "<";
    public string uiTextListDown = "Down";
    public string uiTextListUp = "Up";
    public string uiTextScorePrefix = "Score: ";
    public string uiTextMovesPrefix = "Moves: ";
    public string uiTextTimePrefix = "Time: ";
    public string uiTextNotCleared = "Not cleared yet";
    public string uiTextLegalMoves = "Legal moves: ";
    public string uiTextHudHelpNormal = "Drag / swipe a circle up, down, left or right to swap it with its neighbor. R = reset.";
    public string uiTextHudHelpAutoDemo = "Auto-demo ON (set autoDemo=false in the Inspector). R = reset.";
    public string uiTextTotalPrefix = "Total: ";
    public string uiTextResetBtn = "Reset";
    public string uiTextPauseBtn = "Pause";
    public string uiTextResumeBtn = "Resume";
    public string uiTextPausedPill = "Paused";
    public string uiTextHelpPill = "?";
    public string uiTextYouWin = "YOU WIN!";
    public string uiTextDaily0 = "Pop Frenzy";
    public string uiTextDaily1 = "Gravity Master";
    public string uiTextDaily2 = "Speed Runner";
    public string uiTextDaily3 = "Alchemist";
    public string uiTextDaily4 = "Chain Reaction";
    public string uiTextDaily5 = "Demolition";
    public string uiTextDaily6 = "Big Blaster";
    public string uiTextDaily7 = "Healing Touch";
    public string uiTextDaily8 = "Special Collector";
    public string uiTextDaily9 = "Gold Digger";
    public string uiTextDailiesHeader = "DAILIES";
    public string uiTextDailyDone = "DONE";
    public string uiTextDailyDesc0 = "Match tiles to pop bubbles.";
    public string uiTextDailyDesc1 = "Make a five-match to fire the yellow-target black hole.";
    public string uiTextDailyDesc2 = "Win any level before the time limit runs out.";
    public string uiTextDailyDesc3 = "Use the purple transmuter to convert tiles.";
    public string uiTextDailyDesc4 = "Trigger cascade chains after your own clear.";
    public string uiTextDailyDesc5 = "Break locked stones by matching next to them.";
    public string uiTextDailyDesc6 = "Fire cross or mega blast charges from the HUD.";
    public string uiTextDailyDesc7 = "Land green heal blocks on the board.";
    public string uiTextDailyDesc8 = "Spawn special entities with four- and five-matches.";
    public string uiTextDailyDesc9 = "Pop gold from tiles, CHAIN bonuses included.";
    [Header("Daily reward popup")]
    public float dailyToastDur = 2f;
    public float dailyToastOffX = 16f;
    public float dailyToastOffY = 16f;
    public float dailyToastScale = 1f;
    public float dailyFlyTime = 1.2f;
    public float dailyFlyCurveX = 0f;
    public float dailyFlyCurveY = -80f;
    public float dailyCoinSize = 34f;
    public string uiTextYouLose = "YOU LOSE";
    public string uiTextMovesMadePrefix = "Moves made: ";
    public string uiTextMovesLeftPrefix = "Moves Left: ";
    public string uiTextStarBonusPrefix = "Star Bonus: ";
    public string uiTextTotalGoldPrefix = "Total Gold: ";
    public string uiTextStageNamePattern = "Stage {0}";
    public string uiTextNextLevel = "Next Level ->";
    public string uiTextAllLevelsComplete = "All levels complete!";
    public string uiTextResetStage = "Reset Stage";
    public string uiTextRetry = "Retry";
    public string uiTextOptSfxVolume = "SFX Volume";
    public string uiTextOptTileSize = "Tile Size";
    public string uiTextOptGravity = "Gravity";
    public string uiTextOptSwapTime = "Swap Time";
    public string uiTextOptClearTime = "Clear Time";
    public string uiTextOptPopDelay = "Pop Delay";
    public string uiTextOptShapeDensity = "Shape Density (next board)";
    public string uiTextOptStoneChance = "Stone Chance (next board)";
    public string uiTextOptDemoInterval = "Demo Interval";
    public string uiTextToggleGrid = "Show Board Grid";
    public string uiTextToggleWinStars = "Show Win Stars";
    public string uiTextToggleHiddenStrip = "Show Level/Gold Strip";
    public string uiTextTileRed = "Tile Red (elem 0)";
    public string uiTextTileGreen = "Tile Green (elem 1)";
    public string uiTextTileBlue = "Tile Blue (elem 2)";
    public string uiTextTileYellow = "Tile Yellow (elem 3)";
    public string uiTextTilePurple = "Tile Purple (elem 4)";
    public string uiTextStoneSprite = "Stone Sprite";
    public string uiTextHoleBlockSprite = "Hole Block Sprite";
    public string uiTextLevelBgPattern = "Level Background {0} (elem {1})";
    public string uiTextMusic = "Music";
    public string uiTextVolume = "Volume";
    public string uiTextSfxVol = "SFX";
    public string uiTextMasterVolume = "Master Volume";
    public string uiTextSndSwap = "Swap Swoosh";
    public string uiTextSndPop = "Pop";
    public string uiTextSndStoneBreak = "Stone Break";
    public string uiTextSndBlockedShake = "Blocked Shake";
    public string uiTextSndStoneWiggle = "Stone Wiggle";
    public string uiTextSndHintShake = "Hint Shake";
    public string uiTextSndWin = "Win";
    public string uiTextSndLevelStart = "Level Start";
    public string uiTextSndUiClick = "UI Click";
    public string uiTextSndFallWhoosh = "Fall Whoosh";
    public string uiTextSndStoneCrack = "Stone Crack";
    public string uiTextSndBeamBlast = "Beam Blast";
    public string uiTextSndGreenHeal = "Green Heal";
    public string uiTextSndPurpleArm = "Purple Arm";
    public string uiTextSndPurpleTransmute = "Purple Transmute";
    public string uiTextSndZapBeam = "Zap Beam";
    public string uiTextSndHealLand = "Heal Land";
    public string uiTextSndCoinPop = "Coin Pop";
    public string uiTextSndStarFill = "Star Fill";
    public string uiTextSndCrossBlast = "Cross Blast";
    public string uiTextSndMegaCrossBlast = "Mega Cross Blast";
    public string uiTextSndPurchaseOk = "Purchase OK";
    public string uiTextSndPurchaseDeny = "Purchase Deny";
    public string uiTextSndChain2 = "Chain Wave 2";
    public string uiTextSndChain3 = "Chain Wave 3";
    public string uiTextSndChain4 = "Chain Wave 4+";
    public string uiTextSndBlackHoleSuck = "Black Hole Suck";
    public string uiTextStoreTitle = "STORE";
    public string uiTextStoreItemCross = "Cross Blast";
    public string uiTextStoreItemMega = "Mega Cross";
    public string uiTextStoreItemCrossDesc = "Destroys bubbles in a cross until stone or hole.";
    public string uiTextStoreItemMegaDesc = "Destroys bubbles in a cross AND diagonals.";
    public string uiTextOwnedPrefix = "Owned ";
    public string uiTextBuyBtn = "BUY";
    public string uiTextMaxLabel = "MAX";
    public string uiTextTapToSkip = "tap to skip";
    public string uiTextLoading = "Loading...";
    public string uiTextStartGame = "Start Game";
    [Header("Pause button (top-middle, round - one board circle in size)")]
    public Sprite pauseButtonSprite;
    public Sprite playButtonSprite;
    [Header("Level select Down/Up buttons")]
    public Sprite downButtonSprite;
    public Sprite upButtonSprite;
    [Header("In-game timer (top center)")]
    public Font timerFont;

    [Header("Main menu credits (bottom of screen)")]
    public Font creditsFont;

    [Header("Sounds (per-sound volume multipliers)")]
    public float sfxSwapVol = 1f;
    public float sfxPopVol = 1f;
    public float sfxStoneBreakVol = 1f;
    public float sfxBlockedVol = 1f;
    public float sfxStoneWiggleVol = 1f;
    public float sfxHintVol = 1f;
    public float sfxWinVol = 1f;
    public float sfxLevelStartVol = 1f;
    public float sfxUiClickVol = 1f;
    public float sfxWhooshVol = 1f;
    public float sfxStoneCrackVol = 1f;
    public float sfxBeamVol = 1f;
        public float sfxGreenHealVol = 1f;
    public float sfxPurpleArmVol = 1f;
    public float sfxPurpleTransmuteVol = 1f;
    public float sfxZapVol = 1f;
    public float sfxHealLandVol = 1f;
    public float sfxCoinPopVol = 1f;
    public float sfxStarFillVol = 1f;
    public float sfxCrossBlastVol = 1f;
    public float sfxMegaCrossBlastVol = 1f;
    public float sfxPurchaseOkVol = 1f;
    public float sfxPurchaseDenyVol = 1f;
    public float sfxChain2Vol = 1f;
    public float sfxChain3Vol = 1f;
    public float sfxChain4Vol = 1f;
    public float sfxBlackHoleSuckVol = 1f;

    Color[] tileColors;
    int[,] colorGrid;
    GameObject[,] tiles;
    bool[,] shape;
    Sprite circleSprite;

    Sprite squareSprite;
    Camera cam;
    Transform managerTransform;

    bool busy = false;
    bool paused = false;
    bool helpOpen = false;
    bool pauseOverlayOpen = false;

    int movesMade = 0;
    bool won = false;
    int hp = 10;
    bool lost = false;
    float hpFlashTime = -99f;
    int swapGenAtStart = 0;
    int initialStoneCount = 0;
    float totalGold = 0f;
    public float levelGold = 0f;
    float lastStarBonus = 0f;
    public float hiddenGold = 0f;
    public int crossBlasts = 0;
    public int megaCrossBlasts = 0;

    bool boardIsLandscape = false;

float lastAspect = -1f;

    enum GameState { Menu, LevelSelect, Playing, Options, Sprites, Sounds, SpritePick, ImageBrowse, SoundPick, AudioBrowse, Intro, Loading, Store, StartGate }
    GameState state = GameState.Menu;
    int currentLevel = 1;
    const int totalLevels = 10;
    int unlockedLevel = 1;

    bool deleteProgressArmed = false;
Vector2 levelScroll;
bool dragByTouch = false;

    float levelTime = 0f;
    float idleTimer = 0f;
    public float hintDelay = 15f;
    public float hintRepeatDelay = 3f;
    bool hintRepeating = false;
    public float hintNudgeReach = 0.12f;
int score = 0;

    float demoTimer = 0f;
    int openingMoveCount = 0;
    int availableMoves = 0;
    List<GameObject> holeBlocks = new List<GameObject>();
    HashSet<GameObject> damagedThisPass = new HashSet<GameObject>();
    public int maxHp = 10;
    public float healBlockFlyTime = 0.6f;
    public float healBlockLaunchGap = 0.15f;

    [Header("Per-level HP - one float dial per level 1..10 (rounded to int at load, min 1)")]
    public float[] levelMaxHp = { 3f, 4f, 5f, 6f, 7f, 8f, 10f, 10f, 10f, 10f };
    int LevelMaxHp() => levelMaxHp != null && levelMaxHp.Length > 0 ? Mathf.Max(1, Mathf.RoundToInt(levelMaxHp[Mathf.Clamp(currentLevel - 1, 0, levelMaxHp.Length - 1)])) : maxHp;
    [Header("Per-level move cap - one float dial per level 1..10 (rounded to int); <= 0 or missing = no limit, the level plays exactly as before")]
    public float[] levelMaxMoves = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
    int LevelMovesCap() => levelMaxMoves != null && levelMaxMoves.Length > 0 ? Mathf.Max(0, Mathf.RoundToInt(levelMaxMoves[Mathf.Clamp(currentLevel - 1, 0, levelMaxMoves.Length - 1)])) : 0;

    [Header("Star-bonus gold at win - one dial per half-star mark (cumulative defaults: 1/2*=300g, 1*=1000g, 1.5*=1800g, 2*=2700g, 2.5*=3700g, 3*(full HP)=5000g - retune freely)")]
    public float starGoldHalf1 = 300f;
    public float starGoldFull1 = 700f;
    public float starGoldHalf2 = 800f;
    public float starGoldFull2 = 900f;
    public float starGoldHalf3 = 1000f;
    public float starGoldFull3 = 1300f;

    int HalfStarSteps() => maxHp > 0 ? Mathf.Clamp(Mathf.RoundToInt(hp / (float)maxHp * 3f * 2f), 0, 6) : 0;

    float GetStarBonus()
    {
        int halfSteps = HalfStarSteps();
        float bonus = 0f;
        if (halfSteps >= 1) bonus += starGoldHalf1;
        if (halfSteps >= 2) bonus += starGoldFull1;
        if (halfSteps >= 3) bonus += starGoldHalf2;
        if (halfSteps >= 4) bonus += starGoldFull2;
        if (halfSteps >= 5) bonus += starGoldHalf3;
        if (halfSteps >= 6) bonus += starGoldFull3;
        return bonus;
    }

    int GetHiddenLevel() { int lvl = 0; float m = Mathf.Max(1f, hiddenLevelBase); while (hiddenGold >= m) { lvl++; if (hiddenLevelStepMult <= 1.0005f) break;
        m *= hiddenLevelStepMult; } return lvl; }

    int stonesDestroyedLastResolve = 0;
    bool greenHealFiredLastResolve = false;
    HashSet<Vector2Int> shakeLocks = new HashSet<Vector2Int>();

    void Start()
    {
        managerTransform = transform;
        tileColors = new Color[]
        {
            new Color(0.95f, 0.30f, 0.32f),
            new Color(0.30f, 0.78f, 0.42f),
            new Color(0.32f, 0.52f, 0.95f),
            new Color(0.98f, 0.78f, 0.25f),
            new Color(0.62f, 0.42f, 0.90f)
        };
        colorGrid = new int[rows, cols];
        tiles = new GameObject[rows, cols];
        shape = new bool[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                colorGrid[r, c] = -1;

        SetupCamera();
        circleSprite = CreateCircleSprite();

        menuBgTex = CreateMenuBackground();
        panelBgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        panelBgTex.SetPixel(0, 0, new Color(0.25f, 0.28f, 0.35f, 0.9f));
        panelBgTex.Apply();
        squareSprite = CreateSquareSprite();
        CreateSpecialSprites();
        CreateEconomySprites();
        BuildSfxPool();
        unlockedLevel = Mathf.Clamp(PlayerPrefs.GetInt("M3_unlocked", 1), 1, totalLevels);
        totalGold = PlayerPrefs.GetFloat("M3_totalGold", 0f);
        hiddenGold = PlayerPrefs.GetFloat("M3_hiddenGold", 0f);
        InitDailies();
        crossBlasts = Mathf.Clamp(PlayerPrefs.GetInt("M3_goldCross", 0), 0, maxBlastCharges);
        megaCrossBlasts = Mathf.Clamp(PlayerPrefs.GetInt("M3_goldMega", 0), 0, maxBlastCharges);
        CaptureOptionDefaults();
        LoadOptions();
        LoadPickedAssets();

        CreateCinematicTextures();
        SetupBgmSource();
        musicEnabled = PlayerPrefs.GetInt("M3_opt_musicOn", 1) == 1;

#if UNITY_ANDROID
        if (!showOrientationButton || PlayerPrefs.HasKey("M3_opt_orient")) Screen.orientation = (!showOrientationButton || PlayerPrefs.GetInt("M3_opt_orient", 0) == 1) ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
#endif
showBoardGrid = PlayerPrefs.GetInt("M3_opt_gridOn", 1) == 1;
        showWinStars = PlayerPrefs.GetInt("M3_opt_winStarsOn", showWinStars ? 1 : 0) == 1;

        if (PlayerPrefs.HasKey("M3_opt_qaDebug")) { qaDebugOptions = PlayerPrefs.GetInt("M3_opt_qaDebug", 1) == 1; ApplyQaDebugOptions(); }
showHiddenStrip = PlayerPrefs.GetInt("M3_opt_hiddenStripOn", showHiddenStrip ? 1 : 0) == 1;
        if (Application.platform == RuntimePlatform.WebGLPlayer || alwaysShowStartGate) state = GameState.StartGate;
        else if (introVideoClip != null || introGifFile != null) { SetupIntro(); }
        else                        { if (musicEnabled) StartBgm(); BeginMenuSpawn(); }
    }

    void ApplyQaDebugOptions() { showSpritesButton = qaDebugOptions; showSoundsButton = qaDebugOptions; showOptionsButton = qaDebugOptions; showDeleteProgressButton = qaDebugOptions; showTestPassLevelButton = qaDebugOptions; showCheatGoldButton = qaDebugOptions; showTesterPops = qaDebugOptions; showResetButton = qaDebugOptions; }


    void CreateEconomySprites()
    {
        if (goldCoinSprite == null) goldCoinSprite = MakeFallback64(EconShapeCoin, new Color(0.95f, 0.78f, 0.25f), null);
        if (healBlockSprite == null) healBlockSprite = MakeFallback64(EconShapeHealBlock, new Color(0.30f, 0.78f, 0.42f), null);
        if (crossBlastIcon == null) crossBlastIcon = MakeFallback64(EconShapeCross, new Color(0.95f, 0.45f, 0.25f), null);
        if (megaCrossIcon == null) megaCrossIcon = MakeFallback64(EconShapeMegaStar, new Color(0.62f, 0.42f, 0.90f), null);
        if (starEmptySprite == null) starEmptySprite = MakeFallback64(EconShapeStar, new Color(0.30f, 0.30f, 0.35f), null);
        if (starHalfSprite == null) starHalfSprite = MakeFallback64(EconShapeStar, new Color(0.98f, 0.78f, 0.25f), new Color(0.30f, 0.30f, 0.35f));
        if (starFullSprite == null) starFullSprite = MakeFallback64(EconShapeStar, new Color(0.98f, 0.78f, 0.25f), null);
    }

    Sprite MakeFallback64(System.Func<float, float, bool> inside, Color color, Color? rightColor)
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = size / 2f - 0.5f;
        const float coreScale = 1f / 0.70f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (!inside(x, y)) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }
                bool inCore = inside(c + (x - c) * coreScale, c + (y - c) * coreScale);
                Color col = !inCore ? new Color(0f, 0f, 0f, 1f) : ((rightColor != null && x < c) ? color : (rightColor ?? color));
                tex.SetPixel(x, y, col);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    bool EconShapeCoin(float x, float y) { const float c = 31.5f; return Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) <= 29f; }

    bool EconShapeHealBlock(float x, float y)
    {
        const float c = 31.5f, hx = 26f, cr = 10f;
        float qx = Mathf.Abs(x - c) - (hx - cr), qy = Mathf.Abs(y - c) - (hx - cr);
        return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) <= cr;
    }

    bool EconShapeCross(float x, float y) { const float c = 31.5f, arm = 9f, span = 27f; float dx = Mathf.Abs(x - c), dy = Mathf.Abs(y - c); return (dx <= arm && dy <= span) || (dy <= arm && dx <= span); }

    bool EconShapeMegaStar(float x, float y)
    {
        const float c = 31.5f, arm = 8f, span = 27f;
        float u = ((x - c) + (y - c)) * 0.7071f, v = ((x - c) - (y - c)) * 0.7071f;
        return EconShapeCross(x, y) || (Mathf.Abs(v) <= arm && Mathf.Abs(u) <= span);
    }

    bool EconShapeStar(float x, float y) { if (econStarVerts == null) BuildEconStarVerts(); return PointInPolygon64(x, y, econStarVerts); }

    Vector2[] econStarVerts;
    void BuildEconStarVerts()
    {
        const float c = 31.5f, Ro = 29f, Ri = 29f * 0.42f;
        econStarVerts = new Vector2[10];
        for (int k = 0; k < 5; k++)
        {
            float aO = Mathf.Deg2Rad * (90f + 72f * k);
            float aI = Mathf.Deg2Rad * (126f + 72f * k);
            econStarVerts[2 * k] = new Vector2(c + Ro * Mathf.Cos(aO), c + Ro * Mathf.Sin(aO));
            econStarVerts[2 * k + 1] = new Vector2(c + Ri * Mathf.Cos(aI), c + Ri * Mathf.Sin(aI));
        }
    }

    static bool PointInPolygon64(float px, float py, Vector2[] v)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            if ((v[i].y > py) != (v[j].y > py))
            {
                float xInt = (v[j].x - v[i].x) * (py - v[i].y) / (v[j].y - v[i].y) + v[i].x;
                if (px < xInt) inside = !inside;
            }
        return inside;
    }

void LoadLevel(int lvl)
    {
        currentLevel = Mathf.Clamp(lvl, 1, totalLevels);
        maxHp = LevelMaxHp();
        int baseRows = Mathf.Max(2, Mathf.RoundToInt(boardRowBase)) + currentLevel;
        int baseCols = Mathf.Max(2, Mathf.RoundToInt(boardColBase)) + currentLevel;
        boardIsLandscape = !showOrientationButton || Screen.width > Screen.height;
        if (boardIsLandscape) { rows = baseCols; cols = baseRows; }
        else                  { rows = baseRows; cols = baseCols; }

        colorGrid = new int[rows, cols];
        tiles = new GameObject[rows, cols];
        shape = new bool[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                colorGrid[r, c] = -1;

        score = 0; movesMade = 0; won = false; lost = false; hp = maxHp; hpFlashTime = -99f; busy = false; isDragging = false; hasSel = false;
        levelGold = 0f; lastStarBonus = 0f;
        CloseHelpIfOpen(); pauseOverlayOpen = false;
        paused = false; Time.timeScale = 1f;
        levelTime = 0f; idleTimer = 0f; hintRepeating = false;
        state = GameState.Playing;
        BgmForScreen(currentLevel);
        SetupCamera();
        BuildBoard();
        flyInEndsAt = -1f;
        if (boardTilesFlyIn) StartCoroutine(TileFlyInRoutine());
        PlaySfx(SfxSlot.LevelStart, managerTransform.position);
    }


void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        cam.transform.position = new Vector3(0, 0, -10);
        cam.transform.rotation = Quaternion.identity;

        float aspect = cam.aspect > 0 ? cam.aspect : (float)Screen.width / (float)Screen.height;
        lastAspect = cam.aspect;

        float scrW = Screen.width, scrH = Screen.height;
        float s = UiGlobalScale();
        float halfWd = cols * cellSize / 2f + 0.4f;
        if (scrW <= scrH && showOrientationButton)
        {
            float th = 68f * s * Mathf.Clamp(timerSizeScale, 0.25f, 3f);
            float Xtop = Mathf.Max(105f * s, th + 50f * s);
            Xtop += portraitTopSpace * s;
            float touchGap = 5f * s;
            float availV = Mathf.Max(60f, scrH - Xtop - 12f * s - touchGap);
            cam.orthographicSize = Mathf.Max(
                scrH * (rows + 1f) * cellSize / (2f * availV),
                halfWd / aspect);
            float kPixP = scrH / (2f * cam.orthographicSize);
            float gridTopWorld = rows * cellSize / 2f;
            cam.transform.position = new Vector3(0f, (Xtop - scrH / 2f + gridTopWorld * kPixP) / kPixP, -10f);
        }
        else
        {

            float Tm = 30f * s, Bm = 30f * s;
            cam.orthographicSize = Mathf.Max(
                rows * cellSize * scrH / (2f * Mathf.Max(60f, scrH - Tm - Bm)),
                halfWd * scrH / Mathf.Max(60f, scrW - wideLeftStrip * scrW - wideRightMargin * scrW));
            cam.orthographicSize /= Mathf.Clamp(wideBoardScale, 0.25f, 1f);
            float visW = 2f * aspect * cam.orthographicSize;
            float uC = (wideLeftStrip + (1f - wideRightMargin)) / 2f;
            cam.transform.position = new Vector3(-(uC - 0.5f) * visW, 0f, -10f);

        }

    }



    Transform boardShakeRoot;
    bool boardShakeActive = false;
    float shakeTimeT = 0f, shakeAmpT = 0f, shakeDurT = 0.001f, shakeSpeedT = 1f;
    Vector2 shakeDirT = Vector2.zero;

    void TriggerBoardShake(float amp, float dur, float speed, float angleDeg)
    {
        if (boardShakeRoot == null || amp <= 0f || dur <= 0f) return;
        float rnd = Mathf.Clamp01(boardShakeDirRandomness);
        float ang = Random.Range(0f, 360f) * rnd + angleDeg * (1f - rnd);
        shakeDirT = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
        shakeAmpT = amp; shakeDurT = dur; shakeSpeedT = Mathf.Max(0.1f, speed);
        shakeTimeT = 0f; boardShakeActive = true;
    }

    void TriggerBoardShakeStoneBreak() { TriggerBoardShake(boardShakeStoneAmp, boardShakeStoneDur, boardShakeStoneSpeed, boardShakeStoneAngleDeg); }
    void TriggerBoardShakeCascade()    { TriggerBoardShake(boardShakeCascadeAmp, boardShakeCascadeDur, boardShakeCascadeSpeed, boardShakeCascadeAngleDeg); }
    void TriggerBoardShakeSpecial()    { TriggerBoardShake(boardShakeSpecialAmp, boardShakeSpecialDur, boardShakeSpecialSpeed, boardShakeSpecialAngleDeg); }

    void UpdateBoardShake()
    {
        if (!boardShakeActive) return;
        if (boardShakeRoot == null) { boardShakeActive = false; return; }
        shakeTimeT += Time.deltaTime;
        float k = Mathf.Clamp01(shakeTimeT / shakeDurT);
        if (k >= 1f) { boardShakeRoot.localPosition = Vector3.zero; boardShakeActive = false; return; }
        float pulse = Mathf.Sin(Mathf.PI * shakeSpeedT * k) * Mathf.Pow(1f - k, Mathf.Max(0.05f, boardShakeDampPow));
        boardShakeRoot.localPosition = new Vector3(shakeDirT.x * shakeAmpT * pulse, shakeDirT.y * shakeAmpT * pulse, 0f);
    }

    void StopBoardShakeSnap()
    {
        boardShakeActive = false;
        if (boardShakeRoot != null) boardShakeRoot.localPosition = Vector3.zero;
    }

    void Update()
    {
        UpdateCinematic();


if (cam != null && Mathf.Abs(cam.aspect - lastAspect) > 0.05f)
        {
            SetupCamera();


            if (managerTransform != null)
            {
                var bgT = managerTransform.Find("BoardBackground");
                if (bgT != null)
                {
                    var bsr = bgT.GetComponent<SpriteRenderer>();
                    if (bsr != null && bsr.sprite != null)
                    {
                        float wh2 = bsr.sprite.rect.height / (float)bsr.sprite.pixelsPerUnit;
                        float ww2 = bsr.sprite.rect.width / (float)bsr.sprite.pixelsPerUnit;
                        float sh2 = 2f * cam.orthographicSize, sw2 = sh2 * Mathf.Max(0.1f, cam.aspect);
                        float hs2 = Mathf.Max(sh2 / Mathf.Max(0.01f, wh2), sw2 / Mathf.Max(0.01f, ww2));
                        bgT.localScale = new Vector3(hs2 * boardBgUserMult, hs2 * boardBgUserMult, 1f);
                        bgT.localPosition = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
                    }
                }
            }
        }

        if (state == GameState.LevelSelect && !lvlFlyActive) HandleLevelListScroll();
        else { levelListPointerDown = false; levelListDragActive = false; levelListFlickVel = 0f; }

        if (boardShakeActive && state != GameState.Playing) StopBoardShakeSnap();
        if (state != GameState.Playing) return;


        if (paused) return;
        UpdateBoardShake();

        AgeScorePops();
        if (!busy && fallWhooshSrc != null && fallWhooshSrc.isPlaying) fallWhooshSrc.Stop();

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !busy) ResetBoard();

        if (!won && !busy) CheckWin();
        if (!won && !lost && !busy) CheckMovesBudget();

        if (!won && !lost && Time.time >= flyInEndsAt) levelTime += Time.deltaTime;

        if (won || lost || busy) return;

        if (autoDemo)
        {
            demoTimer += Time.deltaTime;
            if (demoTimer >= demoInterval)
            {
                demoTimer = 0f;
                TryAutoSwap();
            }
        }
        else
        {
            HandlePointer();

            idleTimer += Time.deltaTime;
            if (idleTimer >= (hintRepeating ? hintRepeatDelay : hintDelay))
            {
                idleTimer = 0f;
                hintRepeating = true;
                Vector2Int ha, hb;
                if (FindSmallestMatchMove(out ha, out hb))
                    StartCoroutine(HintNudge(ha, hb));
            }
        }


    }

    void ResetBoard()
    {
        CloseHelpIfOpen();
        score = 0;
        movesMade = 0;
        levelGold = 0f; lastStarBonus = 0f;
        won = false;
        lost = false; hp = maxHp; hpFlashTime = -99f;
        levelTime = 0f; idleTimer = 0f; hintRepeating = false;
        Deselect();
        isDragging = false;
        BuildBoard();
        flyInEndsAt = -1f;
        if (boardTilesFlyIn) StartCoroutine(TileFlyInRoutine());
        PlaySfx(SfxSlot.LevelStart, managerTransform.position);
    }

    int CountStones()
    {
        int n = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (CellHasStone(r, c)) n++;
        return n;
    }

    void CheckWin()
    {
        if (!won && initialStoneCount > 0 && CountStones() == 0)
        {
            CompleteLevel();
            DailyTrackLevelWon(levelTime);
        }
    }


    void CheckMovesBudget()
    {
        int cap = LevelMovesCap();
        if (cap > 0 && movesMade >= cap) { lost = true; CloseHelpIfOpen(); }
    }


    void TestPassLevel()
    {
        SetPaused(false);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var s = GetStoneOnTile(tiles[r, c]);
                if (s != null) Destroy(s);
            }
        CompleteLevel();
    }

    void CompleteLevel()
    {
        if (won) return;
        CloseHelpIfOpen();
        won = true;
        StopBgm(); StopCtxBgm();
        Deselect();
        isDragging = false;
        PlaySfx(SfxSlot.Win, managerTransform.position);

        lastStarBonus = GetStarBonus();
        totalGold += levelGold + lastStarBonus;
        PlayerPrefs.SetFloat("M3_totalGold", totalGold);
        hiddenGold += lastStarBonus;
        PlayerPrefs.SetFloat("M3_hiddenGold", hiddenGold);
        PlaySfx(SfxSlot.StarFill, managerTransform.position);
        PlayerPrefs.SetInt("M3_score_" + currentLevel, score);
        PlayerPrefs.SetInt("M3_moves_" + currentLevel, movesMade);
        PlayerPrefs.SetFloat("M3_time_" + currentLevel, levelTime);
        PlayerPrefs.SetFloat("M3_time_" + currentLevel, levelTime);
        PlayerPrefs.SetInt("M3_stars_" + currentLevel, HalfStarSteps());
            unlockedLevel = currentLevel + 1;
        PlayerPrefs.SetInt("M3_unlocked", unlockedLevel);
        PlayerPrefs.Save();
    }



void SetPaused(bool p)
    {
        paused = p;
        if (!p) pauseOverlayOpen = false;
        isDragging = false;
        Deselect();
        Time.timeScale = p ? 0f : 1f;
        if (p && sfxPool != null) foreach (var src in sfxPool) if (src != null) src.Stop();
        if (p && fallWhooshSrc != null) fallWhooshSrc.Stop();
    }

    void CloseHelpIfOpen()
    {
        if (!helpOpen) return;
        helpOpen = false; helpClosing = false; helpRiseT = -1f; helpCloseT = -1f;
        if (paused && !pauseOverlayOpen) SetPaused(false);
    }

    float lastOptionSaveFlush = -1f;

    void SaveOptions()
    {
        PlayerPrefs.SetFloat("M3_opt_sfxVolume", sfxVolume);
        PlayerPrefs.SetFloat("M3_opt_cellSize", cellSize);
        PlayerPrefs.SetFloat("M3_opt_gravity", gravity);
        PlayerPrefs.SetFloat("M3_opt_swapTime", swapTime);
        PlayerPrefs.SetFloat("M3_opt_clearTime", clearTime);
        PlayerPrefs.SetFloat("M3_opt_popDelay", popDelay);
        PlayerPrefs.SetFloat("M3_opt_stoneSize", stoneSize);
        PlayerPrefs.SetFloat("M3_opt_shapeDensity", shapeDensity);
        PlayerPrefs.SetFloat("M3_opt_stoneChance", stoneChance);
        PlayerPrefs.SetFloat("M3_opt_demoInterval", demoInterval);
        PlayerPrefs.SetFloat("M3_opt_holeBlockScale", holeBlockScale);
        for (int i = 0; i < tileSpriteScales.Length; i++) PlayerPrefs.SetFloat("M3_opt_tileScale_" + i, tileSpriteScales[i]);
        for (int i = 0; i < levelBackgroundScales.Length; i++) PlayerPrefs.SetFloat("M3_opt_bgScale_" + i, levelBackgroundScales[i]);
        PlayerPrefs.SetFloat("M3_opt_sfxSwap", sfxSwapVol);
        PlayerPrefs.SetFloat("M3_opt_sfxPop", sfxPopVol);
        PlayerPrefs.SetFloat("M3_opt_sfxStoneBreak", sfxStoneBreakVol);
        PlayerPrefs.SetFloat("M3_opt_sfxBlocked", sfxBlockedVol);
        PlayerPrefs.SetFloat("M3_opt_sfxStoneWiggle", sfxStoneWiggleVol);
        PlayerPrefs.SetFloat("M3_opt_sfxStoneCrack", sfxStoneCrackVol);
        PlayerPrefs.SetFloat("M3_opt_sfxHint", sfxHintVol);
        PlayerPrefs.SetFloat("M3_opt_sfxWin", sfxWinVol);
        PlayerPrefs.SetFloat("M3_opt_sfxLevelStart", sfxLevelStartVol);
        PlayerPrefs.SetFloat("M3_opt_sfxUiClick", sfxUiClickVol);
        PlayerPrefs.SetFloat("M3_opt_sfxWhoosh", sfxWhooshVol);
        PlayerPrefs.SetFloat("M3_opt_sfxBeam", sfxBeamVol);
                PlayerPrefs.SetFloat("M3_opt_sfxGreenHeal", sfxGreenHealVol);
        PlayerPrefs.SetFloat("M3_opt_sfxPurpleArm", sfxPurpleArmVol);
        PlayerPrefs.SetFloat("M3_opt_sfxPurpleTransmute", sfxPurpleTransmuteVol);
        PlayerPrefs.SetFloat("M3_opt_sfxZap", sfxZapVol);
        PlayerPrefs.SetFloat("M3_opt_sfxHealLand", sfxHealLandVol);
        PlayerPrefs.SetFloat("M3_opt_sfxCoinPop", sfxCoinPopVol);
        PlayerPrefs.SetFloat("M3_opt_sfxStarFill", sfxStarFillVol);
        PlayerPrefs.SetFloat("M3_opt_sfxCrossBlast", sfxCrossBlastVol);
        PlayerPrefs.SetFloat("M3_opt_sfxMegaCrossBlast", sfxMegaCrossBlastVol);
        PlayerPrefs.SetFloat("M3_opt_sfxPurchaseOk", sfxPurchaseOkVol);
        PlayerPrefs.SetFloat("M3_opt_sfxPurchaseDeny", sfxPurchaseDenyVol);
        PlayerPrefs.SetFloat("M3_opt_sfxChain2", sfxChain2Vol);
        PlayerPrefs.SetFloat("M3_opt_sfxChain3", sfxChain3Vol);
        PlayerPrefs.SetFloat("M3_opt_sfxChain4", sfxChain4Vol);
        PlayerPrefs.SetFloat("M3_opt_sfxBlackHoleSuck", sfxBlackHoleSuckVol);
        PlayerPrefs.SetFloat("M3_opt_bgmVol", bgmVolume);
        if (bgmSrc != null) bgmSrc.volume = Mathf.Clamp01(bgmVolume);
        UpdateCtxBgmVolume();

        float now = Time.unscaledTime;
        if (now - lastOptionSaveFlush >= 0.35f) { PlayerPrefs.Save(); lastOptionSaveFlush = now; }
    }

    private float def_sfxVolume, def_cellSize, def_gravity, def_swapTime, def_clearTime;
    private float def_popDelay, def_stoneSize, def_shapeDensity, def_stoneChance, def_demoInterval, def_holeBlockScale;
    private float def_sfxSwapVol, def_sfxPopVol, def_sfxStoneBreakVol, def_sfxBlockedVol, def_sfxStoneWiggleVol, def_sfxStoneCrackVol;
    private float def_sfxHintVol, def_sfxWinVol, def_sfxLevelStartVol, def_sfxUiClickVol, def_sfxWhooshVol, def_sfxBeamVol;
        private float def_sfxGreenHealVol, def_sfxPurpleArmVol, def_sfxPurpleTransmuteVol;
    private float def_sfxZapVol, def_sfxHealLandVol, def_sfxCoinPopVol, def_sfxStarFillVol;
    private float def_sfxCrossBlastVol, def_sfxMegaCrossBlastVol, def_sfxPurchaseOkVol, def_sfxPurchaseDenyVol;
    private float def_sfxChain2Vol, def_sfxChain3Vol, def_sfxChain4Vol, def_sfxBlackHoleSuckVol;
    private float def_bgmVolume;
    private float[] def_tileSpriteScales = new float[0];
    private float[] def_levelBackgroundScales = new float[0];

    void CaptureOptionDefaults()
    {
        def_sfxVolume = sfxVolume; def_cellSize = cellSize; def_gravity = gravity; def_swapTime = swapTime; def_clearTime = clearTime;
        def_popDelay = popDelay; def_stoneSize = stoneSize; def_shapeDensity = shapeDensity; def_stoneChance = stoneChance;
        def_demoInterval = demoInterval; def_holeBlockScale = holeBlockScale;
        def_bgmVolume = bgmVolume;
        def_sfxSwapVol = sfxSwapVol; def_sfxPopVol = sfxPopVol; def_sfxStoneBreakVol = sfxStoneBreakVol; def_sfxBlockedVol = sfxBlockedVol; def_sfxStoneWiggleVol = sfxStoneWiggleVol; def_sfxStoneCrackVol = sfxStoneCrackVol;
        def_sfxHintVol = sfxHintVol; def_sfxWinVol = sfxWinVol; def_sfxLevelStartVol = sfxLevelStartVol; def_sfxUiClickVol = sfxUiClickVol; def_sfxWhooshVol = sfxWhooshVol; def_sfxBeamVol = sfxBeamVol;
        def_sfxGreenHealVol = sfxGreenHealVol;
        def_sfxPurpleArmVol = sfxPurpleArmVol; def_sfxPurpleTransmuteVol = sfxPurpleTransmuteVol;
        def_sfxZapVol = sfxZapVol; def_sfxHealLandVol = sfxHealLandVol; def_sfxCoinPopVol = sfxCoinPopVol; def_sfxStarFillVol = sfxStarFillVol;
        def_sfxCrossBlastVol = sfxCrossBlastVol; def_sfxMegaCrossBlastVol = sfxMegaCrossBlastVol; def_sfxPurchaseOkVol = sfxPurchaseOkVol; def_sfxPurchaseDenyVol = sfxPurchaseDenyVol;
        def_sfxChain2Vol = sfxChain2Vol; def_sfxChain3Vol = sfxChain3Vol; def_sfxChain4Vol = sfxChain4Vol;
        def_sfxBlackHoleSuckVol = sfxBlackHoleSuckVol;
        def_tileSpriteScales = new float[tileSpriteScales.Length];
        for (int i = 0; i < tileSpriteScales.Length; i++) def_tileSpriteScales[i] = tileSpriteScales[i];
        def_levelBackgroundScales = new float[levelBackgroundScales.Length];
        for (int i = 0; i < levelBackgroundScales.Length; i++) def_levelBackgroundScales[i] = levelBackgroundScales[i];
    }

    float OptPref(string key, float inspectorDefault) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : inspectorDefault;

    void LoadOptions()
    {
        sfxVolume = Mathf.Clamp(OptPref("M3_opt_sfxVolume", def_sfxVolume), VolumeMin, VolumeMax);
        cellSize = Mathf.Clamp(OptPref("M3_opt_cellSize", def_cellSize), 0.5f, 4f);
        gravity = Mathf.Clamp(OptPref("M3_opt_gravity", def_gravity), 5f, 200f);
        swapTime = Mathf.Clamp(OptPref("M3_opt_swapTime", def_swapTime), 0.02f, 1f);
        clearTime = Mathf.Clamp(OptPref("M3_opt_clearTime", def_clearTime), 0.02f, 1f);
        popDelay = Mathf.Clamp(OptPref("M3_opt_popDelay", def_popDelay), 0f, 1f);
        stoneSize = Mathf.Clamp(OptPref("M3_opt_stoneSize", def_stoneSize), 0.3f, 2f);
        shapeDensity = Mathf.Clamp(OptPref("M3_opt_shapeDensity", def_shapeDensity), 0.4f, 1f);
        stoneChance = Mathf.Clamp(OptPref("M3_opt_stoneChance", def_stoneChance), 0f, 0.5f);
        demoInterval = Mathf.Clamp(OptPref("M3_opt_demoInterval", def_demoInterval), 0.1f, 5f);
        holeBlockScale = Mathf.Clamp(OptPref("M3_opt_holeBlockScale", def_holeBlockScale), 0.1f, 3f);
        for (int i = 0; i < tileSpriteScales.Length && i < def_tileSpriteScales.Length; i++) tileSpriteScales[i] = Mathf.Clamp(OptPref("M3_opt_tileScale_" + i, def_tileSpriteScales[i]), 0.1f, 3f);
        for (int i = 0; i < levelBackgroundScales.Length && i < def_levelBackgroundScales.Length; i++) levelBackgroundScales[i] = Mathf.Clamp(OptPref("M3_opt_bgScale_" + i, def_levelBackgroundScales[i]), 0.5f, 2f);
        sfxSwapVol = Mathf.Clamp(OptPref("M3_opt_sfxSwap", def_sfxSwapVol), 0f, 3f);
        sfxPopVol = Mathf.Clamp(OptPref("M3_opt_sfxPop", def_sfxPopVol), 0f, 3f);
        sfxStoneBreakVol = Mathf.Clamp(OptPref("M3_opt_sfxStoneBreak", def_sfxStoneBreakVol), 0f, 3f);
        sfxBlockedVol = Mathf.Clamp(OptPref("M3_opt_sfxBlocked", def_sfxBlockedVol), 0f, 3f);
        sfxStoneWiggleVol = Mathf.Clamp(OptPref("M3_opt_sfxStoneWiggle", def_sfxStoneWiggleVol), 0f, 3f);
        sfxStoneCrackVol = Mathf.Clamp(OptPref("M3_opt_sfxStoneCrack", def_sfxStoneCrackVol), 0f, 3f);
        sfxHintVol = Mathf.Clamp(OptPref("M3_opt_sfxHint", def_sfxHintVol), 0f, 3f);
        sfxWinVol = Mathf.Clamp(OptPref("M3_opt_sfxWin", def_sfxWinVol), 0f, 3f);
        sfxLevelStartVol = Mathf.Clamp(OptPref("M3_opt_sfxLevelStart", def_sfxLevelStartVol), 0f, 3f);
        sfxUiClickVol = Mathf.Clamp(OptPref("M3_opt_sfxUiClick", def_sfxUiClickVol), 0f, 3f);
        sfxWhooshVol = Mathf.Clamp(OptPref("M3_opt_sfxWhoosh", def_sfxWhooshVol), 0f, 3f);
        sfxBeamVol = Mathf.Clamp(OptPref("M3_opt_sfxBeam", def_sfxBeamVol), 0f, 3f);
                sfxGreenHealVol = Mathf.Clamp(OptPref("M3_opt_sfxGreenHeal", def_sfxGreenHealVol), 0f, 3f);
        sfxPurpleArmVol = Mathf.Clamp(OptPref("M3_opt_sfxPurpleArm", def_sfxPurpleArmVol), 0f, 3f);
        sfxPurpleTransmuteVol = Mathf.Clamp(OptPref("M3_opt_sfxPurpleTransmute", def_sfxPurpleTransmuteVol), 0f, 3f);
        sfxZapVol = Mathf.Clamp(OptPref("M3_opt_sfxZap", def_sfxZapVol), 0f, 3f);
        sfxHealLandVol = Mathf.Clamp(OptPref("M3_opt_sfxHealLand", def_sfxHealLandVol), 0f, 3f);
        sfxCoinPopVol = Mathf.Clamp(OptPref("M3_opt_sfxCoinPop", def_sfxCoinPopVol), 0f, 3f);
        sfxStarFillVol = Mathf.Clamp(OptPref("M3_opt_sfxStarFill", def_sfxStarFillVol), 0f, 3f);
        sfxCrossBlastVol = Mathf.Clamp(OptPref("M3_opt_sfxCrossBlast", def_sfxCrossBlastVol), 0f, 3f);
        sfxMegaCrossBlastVol = Mathf.Clamp(OptPref("M3_opt_sfxMegaCrossBlast", def_sfxMegaCrossBlastVol), 0f, 3f);
        sfxPurchaseOkVol = Mathf.Clamp(OptPref("M3_opt_sfxPurchaseOk", def_sfxPurchaseOkVol), 0f, 3f);
        sfxPurchaseDenyVol = Mathf.Clamp(OptPref("M3_opt_sfxPurchaseDeny", def_sfxPurchaseDenyVol), 0f, 3f);
        sfxChain2Vol = Mathf.Clamp(OptPref("M3_opt_sfxChain2", def_sfxChain2Vol), 0f, 3f);
        sfxChain3Vol = Mathf.Clamp(OptPref("M3_opt_sfxChain3", def_sfxChain3Vol), 0f, 3f);
        sfxChain4Vol = Mathf.Clamp(OptPref("M3_opt_sfxChain4", def_sfxChain4Vol), 0f, 3f);
        sfxBlackHoleSuckVol = Mathf.Clamp(OptPref("M3_opt_sfxBlackHoleSuck", def_sfxBlackHoleSuckVol), 0f, 3f);
        bgmVolume = Mathf.Clamp(OptPref("M3_opt_bgmVol", def_bgmVolume), VolumeMin, VolumeMax);
    }

    void ResetOptionDefaults()
    {
        sfxVolume = Mathf.Clamp(def_sfxVolume, VolumeMin, VolumeMax); cellSize = def_cellSize; gravity = def_gravity; swapTime = def_swapTime; clearTime = def_clearTime;
        popDelay = def_popDelay; stoneSize = def_stoneSize; shapeDensity = def_shapeDensity; stoneChance = def_stoneChance; demoInterval = def_demoInterval; holeBlockScale = def_holeBlockScale;
        sfxSwapVol = def_sfxSwapVol; sfxPopVol = def_sfxPopVol; sfxStoneBreakVol = def_sfxStoneBreakVol; sfxBlockedVol = def_sfxBlockedVol; sfxStoneWiggleVol = def_sfxStoneWiggleVol; sfxStoneCrackVol = def_sfxStoneCrackVol;
        sfxHintVol = def_sfxHintVol; sfxWinVol = def_sfxWinVol; sfxLevelStartVol = def_sfxLevelStartVol; sfxUiClickVol = def_sfxUiClickVol; sfxWhooshVol = def_sfxWhooshVol; sfxBeamVol = def_sfxBeamVol;
        sfxGreenHealVol = def_sfxGreenHealVol;
        sfxPurpleArmVol = def_sfxPurpleArmVol; sfxPurpleTransmuteVol = def_sfxPurpleTransmuteVol;
        sfxZapVol = def_sfxZapVol; sfxHealLandVol = def_sfxHealLandVol; sfxCoinPopVol = def_sfxCoinPopVol; sfxStarFillVol = def_sfxStarFillVol;
        sfxCrossBlastVol = def_sfxCrossBlastVol; sfxMegaCrossBlastVol = def_sfxMegaCrossBlastVol; sfxPurchaseOkVol = def_sfxPurchaseOkVol; sfxPurchaseDenyVol = def_sfxPurchaseDenyVol;
        sfxChain2Vol = def_sfxChain2Vol; sfxChain3Vol = def_sfxChain3Vol; sfxChain4Vol = def_sfxChain4Vol;
        sfxBlackHoleSuckVol = def_sfxBlackHoleSuckVol;
        bgmVolume = Mathf.Clamp(def_bgmVolume, VolumeMin, VolumeMax);
        for (int i = 0; i < tileSpriteScales.Length && i < def_tileSpriteScales.Length; i++) tileSpriteScales[i] = def_tileSpriteScales[i];
        for (int i = 0; i < levelBackgroundScales.Length && i < def_levelBackgroundScales.Length; i++) levelBackgroundScales[i] = def_levelBackgroundScales[i];
        DeleteOptionPrefs();
    }

    void DeleteOptionPrefs()
    {
        PlayerPrefs.DeleteKey("M3_opt_sfxVolume");
        PlayerPrefs.DeleteKey("M3_opt_cellSize");
        PlayerPrefs.DeleteKey("M3_opt_gravity");
        PlayerPrefs.DeleteKey("M3_opt_swapTime");
        PlayerPrefs.DeleteKey("M3_opt_clearTime");
        PlayerPrefs.DeleteKey("M3_opt_popDelay");
        PlayerPrefs.DeleteKey("M3_opt_stoneSize");
        PlayerPrefs.DeleteKey("M3_opt_shapeDensity");
        PlayerPrefs.DeleteKey("M3_opt_stoneChance");
        PlayerPrefs.DeleteKey("M3_opt_demoInterval");
        PlayerPrefs.DeleteKey("M3_opt_holeBlockScale");
        for (int i = 0; i < tileSpriteScales.Length; i++) PlayerPrefs.DeleteKey("M3_opt_tileScale_" + i);
        for (int i = 0; i < levelBackgroundScales.Length; i++) PlayerPrefs.DeleteKey("M3_opt_bgScale_" + i);
        PlayerPrefs.DeleteKey("M3_opt_sfxSwap");
        PlayerPrefs.DeleteKey("M3_opt_sfxPop");
        PlayerPrefs.DeleteKey("M3_opt_sfxStoneBreak");
        PlayerPrefs.DeleteKey("M3_opt_sfxBlocked");
        PlayerPrefs.DeleteKey("M3_opt_sfxStoneWiggle");
        PlayerPrefs.DeleteKey("M3_opt_sfxStoneCrack");
        PlayerPrefs.DeleteKey("M3_opt_sfxHint");
        PlayerPrefs.DeleteKey("M3_opt_sfxWin");
        PlayerPrefs.DeleteKey("M3_opt_sfxLevelStart");
        PlayerPrefs.DeleteKey("M3_opt_sfxUiClick");
        PlayerPrefs.DeleteKey("M3_opt_sfxWhoosh");
        PlayerPrefs.DeleteKey("M3_opt_sfxBeam");
                PlayerPrefs.DeleteKey("M3_opt_sfxGreenHeal");
        PlayerPrefs.DeleteKey("M3_opt_sfxPurpleArm");
        PlayerPrefs.DeleteKey("M3_opt_sfxPurpleTransmute");
        PlayerPrefs.DeleteKey("M3_opt_sfxZap");
        PlayerPrefs.DeleteKey("M3_opt_sfxHealLand");
        PlayerPrefs.DeleteKey("M3_opt_sfxCoinPop");
        PlayerPrefs.DeleteKey("M3_opt_sfxStarFill");
        PlayerPrefs.DeleteKey("M3_opt_sfxCrossBlast");
        PlayerPrefs.DeleteKey("M3_opt_sfxMegaCrossBlast");
        PlayerPrefs.DeleteKey("M3_opt_sfxPurchaseOk");
        PlayerPrefs.DeleteKey("M3_opt_sfxPurchaseDeny");
        PlayerPrefs.DeleteKey("M3_opt_sfxChain2");
        PlayerPrefs.DeleteKey("M3_opt_sfxChain3");
        PlayerPrefs.DeleteKey("M3_opt_sfxChain4");
        PlayerPrefs.DeleteKey("M3_opt_sfxBlackHoleSuck");
        PlayerPrefs.DeleteKey("M3_opt_bgmVol");
        PlayerPrefs.Save();
    }

    void SaveCharges()
    {
        PlayerPrefs.SetInt("M3_goldCross", crossBlasts);
        PlayerPrefs.SetInt("M3_goldMega", megaCrossBlasts);
        PlayerPrefs.Save();
    }

    void DeleteAllProgress()
    {
        PlayerPrefs.DeleteKey("M3_unlocked");
        PlayerPrefs.DeleteKey("M3_totalGold");
        PlayerPrefs.DeleteKey("M3_hiddenGold");
        PlayerPrefs.DeleteKey("M3_goldCross");
        PlayerPrefs.DeleteKey("M3_goldMega");
        for (int lvl = 1; lvl <= totalLevels; lvl++)
        {
            PlayerPrefs.DeleteKey("M3_score_" + lvl);
            PlayerPrefs.DeleteKey("M3_moves_" + lvl);
            PlayerPrefs.DeleteKey("M3_time_" + lvl);
        }
        ResetDailyProgress();
        PlayerPrefs.Save();

        unlockedLevel = 1;
        totalGold = 0f; levelGold = 0f; lastStarBonus = 0f;
        hiddenGold = 0f;
        crossBlasts = 0; megaCrossBlasts = 0;
        deleteProgressArmed = false;
        Debug.Log("Match3: all progress deleted");
    }

}

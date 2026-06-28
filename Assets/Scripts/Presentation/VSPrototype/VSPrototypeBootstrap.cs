// VSプロト起動エントリ：Phase 管理＋メタ進行ロード／セーブ＋ラン進行管理＋演出＋内政フェーズ。
//
// 【使い方】
// 1. Unity Editor で Assets/Scenes/EcholosProto_VS.unity を開く
// 2. 空 GameObject に以下5コンポーネントをアタッチ：
//    - VSPrototypeBootstrap（このスクリプト）
//    - VSPrototypeMapGUI（ラン中＝Phase.Run 時のマップ描画）
//    - VSPrototypeInteriorGUI（Phase.InitialDraft / InteriorAction 時の内政描画）
//    - VSPrototypeStoryGUI（Phase.StoryEvent 時の演出描画）
//    - MetaHubGUI（メタ拠点＝Phase.Hub 時の強化選択画面）
//
// 【Phase 設計】
// - Hub           ：メタ拠点。次ラン強化選択 → StartNewRun() で InitialDraft へ
// - InitialDraft  ：ラン開始時の初期ドラフト（5回＋メタ強化分）。全消費後に R1 InteriorAction へ
// - InteriorAction：R1〜R6 内政フェーズ。行動力で召集／ユニット強化 → 「内政終了」で Run へ
// - Run           ：配置＋戦闘実行。R7 終了 or 本拠地崩壊 → StoryEvent（エンディング）へ
// - Battle        ：戦闘再生中
// - StoryEvent    ：各種演出（ラウンド開始時／ブリジット／エンディング／プロローグ
//                   をすべて統合）。再生中のシーン ID は StoryProgress に持ち、完了時の挙動は
//                   _onStoryComplete コールバックで分岐
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Data;
using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Battle.Synergy;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Catalog;
using Echolos.Domain.Effects;
using Echolos.Domain.Meta;
using Echolos.Domain.Models;
using Echolos.Domain.Save;
using Echolos.UseCase.VSPrototype;
using Echolos.UseCase.Demo;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;

namespace Echolos.Presentation.VSPrototype
{
    /// <summary>
    /// 内政フェーズ中の UI サブモード（Phase 1：MapGUI 一体化）。
    /// MapGUI が常時マップを描画し、サブモーダルとして召集ドラフト or 強化リストを重ねるための状態。
    /// </summary>
    public enum VSPrototypeInteriorSubMode
    {
        /// <summary>メインメニュー（行動力で次の選択待ち）。MapGUI 右パネルから召集/強化ボタンで遷移。</summary>
        None,
        /// <summary>召集ドラフトカード表示中。</summary>
        Conscript,
        /// <summary>兵種強化リスト表示中。</summary>
        Upgrade,
    }

    /// <summary>VSプロトのフェーズ。GUI は自身の描画可否をここから判定する。</summary>
    public enum VSPrototypePhase
    {
        /// <summary>タイトル画面：起動直後に表示。ゲームスタートボタンでセーブ有無別に分岐遷移。</summary>
        Title,
        /// <summary>メタ拠点：次ラン強化選択画面。</summary>
        Hub,
        /// <summary>ラン開始時の初期ドラフト（5回＋メタ強化分の連続ドラフト）。</summary>
        InitialDraft,
        /// <summary>R1〜R6 内政フェーズ。行動力で召集／ユニット強化を実行。</summary>
        InteriorAction,
        /// <summary>ラン中：戦略マップ画面で配置＋戦闘実行。</summary>
        Run,
        /// <summary>戦闘再生中：VSPrototypeBattleGUI が単一戦闘を再生・複数戦闘を順次連鎖。</summary>
        Battle,
        /// <summary>
        /// 各種演出：ラウンド開始時（B-a/b1/b2/c・B-e SwordEmpowered・A-c1/c2）／
        /// バルドゥイン拠点解放直後（BalduinRescue・本ラン初回のみ）／エンディング（Defeat/True）／プロローグ（A-a）。
        /// 再生中のシーン内容は <see cref="StoryProgress"/> に格納され、完了挙動は内部コールバックで分岐。
        /// </summary>
        StoryEvent,
    }

    /// <summary>
    /// 戦闘再生キューの 1 要素。BattleGUI が順次再生する単一戦闘＋表示タイトル（「自領 中央」「本拠地 連続防衛 1/3」等）。
    /// </summary>
    public sealed class VSPrototypeBattleSegment
    {
        public string Title { get; }
        public BattleReport Report { get; }

        public VSPrototypeBattleSegment(string title, BattleReport report)
        {
            Title = title;
            Report = report;
        }
    }

    /// <summary>VSプロト Scene のエントリポイント。Phase 管理＋ラン進行＋演出＋内政の制御。</summary>
    public sealed class VSPrototypeBootstrap : MonoBehaviour, IBattleReplayHost
    {
        private VSPrototypeMapState _mapState;
        private List<Unit> _roster;
        private System.Random _rng;

        private VSPrototypeDraftService _draftService;
        private VSPrototypeInteriorService _interiorService;
        private VSPrototypeEndingResolver _endingResolver;
        private VSPrototypeEnemyPatterns _enemyPatterns;
        private VSPrototypeRoundManager _roundManager;
        private IMetaUpgradeCatalog _metaUpgradeCatalog;
        private IUnitCatalog _unitCatalog;
        private IWazaCatalog _wazaCatalog;
        private IUnitUpgradeCatalog _upgradeCatalog;
        private IDraftPoolCatalog _draftPoolCatalog;
        private IStorySceneCatalog _storySceneCatalog;
        private ISaveStore _saveStore;
        private MetaProgressStore _metaProgressStore;
        // 試遊モード（DemoMode）進行制御。通常モードでは NullDemoFlowController で no-op。
        // 試遊シーン起動時のみ StartDemoMode 経由で DemoFlowController に差し替える。
        private IDemoFlowController _demo = new NullDemoFlowController();

        /// <summary>試遊モード進行制御への読み取りアクセス（GUI 層から目的バー等で参照）。</summary>
        public IDemoFlowController Demo => _demo;

        /// <summary>領地マップの全体状態（Phase=Run 時のみ意味を持つ）。</summary>
        public VSPrototypeMapState MapState => _mapState;

        /// <summary>ラウンド進行サービス（GUI 側から CanAssign / CountFallenFronts 等の判定にアクセスするため公開）。</summary>
        public VSPrototypeRoundManager RoundManager => _roundManager;

        /// <summary>メタ拠点強化カタログ（MetaHubGUI から GetAll で 3 項目描画する用）。</summary>
        public IMetaUpgradeCatalog MetaUpgradeCatalog => _metaUpgradeCatalog;

        /// <summary>ドラフトプールカタログ（MapGUI が王国軍リスト・配置モーダルの表示順正規化に使用）。</summary>
        public IDraftPoolCatalog DraftPoolCatalog => _draftPoolCatalog;

        /// <summary>ユニットカタログ（MetaHubGUI が王女/ブリジットの AvailableUpgrades を引くのに使用）。</summary>
        public IUnitCatalog UnitCatalog => _unitCatalog;

        /// <summary>プレイヤーの王国軍（解禁済固有ユニットを含む。召集で増える）。</summary>
        public IList<Unit> Roster => _roster;

        /// <summary>ラン外で永続するメタ進行状態（起動時にロード／FinishCurrentRun で永続化）。</summary>
        public MetaProgressState Meta { get; private set; }

        /// <summary>
        /// 本ラン開始時点の <see cref="MetaProgressState.HasFirstReachedBoss"/> スナップショット。
        /// Defeat 演出 3 分割の振り分けで「本ラン中に新たに R7 到達した」判定に使う。
        /// ラン中に R7 敗北が確定すると Meta.MarkFirstReachedBoss() で現在値が true 化されるため、
        /// 「スナップ false ＆ 現在 true」=本ランで初到達と判定できる。
        /// </summary>
        private bool _hadFirstReachedBossAtRunStart;

        /// <summary>現在のフェーズ。GUI 側はこれを見て自身の描画可否を決める。</summary>
        public VSPrototypePhase CurrentPhase { get; private set; }

        /// <summary>現在のラン中のラウンド番号（1〜MaxRounds）。Hub 中も最後の値が残る。</summary>
        public int CurrentRound { get; private set; }

        /// <summary>直近の解決済みラウンド結果。Phase=Run 中の戦闘実行後に Map GUI が参照。</summary>
        public VSPrototypeRoundResult LastRoundResult { get; private set; }

        /// <summary>演出ページ進捗（Phase=StoryEvent 中の共通進捗）。</summary>
        public StoryProgress StoryProgress { get; private set; }

        /// <summary>StoryScene カタログへの開発用直接アクセス（DevTools/StoryViewerGUI 等から使用）。</summary>
        public IStorySceneCatalog StorySceneCatalog => _storySceneCatalog;

        /// <summary>直近のラン獲得通貨量（Hub 表示用）。</summary>
        public int LastRunReward { get; private set; }

        /// <summary>直近のラン到達エンディング種別（Hub 表示用）。</summary>
        public VSPrototypeEndingKind LastEnding { get; private set; }

        /// <summary>内政・初期ドラフトの状態。</summary>
        public VSPrototypeInteriorState InteriorState { get; private set; }

        /// <summary>表示中のドラフトオファー（InitialDraft または InteriorAction 中の Conscript で生成）。</summary>
        public VSPrototypeDraftOffer CurrentDraftOffer { get; private set; }

        /// <summary>
        /// 内政フェーズ中のサブモード。MapGUI が右パネルから召集/強化ボタンでサブモードを起動し、
        /// InteriorGUI がモーダル描画する。
        /// </summary>
        public VSPrototypeInteriorSubMode CurrentInteriorSubMode { get; private set; }

        // 戦闘再生キュー。Phase=Battle 中のみ保持。
        // BattleGUI が CurrentBattleSegment を再生し、完了で AdvanceToNextBattleSegment / FinishBattleReplay を呼ぶ。
        private List<VSPrototypeBattleSegment> _battleSegments;

        /// <summary>戦闘再生キュー全体（Phase=Battle 中のみ非 null）。</summary>
        public IReadOnlyList<VSPrototypeBattleSegment> BattleSegments => _battleSegments;

        /// <summary>現在再生中の戦闘の index（0-based）。Phase!=Battle 時は意味を持たない。</summary>
        public int CurrentBattleIndex { get; private set; }

        /// <summary>現在再生中の戦闘セグメント（範囲外なら null）。</summary>
        public VSPrototypeBattleSegment CurrentBattleSegment =>
            _battleSegments != null && CurrentBattleIndex >= 0 && CurrentBattleIndex < _battleSegments.Count
                ? _battleSegments[CurrentBattleIndex]
                : null;

        // 演出完了時のコールバック（Phase ごとに付け替える）
        private Action _onStoryComplete;

        private void Awake()
        {
            // Domain.Catalog / Save 抽象を Data 実装でインスタンス化（Composition Root）
            _saveStore = new PlayerPrefsSaveStore();
            _metaProgressStore = new MetaProgressStore(_saveStore);
            Meta = _metaProgressStore.Load();
            // 起動直後はタイトル画面。ゲームスタート押下時に StartFromTitle() で
            // セーブ有無別に Hub / 初回プレイ（即ラン開始）へ分岐する。
            CurrentPhase = VSPrototypePhase.Title;
            _rng = new System.Random();
            _wazaCatalog = new WazaCatalog();
            _upgradeCatalog = new UnitUpgradeCatalog();
            _unitCatalog = new UnitCatalog(_wazaCatalog, _upgradeCatalog);
            _draftPoolCatalog = new DraftPoolCatalog();
            _draftService = new VSPrototypeDraftService(() => _rng.Next(), _draftPoolCatalog, _unitCatalog);
            _interiorService = new VSPrototypeInteriorService();
            _endingResolver = new VSPrototypeEndingResolver();
            _enemyPatterns = new VSPrototypeEnemyPatterns(_unitCatalog, () => _rng.Next());
            IMetaRewardFormulaCatalog rewardFormulaCatalog = new MetaRewardFormulaCatalog();
            _roundManager = new VSPrototypeRoundManager(_endingResolver, _enemyPatterns, rewardFormulaCatalog, Meta);
            _metaUpgradeCatalog = new MetaUpgradeCatalog();
            _storySceneCatalog = new StorySceneCatalog();
            InteriorState = new VSPrototypeInteriorState();
            StoryProgress = new StoryProgress();
            // MapState/Roster はラン開始時に生成（StartNewRun で）
        }

        // Title → Hub / 初回ラン

        /// <summary>
        /// タイトル画面の「ゲームスタート」ボタンから呼ばれる総合エントリ：
        ///   - セーブデータあり（2 周目以降）→ Phase=Hub（メタ拠点）
        ///   - セーブデータなし（初回プレイ）→ <see cref="StartNewRun"/> 直行（A-a プロローグ→固定構成）
        /// 初回プレイの「固定構成スタート」は既存 <see cref="StartNewRunCore"/> 内の
        /// <c>Meta.RunCount == 0</c> 分岐がそのまま機能する。
        /// </summary>
        public void StartFromTitle()
        {
            if (_metaProgressStore.HasSaveData())
            {
                CurrentPhase = VSPrototypePhase.Hub;
            }
            else
            {
                StartNewRun();
            }
        }

        /// <summary>
        /// メタ拠点（Hub）からタイトル画面に戻る。メタ進行は既にラン終了時の
        /// <see cref="FinishCurrentRun"/> で MetaProgressStore.Save 済みなので、ここでは Phase 遷移のみ。
        /// タイトルから再度「ゲームスタート」で <see cref="StartFromTitle"/> 経由で Hub に戻れる。
        /// </summary>
        public void ReturnToTitleFromHub()
        {
            if (CurrentPhase != VSPrototypePhase.Hub) return;
            CurrentPhase = VSPrototypePhase.Title;
        }

        /// <summary>
        /// 【開発用】指定 StorySceneId をその場で再生する（Debug_StoryViewer 用）。
        /// <paramref name="treatAsSeen"/>=false なら必ず本文ページを再生／true なら短縮ナレ
        /// 1 ページを再生する。<see cref="BeginStorySceneById"/> を経由せず直接 <see cref="BeginStory"/>
        /// を呼ぶことで Meta.HasSeenStoryScene の状態に依らず指定通りの再生を行い、
        /// 同時に <see cref="MetaProgressState.MarkStorySceneSeen"/> も実行しない（連続確認のため
        /// Meta を汚さない）。再生完了で <see cref="VSPrototypePhase.Title"/> に戻す。
        /// </summary>
        public void DevPlayStoryScene(string sceneId, bool treatAsSeen)
        {
            if (string.IsNullOrEmpty(sceneId)) return;
            if (_storySceneCatalog == null) return;
            if (!_storySceneCatalog.IsRegistered(sceneId)) return;
            var scene = _storySceneCatalog.Get(sceneId);
            if (scene == null) return;

            IReadOnlyList<StoryPage> pages;
            if (treatAsSeen && !string.IsNullOrEmpty(scene.RepeatNarration))
            {
                pages = new[] {
                    new StoryPage(
                        imagePath: string.Empty,
                        narrationText: scene.RepeatNarration,
                        fadeInSeconds: 0.3f,
                        displaySeconds: 0f,
                        fadeOutSeconds: 0.3f,
                        fallbackImagePath: string.Empty),
                };
            }
            else
            {
                pages = scene.Pages;
            }

            BeginStory(pages, onComplete: ReturnToTitleAfterDevPlay);
            CurrentPhase = VSPrototypePhase.StoryEvent;
        }

        private void ReturnToTitleAfterDevPlay()
        {
            CurrentPhase = VSPrototypePhase.Title;
        }

        /// <summary>
        /// 【開発用】Debug_StoryViewer 用の進行リセット。<see cref="ResetAllProgress"/> と
        /// 同等の Meta クリアを実行したうえで Phase=Title を維持する（通常リセットは Hub に遷移する）。
        /// </summary>
        public void DevResetProgressForStoryViewer()
        {
            _metaProgressStore.DeleteAll();
            Meta.LoadFromSerializedState(
                memories: 0, runCount: 0, hasReachedTrueEnd: false,
                unlockedUnits: null, appliedUpgrades: null,
                seenStorySceneIds: null);
            CurrentPhase = VSPrototypePhase.Title;
        }

        // Hub ⇔ Run の遷移

        /// <summary>
        /// 新しいランを開始する：MapState＋王国軍（王女＋解禁ブリジット）を初期化し、
        /// 内政状態をメタ強化反映で初期化、初期ドラフトオファーを準備して Phase=InitialDraft へ。
        /// メタ拠点の「次のランへ」ボタンから呼ばれる。
        /// </summary>
        public void StartNewRun()
        {
            // 1 周目のラン開始は A-a プロローグ演出から入る。SO 不在時はフォールバックで本体処理へ直進。
            if (TryBeginOpeningEvent()) return;
            StartNewRunCore();
        }

        /// <summary>
        /// 1 周目（<see cref="MetaProgressState.RunCount"/>==0）のラン開始時に A-a プロローグ演出を
        /// 流す。発火後の演出完了で <see cref="StartNewRunCore"/> に進む。SO 不在時 / 2 周目以降は
        /// false を返してフォールバック。
        /// </summary>
        private bool TryBeginOpeningEvent()
        {
            if (Meta.RunCount != 0) return false;
            if (_storySceneCatalog == null) return false;
            if (!_storySceneCatalog.IsRegistered(VSPrototypeStorySceneIds.Opening)) return false;
            var scene = _storySceneCatalog.Get(VSPrototypeStorySceneIds.Opening);
            if (scene == null || scene.Pages == null || scene.Pages.Count == 0) return false;

            BeginStorySceneById(scene, onComplete: StartNewRunCore);
            CurrentPhase = VSPrototypePhase.StoryEvent;
            return true;
        }

        /// <summary>
        /// ラン開始本体処理。MapState/Roster/InteriorState 初期化＋ Phase 遷移。
        /// A-a プロローグ演出があれば完了コールバックから、無ければ <see cref="StartNewRun"/> から直接呼ばれる。
        /// </summary>
        private void StartNewRunCore()
        {
            // Defeat 3 分割の「本ラン中に R7 初到達」判定用にスナップショット。
            _hadFirstReachedBossAtRunStart = Meta.HasFirstReachedBoss;

            _mapState = new VSPrototypeMapState(Meta.HasRescuedBalduin);
            var princess = _unitCatalog.Get(UniqueUnitIds.Princess);
            ApplyMetaUpgradeChoices(princess, UniqueUnitIds.Princess);
            _roster = new List<Unit> { princess };

            // 解禁済固有ユニットを追加（ブリジット永続加入）。
            if (Meta.IsUnitUnlocked(UniqueUnitIds.Bridget))
            {
                var bridget = _unitCatalog.Get(UniqueUnitIds.Bridget);
                ApplyMetaUpgradeChoices(bridget, UniqueUnitIds.Bridget);
                _roster.Add(bridget);
            }

            // 内政状態を初期化（メタ強化反映）
            int actionPointsPerRound = 2 + Meta.GetUpgradeLevel(MetaUpgradeIds.ActionPoints);
            int initialDraftCount = VSPrototypeInteriorState.BaseInitialDraftCount
                + Meta.GetUpgradeLevel(MetaUpgradeIds.InitialUnit);

            // 1 周目は初期ドラフトをスキップして固定構成で開始。
            // プレイヤーが「与えられたユニットの配置を考える」ことに集中できるよう、
            // 編成判断ゲーム（ドラフト）は召集コマンドで提供する設計。
            bool isFirstRun = Meta.RunCount == 0;
            if (isFirstRun)
            {
                foreach (var unitId in VSPrototypeFirstRunFixedRoster.UnitIds)
                {
                    var unit = _unitCatalog.Get(unitId);
                    if (unit != null) _roster.Add(unit);
                }
                initialDraftCount = 0;
            }

            InteriorState.InitializeForRun(actionPointsPerRound, initialDraftCount);

            CurrentRound = 0; // R1 はドラフト完了後に開始
            LastRoundResult = null;
            LastRunReward = 0;
            LastEnding = VSPrototypeEndingKind.None;
            CurrentDraftOffer = null;

            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;

            // 初期ドラフトが必要なら Phase=InitialDraft、不要なら（=0 設定時）直接 R1 内政フェーズへ
            if (InteriorState.HasInitialDraftRemaining)
            {
                CurrentDraftOffer = _draftService.DrawForInitial();
                CurrentPhase = VSPrototypePhase.InitialDraft;
            }
            else
            {
                BeginRoundInteriorPhase(1);
            }
        }

        /// <summary>
        /// テストプレイ用：初期ドラフトをスキップして固定構成でラン開始する。バランス調整／戦闘層の検証用。
        /// R1 内政フェーズに直接遷移する。構成は 1 周目スキップと同じ <see cref="VSPrototypeFirstRunFixedRoster.UnitIds"/> を使う（Single Source of Truth）。
        /// </summary>
        public void StartNewRunWithDefaultRoster()
        {
            // StartNewRunCore と同じく Defeat 3 分割判定用スナップを取る。
            _hadFirstReachedBossAtRunStart = Meta.HasFirstReachedBoss;

            _mapState = new VSPrototypeMapState(Meta.HasRescuedBalduin);
            var princess = _unitCatalog.Get(UniqueUnitIds.Princess);
            ApplyMetaUpgradeChoices(princess, UniqueUnitIds.Princess);
            _roster = new List<Unit> { princess };
            foreach (var unitId in VSPrototypeFirstRunFixedRoster.UnitIds)
            {
                var unit = _unitCatalog.Get(unitId);
                if (unit != null) _roster.Add(unit);
            }
            if (Meta.IsUnitUnlocked(UniqueUnitIds.Bridget))
            {
                var bridget = _unitCatalog.Get(UniqueUnitIds.Bridget);
                ApplyMetaUpgradeChoices(bridget, UniqueUnitIds.Bridget);
                _roster.Insert(1, bridget);
            }

            int actionPointsPerRound = 2 + Meta.GetUpgradeLevel(MetaUpgradeIds.ActionPoints);
            // 固定スタートなので初期ドラフトはスキップ（initialDraftCount=0）
            InteriorState.InitializeForRun(actionPointsPerRound, initialDraftCount: 0);

            CurrentRound = 0;
            LastRoundResult = null;
            LastRunReward = 0;
            LastEnding = VSPrototypeEndingKind.None;
            CurrentDraftOffer = null;
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;

            // 直接 R1 内政フェーズへ
            BeginRoundInteriorPhase(1);
        }

        /// <summary>
        /// 試遊モード用：DemoSaveDefinition のスナップショットから状態を一括復元してラン開始する。
        /// メタ進行・マップ状態・初期手駒を save 値で上書きしたうえで、save.StartRound の内政フェーズへ直進。
        /// initialRoster が空なら StartNewRunWithDefaultRoster と同じ固定構成スタートにフォールバック。
        /// </summary>
        public void StartNewRunFromDemoSave(DemoSaveDefinition save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            // デモは Defeat 3 分割（初回 7R 到達演出）の対象外。常に false で開始。
            _hadFirstReachedBossAtRunStart = false;

            // メタ進行を save 内容で上書き
            Meta.LoadFromSerializedState(
                memories: save.Memories,
                runCount: 0,
                hasReachedTrueEnd: false,
                unlockedUnits: save.UnlockedUnits,
                appliedUpgrades: ToMutableUpgrades(save.AppliedUpgrades),
                hasFirstReachedBoss: false,
                hasRescuedBalduin: save.HasRescuedBalduin,
                hasNotedPendantPower: save.HasNotedPendantPower,
                appliedUpgradeChoices: ToMutableChoices(save.AppliedUpgradeChoices),
                seenStorySceneIds: null);

            // マップ状態構築
            _mapState = new VSPrototypeMapState(save.HasRescuedBalduin);
            foreach (var entry in save.NodeStates)
            {
                var node = _mapState.GetNode(entry.Col, entry.Layer);
                if (entry.IsCaptured) node.Capture();
                if (entry.IsFallen) node.MarkFallen();
            }
            if (save.IsBridgetRescued) _mapState.MarkBridgetRescued();
            if (save.IsBalduinRescuePlayed) _mapState.MarkBalduinRescuePlayed();

            // 初期手駒構築
            _roster = new List<Unit>();
            if (save.InitialRoster.Count > 0)
            {
                // セーブで明示的に手駒指定（Level 含む）
                foreach (var entry in save.InitialRoster)
                {
                    var unit = _unitCatalog.Get(entry.UnitId);
                    if (unit == null) continue;
                    ApplyLevelUpgrades(unit, entry.Level);
                    _roster.Add(unit);
                }
            }
            else
            {
                // セーブが空ロスター → 通常の固定構成スタート（StartNewRunWithDefaultRoster と同等）
                var princess = _unitCatalog.Get(UniqueUnitIds.Princess);
                ApplyMetaUpgradeChoices(princess, UniqueUnitIds.Princess);
                _roster.Add(princess);
                foreach (var unitId in VSPrototypeFirstRunFixedRoster.UnitIds)
                {
                    var unit = _unitCatalog.Get(unitId);
                    if (unit != null) _roster.Add(unit);
                }
                if (Meta.IsUnitUnlocked(UniqueUnitIds.Bridget))
                {
                    var bridget = _unitCatalog.Get(UniqueUnitIds.Bridget);
                    ApplyMetaUpgradeChoices(bridget, UniqueUnitIds.Bridget);
                    _roster.Insert(1, bridget);
                }
            }

            int actionPointsPerRound = 2 + Meta.GetUpgradeLevel(MetaUpgradeIds.ActionPoints);
            InteriorState.InitializeForRun(actionPointsPerRound, initialDraftCount: 0);

            CurrentRound = 0;
            LastRoundResult = null;
            LastRunReward = 0;
            LastEnding = VSPrototypeEndingKind.None;
            CurrentDraftOffer = null;
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;

            BeginRoundInteriorPhase(save.StartRound);
        }

        // DemoSaveDefinition の読み取り専用辞書を MetaProgressState.LoadFromSerializedState 用の可変辞書に変換
        private static Dictionary<string, int> ToMutableUpgrades(
            System.Collections.Generic.IReadOnlyDictionary<string, int> src)
        {
            var dst = new Dictionary<string, int>();
            if (src == null) return dst;
            foreach (var kv in src) dst[kv.Key] = kv.Value;
            return dst;
        }

        private static Dictionary<string, List<string>> ToMutableChoices(
            System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> src)
        {
            var dst = new Dictionary<string, List<string>>();
            if (src == null) return dst;
            foreach (var kv in src) dst[kv.Key] = new List<string>(kv.Value);
            return dst;
        }

        // DemoSave.InitialRoster の Level 指定を Unit.AvailableUpgrades → AppliedUpgrades に反映する
        // （通常の Lv 強化フローと同じ「先頭から順に AppliedUpgrades へ移す」と等価）。
        private static void ApplyLevelUpgrades(Unit unit, int targetLevel)
        {
            int needed = targetLevel - unit.Level;
            for (int i = 0; i < needed; i++)
            {
                if (unit.AvailableUpgrades.Count == 0) break;
                var upgrade = unit.AvailableUpgrades[0];
                unit.AvailableUpgrades.Remove(upgrade);
                unit.AppliedUpgrades.Add(upgrade);
                unit.Level++;
            }
        }

        // メタ強化「王女 Lv 強化」「ブリジット Lv 強化」で選択された Upgrade ID 列を、
        // ラン開始時のユニット生成直後に AppliedUpgrades へ移しつつ Unit.Level を上げる
        // （味方の通常 Lv 強化と同じ「AvailableUpgrades から 1 件選択して AppliedUpgrades へ」と同等の処理）。
        private void ApplyMetaUpgradeChoices(Unit unit, string unitId)
        {
            var chosenIds = Meta.GetUpgradeChoices(unitId);
            foreach (var upgradeId in chosenIds)
            {
                var upgrade = unit.AvailableUpgrades.Find(u => u.UpgradeId == upgradeId);
                if (upgrade == null) continue; // 万一 ID 不一致なら無視（SO 再生成等の整合性ずれ）
                unit.AvailableUpgrades.Remove(upgrade);
                unit.AppliedUpgrades.Add(upgrade);
                unit.Level++;
            }
        }

        /// <summary>開発用：ラン途中でメタ拠点に戻る（ラン結果は捨てる・メタ通貨も加算しない）。</summary>
        public void AbandonRunAndReturnToHub()
        {
            _metaProgressStore.Save(Meta);
            CurrentPhase = VSPrototypePhase.Hub;
        }

        /// <summary>
        /// 開発用：メタ進行を含めて完全リセット。
        /// Meta インスタンスは再代入せず in-place で初期化する（RoundManager 等の DI 注入された
        /// IMetaProgressView 参照が切れないようにするため）。
        /// </summary>
        public void ResetAllProgress()
        {
            _metaProgressStore.DeleteAll();
            Meta.LoadFromSerializedState(
                memories: 0, runCount: 0, hasReachedTrueEnd: false,
                unlockedUnits: null, appliedUpgrades: null,
                seenStorySceneIds: null);
            CurrentPhase = VSPrototypePhase.Hub;
        }

        // 初期ドラフト

        /// <summary>
        /// 初期ドラフトの候補1体を選んで王国軍に加える。
        /// 残数があれば次オファーを生成、なければ R1 内政フェーズへ自動遷移。
        /// </summary>
        public bool AcceptInitialDraftPick(int candidateIndex)
        {
            if (CurrentPhase != VSPrototypePhase.InitialDraft) return false;
            if (CurrentDraftOffer == null) return false;

            var picked = _draftService.AcceptPick(InteriorState, CurrentDraftOffer, candidateIndex);
            if (picked == null) return false;

            _roster.Add(picked);
            InteriorState.ConsumeInitialDraft();

            if (InteriorState.HasInitialDraftRemaining)
            {
                CurrentDraftOffer = _draftService.DrawForInitial();
            }
            else
            {
                CurrentDraftOffer = null;
                BeginRoundInteriorPhase(1);
            }
            return true;
        }

        // 内政フェーズ

        /// <summary>
        /// 指定ラウンドの内政フェーズに入る：MapState の StartRound＋InteriorState の Reset＋Phase=InteriorAction。
        /// R7 は内政フェーズをスキップして直接 Phase=Run へ。
        /// 本体処理に入る前にラウンド開始時演出（B-a/b1/b2/c・B-e SwordEmpowered・A-c1/c2）の発火条件を判定し、
        /// 該当があれば <see cref="VSPrototypePhase.StoryEvent"/> 経由で先に演出を流す。
        /// </summary>
        private void BeginRoundInteriorPhase(int round)
        {
            CurrentRound = round;
            if (TryBeginRoundStartEvent(round)) return;
            ContinueRoundInteriorPhase(round);
        }

        /// <summary>
        /// ラウンド開始時演出の発火条件を判定し、該当シーン ID があれば演出フェーズへ突入する。
        /// 戻り値 true で演出開始（呼び出し側は <see cref="ContinueRoundInteriorPhase"/> を呼ばない）／
        /// false で「演出なし」（呼び出し側がそのまま本体処理へ進む）。
        /// SO アセット未登録時は false を返してフォールバック。
        /// </summary>
        private bool TryBeginRoundStartEvent(int round)
        {
            if (_storySceneCatalog == null) return false;
            var sceneId = VSPrototypeRoundStartEventResolver.Resolve(round, Meta, _mapState);
            if (sceneId == null) return false;
            if (!_storySceneCatalog.IsRegistered(sceneId)) return false;
            var scene = _storySceneCatalog.Get(sceneId);
            if (scene == null || scene.Pages == null || scene.Pages.Count == 0) return false;

            // B-e SwordEmpowered は完了時にペンダント気づきフラグをセット（1 シーンで気づき＋強化を描く）
            if (sceneId == VSPrototypeStorySceneIds.SwordEmpowered)
            {
                BeginStorySceneById(scene, () => OnSwordEmpoweredCompleted(round));
            }
            // B-b2 BalduinSurrender は完了時に救援打ち切り＝左列敵拠点を通常敵拠点化（330 §3.3）
            else if (sceneId == VSPrototypeStorySceneIds.BalduinSurrender)
            {
                BeginStorySceneById(scene, () => OnBalduinSurrenderCompleted(round));
            }
            else
            {
                BeginStorySceneById(scene, () => ContinueRoundInteriorPhase(round));
            }
            CurrentPhase = VSPrototypePhase.StoryEvent;
            return true;
        }

        private void OnSwordEmpoweredCompleted(int round)
        {
            Meta.MarkPendantPowerNoted();
            // ペンダント気づき後に同じラウンドで連鎖発火する演出（R7 の A-c2 等）があれば再判定する。
            // R6 で B-e が発火した場合は再判定で null が返り、そのまま ContinueRoundInteriorPhase へ。
            if (TryBeginRoundStartEvent(round)) return;
            ContinueRoundInteriorPhase(round);
        }

        private void OnBalduinSurrenderCompleted(int round)
        {
            _mapState.MarkBalduinSurrendered();
            ContinueRoundInteriorPhase(round);
        }

        /// <summary>ラウンド開始の本体処理（ラウンド開始時演出の有無に依らず最後に必ず走る）。</summary>
        private void ContinueRoundInteriorPhase(int round)
        {
            LastRoundResult = null;
            CurrentDraftOffer = null;
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;
            _roundManager.StartRound(_mapState, round);

            if (round >= VSPrototypeRoundManager.BossRound)
            {
                // R7 ボス戦：内政なし、直接配置＋戦闘
                LastRoundStartedAsBoss = true;
                CurrentPhase = VSPrototypePhase.Run;
                return;
            }

            InteriorState.ResetForNewRound();
            PerformAutoConscript();
            CurrentPhase = VSPrototypePhase.InteriorAction;
        }

        /// <summary>
        /// 指定セーブで試遊モードを開始する。タイトル画面の試遊ボタン（VSPrototypeTitleGUI）から呼ばれる。
        /// _demo を NullDemoFlowController から DemoFlowController に差し替え、セーブをロードしてラン開始。
        /// 試遊モード中はメタ進行を保存せず、救出ピーク（B-d）・ラン終了でタイトル戻りに分岐する。
        /// </summary>
        public void StartDemoMode(string saveId)
        {
            var demoController = new DemoFlowController();
            demoController.LoadSave(saveId);
            _demo = demoController;
            StartNewRunFromDemoSave(_demo.CurrentSave);
        }

        /// <summary>
        /// 内政フェーズ開始時の自動加入 1 体。優先順は ①未所持 Normal → ②未所持 Rare → ③全候補ランダム。
        /// 行動力を消費せず、加入したユニットは <see cref="LastAutoConscriptUnit"/> に保持し
        /// GUI が Flash 表示後にクリアする。新規加入は Lv1 で開始する。
        /// </summary>
        private void PerformAutoConscript()
        {
            var ownedIds = new HashSet<string>();
            foreach (var u in _roster) ownedIds.Add(u.Id);
            var picked = _draftService.DrawAutoConscript(ownedIds);
            if (picked == null) return;

            _roster.Add(picked);
            LastAutoConscriptUnit = picked;
        }

        /// <summary>
        /// 直近の自動加入で取得したユニット。GUI が Flash 表示後に <see cref="ConsumeAutoConscriptNotice"/> で消費する。
        /// </summary>
        public Unit LastAutoConscriptUnit { get; private set; }

        /// <summary>Flash 表示後の消費（同じユニットが繰り返し表示されないよう null クリア）。</summary>
        public void ConsumeAutoConscriptNotice() => LastAutoConscriptUnit = null;

        /// <summary>R7 ボス戦突入時に true（ラウンド開始通知用・GUI 側 Flash 表示後に Consume）。</summary>
        public bool LastRoundStartedAsBoss { get; private set; }

        /// <summary>Flash 表示後の消費。</summary>
        public void ConsumeBossRoundStartNotice() => LastRoundStartedAsBoss = false;

        /// <summary>直近の手動召集で取得したユニット（招集モーダルから戻った瞬間のフラッシュ表示用）。</summary>
        public Unit LastConscriptedUnit { get; private set; }

        /// <summary>Flash 表示後の消費。</summary>
        public void ConsumeConscriptNotice() => LastConscriptedUnit = null;

        /// <summary>直近に強化したユニットと採用された強化（強化モーダルから戻った瞬間のフラッシュ表示用）。</summary>
        public Unit LastUpgradedUnit { get; private set; }
        public UnitUpgrade LastUpgradedUpgrade { get; private set; }

        /// <summary>Flash 表示後の消費。</summary>
        public void ConsumeUpgradeNotice()
        {
            LastUpgradedUnit = null;
            LastUpgradedUpgrade = null;
        }

        /// <summary>「召集」コマンド開始：3択ドラフトオファーを生成し、SubMode=Conscript へ。</summary>
        public bool BeginConscript()
        {
            if (CurrentPhase != VSPrototypePhase.InteriorAction) return false;
            if (!_interiorService.CanConscript(InteriorState)) return false;
            CurrentDraftOffer = _draftService.DrawForConscript();
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.Conscript;
            return true;
        }

        /// <summary>
        /// 「ユニット強化」コマンド開始：SubMode=Upgrade に切替えて強化リスト表示を要求する。
        /// 行動力なしでも一覧画面までは進めるため、Phase 条件のみチェックする
        /// （行動力チェックは最終の「採用」ボタン側で行う）。
        /// </summary>
        public bool BeginUpgradeSubMode()
        {
            if (CurrentPhase != VSPrototypePhase.InteriorAction) return false;
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.Upgrade;
            return true;
        }

        /// <summary>召集ドラフトから1体選び、行動力消費＋Roster 追加を実行する（成功時 SubMode=None）。</summary>
        public bool AcceptConscriptPick(int candidateIndex)
        {
            if (CurrentPhase != VSPrototypePhase.InteriorAction) return false;
            if (CurrentDraftOffer == null) return false;

            var picked = _draftService.AcceptPick(InteriorState, CurrentDraftOffer, candidateIndex);
            if (picked == null) return false;

            bool ok = _interiorService.ExecuteConscript(InteriorState, _roster, picked);
            if (ok) LastConscriptedUnit = picked;
            CurrentDraftOffer = null; // 結果に関わらずオファーは閉じる
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;
            return ok;
        }

        /// <summary>召集ドラフトをキャンセル（行動力は消費しない）。SubMode=None に戻す。</summary>
        public void CancelConscript()
        {
            if (CurrentPhase != VSPrototypePhase.InteriorAction) return;
            CurrentDraftOffer = null;
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;
        }

        /// <summary>内政サブモード（Conscript / Upgrade）をキャンセルしてメインメニュー（SubMode=None）に戻す。</summary>
        public void CancelInteriorSubMode()
        {
            if (CurrentPhase != VSPrototypePhase.InteriorAction) return;
            CurrentDraftOffer = null;
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;
        }

        /// <summary>「ユニット強化」コマンド：指定ユニットに選択した強化を適用する（成功時 SubMode=None）。</summary>
        public bool ExecuteUpgradeUnit(Unit unit, UnitUpgrade selectedUpgrade)
        {
            if (CurrentPhase != VSPrototypePhase.InteriorAction) return false;
            bool ok = _interiorService.ExecuteUpgradeUnit(InteriorState, unit, selectedUpgrade);
            if (ok)
            {
                LastUpgradedUnit = unit;
                LastUpgradedUpgrade = selectedUpgrade;
                CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;
            }
            return ok;
        }

        /// <summary>「内政終了 →」：内政を打ち切って配置・戦闘フェーズへ進む。</summary>
        public void FinishInteriorPhase()
        {
            if (CurrentPhase != VSPrototypePhase.InteriorAction) return;
            CurrentDraftOffer = null;
            CurrentInteriorSubMode = VSPrototypeInteriorSubMode.None;
            CurrentPhase = VSPrototypePhase.Run;
        }

        // ラン進行：戦闘実行・次ラウンド・終了

        /// <summary>現在のラウンドの戦闘を全解決する。結果は LastRoundResult に格納。</summary>
        public VSPrototypeRoundResult ResolveCurrentRound()
        {
            if (_mapState == null) return null;
            LastRoundResult = _roundManager.ResolveAllBattles(
                _mapState, CurrentRound,
                resolver: ResolveBattle,
                rng: () => _rng.Next(0, 100));
            HealAllAlliesAfterRound();
            return LastRoundResult;
        }

        // 戦闘終了時に Roster 全員の HP を MaxHP に回復する。
        // VSプロトでは戦闘間の HP 引き継ぎは仕様外＝戦闘中以外は常に HP=MaxHP の前提。
        // RuntimeUnit.CurrentHP は BaseUnit.CurrentHP の proxy なので、Roster の Unit を
        // 回復すれば配置中の RuntimeUnit も同一インスタンス参照で同時に回復される。
        // carryOver / 敗北占領マスからの外し は HP 操作不要＝この一括回復で吸収される。
        private void HealAllAlliesAfterRound()
        {
            if (_roster == null) return;
            foreach (var unit in _roster)
                unit.CurrentHP = unit.EffectiveMaxHP;
        }

        // BattleRunner.Run を 1 戦闘ごとに呼ぶラッパー。
        // 戦闘開始前に各 RuntimeUnit の BattleWazas（CD 等の戦闘中状態）と
        // ActiveEffects（前戦闘の状態異常）をリセットし、永続パッシブ
        // （Unit.PersistentEffects）を複製付与する。
        private BattleReport ResolveBattle(
            List<RuntimeUnit> allies, List<RuntimeUnit> enemies,
            int maxTurns, Func<int> rng,
            bool isAttackingSide, TerrainStrength terrainStrength)
        {
            PrepareForBattle(allies);
            PrepareForBattle(enemies);

            var battleWazasByUnit = new Dictionary<RuntimeUnit, IList<RuntimeWaza>>();
            foreach (var u in allies)  battleWazasByUnit[u] = u.BattleWazas;
            foreach (var u in enemies) battleWazasByUnit[u] = u.BattleWazas;

            return BattleRunner.Run(
                allies, enemies, maxTurns,
                battleWazasByUnit,
                terrain: TerrainKind.Neutral,
                terrainStrength: terrainStrength,
                isAttackingSide: isAttackingSide,
                random0to99: rng,
                synergyDefinitions: SynergyDefinitions.All);
        }

        // 戦闘開始時の RuntimeUnit 初期化：
        // - BattleWazas を BaseWazas から再構築（前戦闘の CD・使用回数を破棄）
        // - 全 ActiveEffects をクリア（前戦闘の状態異常・バフ・デバフを破棄）
        // - PersistentEffects（パッシブ）を Clone して再付与
        private static void PrepareForBattle(IList<RuntimeUnit> units)
        {
            foreach (var u in units)
            {
                if (u == null) continue;

                u.BattleWazas = new List<RuntimeWaza>();
                if (u.BaseUnit.BaseWazas != null)
                    foreach (var w in u.BaseUnit.BaseWazas)
                        if (w != null) u.BattleWazas.Add(new RuntimeWaza(w));

                u.ClearAllEffects();

                if (u.BaseUnit.PersistentEffects != null)
                    foreach (var e in u.BaseUnit.PersistentEffects)
                    {
                        if (e == null) continue;
                        var effect = e.ToEffect();
                        // PersistentEffectBoost：Unit.AppliedUpgrades から (Kind, SourceAbilityName) 一致の
                        // Magnitude 合計を取り、PersistentEffect の Magnitude に加算する。
                        int boost = UpgradeMagnitudeResolver.SumPersistentEffectBoost(
                            u.BaseUnit, e.Kind, e.SourceAbilityName);
                        if (boost != 0)
                            EffectMagnitudeAccumulator.Add(effect, boost);
                        u.AddEffect(effect);
                    }
            }
        }

        /// <summary>
        /// 戦闘解決と再生フェーズ突入を統合した進行 API。
        /// 1) ResolveCurrentRound で全戦闘を解決し LastRoundResult を確定
        /// 2) 結果から再生キューを構築
        /// 3) 再生対象がある場合のみ Phase=Battle に遷移（敵不在等で戦闘ゼロなら Phase=Run のまま即終了扱い）
        /// </summary>
        public VSPrototypeRoundResult ResolveAndBeginBattleReplay()
        {
            var result = ResolveCurrentRound();
            BuildBattleSegments(result);
            if (_battleSegments == null || _battleSegments.Count == 0) return result;
            CurrentBattleIndex = 0;
            CurrentPhase = VSPrototypePhase.Battle;
            return result;
        }

        /// <summary>
        /// VSPrototypeRoundResult から再生キュー（タイトル付き戦闘リスト）を構築する。
        /// 順序：マス順（NodeResults の順）→ 本拠地連続防衛（R1-R6）or 本拠地ボス戦（R7）。
        /// BattleReport が null のマス（敵不在で戦闘なし）はスキップ。
        /// </summary>
        private void BuildBattleSegments(VSPrototypeRoundResult result)
        {
            _battleSegments = new List<VSPrototypeBattleSegment>();
            if (result == null) return;

            foreach (var nr in result.NodeResults)
            {
                if (nr.BattleReport == null) continue;
                _battleSegments.Add(new VSPrototypeBattleSegment(BuildNodeBattleTitle(nr), nr.BattleReport));
            }

            // 本拠地戦：R7 ボス戦は固定タイトル。R1-R6 は陥落自領が残っている時に 1 回だけ発生
            // （連戦は廃止＝3c）。
            int homeCount = result.HomeBattleReports.Count;
            bool isBoss = result.Round == VSPrototypeRoundManager.BossRound;
            for (int i = 0; i < homeCount; i++)
            {
                string title = isBoss ? "本拠地 ボス戦" : "本拠地 防衛戦";
                _battleSegments.Add(new VSPrototypeBattleSegment(title, result.HomeBattleReports[i]));
            }
        }

        /// <summary>マス戦闘のタイトル文字列（例：「自領 中央」「敵拠点 左」）。</summary>
        private static string BuildNodeBattleTitle(VSPrototypeNodeResult nr)
        {
            string layer = nr.Kind == MapNodeKind.Friendly ? "自領"
                : nr.Kind == MapNodeKind.EnemyTerritory ? "敵領"
                : nr.Kind == MapNodeKind.EnemyStronghold ? "敵拠点"
                : "?";
            string col = nr.Col == 0 ? "左" : nr.Col == 1 ? "中" : "右";
            return $"{layer} {col}";
        }

        /// <summary>
        /// 再生キューを 1 つ進める。次があれば true・終端なら FinishBattleReplay を発火して false。
        /// </summary>
        public bool AdvanceToNextBattleSegment()
        {
            if (CurrentPhase != VSPrototypePhase.Battle) return false;
            if (_battleSegments == null) return false;
            CurrentBattleIndex++;
            if (CurrentBattleIndex >= _battleSegments.Count)
            {
                FinishBattleReplay();
                return false;
            }
            return true;
        }

        /// <summary>戦闘再生を打ち切って Phase=Run に戻す（全スキップ／自然終了どちらの経路でも呼ぶ）。</summary>
        public void FinishBattleReplay()
        {
            _battleSegments = null;
            CurrentBattleIndex = 0;
            CurrentPhase = VSPrototypePhase.Run;
        }

        // IBattleReplayHost 実装（VSPrototypeBattleGUI 共有用・Debug シーン互換）

        bool IBattleReplayHost.IsActive => CurrentPhase == VSPrototypePhase.Battle;
        IReadOnlyList<VSPrototypeBattleSegment> IBattleReplayHost.Segments => BattleSegments;
        int IBattleReplayHost.CurrentIndex => CurrentBattleIndex;
        VSPrototypeBattleSegment IBattleReplayHost.CurrentSegment => CurrentBattleSegment;
        string IBattleReplayHost.HeaderProgressLabel =>
            $"R{CurrentRound}/{VSPrototypeRoundManager.MaxRounds}";
        bool IBattleReplayHost.AdvanceToNext() => AdvanceToNextBattleSegment();
        void IBattleReplayHost.FinishAll() => FinishBattleReplay();

        /// <summary>
        /// 次ラウンドへ進む：エンディング確定済 or 最終ラウンドの場合は false。
        /// 成功時は内政フェーズ（R1〜R6）or Run（R7）に遷移する。
        /// </summary>
        public bool TryAdvanceToNextRound()
        {
            if (_mapState == null) return false;
            if (LastRoundResult == null) return false;
            if (LastRoundResult.EndingKind != VSPrototypeEndingKind.None) return false;
            if (CurrentRound >= VSPrototypeRoundManager.MaxRounds) return false;

            BeginRoundInteriorPhase(CurrentRound + 1);
            return true;
        }

        /// <summary>ラン終了処理：エンディング確定後のメタ反映＋通貨加算＋永続化＋Hub 戻り。</summary>
        public int FinishCurrentRun()
        {
            if (_mapState == null) return 0;
            if (LastRoundResult == null || LastRoundResult.EndingKind == VSPrototypeEndingKind.None)
                return 0;

            int reward = _roundManager.FinishRun(
                _mapState,
                Meta,
                LastRoundResult.EndingKind,
                roundsCompleted: CurrentRound,
                reachedBossRound: CurrentRound >= VSPrototypeRoundManager.BossRound,
                bossDefeated: LastRoundResult.BossDefeated);

            LastRunReward = reward;
            LastEnding = LastRoundResult.EndingKind;
            _metaProgressStore.Save(Meta);
            CurrentPhase = VSPrototypePhase.Hub;
            return reward;
        }

        // 演出 Phase

        /// <summary>
        /// 「次のラウンドへ →」総合エントリ：
        ///   - 本ランで初めて救援成功した（IsBridgetRescued ＆ ! IsBalduinRescuePlayed）なら
        ///     → B-d バルドゥイン救援成功演出（BalduinRescue）に突入。発火直前にラン内
        ///        フラグ IsBalduinRescuePlayed を立てて再発火を防ぐ。演出完了時に次ラウンド内政へ
        ///   - R5 終了かつ救援失敗時の演出は R5 開始時の BalduinSurrender で既出のため、
        ///     ここでは何も再生せず次ラウンドへ進める
        ///   - それ以外はそのまま次ラウンドへ（=内政フェーズへ）
        /// </summary>
        public bool AdvanceAfterRound()
        {
            if (LastRoundResult == null) return false;
            if (LastRoundResult.EndingKind != VSPrototypeEndingKind.None) return false;

            if (_mapState != null && _mapState.IsBridgetRescued && !_mapState.IsBalduinRescuePlayed)
            {
                _mapState.MarkBalduinRescuePlayed();
                var scene = _storySceneCatalog.Get(VSPrototypeStorySceneIds.BalduinRescue);
                BeginStorySceneById(scene, onComplete: AdvanceToNextRoundAfterBalduinRescueEvent);
                CurrentPhase = VSPrototypePhase.StoryEvent;
                return true;
            }

            return TryAdvanceToNextRound();
        }

        /// <summary>「メタ拠点へ →」総合エントリ：エンディング演出に突入。</summary>
        public bool EnterEndingEvent()
        {
            if (LastRoundResult == null) return false;

            // R7 敗北を確定した瞬間に Meta.MarkFirstReachedBoss() を立てる。
            // 6R 防衛達成 → 7R 到達 → 7R 敗北のパターンで、Defeat 3 分岐の NormalClear 判定に使う。
            // 連続防衛敗北で R5/R6 で終わった場合は CurrentRound < BossRound なのでマークしない。
            if (LastRoundResult.EndingKind == VSPrototypeEndingKind.Defeat
                && CurrentRound >= VSPrototypeRoundManager.BossRound)
            {
                Meta.MarkFirstReachedBoss();
            }

            var pages = ResolveEndingPages(LastRoundResult.EndingKind);
            if (pages == null) return false;

            BeginStory(pages, onComplete: FinishRunAfterEndingEvent);
            CurrentPhase = VSPrototypePhase.StoryEvent;
            return true;
        }

        private IReadOnlyList<StoryPage> ResolveEndingPages(VSPrototypeEndingKind kind)
        {
            switch (kind)
            {
                case VSPrototypeEndingKind.Defeat:
                    // 3 分割（First / NormalClear / Repeated）の振り分けは純関数経由。
                    var defeatSceneId = VSPrototypeDefeatSceneResolver.Resolve(
                        runCount: Meta.RunCount,
                        hadFirstReachedBossAtRunStart: _hadFirstReachedBossAtRunStart,
                        currentHasFirstReachedBoss: Meta.HasFirstReachedBoss);
                    return _storySceneCatalog.Get(defeatSceneId).Pages;
                case VSPrototypeEndingKind.True:
                    return _storySceneCatalog.Get(VSPrototypeStorySceneIds.EndingTrue).Pages;
                default: return null;
            }
        }

        private void BeginStory(IReadOnlyList<StoryPage> pages, Action onComplete)
        {
            _onStoryComplete = onComplete;
            StoryProgress.Initialize(pages, OnStoryProgressCompleted);
        }

        // ストーリーシーンの ID 経由再生：初見なら本文・既見なら短縮ナレ 1 ページ。
        // 完了時に Meta.MarkStorySceneSeen で既見化＋ onComplete を発火。
        // 既見だが短縮ナレ未設定のシーンは警告ログ＋通常本文にフォールバック。
        private void BeginStorySceneById(StoryScene scene, Action onComplete)
        {
            if (scene == null) { onComplete?.Invoke(); return; }

            IReadOnlyList<StoryPage> pages;
            if (Meta.HasSeenStoryScene(scene.Id) && !string.IsNullOrEmpty(scene.RepeatNarration))
            {
                pages = new[] {
                    new StoryPage(
                        imagePath: string.Empty,
                        narrationText: scene.RepeatNarration,
                        fadeInSeconds: 0.3f,
                        displaySeconds: 0f,
                        fadeOutSeconds: 0.3f,
                        fallbackImagePath: string.Empty),
                };
            }
            else
            {
                if (Meta.HasSeenStoryScene(scene.Id))
                {
                    Debug.LogWarning(
                        $"[StoryScene] '{scene.Id}' は既見だが RepeatNarration 未設定。通常本文を再生します。");
                }
                pages = scene.Pages;
            }

            BeginStory(pages, () =>
            {
                Meta.MarkStorySceneSeen(scene.Id);
                onComplete?.Invoke();
            });
        }

        private void OnStoryProgressCompleted()
        {
            var cb = _onStoryComplete;
            _onStoryComplete = null;
            cb?.Invoke();
        }

        private void AdvanceToNextRoundAfterBalduinRescueEvent()
        {
            // バルドゥイン救援成功演出（BalduinRescue）完了 → 本ラン中の手駒として
            // ブリジットを Roster に加入させる（演出本文「ブリジット、こちらへ」に合わせて
            // 演出完了直後のタイミング）。
            AddBridgetToRosterIfAbsent();

            // 試遊モード：B-d ブリジット加入演出が体験のピーク＝ここで完結。
            // R7 連鎖（B-e/A-c2/皇太子戦）はカットしてタイトル戻り。
            if (_demo.IsActive)
            {
                CurrentPhase = VSPrototypePhase.Title;
                return;
            }

            // 「解放したラウンドの終了直後」発火なので CurrentRound+1 で次内政フェーズへ。
            if (CurrentRound < VSPrototypeRoundManager.MaxRounds)
                BeginRoundInteriorPhase(CurrentRound + 1);
            else
                CurrentPhase = VSPrototypePhase.Run;
        }

        // ブリジットを本ランの手駒として加入させる（既に居る場合は何もしない＝冪等）。
        // 永続解禁経由で既に Roster に居るケースは構造的に発生しない想定だが
        //（balduinAlreadyRescued ラン中はバルドゥイン拠点が通常敵拠点扱い＝ IsBridgetRescued
        //   が立たず B-d も発火しない）、防御として Id 重複ガードを入れる。
        private void AddBridgetToRosterIfAbsent()
        {
            if (_roster == null) return;
            foreach (var u in _roster)
                if (u != null && u.Id == UniqueUnitIds.Bridget) return;

            var bridget = _unitCatalog.Get(UniqueUnitIds.Bridget);
            if (bridget == null) return;
            ApplyMetaUpgradeChoices(bridget, UniqueUnitIds.Bridget);
            _roster.Add(bridget);
        }

        private void FinishRunAfterEndingEvent()
        {
            if (_demo.IsActive)
            {
                // 試遊モード：メタ進行は保存しない＝ MetaProgressStore.Save なし。
                // タイトルに戻る（Phase 5 で「もう一度試遊しますか？」モーダルに置き換える）。
                CurrentPhase = VSPrototypePhase.Title;
                return;
            }
            FinishCurrentRun(); // 通常版：内部で Phase=Hub に切り替わる
        }
    }
}

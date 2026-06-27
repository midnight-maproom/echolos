// 領地マップ全体の状態（3列×3層9マス＋本拠地）を保持するドメインモデル。
//
// 【設計方針】
// - Grid は 3列 × 3層（自領／敵領／敵拠点）。本拠地は別フィールド。
// - 内部表現は _grid[col, layerIndex] で layerIndex = Layer - 1 にマッピング。
// - 状態変化（IsBridgetRescued）は MarkBridgetRescued() メソッド経由のみ。
using System;
using System.Collections.Generic;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Story;
namespace Echolos.UseCase.VSPrototype
{
    /// <summary>VSプロト領地マップの全体状態（9マス＋本拠地＋ラン中フラグ）。</summary>
    public sealed class VSPrototypeMapState
    {
        /// <summary>列数（左／中／右）。</summary>
        public const int ColCount = 3;

        /// <summary>本拠地の Layer 値（Grid には入らない）。</summary>
        public const int LayerHome = 0;

        /// <summary>自領の Layer 値（Grid のインデックス 0 に対応）。</summary>
        public const int LayerFriendly = 1;

        /// <summary>敵領の Layer 値（Grid のインデックス 1 に対応）。</summary>
        public const int LayerEnemyTerritory = 2;

        /// <summary>敵拠点の Layer 値（Grid のインデックス 2 に対応）。</summary>
        public const int LayerEnemyStronghold = 3;

        /// <summary>本拠地の Col 値（マップ中央固定）。</summary>
        public const int HomeCol = 1;

        /// <summary>バルドゥイン拠点が配置される列（左列固定）。</summary>
        public const int BalduinCol = 0;

        // _grid[col, layerIndex] : layerIndex = 0(自領) / 1(敵領) / 2(敵拠点)
        private readonly MapNode[,] _grid;

        /// <summary>
        /// 領地マップを初期化する。
        /// <paramref name="balduinAlreadyRescued"/>=true なら、過去ランでバルドゥインを救出済の
        /// 世界線として左列敵拠点の<see cref="MapNode.IsBalduinStronghold"/> 扱いを解除する
        /// （バルドゥインが居らずすぐに降伏した「通常の敵拠点」として中央列・右列と同じ振る舞いに）。
        /// 制圧操作で <see cref="IsBridgetRescued"/> が立つ経路は <see cref="MapNode.IsBalduinStronghold"/>
        /// が false になるため自然に抑止される。
        /// <paramref name="columnTerrains"/> はランごとに抽選した列地形（左／中／右の 3 要素）。
        /// null の場合は全列 Neutral で初期化する（列地形ランダム選定は未実装のため現状は常に既定で動く）。
        /// </summary>
        public VSPrototypeMapState(bool balduinAlreadyRescued = false, IList<TerrainKind> columnTerrains = null)
        {
            if (columnTerrains != null && columnTerrains.Count != ColCount)
                throw new ArgumentException(
                    $"columnTerrains の要素数は {ColCount} 必須", nameof(columnTerrains));

            var terrains = new TerrainKind[ColCount];
            if (columnTerrains != null)
            {
                for (int i = 0; i < ColCount; i++) terrains[i] = columnTerrains[i];
            }
            ColumnTerrains = terrains;

            Home = new MapNode(MapNodeKind.Home, col: HomeCol, layer: LayerHome);

            _grid = new MapNode[ColCount, 3];
            for (int col = 0; col < ColCount; col++)
            {
                _grid[col, 0] = new MapNode(MapNodeKind.Friendly,        col, LayerFriendly);
                _grid[col, 1] = new MapNode(MapNodeKind.EnemyTerritory,  col, LayerEnemyTerritory);
                // 通常時は左列の敵拠点だけがバルドゥイン拠点。過去ラン救出済の世界線では
                // 左列も中央・右列と同じ「通常の敵拠点」扱いにする（バルドゥインが居らずすぐに降伏した世界）。
                bool isBalduin = !balduinAlreadyRescued && col == BalduinCol;
                _grid[col, 2] = new MapNode(MapNodeKind.EnemyStronghold, col, LayerEnemyStronghold,
                    isBalduinStronghold: isBalduin);
            }
        }

        /// <summary>
        /// 列ごとの地形種別（左／中／右の 3 要素・320 §5.2）。
        /// 同列内の自領・敵領・敵拠点は同じ TerrainKind を共有し、強度だけが層別に変化する。
        /// 本拠地は中立固定（呼び出し側で MapNodeKind を見て分岐）。
        /// </summary>
        public IReadOnlyList<TerrainKind> ColumnTerrains { get; }

        /// <summary>本拠地マス。</summary>
        public MapNode Home { get; }

        /// <summary>このラン中にブリジット（バルドゥイン拠点）を救出したか。</summary>
        public bool IsBridgetRescued { get; private set; }

        /// <summary>このラン中に B-d バルドゥイン救援成功演出を再生済か（解放直後 1 回のみ発火する制御フラグ）。</summary>
        public bool IsBalduinRescuePlayed { get; private set; }

        /// <summary>このラン中に B-b2 バルドゥイン降伏演出が発火完了したか。
        /// R6 B-c 謎の少女の発火条件に使う（B-b2 既発火が前提＝救援打ち切り後の演出）。</summary>
        public bool IsBalduinSurrendered { get; private set; }

        /// <summary>バルドゥイン拠点（左列の敵拠点）への直接アクセス。</summary>
        public MapNode BalduinStronghold => _grid[BalduinCol, 2];

        /// <summary>
        /// 指定された (col, layer) のマスを取得する。
        /// layer = 0 は Home を返す（col は HomeCol でなくても許容するか？→ 厳密に LayerHome+HomeCol のみ）。
        /// </summary>
        public MapNode GetNode(int col, int layer)
        {
            if (layer == LayerHome)
            {
                if (col != HomeCol)
                    throw new ArgumentOutOfRangeException(nameof(col),
                        $"Home の col は {HomeCol} のみ");
                return Home;
            }
            if (col < 0 || col >= ColCount)
                throw new ArgumentOutOfRangeException(nameof(col),
                    $"col は 0〜{ColCount - 1} の範囲");
            if (layer < LayerFriendly || layer > LayerEnemyStronghold)
                throw new ArgumentOutOfRangeException(nameof(layer),
                    $"layer は {LayerFriendly}〜{LayerEnemyStronghold} の範囲（Home は LayerHome）");
            return _grid[col, layer - 1];
        }

        /// <summary>マップ上の全マス（Home＋9マス）を順に列挙する。</summary>
        public IEnumerable<MapNode> AllNodes()
        {
            yield return Home;
            for (int col = 0; col < ColCount; col++)
                for (int layerIndex = 0; layerIndex < 3; layerIndex++)
                    yield return _grid[col, layerIndex];
        }

        /// <summary>3つの自領マスを列挙する（左→中→右）。</summary>
        public IEnumerable<MapNode> FriendlyNodes()
        {
            for (int col = 0; col < ColCount; col++)
                yield return _grid[col, 0];
        }

        /// <summary>3つの敵領マスを列挙する。</summary>
        public IEnumerable<MapNode> EnemyTerritoryNodes()
        {
            for (int col = 0; col < ColCount; col++)
                yield return _grid[col, 1];
        }

        /// <summary>3つの敵拠点マスを列挙する。</summary>
        public IEnumerable<MapNode> EnemyStrongholdNodes()
        {
            for (int col = 0; col < ColCount; col++)
                yield return _grid[col, 2];
        }

        /// <summary>
        /// ブリジット救出フラグを立てる（バルドゥイン拠点が制圧された直後に呼ばれる想定）。
        /// 冪等：既に救出済でも再度呼んで問題ない（フラグは true のまま）。
        /// </summary>
        public void MarkBridgetRescued()
        {
            IsBridgetRescued = true;
        }

        /// <summary>B-d バルドゥイン救援成功演出の再生済フラグを立てる。冪等。</summary>
        public void MarkBalduinRescuePlayed()
        {
            IsBalduinRescuePlayed = true;
        }

        /// <summary>
        /// B-b2 バルドゥイン降伏演出完了時に呼ばれる。
        /// IsBalduinSurrendered フラグを立てる＋左列敵拠点を通常敵拠点扱いに転落させる
        /// （以降このマスを制圧してもブリジット加入トリガしない）。冪等。
        /// </summary>
        public void MarkBalduinSurrendered()
        {
            IsBalduinSurrendered = true;
            BalduinStronghold.MarkBalduinStrongholdCleared();
        }
    }
}

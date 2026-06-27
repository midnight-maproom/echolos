# Echoes of the Lost Kingdom — ドキュメント目次

## フル版関連

最終的に完成させたいゲームの姿。

- **[100_game_design.md](100_game_design.md)** — ゲームデザイン全体（フルゲーム版・7章構成）

100 はフル版設計再開時に再構成予定。戦闘システム仕様（旧 110）と実装タスクリスト（旧 400）は VSプロト範囲との整合が取れなくなったため `_old/` 配下にアーカイブし、フル版設計を再開する時点で新エンジン基盤に合わせて書き直す。

---

## VSプロト（USP実証 Vertical Slice）関連

USP「戦略が結末を決め、感情が周回を生む」を視覚的・体験的に実証するためのプロトタイプ。領地マップ視覚化・固有ユニット加入・メタ強化サイクル・「ありがとう」シーン拡張を含む。

- **[300_vsprototype_policy.md](300_vsprototype_policy.md)** — VSプロトの方針書（H4 USP核実証）
- **[310_vsprototype_spec.md](310_vsprototype_spec.md)** — VSプロト仕様書
- **[330_vsprototype_storyplot.md](330_vsprototype_storyplot.md)** — ストーリープロット・分岐ロジック・テキスト草案
- **[320_vsprototype_combat_spec.md](320_vsprototype_combat_spec.md)** — 戦闘システム仕様（シナジー中心）

---

## 開発用

- **[500_architecture.md](500_architecture.md)** — アーキテクチャ（プロトで構築・本番でも引き継ぐ基盤）
- **[900_development_rules.md](900_development_rules.md)** — 開発ルール（AI Assistant 向け規約）
- **[912_vsprototype_devlog_2.md](912_vsprototype_devlog_2.md)** — VSプロト現役 devlog（Phase R-4 / R-6 / 試遊 FB 等の残タスク＋作業ログ）
- **[920_implementation_notes.md](920_implementation_notes.md)** — 実装ノート（IMGUI 落とし穴集 + Claude 操作 Tips）
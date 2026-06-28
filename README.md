# Echoes of the Lost Kingdom

> **戦略が結末を決め、感情が周回を生む ローグライトSLG**

亡国の王女に呼び出された主人公が、1000 年前の王国で帝国軍の侵略を防ぐ。
限られた戦力をどの戦線に送るか、誰を救い・誰を諦めるかの戦略判断が物語の結末を分岐させ、救えなかった後悔が次の周回の動機になる。

---

## ジャンル・プラットフォーム

| 項目 | 内容 |
|---|---|
| ジャンル | ローグライトSLG（王国奪還SLG × ストーリー × ローグライト） |
| プラットフォーム | PC（Steam／WebGL） |
| エンジン | Unity 6 |

## 現状

プロトタイプ段階。

戦略・戦闘・物語・周回を通しで動かす Vertical Slice の統合実装が完成。
ゲームのコア体験（戦略性／物語の周回動機／オートバトル／ローグライト要素）の十分な検証はこれから本格化。

## 試遊版

準備中。

## 企画書

[echolos_pitch.pdf](https://drive.google.com/file/d/1dscbopKUAIxZhwZmcqN81oEp5TYh_gEv/view?usp=sharing)

## 仕様書

開発仕様書の一覧は [Docs/000_index.md](Docs/000_index.md) を参照。

## プロジェクト構造

```
Assets/
├── Scripts/
│   ├── Domain/         # ドメインロジック（純 C#・Unity 非依存）
│   ├── Data/           # SO ラッパー・Catalog 実装
│   ├── UseCase/        # ゲーム進行ロジック
│   └── Presentation/   # MonoBehaviour・UI（OnGUI）
├── Resources/          # SO アセット・画像・フォント
└── Tests/Editor/Domain/ # NUnit テスト

Docs/                   # 仕様書・設計書・ピッチ資料
```

詳細は [Docs/500_architecture.md](Docs/500_architecture.md) を参照。

## 開発環境

- Unity 6（LTS）
- C# / .NET Standard 2.1
- NUnit（Unity Test Framework・Editor Mode）

## 開発者

**Midnight Maproom**（個人開発）／ [okccl](https://github.com/okccl)

## ライセンス

All Rights Reserved（保守的に設定）。詳細は [LICENSE](LICENSE) を参照。

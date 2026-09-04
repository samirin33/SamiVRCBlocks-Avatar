# LinkedTransform

## 概要

任意の複数 Transform を **重み付きでブレンド**し、オフセット・倍率・座標空間を指定してターゲットへコピーする、**エディタ用**コンポーネントです。
(将来的にビルド後でも同じ結果が期待できるような機能を実装予定です)
ターゲット未指定時は自身に適用します。ビルド時に一度ベイクしてコンポーネントを削除します。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB LinkedTransform**
- コピー先（または近くの調整用オブジェクト）にアタッチします。

## 使い方

### 基本手順

1. LinkedTransform をアタッチ
2. 必要なら **Target** に適用先 Transform を指定（未指定なら自身）
3. **Sources** にコピー元 Transform と Weight を追加（追加時の Weight 初期値は 1）
4. Position / Rotation / Scale でコピーする軸・座標空間・倍率・オフセットを設定
5. エディタ非再生時は自動で継続適用されます

### オプション・設定

| 項目 | 説明 |
|------|------|
| Target | 適用先。未指定なら自身 |
| Sources | Constraint と同様の Transform + Weight の配列。重み付き平均に使う |
| Position / Rotation / Scale | コピーの有無、軸（X/Y/Z）、座標空間、倍率、オフセット |

**計算式**

`結果 = ソースの重み付き平均 × 倍率 + オフセット`（無効軸は現状維持）

**座標空間**

| 値 | Position | Rotation | Scale |
|----|----------|----------|-------|
| World | `position` | ワールド回転 | `lossyScale` |
| Local | `localPosition` | ローカル回転 | `localScale` |

## 注意事項

- エディタ専用の想定です。プレイモード中は適用しません。

## 関連

- [TuningObject](10-tuning-object.md) — チューニング用のオフセット適用・ギズモ
- [GameObjectResetter](03-game-object-resetter.md) — ビルド時の姿勢リセット

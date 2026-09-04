# TuningObject

## 概要

チューニング用の **ギズモ表示** と、任意 Transform への **オフセット適用** を行うコンポーネントです。
自身または親が選択されているとき、Scene ビューに中心球・矢印・ラベル・半透明メッシュを描画します。
**Active** 時は `targetTransforms` に「自身の Transform + Offset」を継続適用します。
**Preview Particles** 時は、TuningObject 自身の選択中のみ Target 配下の ParticleSystem をエディタ上でプレビュー再生します。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB TuningObject**
- 調整用の空オブジェクトやガイド用オブジェクトにアタッチします。

## 使い方

### 基本手順（オフセット適用）

1. TuningObject をアタッチしたオブジェクトを配置
2. **Target Transforms** に動かしたい Transform と Offset（位置・回転・スケール）を設定
3. Active がオフのとき、各要素に次のボタンが表示されます
   - **Targetへ移動** — 自身を Target のワールド姿勢へ移動
   - **差分をOffsetに登録** — 自身と Target の現在差分を Offset に書き込む
4. **Active** をオンにすると、各 Target に「自身 + Offset」が継続適用されます

### 最初の状態（スナップ姿勢）

配置時にローカル姿勢がスナップ基準として記録されます。Inspector から次の操作ができます。

| ボタン | 説明 |
|--------|------|
| 最初の状態にリセット | 記録済みのローカル位置・回転・スケールへ戻す |
| 最初の状態を更新 | 現在のローカル姿勢を新しい基準として上書き記録する |

### パーティクルプレビュー

1. Target 配下（Target 自身を含む）に ParticleSystem があることを確認する
2. **Preview Particles** をオンにする
3. **TuningObject 自身** を Hierarchy / Scene で選択するとプレビュー再生される

| 項目 | 説明 |
|------|------|
| Preview Particles | Target 配下の ParticleSystem をエディタ上で Simulate してプレビューする |

### ギズモ表示

自身または親が Hierarchy / Scene で選択されているときのみ描画します。

| 項目 | 説明 |
|------|------|
| Show Sphere | 中心点の球を表示 |
| Sphere Radius / Color | 球の大きさ・色 |
| Arrows | 矢印の向き・長さ・ヘッドサイズ・色・ローカル/ワールド |
| Show Label | 中心右下にテキストを表示 |
| Label Text / Color / Offset | 表示文言・色・画面上オフセット |
| Show Mesh | 任意メッシュを半透明表示 |
| Preview Mesh ほか | メッシュ・色・ローカル Offset / Rotation / Scale |

## ビルド時の挙動

- **Transforming（Modular Avatar より後）**: 子が無い場合は GameObject ごと削除、子がある場合は本コンポーネントのみ削除します。

## 注意事項

- Active 中は Target へ毎フレーム（エディタでは EditorUpdate）姿勢を書き込みます。意図しない上書きに注意してください。
- パーティクルプレビューは **TuningObject 自身が直接選択されているときだけ** 動作します。親・Target・パーティクル本体を選んでも再生しません。
- プレビュー終了時は `Clear` のみ行い `Stop()` は呼びません。パーティクル本体を選択したときの Unity 標準の再生操作には干渉しません。

## 関連

- [LinkedTransform](11-linked-transform.md) — 複数ソースの Transform を重み付きでコピーする場合

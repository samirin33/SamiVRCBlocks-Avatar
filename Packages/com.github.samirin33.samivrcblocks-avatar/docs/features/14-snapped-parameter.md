# SnappedParameter

## 概要

指定した Float パラメーターを任意範囲で **n 分割**し、分解能にスナップした値（`_Snapped`）とスムージング後の値（`_SnapSmoothed`）を出力する Animator を生成します。
同期 Bit 分解は行いません（ResizableSyncParameters のスナップ部分だけを切り出したコンポーネントです）。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB SnappedParameter**
- アバター配下の任意の GameObject に追加できます（複数可。同名パラメータは先勝ちでマージされます）

## 使い方

### 基本手順

1. SnappedParameter をアタッチした GameObject を選択
2. **スナップ設定** で **+ 追加** する
3. 各要素で次を指定する
   - **パラメータ名**（空の場合は `Param`）
   - **Float範囲**（`-1~1` / `0~1` / カスタム）
   - **分割数**（範囲を何等分するか。段階数は分割数+1）
   - 必要なら **スムージング重み**

### オプション・設定

| 項目 | 内容 |
|------|------|
| **スムーズ度の一括設定** | 全要素の `smoothWeight` をまとめて適用 |
| **生成されるステートのWrite Default** | 生成レイヤーの Write Defaults |
| **Animator内のFloatパラメーターをSnapSmoothedに置き換える** | 有効時、ビルド後に FX 内の元 Float 名参照を `{名前}_SnapSmoothed` に置換 |

- Inspector に `{名前}_Snapped` / `{名前}_SnapSmoothed` が表示され、コピーできます
- ビルド時に **ParameterSmoothing** と FPSCounter モジュールが自動利用されます

### 分割数について

入力→内部 Int・Int→スナップ値とも、指定の最小〜最大を端点として線形に写像します。

- 分割数 `n` → 内部 Int は `0..n`、分解能は `(max-min)/n`、段階数は `n+1`
- 例: `0~1`・分割数 2 → 値は `0` / `0.5` / `1`
- `-1~1` で `0` を含めたい場合は、分割数を偶数にしてください（例: 2 → `-1` / `0` / `1`）

## ビルド時の挙動

1. **Resolving**（Modular Avatar より前）: スナップ用 AnimatorController を生成し、MA Merge Animator / Parameters として登録。続けて ParameterSmoothing も構築
2. **Optimizing**（Modular Avatar より前）: `replaceWithSnapSmoothedInAnimator` が有効なら FX コントローラ内の Float 参照を `_SnapSmoothed` に置換し、コンポーネントを削除

生成される主なパラメータ:

- `{名前}` … 入力の元 Float（変更しない）
- `{名前}_SnapInt` … 分割用の内部 Int
- `{名前}_Snapped` … 分解能にスナップした値
- `{名前}_SnapSmoothed` … スムージング後

## 注意事項

- スムージングは [AAP](https://vrc.school/docs/Other/AAPs/) で行われます。スムージング後の値は **VRC Parameter Driver では取得できません**
- 同名パラメータを複数の SnappedParameter に書くと、先に見つかった設定だけが使われます
- ResizableSyncParameters と同じパラメータ名を使うと `_Snapped` が衝突するため避けてください

## 関連

- [ResizableSyncParameters](01-resizable-sync-parameters.md) — 同期 Bit 削減付きのスナップ／スムージング
- [ParameterSmoothing](04-parameter-smoothing.md) — Float のスムージング（SnappedParameter から自動利用）

# ResizableSyncParameters

## 概要

VRChat で同期するパラメーターの **同期範囲** や **分解能** を抑えることで、同期に使う Bit 数を削減できます。
Int / Float どちらも扱え、Float ではスナップ後の値とスムージング後の値を利用できます。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB ResizableSyncParameters**
- アバター配下の任意の GameObject に追加できます（複数可。同名パラメータは先勝ちでマージされます）

## 使い方

### 基本手順

1. ResizableSyncParameters をアタッチした GameObject を選択
2. **同期パラメータ設定** で **+ 追加** する
3. 各要素で次を指定する
   - **パラメータ名**（空の場合は `Param_{型}{ビット}` が自動生成）
   - **タイプ**（`Int` / `Float`）
   - **ビット数**（`_1bit` 〜 `_7bit`、または **カスタム** で 1〜16 bit）
4. 型に応じて範囲を設定する
   - Int: **Int範囲**（`0~2^n` / カスタム最小値）
   - Float: **Float範囲**（`-1~1` / `0~1` / カスタム）、**分割方式**（偶数分割 / 奇数分割）、必要なら **スムージング重み**
5. 必要に応じて共通オプションを設定する（下記）

### オプション・設定

| 項目 | 内容 |
|------|------|
| **スムーズ度の一括設定** | Float 要素すべての `smoothWeight` をまとめて適用 |
| **生成されるステートのWrite Default** | 生成レイヤーの Write Defaults |
| **Animator内のFloatパラメーターをSmoothedに置き換える** | 有効時、ビルド後に FX 内の元 Float 名参照を `{名前}_Smoothed` に置換（Smoothing 関連レイヤーは除外） |

- 複数パラメータを登録でき、Inspector で並び替え（↑↓）・削除が可能です
- Float 設定時は Inspector に `{名前}_Snapped` / `{名前}_Smoothed` が表示され、コピーできます
- Float がある場合、ビルド時に **ParameterSmoothing** と FPSCounter モジュールが自動利用されます（コンポーネントを手動追加する必要はありません）

### Float の分割方式について

- **奇数分割**: 範囲を `maxValue`（= 2ⁿ−1）等分。端点を含みます（例: 4bit なら分解能 1/15）。`-1~1` の既定で、0 を表現しやすくします
- **偶数分割**: 範囲を `maxValue + 1`（= 2ⁿ）等分。各ビンの中心に寄せます（例: 4bit なら分解能 1/16）。`0~1` / カスタムの既定です
- Float 範囲を変えると、分割方式の既定値も合わせて更新されます（`0~1` / カスタム → 偶数、`-1~1` → 奇数）

## ビルド時の挙動

1. **Resolving**（Modular Avatar より前）: ResizableSync 用 AnimatorController を生成し、MA Merge Animator / Parameters として登録。Float は ParameterSmoothing も構築
2. **Optimizing**（Modular Avatar より前）: `replaceWithSmoothedInAnimator` が有効なら FX コントローラ内の Float 参照を `_Smoothed` に置換し、コンポーネントを削除

生成・同期に関わる主なパラメータ例:

- `{名前}` … ローカルで扱う元の Int / Float
- `{名前}_Int` … Bit 分解用の内部 Int
- `SUM/ResizableSync/{名前}_Int/{i}` … 同期用 Bool（Bit 数ぶん）
- Float のみ: `{名前}_Snapped`（分解能にスナップした値）、`{名前}_Smoothed`（スムージング後）

## 注意事項

- スムージングは [AAP](https://vrc.school/docs/Other/AAPs/) で行われます。スムージング後の値は **VRC Parameter Driver では取得できません**
- Remote 側で見える結果は、Float では `_Snapped` / `_Smoothed` 側に一致します
- 同名パラメータを複数の ResizableSyncParameters に書くと、先に見つかった設定だけが使われます

## 関連

- [ParameterSmoothing](04-parameter-smoothing.md) — Float のスムージング（ResizableSyncParameters から自動利用）
- [ExtendedParameters](05-extended-parameters.md) — ParameterSmoothing が FPSCounter を登録する先

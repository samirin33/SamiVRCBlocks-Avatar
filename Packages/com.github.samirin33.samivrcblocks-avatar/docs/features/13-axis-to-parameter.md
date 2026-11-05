# AxisToParameter

## 概要

自身または指定したオブジェクトの **X / Y / Z 軸まわりの回転角** を、Float パラメーターとして取得するコンポーネントです。
ビルド時に Box 型の VRC Contact と VRC Rotation Constraint の構造、および Contact の値（sin / cos 相当）から角度を復元する AAP（Animated Animator Parameter）の Animator を自動生成します。
リストで複数指定でき、出力範囲は 0〜1 / -1〜1 / 0〜360 / 任意範囲 から選べます。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB AxisToParameter**
- アバター配下の任意のオブジェクト（ギミック用の空オブジェクトなど）にアタッチします。

## 使い方

### 基本手順

1. AxisToParameter をアタッチ
2. **+ 追加** で項目を増やす
3. **パラメーター名** に出力先の Float パラメーター名を入力
4. **ターゲット** に回転を測りたいオブジェクトを指定（未指定なら自身）
5. **回転軸** と **出力範囲** を選ぶ
6. インスペクタの「現在の値（目安）」で、エディタ上の姿勢から計算した値を確認できます

### オプション・設定

項目ごと:

| 項目 | 説明 |
|------|------|
| パラメーター名 | 出力先の Float パラメーター。他の項目・他の AxisToParameter と重複させない |
| ターゲット | 回転を測るオブジェクト。未指定なら自身 |
| 基準 | このオブジェクトから見た回転を測る。未指定ならワールド基準（下記） |
| 回転軸 | X / Y / Z（基準のローカル軸。未指定ならワールドの軸）。正の向きは Unity の `localEulerAngles` と同じ |
| 出力範囲 | 下表を参照 |

| 出力範囲 | 角度 → 出力 |
|----------|-------------|
| 0〜1 | 0°〜360° → 0〜1（0° で値が飛ぶ） |
| -1〜1 | -180°〜180° → -1〜1（±180° で値が飛ぶ） |
| 0〜360 | 0°〜360° → 0〜360（0° で値が飛ぶ） |
| 任意範囲 | 角度の最小〜最大 → 出力の最小〜最大。角度の幅は 360° まで。範囲外の角度は近い側の端の値になる（例: -90°〜90° → 0〜1 なら、背面側の中央で 0 と 1 が切り替わる） |

コンポーネント全体:

| 項目 | 説明 | アニメーション |
|------|------|----------------|
| Local Only | Contact をローカルでのみ動かす。パフォーマンスランクに影響しなくなるが、他のプレイヤーの環境では値が正しくならない | - |
| Constraint 有効 | オフで生成した VRC Rotation Constraint をすべて止める | 可 |
| Contact 有効 | オフで生成した Contact Sender / Receiver をすべて止める | 可 |

### 角度の測り方

回転 0° のときに軸と垂直な方向（X 軸・Y 軸は前方 +Z、Z 軸は上方 +Y）を向くベクトルが、基準から見てどちらを向いているかを、軸に垂直な平面へ投影して測ります。

- 1 つの軸だけで回転しているときは、その軸の `localEulerAngles` と一致します。
- 他の軸も大きく回転していると、投影した向きで測るため Euler 角とは一致しません。計測ベクトルが軸と平行に近づく（例: Y 軸を測っていて真上を向く）と角度が不安定になります。
- 0° の位置をずらしたいときは、基準に「0° にしたい姿勢の空オブジェクト」を指定してください。
- 親から見た回転（ローカル回転）が欲しいときは、基準にターゲットの親を指定してください。

### ワールド基準

基準が未指定の項目は、[WorldFix](02-world-fix.md) / [RemapPosition](12-remap-position.md) と同じ World プレハブを `GetAxis_` の Rotation Constraint ソースにします（ワールド軸）。

- 上下（X 軸・Z 軸の傾き）はワールドの水平・鉛直に一致します。
- Y 軸（ヨー）の 0° もワールドの前方基準です。
- エディタ上のプレビューも Unity のワールド軸で計算します。

### アニメーション

このコンポーネントの `Constraint 有効` / `Contact 有効` を Animation ウィンドウで録画したクリップは、ビルド時に次のように書き換えます。

| コンポーネントのプロパティ | 書き換え先 |
|----------------------------|------------|
| `constraintActive` | 生成したすべての VRC Rotation Constraint の `IsActive` |
| `contactActive` | 生成したすべての VRC Contact Sender / Receiver の `m_Enabled` |

## ビルド時の挙動

### Generating フェーズ（Modular Avatar の前）

Animator を生成し、このオブジェクトの子 `AxisToParameter_Module` に MA Merge Animator（FX、Write Defaults オン）として追加します。
レイヤーは 1 ステートの Direct BlendTree で、次の 2 段で角度を復元します。

1. 計測方式が用意した「Sender の座標に比例するパラメーター」から sin / cos 成分を線形結合し、項目ごとに「値が飛ぶ角度」が真後ろに来るよう回した 2 つの値（P, Q）を AAP で書く（`SenderLocalPosition` は Direct 重みではなく 1D の blendParameter で写し、角度復元は次レイヤー）
2. P の符号で左右を切り替え、それぞれを 2D Simple Directional（15° 間隔＋任意範囲の端）でブレンドして出力パラメーターへ書く

Simple Directional は入力の長さがサンプルの輪（長さ 1）より短いと全サンプルの平均へ寄っていくため、P, Q は十分大きくして常に輪の外側に置いています。
輪の外側では向きだけでブレンドされるので、コンポーネントスケールや他の軸の回転で sin / cos の大きさが変わっても角度は変わりません（真横を向いて長さがほぼ 0 になる場合を除く）。
1 段目の AAP を 2 段目が読むため、出力は 1 フレーム遅れます（`SenderLocalPosition` では段ごとにレイヤーを分けます）。

内部パラメーターは `SB_AxisToParameter/...` で始まる名前です（同期しません）。

### Transforming フェーズ（Modular Avatar の後）

Merge Armature / Bone Proxy で階層が確定した後に、アバター直下へ次の構造を生成します。
同じターゲット・同じ基準の項目は Sender / Receiver を共有します。

SDK 3.10.6+（SenderLocalPosition）:

```
アバタールート
└─ AxisToParameter                 … VRC Scale Constraint（ソース = WorldFix と同じ World プレハブ。ロスティスケール 1 固定）
   └─ GetAxis_(ターゲット名)       … VRC Rotation Constraint（ソース = 基準。未指定なら World プレハブ）。リグごとに X 方向へ 0.2 ずつずらす
      ├─ Rotation                  … VRC Rotation Constraint（ソース = ターゲット）
      │  └─ Sender                 … 0° 方向へ 0.05 の位置。Sphere Sender（向き合わせ不要）
      └─ Receiver_SenderLocalPosition … Sphere Receiver 1 つ。parameter 接頭辞 + Advanced Parameters の SenderLocalPosition
```

コンテナをアバター直下に置きスケールをワールド固定するため、コンポーネント親のスケールに依らず Contact の実寸と `_SenderLocalPosition` の振幅（≈ ±0.05 m）は安定します。
フォールバック（Box Face Proximity）では Sender に向き固定 Constraint が付き、使う軸だけ `Receiver_X / _Y / _Z` を生成します。

Z 軸の項目は上方ベクトルを使うため、`GetAxis_(ターゲット名)_Up` を別に生成します。
基準が未指定の項目では、`GetAxis_` の Constraint のソースが WorldFix と同じ World プレハブになります。

| 取得する軸 | Sender の向き | 使う座標成分（sin / cos） |
|------------|---------------|---------------------------|
| X | 前方（+Z） | Y, Z |
| Y | 前方（+Z） | X, Z |
| Z | 上方（+Y） | X, Y |

同じフェーズで、このコンポーネント向けのアニメーションカーブを生成先へ書き換えます。
VRCFury 等が後から追加したアニメーションも拾うため、Optimizing フェーズ（Modular Avatar の前）でもう一度書き換えてからコンポーネントを削除します。

### 計測方式

Contact から Sender の座標を読む部分は `AxisSensor`（`Editor/Components/AxisToParameterSensors.cs`）として差し替えられるようにしてあり、Animator とリグの生成はどちらもここを経由します。優先順は次のとおりです。

| 優先 | 方式 | 条件 | Contact | 座標パラメーター | BlendTree の正規化 |
|------|------|------|---------|------------------|-------------------|
| 1 | `SenderLocalPositionAxisSensor` | SDK 3.10.6+ | Sender 1 + Receiver 1 | `{prefix}_SenderLocalPositionX/Y/Z`（相対位置。振幅 ≈ ±0.05 m） | `値 / 0.05` |
| 2 | `BoxProximityAxisSensor` | 常時（フォールバック） | Sender 1 + Receiver 2〜3 | `{prefix}/X|Y|Z` | `2a - 1` |

インスペクタの「計測方式」表示で、実際に選ばれた方式を確認できます。
コンポーネントの保存データは計測方式に依存しないので、既存のアバターも設定を変えずに新しい方式で再ビルドできます。

ZeroToOne など一周する範囲では、角度 0° が不連続点になるため、P をわずかに valueMin 側へ寄せて 0° で 0 と 1 の平均（0.5）にならないようにしています。

## 注意事項

- 出力パラメーターは Float として生成します。同名の Int / Bool パラメーターが他にあると Modular Avatar の型衝突になります。
- 同じパラメーター名を複数の項目に指定した場合、2 つ目以降はスキップします（値が加算されて壊れるため）。
- Contact 数は計測方式と共有ユニット数で変わります（Local Only でない場合はパフォーマンスランクに含まれます）。インスペクタに生成数を表示します。
- このオブジェクトやターゲットに非一様なスケールをかけると、Contact の形が歪んで値がずれます。
- 「Animator をアセットとして書き出す」は確認用です。`Assets/Generated/SamiVRCBlocks/AxisToParameter/` に保存するだけで、シーンには何も追加しません。

## 関連

- [WorldFix](02-world-fix.md) — 生成コンテナのスケール固定に同じ World プレハブを使用
- [RemapPosition](12-remap-position.md) — 生成した Constraint へのアニメーション書き換えの仕組みが同じ
- [ParameterSmoothing](04-parameter-smoothing.md) — 出力した値を滑らかにする

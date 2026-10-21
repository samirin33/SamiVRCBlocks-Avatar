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

アバターからはワールドの絶対的な向きを知る手段が無いため、基準が未指定の項目は「アバター読み込み時の向きでワールドに固定したオブジェクト」を基準にします。

- 上下（X 軸・Z 軸の傾き）はワールドの水平・鉛直に一致します（読み込み時にアバターが直立している前提）。
- Y 軸（ヨー）の 0° は、アバターを読み込んだときにアバターが向いていた方向です。ワールド内で向きを変えると、そこからの角度になります。
- 読み込みのタイミングはプレイヤーごとに違うため、他のプレイヤーの環境ではヨーの 0° がずれます。上下の傾きは一致します。
- `Constraint 有効` をオフ→オンにすると、その時点のアバターの向きで固定し直します（ヨーの 0° をリセットできます）。
- エディタ上のプレビューは Unity のワールド軸で計算します。

### アニメーション

このコンポーネントの `Constraint 有効` / `Contact 有効` を Animation ウィンドウで録画したクリップは、ビルド時に次のように書き換えます。

| コンポーネントのプロパティ | 書き換え先 |
|----------------------------|------------|
| `constraintActive` | 生成したすべての VRC Rotation Constraint（ワールド基準のアンカーを含む）の `IsActive` |
| `contactActive` | 生成したすべての VRC Contact Sender / Receiver の `m_Enabled` |

## ビルド時の挙動

### Generating フェーズ（Modular Avatar の前）

Animator を生成し、このオブジェクトの子 `AxisToParameter_Module` に MA Merge Animator（FX、Write Defaults オン）として追加します。
レイヤーは 1 ステートの Direct BlendTree で、次の 2 段で角度を復元します。

1. 計測方式が用意した「Sender の座標に比例するパラメーター」から sin / cos 成分を線形結合し、項目ごとに「値が飛ぶ角度」が真後ろに来るよう回した 2 つの値（P, Q）を AAP で書く
2. P の符号で左右を切り替え、それぞれを 2D Simple Directional（15° 間隔＋任意範囲の端）でブレンドして出力パラメーターへ書く

Simple Directional は入力の長さがサンプルの輪（長さ 1）より短いと全サンプルの平均へ寄っていくため、P, Q は 100 倍して常に輪の外側に置いています。
輪の外側では向きだけでブレンドされるので、他の軸の回転で sin / cos の大きさが縮んでも角度は変わりません（真横を向いて長さがほぼ 0 になる場合を除く）。
1 段目の AAP を 2 段目が読むため、出力は 1 フレーム遅れます。

内部パラメーターは `SB_AxisToParameter/...` で始まる名前です（同期しません）。

### Transforming フェーズ（Modular Avatar の後）

Merge Armature / Bone Proxy で階層が確定した後に、このオブジェクトの下へ次の構造を生成します（`Runtime/Components/Prefab/Axis2Prameter.prefab` と同じ構造）。
同じターゲット・同じ基準の項目は Sender / Receiver を共有し、使わない軸の Receiver は生成しません。

```
このオブジェクト
└─ AxisToParameter
   └─ GetAxis_(ターゲット名)       … VRC Rotation Constraint（ソース = 基準）。リグごとに X 方向へ 0.2 ずつずらす
      ├─ Rotation                  … VRC Rotation Constraint（ソース = ターゲット）
      │  └─ Sender                 … 0° 方向へ 0.05 の位置。VRC Rotation Constraint（ソース = GetAxis）で向きを固定した Box Sender
      ├─ Receiver_X / _Y / _Z      … 使う軸だけ。+Z 面を各軸の正方向へ向けた Box Receiver（Face Proximity）

アバタールート
└─ AxisToParameter_World           … 基準が未指定の項目があるときだけ。VRC Rotation Constraint（ソース = アバタールート、Freeze To World オン）
```

Z 軸の項目は上方ベクトルを使うため、`GetAxis_(ターゲット名)_Up` を別に生成します。
基準が未指定の項目では、`GetAxis_` の Constraint のソースが `AxisToParameter_World` になります。

| 取得する軸 | Sender の向き | 使う Receiver |
|------------|---------------|---------------|
| X | 前方（+Z） | Y, Z |
| Y | 前方（+Z） | X, Z |
| Z | 上方（+Y） | X, Y |

同じフェーズで、このコンポーネント向けのアニメーションカーブを生成先へ書き換えます。
VRCFury 等が後から追加したアニメーションも拾うため、Optimizing フェーズ（Modular Avatar の前）でもう一度書き換えてからコンポーネントを削除します。

### 計測方式

Contact から Sender の座標を読む部分は `AxisSensor`（`Editor/Components/AxisToParameterSensors.cs`）として差し替えられるようにしてあり、Animator とリグの生成はどちらもここを経由します。
現在の方式は `BoxProximityAxisSensor` のみです（Receiver の値 `a` に対して `2a - 1` が座標 / 軌道半径にほぼ等しい）。

VRChat は今後の SDK で、Receiver が「Sender の中心点の、Receiver から見たローカル座標」をパラメーターとして出せるようにする予定です（[Developer Update - 13 August 2026](https://ask.vrchat.com/t/developer-update-13-august-2026/48800)）。
これが使えるようになれば、次の手順で Receiver 1 つの方式へ移行できます。

1. `AxisSensor` を継承したクラスを作り、`GetChannel` で座標パラメーター名と線形変換（`Scale * 値 + Offset` が座標 / 0.05 m になるよう）を返す
2. `CreateReceivers` で `GetAxis_` 直下に Receiver を 1 つ作る。Sender の向きを揃える必要が無いので `RequiresAlignedSender` は false にする
3. `IsSupported` で SDK が対応しているかを判定し、`AxisSensors` の候補の先頭に足す

角度の復元は sin / cos の比だけを使うため、座標の単位や倍率が全成分で共通なら結果は変わらず、BlendTree の生成側は変更不要です。
コンポーネントの保存データは計測方式に依存しないので、既存のアバターも設定を変えずに新しい方式で再ビルドできます。
テストの `GeneratedAnimator_RestoresAngleWithPositionSensor` は、単位と原点を変えた仮の座標方式で角度が復元できることを確認しています。

## 注意事項

- 出力パラメーターは Float として生成します。同名の Int / Bool パラメーターが他にあると Modular Avatar の型衝突になります。
- 同じパラメーター名を複数の項目に指定した場合、2 つ目以降はスキップします（値が加算されて壊れるため）。
- 現在の計測方式では、1 項目あたり Contact は Sender 1 + Receiver 2（同じターゲットの X と Y は Sender 1 + Receiver 3 を共有）。Local Only でない場合はパフォーマンスランクの Contact 数に含まれます。インスペクタに生成数を表示します。
- このオブジェクトやターゲットに非一様なスケールをかけると、Contact の形が歪んで値がずれます。
- 「Animator をアセットとして書き出す」は確認用です。`Assets/Generated/SamiVRCBlocks/AxisToParameter/` に保存するだけで、シーンには何も追加しません。

## 関連

- [RemapPosition](12-remap-position.md) — 生成した Constraint へのアニメーション書き換えの仕組みが同じ
- [ParameterSmoothing](04-parameter-smoothing.md) — 出力した値を滑らかにする

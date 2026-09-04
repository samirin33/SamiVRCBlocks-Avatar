# ExtendedParameters

## 概要

ビルド時（Resolving・Modular Avatar より前）に **パラメーター追加プレファブ** をアバター直下に配置するコンポーネントです。
**パラメーター追加プレファブ**はVRC上でAnimator上で取得できる新しいパラメーターを追加することができます。例えば現在のFPS値をパラメーターで取得できるようになります。
アバターに追加されているギミックに一つでも**パラメーター追加プレファブ**が指定されているとその機能が追加されます。複数ギミックに渡って複数箇所から指定されている場合でも重複しません。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB ExtendedParameters**
- アバターの Animator がアタッチされている GameObject、またはその子に追加します。

## 使い方

### 基本手順

1. ExtendedParameters をアタッチした GameObject を選択
2. **パラメーター追加プレファブを追加** ボタンから、配置したいプレファブを選択（パッケージ内のモジュール用フォルダから選択）
3. 選択中のプレファブ一覧で、配置するプレファブを追加・削除
4. プレファブが Animator パラメーターを必要とする場合は、**Animatorにパラメーターを追加** ボタンで不足パラメーターを一括追加

それぞれのパラメーター追加プレファブの機能については[下記](#パラメーター追加プレファブ)を参照してください。

## 注意事項

- Animator にパラメーターを追加する機能は、AnimatorController が直接設定されている場合にのみ利用できます。AnimatorOverrideController の場合は、ベースの AnimatorController が編集されます。

# パラメーター追加プレファブ

## FPSCounter

アバター動作環境の現在のフレームレートを取得できます。
(厳密には1フレーム前の値が取得されます)
1~255で取得する FPS/Result と
0~1で取得する FPS/Value があります。
Constraint移動の速度補完等に有効です。

## IsMirrorReflection

Animatorの動作環境がPlayerLocalかMirrorReflectionレイヤーのどちらで行われているかを取得できます。
情報を取得するまでに数フレーム待機する必要があるのでIsMirrorReflectionパラメーターが-0.5以上になるのを待機してください。
ローカル視点にのみ映るオブジェクトやコライダージャンプさせたくないコライダーの設定に有効です。

## HeadRotX

プレイヤーの頭のX軸角度を取得します。範囲は0~1で下を向くと0，上を向くと1になります。
HeadRotX_Angleだと移動時に値が振動するのでHeadRotX_Angle_Smoothedでスムージング化された値の使用をおすすめします。
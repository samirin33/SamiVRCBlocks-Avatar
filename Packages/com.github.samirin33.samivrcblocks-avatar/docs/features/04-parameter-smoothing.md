# ParameterSmoothing

## 概要

指定した **Animator パラメータ**の値をスムージングするコンポーネントです。パラメータ名とスムージングの重み（smoothWeight）を複数登録でき、ビルド時にスムージング用のレイヤーなどが生成されます。**ResizableSyncParameters** で Float 型を指定した場合も、内部でこのコンポーネントが自動追加・設定されます。また、スムージング用に FPSCounter モジュールを ExtendedParameters に登録します。ExtendedParameters 経由で配置されたプレファブ内の本コンポーネントもビルド対象です。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB ParameterSmoothing**
- アバターの Animator がアタッチされている GameObject、またはその子に追加します。ResizableSyncParameters の Float 設定がある場合は、同じ GameObject に ParameterSmoothing が自動で追加されます。

## 使い方

### 基本手順

1. ParameterSmoothing をアタッチした GameObject を選択
2. **parameterSmoothingData** に要素を追加
3. 各要素で **parameterName**（スムージング対象のパラメータ名）と **smoothWeight**（0〜1、スムージングの強さ）を指定

### オプション・設定

- smoothWeight が大きいほど変化がなめらかになり、小さいほど元の値に近く素早く追従します。

## 注意事項

- スムージング処理は[AAP](https://vrc.school/docs/Other/AAPs/)によって行われます。処理後の値はVRCPrameterDriverで取得できないのでご注意ください。
- ResizableSyncParameters で Float 型を複数指定していると、ParameterSmoothing の parameterSmoothingData に `_Snapped` 付きのパラメータが自動で追加されます。手動で同じ名前を追加すると二重になるため避けてください。

## 関連

- [ResizableSyncParameters](01-resizable-sync-parameters.md) — ビット削減とスムージングの組み合わせ
- [ExtendedParameters](05-extended-parameters.md) — ParameterSmoothing が FPSCounter を登録する先

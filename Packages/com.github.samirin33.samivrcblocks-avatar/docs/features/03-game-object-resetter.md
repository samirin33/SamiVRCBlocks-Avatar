# GameObjectResetter

## 概要

ビルド時に対象 GameObject の **有効/無効**・**位置**・**回転**・**スケール**を指定値にリセットするコンポーネントです。
他ユーザーが改変しやすいようにモデルの配置をずらして配置し、ビルド時に再配置や非アクティブ化することで正しくギミックを動作させたりVRC上でのデフォルト状態やアバタープレビューで意図せずオブジェクトが表示されてしまう問題を回避します。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB GameObjectResetter**
- リセットしたい GameObject にアタッチします。

## 使い方

### 基本手順

1. リセットしたい GameObject に GameObjectResetter をアタッチ
2. **objectEnable** を有効にすると、**resetObjectEnable** の値でビルド後の Active 状態を設定
3. **resetPosition** / **resetRotation** / **resetScale** で、位置・回転・スケールをリセットするか選択
4. 各 **reset〜Value** でリセット先の値を指定
5. **isLocalPosition** / **isLocalRotation** / **isLocalScale** で、ローカルかワールドかを選択
6. **destroyOnReset** を有効にすると、リセット処理のあとその GameObject が削除されます

## 注意事項

- destroyOnReset を使うと、その GameObject はビルド後のアバターからなくなります。子オブジェクトが必要な場合は、リセット対象を子だけにするか、削除を使わないでください。(EditorOnlyタグとほぼ同挙動です)
- 位置・回転・スケールは「ビルド時」に一度だけ適用されます。ランタイムで動的に変えることはしません。

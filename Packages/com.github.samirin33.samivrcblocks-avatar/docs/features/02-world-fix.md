# WorldFix

## 概要

指定オブジェクトを **ワールド空間で固定**するコンポーネントです。ビルド時に Position / Rotation / Scale の Constraint を付与し、VRChat のワールド基準オブジェクトをソースにして位置・回転・スケールを維持します。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB WorldFix**
- ワールドで固定したい GameObject にアタッチします。

## 使い方

### 基本手順

1. 固定したい GameObject に WorldFix をアタッチ
2. **fixPosition** / **fixRotation** / **fixScale** で、位置・回転・スケールのうちどれを固定するか選択
3. **positionX/Y/Z**、**rotationX/Y/Z**、**scaleX/Y/Z** で軸ごとの有効/無効を指定
4. **editorApply** を有効にすると、エディタの再生なしでも Constraint の効果をプレビューできます

### オプション・設定

- 位置と回転を両方有効にした場合は **ParentConstraint**、それ以外は **PositionConstraint** / **RotationConstraint** / **ScaleConstraint** が使われます。
- ソースはパッケージ内のワールド基準用プレファブ（GUID 参照）です。

## ビルド時の挙動

- **Resolving（Modular Avatar より前）**: 対象 GameObject に Constraint を追加し、ワールド基準の Transform をソースとして設定します。

## 注意事項

- 実行時は `ExecuteAlways` でエディタプレビューを更新しています。editorApply が true のときのみ、非再生時にも適用されます。
- Constraint のソースはビルド時に解決されるため、既存の Constraint 設定と競合しないか確認してください。

## 関連

- [GameObjectResetter](03-game-object-resetter.md) — ビルド時に位置・回転・スケールをリセットする場合

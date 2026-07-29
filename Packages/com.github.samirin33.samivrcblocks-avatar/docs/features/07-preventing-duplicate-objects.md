# PreventingDuplicateObjects

## 概要

同じ **id** を持つオブジェクトがアバター内に複数あるとき、ビルド時に警告し、最初の1つ以外を削除するコンポーネントです。
プレハブやギミックを複数載せた結果、意図せず同種オブジェクトが重複する事故を防ぎます。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB PreventingDuplicateObjects**
- 重複を検知したい GameObject にアタッチし、一意な **ID** を設定します。

## 使い方

### 基本手順

1. 監視したい GameObject に PreventingDuplicateObjects をアタッチ
2. **ID** に識別子を入力（同じ種類のオブジェクトは同じ ID）
3. アバター内に同じ ID が複数ある状態でビルドすると、ダイアログで階層一覧が表示され、2つ目以降の GameObject が削除されます

## ビルド時の挙動

- NDMF **Transforming**（Modular Avatar より前）で、アバター内の全 `PreventingDuplicateObjects` をまとめて処理します
- ID が空のものは重複判定の対象外です
- 処理後、残ったコンポーネント自身も削除されます

## 注意事項

- 削除対象はコンポーネントだけでなく、その **GameObject 全体** です
- 子オブジェクトごと消えるため、必要な子がある場合は ID の付け方・配置を見直してください

## 関連

- [AudioClipReplacer](06-audio-clip-replacer.md)


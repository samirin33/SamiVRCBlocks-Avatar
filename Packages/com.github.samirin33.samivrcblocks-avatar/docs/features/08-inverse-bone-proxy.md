# InverseBoneProxy

## 概要

通常の Modular Avatar Bone Proxy とは **付与先が逆**のコンポーネントです。AvatarRoot からの相対パスで対象を探し、ビルド時にそのオブジェクトへ MA Bone Proxy を付与し、本コンポーネントの Transform を追従させます（対象が自身を追従する形）。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB Inverse Bone Proxy**
- 追従させたい側（ソース側）の GameObject にアタッチします。

## 使い方

### 基本手順

1. 追従元となる GameObject に InverseBoneProxy をアタッチ
2. **Target Object** に、追従させたいアバター配下の Transform を指定（パス文字列でも可）
3. **Attachment Mode** で位置・回転の合わせ方を選択
4. 必要なら **Match Scale** を有効化
5. エディタ上でプレビューしたい場合は **Editor Apply Transform** を有効化

### オプション・設定

| 項目 | 説明 |
|------|------|
| Target Object | ビルド時に MA Bone Proxy を付与する対象（Avatar 配下） |
| Target Object Path | AvatarRoot からの相対パス。空なら Target Object の参照を使用 |
| Attachment Mode | MA Bone Proxy と同じ。位置・回転の適用方法 |
| Match Scale | スケールを対象に合わせるか |
| Editor Apply Transform | エディタ非再生時に、対象へ位置・回転・スケールをプレビュー適用するか |

**Attachment Mode の意味**

- **As Child At Root** — 位置・回転とも合わせる
- **As Child Keep World Pose** — ワールド姿勢を維持
- **As Child Keep Rotation** — 回転を維持・位置のみ合わせる
- **As Child Keep Position** — 位置を維持・回転のみ合わせる

## ビルド時の挙動

- **Resolving（Modular Avatar より前）**: 対象 Transform を解決し、既存の MA Bone Proxy を除去したうえで新規付与します。ターゲットは本コンポーネントの Transform です。
- 対象が見つからない場合は警告を出してスキップします。

## 注意事項

- 通常の Bone Proxy は「自身がターゲットを追従」するのに対し、本コンポーネントは「ターゲットが自身を追従」します。

## 関連

- Modular Avatar の Bone Proxy（追従関係が逆）

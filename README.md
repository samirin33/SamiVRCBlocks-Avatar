# SamiVRCBlocks-Avatar

Samirin33 の VRChat Avatar 3.0 向け **NDMF（Modular Avatar）** ベースのビルド時コンポーネントパッケージです。  
パラメータの同期ビット削減・ワールド固定・オブジェクトリセット・モジュール追加など、アバターのビルド時に処理を行うコンポーネントを提供します。

## 必要環境

- Unity 2022.3 以降（VRChat SDK 対応バージョン）
- [VRChat Avatars](https://vcc.docs.vrchat.com/vpm/packages#vrchat-official-packages) 3.7.0 以上
- [NDMF](https://github.com/bdunderscore/ndmf) 1.5.0 以上
- [Modular Avatar](https://github.com/bdunderscore/modular-avatar) 1.8.0 以上

## インストール

こちらから  
https://samirin33.github.io/Samirin33VPM/

### VPM（推奨）

1. [VRChat Creator Companion](https://vcc.docs.vrchat.com/vpm/installation) を開く
2. **Settings** → **Packages** → **Add Repository**
3. Listing URL を追加  
   `https://samirin33.github.io/Samirin33VPM/vpm.json`
4. プロジェクトのパッケージ一覧から **SamiVRCBlocks-Avatar** をインストール

### 手動（UPM / Git）

1. `Window` → `Package Manager` → `+` → `Add package from git URL`
2. 以下を入力  
   `https://github.com/samirin33/SamiVRCBlocks-Avatar.git`

## 使い方

各コンポーネントは **Add Component** から **`SamiVRCBlocks-Avatar`** カテゴリで追加できます（表示名は **`SB {コンポーネント名}`**）。

詳細は **[機能別ドキュメント](./Packages/com.github.samirin33.samivrcblocks-avatar/docs/README.md)** を参照してください。

## ライセンス

MIT License

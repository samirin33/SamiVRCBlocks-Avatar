# AudioClipReplacer

## 概要

Animator 内の **VRC Animator Play Audio**、または指定 **AudioSource** の音源を、ビルド時に差し替えるコンポーネントです。
ギミック開発者がターゲットと説明を用意し、エンドユーザーが差し替えたい音だけを有効化して Clips を指定する想定です。

## 追加方法

- **Add Component** → **SamiVRCBlocks-Avatar** → **SB AudioClipReplacer**
- MA Merge Animator と同じ階層、または Playable Layer を書き換える対象アバター配下にアタッチします。

## 使い方

### ユーザー向け（音の差し替え）

1. 置き換え項目のトグルを有効にする
2. **Clips** に差し替えたい AudioClip を設定（AudioSource ターゲットでは先頭の1つを使用）
3. 必要に応じて Volume / Pitch（Animator State 時は Playback Order / Delay も）の上書きを有効化
4. **デフォルト値を取得** で、元の Play Audio または AudioSource から現在値を読み込めます

### 開発者向け設定

1. Inspector の **開発者向け設定** を開く
2. **Mode** を選択
   - **Direct**: Merge Animator と同じ GameObject の **Animator** の Controller を複製・差し替え（Source Controller は自動表示）
   - **PlayableLayer**: Avatar Descriptor の指定 Playable Layer を複製・差し替え
3. **項目を追加** で置き換えエントリを作成し、**説明** と **ターゲット** を設定
   - **AnimatorState**: 検索ステート名を指定（VRC Animator Play Audio を置き換え）
   - **AudioSource**: 対象 AudioSource を指定（アタッチされている clip を直接置き換え）

## ビルド時の挙動

- NDMF **Resolving**（Modular Avatar より前）で実行されます
- AnimatorState ターゲット: 対象コントローラを `Assets/Generated/SamiVRCBlocksAvatar/AudioClipReplacer/` に複製し、ステート上の VRC Animator Play Audio を上書きします
- AudioSource ターゲット: 指定 AudioSource の clip / volume / pitch を直接上書きします（コントローラ複製は不要）
- 処理後、本コンポーネントは削除されます

## 注意事項

- Direct モードでは Merge Animator と同じ GameObject に Animator が必要です
- ステート名がコントローラ内に見つからない場合は警告が出て、そのエントリはスキップされます

## 関連

- [PreventingDuplicateObjects](07-preventing-duplicate-objects.md)


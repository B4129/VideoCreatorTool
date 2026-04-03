# Project Memory

重要な決定事項、実装済み機能、現在の進捗を記録します。

## 最近の更新 (2026-04-04)

### TODO実装完了

**音声伸長機能（ffmpeg統合）**
- Line 612のTODOを実装しました
- `Services/AudioStretchService.cs`を作成し、ffmpegを使った実際の音声伸長機能を追加
- ブロックリサイズ時に音声ファイルの長さも自動的に伸長されるようになりました
- 伸長比率が0.5x〜2.0xを超える場合は複数フィルターチェーンで対応

### クリーンアップ処理
- `App.xaml.cs`に`OnExit()`ハンドラーを追加
- アプリケーション終了時に一時音声ファイルを自動削除
- Tempフォルダー: `%TEMP%/VideoCreator/AudioStretch/`

### 残りのTODO
- Line 512 & 517: 視覚的フィードバック（ドラッグ＆ドロップ時のトラック強調表示）
  - これらはTracksPanelがXAMLに追加されるまで実装不可能
  - コメントを更新して理由を明記

## 重要な決定事項

### ffmpeg依存関係
- 音声伸長機能にはffmpegが必要
- ffmpegがインストールされていない場合は元の音声をそのまま使用（機能グラデーション）
- ffmpeg探索パス:
  1. PATH環境変数
  2. `C:\ffmpeg\bin\ffmpeg.exe`
  3. `C:\Program Files\ffmpeg\bin\ffmpeg.exe`

### 一時ファイル管理
- 伸長した音声ファイルはTempフォルダーに保存
- アプリケーション終了時に自動クリーンアップ
- 手動削除も可能: `%TEMP%/VideoCreator/AudioStretch/`

## リファクタリング完了事項

### 音声サービス改善
- `Services/AudioService.cs` - シンプルで安定した設計
  - 単一MediaPlayerインスタンス
  - `StopAll()`で即時停止
  - 適切な非同期処理

### コード整理
- `ViewModels/TimelineModels.cs` - モデルクラスを分離
- `ViewModels/TimelineTrackViewModel.cs` - トラックViewModelを分離
- `Utilities/ColorHelper.cs` - ランダムカラー生成を共通化
- `Core/TimelineConstants.cs` - マジックナンバーを定数化

### アーキテクチャ
- `Views/TimelineView.xaml.cs` - イベントハンドラーを維持しつつ整理

## 現在の状態

**ビルド状態**: 成功 （警告50個、エラー0個）
**音声同期**: 改善済み（スタッタリング解消、停止時に音も停止）
**グリッド表示**: トラック領域内に修正済み
**リファクタリング**: 主要なものは完了

## 次のステップ（提案）

1. ffmpegのインストール方法のドキュメント化
2. TracksPanel UI実装時の視覚的フィードバック追加
3. 音声伸長処理の進捗表示UI（オプション）

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

**ビルド状態**: 成功 （警告48個、エラー0個）
**音声同期**: 改善済み（スタッタリング解消、停止時に音も停止）
**グリッド表示**: トラック領域内に修正済み
**リファクタリング**: 主要なものは完了
**実装済み機能**:
- テキスト位置プレビュードラッグ
- 立ち絵表示
- 画像ブロックプレビュー
- トラック別音量コントロール
- トラックミュート機能
- 音声速度変更プレビュー
- エクスポート失敗時エラー詳細表示
- Undo/Redo機能（ブロック移動、削除、分割、テキスト編集、プロパティ変更、トラック操作）
- 未保存変更検出と警告
- 最近開いたファイル履歴

## 最近の更新 (2026-04-04 夕刻)

### テキスト位置プレビュードラッグ実装

**機能概要:**
- プレビュー画面上でテキストブロックをドラッグ移動
- ドラッグ位置に応じてTextPositionX/Yプロパティを自動更新（0-100%範囲）
- 画面座標からパーセンテージへの変換ロジックを実装

**更新ファイル:**
1. `Views/PreviewView.xaml` - テキストオーバーレイ領域にMouseイベントハンドラー追加
2. `Views/PreviewView.xaml.cs` - ドラッグ処理（MouseDown/Move/Up）を実装
3. `Converters/PercentageToPixelConverter.cs` - 新規作成、座標変換用

**実装詳細:**
- TextOverlayBorder（透明Border）で全体を覆い、マウスイベントをキャプチャ
- 開始位置と現在位置の差分をピクセル単位で計算
- コンテナサイズで割ってパーセンテージに変換
- TextPositionX/Yは0-100%の範囲にクランプ

### エクスポート失敗時エラー詳細表示

**機能概要:**
- エクスポート失敗時にffmpegのエラー詳細をMessageBoxに表示
- 終了コードとstderr出力を両方表示
- ユーザーが失敗原因を特定しやすく改善

**更新ファイル:**
1. `Services/ExportService.cs` - エラー出力収集と表示処理を追加

### 立ち絵表示機能

**機能概要:**
- キャラクター画像（立ち絵）をプレビュー画面に表示
- TextPositionX/Yの値に応じて配置調整

**更新ファイル:**
1. `Views/PreviewView.xaml` - 立ち絵Image要素追加
2. `ViewModels/PreviewViewModel.cs` - LoadStandingImage()メソッド追加

### 画像ブロックのプレビュー表示

**機能概要:**
- プレビュー画面に画像ブロックを重ねて表示
- オパシティ設定も反映

**更新ファイル:**
1. `Views/PreviewView.xaml` - ItemsControlで画像ブロックを表示
2. `ViewModels/PreviewViewModel.cs` - CurrentImageBlocksプロパティ追加

### トラック別音量コントロール

**機能概要:**
- 各トラックにVolumeスライダー（0.0-2.0）を追加
- 音声再生時にトラックの音量を反映

**更新ファイル:**
1. `ViewModels/TimelineTrackViewModel.cs` - Volumeプロパティ追加
2. `Services/AudioService.cs` - 音量パラメータ対応
3. `ViewModels/TimelineViewModel.cs` - PlayAudioBlockAsyncで音量を渡す

### トラックミュート機能実装

**機能概要:**
- 各トラックにミュートチェックボックスを追加
- ミュート時はそのトラックの音声を再生しない

**更新ファイル:**
1. `ViewModels/TimelineTrackViewModel.cs` - IsMutedプロパティ追加
2. `Services/AudioService.cs` - isMutedパラメータ対応
3. `ViewModels/TimelineViewModel.cs` - PlayAudioBlockAsyncでミュート状態を渡す

### 音声速度変更プレビュー実装

**機能概要:**
- ブロックのPlaybackSpeed設定をAudioServiceで反映
- プレビュー時に指定速度で再生

**更新ファイル:**
1. `ViewModels/TimelineViewModel.cs` - PlayAudioBlockAsyncでblock.PlaybackSpeedを渡す

---

## 最近の更新 (2026-04-04 午後)

### FFMpegCoreライブラリ統合

**重大なアーキテクチャ変更:**
- 外部ffmpeg呼び出しをFFMpegCore NuGetパッケージに置き換え
- ユーザーはffmpegを個別インストール不要（将来的に自動ダウンロード可能）

**更新ファイル:**
1. `Services/AudioStretchService.cs` - FFMpegCore使用に書き換え
2. `Services/WaveformService.cs` - 波形抽出をFFMpegCore化、非同期メソッド化
3. `ViewModels/PreviewViewModel.cs` - プレビューフレーム抽出をFFMpegCore化
4. `Services/FFMpegDownloadService.cs` - 新規作成、ffmpeg自動ダウンロード機能

**メリット:**
- ユーザーがffmpegをインストールしなくても動作（将来的に自動ダウンロード）
- 型の安全なAPI（Process.Startの脆弱性を回避）
- エラーハンドリングが容易
- コードが簡潔

**残る箇所:**
- `Services/ExportService.cs` - 複雑な動画エクスポート機能、まだffmpeg直接呼び出し
  - 将来的にFFMpegCore化可能だが、作業量が多いため保留

## 次のステップ（提案）

1. ~~ffmpegのインストール方法のドキュメント化~~ - 不要になりました（FFMpegCore統合）
2. TracksPanel UI実装時の視覚的フィードバック追加
3. 音声伸長処理の進捗表示UI（オプション）
4. ExportServiceのFFMpegCore化（大規模作業、別タスクとして計画）

# Claude Code 作業ルール

## 言語設定
- 全てのやり取りは日本語で行う

## Git運用
- 実装が完了したら必ずGitHubにpushする
- developブランチを使用して作業
- コミットメッセージは日本語で簡潔に記載

### コミット手順
```bash
cd "C:\Users\neko3\Desktop\agent\動画作成ツール\VideoCreatorWPF"
git add -A
git commit -m "feat: 実装内容の説明"
git push origin develop
```

## 開発フロー
1. 機能実装
2. ビルド確認
3. Gitコミット
4. GitHub push（自動実行）
5. Issue修正完了後、そのIssueをclose

## Issue管理
- 修正が完了したIssueは順次closeする
- GitHub APIまたはgh CLIを使用してclose
- コミットメッセージにIssue番号を含める（例: `fix: Issue #3 - 問題の説明`）

## スクリーンショット
- 機能追加時はdocsフォルダにスクリーンショットを保存
- README.mdに機能ごとのスクリーンショットを追加

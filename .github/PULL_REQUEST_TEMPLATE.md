## 概要

<!-- この PR が何を解決するか、1〜3 行で説明してください -->

## 関連 Issue

<!-- Closes #123 / Fixes #456 / Related to #789 のように記載 -->

## 変更内容

<!-- 主な変更点を箇条書き -->

-
-
-

## 動作確認

<!-- どのように動作を確認したか -->

- [ ] `dotnet build src/Kibinago.sln` 成功
- [ ] `dotnet test src/WindowsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj` がすべて緑
- [ ] サンプルアプリ (`Kibinago.Samples`) で動作確認

## チェックリスト

- [ ] [MVP 設計ルール](https://github.com/VoidAxon/winforms-mvp/wiki/Design-Rules) に違反していない
- [ ] パブリック API には XML ドキュメントコメントを記載
- [ ] 動作変更時は `CHANGELOG.md` の `[Unreleased]` セクションを更新
- [ ] 関連する Wiki ドキュメントを更新 (該当する場合)
- [ ] 新規コードに対してテストを追加 (該当する場合)
- [ ] `Kibinago.Core` (`netstandard2.0`) に WinForms 依存を持ち込んでいない / `net472` と `net10.0-windows` の両方でビルドできる

## 影響範囲

<!-- この変更が壊す可能性のある既存挙動 / 既存利用者への影響 -->

## スクリーンショット (UI 変更時)

<!-- 該当する場合のみ -->

## 備考

<!-- レビュアーに事前に伝えておきたい設計判断、未解決の論点等 -->

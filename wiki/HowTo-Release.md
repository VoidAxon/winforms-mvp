# HowTo: リリースする（GitHub Releases + GitHub Packages）

このページは、`WinformsMVP` / `WinformsMVP.DependencyInjection` の NuGet パッケージを
**GitHub Packages** に発行し、`.nupkg` を **GitHub Release** に添付するまでの、
メンテナ向けの完全な手順です。仕組みの説明・初回セットアップ・利用者側の使い方・
トラブルシュート・後片付けまで含みます。

AI / 機械向けの最短手順は [CLAUDE.md](../CLAUDE.md) の「Releasing」節を参照してください。

---

## 1. リリースの基本：タグを push するだけ

リリースは**タグ駆動**です。`v` で始まるタグを push すると、
`.github/workflows/release.yml` が自動で起動し、ビルド → テスト → パック → 発行 →
Release 作成までを行います。

```bash
# プレビュー版
git tag v1.0.0-preview.1
git push origin v1.0.0-preview.1

# 安定版
git tag v1.0.0
git push origin v1.0.0
```

- **バージョンはタグから決まります。** 先頭の `v` を取り除いた文字列がパッケージ
  バージョンになります（`v1.2.3-preview.4` → `1.2.3-preview.4`）。
  どの `.csproj` にもバージョンは書かれていません。**ハードコードしないでください。**
  ワークフローが `-p:Version=` で注入します。
- タグにハイフンが含まれる（例 `-preview.1`、`-rc.1`）と、
  - **NuGet 側**：自動的にプレリリース扱いになり、既定の一覧には出ません
    （`--prerelease` を付けた人だけが取得できます）。
  - **GitHub Release 側**：ワークフローが `--prerelease` を明示的に渡し、
    Release に pre-release の印が付きます。
- ハイフンの無いタグ（例 `v1.0.0`）は安定版リリースになります。

> プレビューを重ねる場合は `v1.0.0-preview.1` → `v1.0.0-preview.2` → … と進め、
> 固まったら `v1.0.0` を打ちます。

---

## 2. ワークフローが何をしているか

`.github/workflows/release.yml` の各ステップ（`windows-latest` 上で実行）：

1. **Checkout / Setup .NET SDK** — リポジトリ取得と SDK 準備。
2. **Derive version from tag** — `github.ref_name` から先頭 `v` を除去して `VERSION` を作り、
   ハイフンの有無で `IS_PRERELEASE` を決めます。
3. **Restore / Build (Release)** — `-p:Version=$VERSION` 付きでソリューションをビルド。
4. **Test (gate)** — テストスイートを実行。**1 件でも失敗するとリリースは中断します。**
5. **Pack** — `WinformsMVP` と `WinformsMVP.DependencyInjection` を同じ `-p:Version` でパック。
   DI パッケージはコアを ProjectReference 参照しているため、**同一バージョンのコアへの依存**が
   自動的に記録されます。
6. **Assert analyzer is bundled** — コアパッケージ内に
   `analyzers/dotnet/cs/WinformsMVP.Analyzers.dll` が含まれているかを検証し、
   無ければジョブを失敗させます（後述の「アナライザー同梱」を守るガード）。
7. **Push to GitHub Packages** — `dotnet nuget push` で各 `.nupkg` を発行。
8. **Create GitHub Release** — `gh release create` で Release を作成し、`.nupkg` を添付。
   同名タグの Release が既にある場合は資産を `--clobber` で上書きアップロード（再実行に強い）。

### なぜ `windows-latest` なのか

コアは `net40;net48` をターゲットにし、WinForms に依存します。これらは
.NET Framework の MSBuild が必要で、Linux ランナーではビルドできません。

### なぜ net40 がランナーでビルドできるのか

GitHub ランナーには net40 のターゲティングパックが入っていません。そこでコアの
`.csproj` は `Microsoft.NETFramework.ReferenceAssemblies.net40`（ビルド専用、
`PrivateAssets="all"`）を参照しています。これが net40 の参照アセンブリ（WinForms 含む）を
供給するため、ターゲティングパック無しでもコンパイルできます。`PrivateAssets="all"` により
このパッケージは利用者への依存として流れません。

### アナライザーの同梱（最重要の注意点）

MVP 設計ルールの Roslyn アナライザーは**コアパッケージに同梱**されており、
単独パッケージとしては発行しません（`WinformsMVP.Analyzers` は `IsPackable=false`）。

`IsPackable=false` は「このプロジェクトは単独でパッケージを出さない」という意味であって、
**その成果物が自動的にコアパッケージへ入るわけではありません**。同梱は
`src/WinformsMVP/WinformsMVP.csproj` で明示的に配線しています：

- MSBuild Target `BuildMVPAnalyzers` がアナライザーをビルドし、
- `<None Include="...\WinformsMVP.Analyzers.dll" Pack="true" PackagePath="analyzers/dotnet/cs" />`
  が `.nupkg` の `analyzers/dotnet/cs/` に配置します。

ワークフローのステップ 6 はこれが効いていることを毎回検証します。
「発行してからルールが効いていなかった」を防ぐためのガードなので、外さないでください。

---

## 3. 初回セットアップ（既に完了済み・再構築用の記録）

新しいリポジトリで同じ仕組みを一から作る場合の手順です。本リポジトリでは設定済みです。

1. **`Directory.Build.props`（リポジトリ直下）** — 共通のパッケージメタデータ
   （`Authors` / `PackageLicenseExpression=MIT` / `RepositoryUrl` / `PackageReadmeFile` など）を集約し、
   既定で `IsPackable=false`。誤って samples / tests / アナライザーを発行しないための土台です。
2. **発行する 2 プロジェクトの `.csproj`** — `<IsPackable>true</IsPackable>` で opt-in し、
   `PackageId` / `Description` / `PackageTags` と、README をパッケージに含める
   `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` を記述。
   （README の `<None>` は各 `.csproj` に置きます。`Directory.Build.props` は各プロジェクト本体より
   先に評価されるため、そこに `IsPackable` 条件付きで置くと `true` になる前に評価され効きません。）
3. **`.github/workflows/release.yml`** — 上記「2. ワークフローが何をしているか」の内容。
   トリガーは `on: push: tags: ['v*']`、`runs-on: windows-latest`、
   `permissions: contents: write` / `packages: write`。
4. **README** — 利用者向けのインストール手順（後述）を記載。

認証は GitHub Actions 組み込みの `GITHUB_TOKEN` で足ります（`packages: write` で発行、
`contents: write` で Release 作成）。**追加のシークレットは不要**です。発行先は
`https://nuget.pkg.github.com/${{ github.repository_owner }}/index.json`（owner を
コンテキスト変数で解決しているので fork / 移管にも追従します）。

---

## 4. リリース後の確認

```bash
# 実行状況を追う
gh run watch <run-id> --exit-status

# Release を確認（pre-release 印と添付 .nupkg）
gh release view v1.0.0-preview.1 --json isPrerelease,tagName,assets
```

GitHub の **Releases** ページ、および **Packages** ページ
（`https://github.com/<owner>?tab=packages`）にバージョンが出ていれば成功です。

---

## 5. 利用者側の使い方（README にも記載）

GitHub Packages は public リポジトリでも **NuGet の復元に認証が必要**です。
利用者には次のどちらかを案内します。

- **方法A — GitHub Packages フィード**：`read:packages` スコープの PAT を作り、
  `nuget.config` にソースと資格情報を設定して `dotnet add package WinformsMVP --prerelease`。
- **方法B — Releases から手動ダウンロード（認証不要）**：Release ページから `.nupkg` を落とし、
  ローカルフォルダをソースに登録してインストール。

詳細な手順とサンプル `nuget.config` は [README.md](../README.md) の「インストール」節にあります。

> 将来 nuget.org にも公開する場合は、ワークフローに nuget.org 向けの
> `dotnet nuget push`（nuget.org の API キーを使用）を 1 ステップ足すだけです。
> メタデータとパック工程は共通なので作り直しは不要です。

---

## 6. トラブルシュート

- **`dotnet nuget push` が "File does not exist (artifacts/*.nupkg)"**
  `dotnet nuget push` は引用符付きのワイルドカードを自前で展開しません。
  ワークフローは `Get-ChildItem` でファイルを列挙して 1 つずつ push しています。
  手動で push するときも実ファイルパスを渡してください。
- **net40 のビルドがランナーで失敗する**
  `Microsoft.NETFramework.ReferenceAssemblies.net40` の参照が消えていないか確認。
- **アナライザー検証ステップで失敗する**
  コア `.csproj` の `BuildMVPAnalyzers` Target と `analyzers/dotnet/cs` への
  `<None Include>` が壊れていないか確認。
- **テストで落ちてリリースが止まる**
  テストはゲートです。先にテストを直してから再度タグを打ちます。
- **同じタグで Release 作成が失敗する**
  Release ステップは既存 Release があれば `--clobber` で資産を上書きするため再実行に強い設計ですが、
  内容をやり直したいときは下記の後片付けでタグ・Release を消してから打ち直します。

---

## 7. 後片付け（取り消し・やり直し）

検証用タグや誤ったリリースを取り消す手順です。

```bash
# Release を削除
gh release delete v0.0.1-citest.1 --yes

# タグを削除（リモート → ローカル）
git push origin :refs/tags/v0.0.1-citest.1
git tag -d v0.0.1-citest.1
```

GitHub Packages に発行済みのバージョンを消すには、`read:packages` / `delete:packages`
スコープが要ります。

- CLI：`gh auth refresh -h github.com -s read:packages,delete:packages` でスコープを足してから、
  `gh api --method DELETE /user/packages/nuget/<package>/versions/<version-id>`
  （org 所有なら `/orgs/<org>/packages/...`）。
- UI：Packages → 対象パッケージ → **Package settings** → 最下部の **Danger Zone** から
  バージョン（またはパッケージ）を削除。

> プレリリース版（例 `0.0.1-citest.1`）は残しても実害はほぼありません。
> バージョン番号が低ければ正式版の解決時に選ばれず、`--prerelease` を付けない限り一覧にも出ません。

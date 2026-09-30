# 開発者向け情報

## 前提条件

- .NET SDK: `global.json` で固定しているバージョン（本リポジトリでは
  `10.0.401`、.NET 10 / C# 14 系のGA版。Preview・RC版は使用していません）。
- Windows 向け Native AOT ビルドを行うには、Windows環境に加えて
  「C++によるデスクトップ開発」ワークロード（Visual Studio Build Tools に含まれる
  MSVC ツールチェーンとWindows SDK）が必要です。
  ([参考: .NET Native AOTの前提条件](https://learn.microsoft.com/dotnet/core/deploying/native-aot/))

## ビルド・テスト・発行コマンド

restore / build / test は Windows・Linux・macOS のどこでも実行できます。

```bash
dotnet restore
dotnet build
dotnet test
```

### Windows向け Native AOT 発行

Windows向け Native AOT の単一exe発行は、Windowsで次のコマンドを実行して
ください。

```powershell
dotnet publish src/Moinsearch -c Release -r win-x64 --self-contained -p:PublishAot=true
```

Linuxからのクロスコンパイルはサポートされていません。

生成された `moinsearch.exe` は
`src/Moinsearch/bin/Release/net10.0/win-x64/publish/` 配下に出力されます。

## GitHub Release

`vMAJOR.MINOR.PATCH` 形式のタグを push すると、GitHub Actions が Windows runner でテストと Native AOT 発行を行い、`moinsearch-<tag>-win-x64.zip`（例: `moinsearch-v0.0.1-win-x64.zip`）を添付した GitHub Release を作成します。ZIPには `moinsearch.exe`、`README.md`、`LICENSE` が含まれます。exeの製品バージョンはタグから設定されます。

```powershell
git tag v0.0.1
git push origin v0.0.1
```

## 動作確認

次のコマンドで `--help` が通信なしに表示されることを確認できます。

```powershell
.\moinsearch.exe --help
```

## MoinMoin Wiki側のXML-RPCの制約

- moinsearchの動作には対象WikiでXML-RPC (`?action=xmlrpc2`) の有効化が必要
- 検索結果は指定したWikiNameの閲覧権限に従う

# Key Vault Acmebot

[![Build Status](https://dev.azure.com/shibayan/azure-acmebot/_apis/build/status/Build%20keyvault-acmebot?branchName=master)](https://dev.azure.com/shibayan/azure-acmebot/_build/latest?definitionId=38&branchName=master)
[![Release](https://img.shields.io/github/release/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/releases/latest)
[![License](https://img.shields.io/github/license/shibayan/keyvault-acmebot.svg)](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)

これは Azure Key Vault 向けに Let's Encrypt 証明書の発行と更新を自動化するためのアプリケーションです。以下のような課題を解決するために開始しました。

- Key Vault を利用して安全に証明書を格納する
- 数多くの証明書を 1 つの Key Vault を使って集中管理
- 簡単にデプロイと設定が完了する
- 信頼性の高い実装
- モニタリングを容易に (Application Insights, Webhook)

Key Vault を使うことで Let's Encrypt 証明書の安全かつ集中管理が行えます。

## 注意

### Acmebot v3 へのアップグレード

https://github.com/shibayan/keyvault-acmebot/issues/80

## 目次

- [対応している機能](#対応している機能)
- [必要なもの](#必要なもの)
- [開始する](#開始する)
- [使い方](#使い方)
- [謝辞](#謝辞)
- [ライセンス](#ライセンス)

## 対応している機能

- 全ての Azure App Service (Web Apps / Functions / Containers, OS に関係なし)
- Azure CDN と Front Door
- Azure Application Gateway v2
- SANs (サブジェクト代替名) を持つ証明書の発行 (1 つの証明書で複数ドメインに対応)
- Zone Apex ドメイン向け証明書とワイルドカード証明書の発行

## 必要なもの

- Azure サブスクリプション
- Azure DNS と Azure Key Vault (Key Vault はデプロイ時に作成が可能)
- E メールアドレス (Let's Encrypt の利用登録に必要)

## 開始する

### 1. Acmebot をデプロイする

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank">
  <img src="https://aka.ms/deploytoazurebutton" />
</a>

### 2. アプリケーション設定の追加

- Acmebot:VaultBaseUrl
  - Azure Key Vault の DNS 名 (既に存在する Key Vault を使う場合)
- Acmebot:Webhook
  - Webhook 送信先の URL (オプション, Slack と Microsoft Teams を推奨)

### 3. App Service 認証を有効化する

Azure Portal にて `認証/承認` メニューを開き、App Service 認証を有効化します。「要求が認証されない場合に実行するアクション」として `Azure Active Directory でのログイン` を選択します。認証プロバイダーとして Azure Active Directory を利用することを推奨していますが、他のプロバイダーでもサポート外ですが動作します。

![Enable App Service Authentication with AAD](https://user-images.githubusercontent.com/1356444/49693401-ecc7c400-fbb4-11e8-9ae1-5d376a4d8a05.png)

認証プロバイダーとして Azure Active Directory を選択し、管理モードとして `簡易` を選択し「OK」を選択します。

![Create New Azure AD App](https://user-images.githubusercontent.com/1356444/49693412-6f508380-fbb5-11e8-81fb-6bbcbe47654e.png)

最後にこれまでの設定を保存して、App Service 認証の有効化が完了します。

### 4. Azure DNS へのアクセス制御 (IAM) を追加する

対象の DNS ゾーンもしくは DNS ゾーンが含まれているリソースグループの `アクセス制御 (IAM)` を開き、デプロイしたアプリケーションに対して `DNS Zone Contributor` のロールを割り当てます。

![temp](https://user-images.githubusercontent.com/1356444/64354572-a9628f00-d03a-11e9-93c9-0c12992ca9bf.png)

### 5. Key Vault のアクセスポリシーに追加 (既に存在する Key Vault を使う場合)

Key Vault のアクセスポリシーを開き、デプロイしたアプリケーションに対して `Certificate management` アクセスポリシーを追加します。

![image](https://user-images.githubusercontent.com/1356444/46597665-19f7e780-cb1c-11e8-9cb3-82e706d5dfd6.png)

## 使い方

### 新しく証明書を発行する

ブラウザで `https://YOUR-FUNCTIONS.azurewebsites.net/add-certificate` へアクセスして、Azure Active Directory で認証すると Web UI が表示されます。その画面から対象のドメインを選択し、必要なサブドメインを追加して実行すると、数十秒後に証明書の発行が完了します。

![Add certificate](https://user-images.githubusercontent.com/1356444/64176075-9b283d80-ce97-11e9-8ee7-02530d0c03f2.png)

`アクセス制御 (IAM)` の設定が正しくない場合には、ドロップダウンリストには何も表示されません。

### App Service (Web Apps / Functions / Containers)

Azure Portal から `TLS/SSL の設定` を開き、「秘密キー証明書 (.pfx)」から「Key Vault 証明書のインポート」ボタンを選択すると、Key Vault 証明書から App Service へインポートが行えます。

![image](https://user-images.githubusercontent.com/1356444/64438173-974c2380-d102-11e9-88c0-5ed34a5ce42a.png)

インポート後は、App Service によって自動的に証明書の更新がチェックされます。

### Application Gateway v2

- https://docs.microsoft.com/en-us/azure/application-gateway/key-vault-certs

### Azure CDN / Front Door

- https://docs.microsoft.com/en-us/azure/cdn/cdn-custom-ssl?tabs=option-2-enable-https-with-your-own-certificate
- https://docs.microsoft.com/en-us/azure/frontdoor/front-door-custom-domain-https#option-2-use-your-own-certificate

### API Management

- https://docs.microsoft.com/en-us/azure/api-management/configure-custom-domain

## 謝辞

- [ACMESharp Core](https://github.com/PKISharp/ACMESharpCore) 作者 @ebekker
- [Durable Functions](https://github.com/Azure/azure-functions-durable-extension) 作者 @cgillum とコントリビューター
- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) 作者 @MichaCo

## License

このプロジェクトは [Apache License 2.0](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE) の下でライセンスされています。

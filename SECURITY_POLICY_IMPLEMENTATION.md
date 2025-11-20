# OPC UA Security Policy 實作總結

## 概述

此次重構為 OPC UA 客戶端應用程式加入了完整的安全策略（Security Policy）和訊息安全模式（Message Security Mode）支援，使客戶端能夠與不同安全等級的 OPC UA 伺服器建立安全連線。

## 實作日期

2025-11-20

## 主要功能

### 1. Security Policy 支援

應用程式現已支援以下四種 OPC UA Security Policy：

| Security Policy | 加密演算法 | 簽名演算法 | 金鑰加密 | 安全等級 |
|----------------|-----------|----------|---------|---------|
| **None** | 無 | 無 | 無 | 0（無安全性） |
| **Basic256Sha256** | AES-256-CBC | SHA256 | RSA-OAEP | 中 |
| **Aes128_Sha256_RsaOaep** | AES-128-CBC | SHA256 | RSA-OAEP | 中高 |
| **Aes256_Sha256_RsaPss** | AES-256-CBC | SHA256 | RSA-PSS | 高 |

### 2. Message Security Mode 支援

每個 Security Policy 支援不同的 Message Security Mode：

- **None Policy**: 僅支援 `None` 模式（強制）
- **其他 Policy**: 支援 `Sign` 和 `SignAndEncrypt` 模式
  - `Sign`: 僅簽名，確保訊息完整性
  - `SignAndEncrypt`: 簽名 + 加密，確保訊息完整性和機密性（預設）

### 3. 自動證書管理

- 當選擇非 None 的 Security Policy 時，自動檢查並創建應用程式證書
- 證書規格：
  - **演算法**: RSA 2048-bit
  - **簽名**: SHA256
  - **有效期**: 60 個月
  - **儲存位置**: `%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault`

## 程式碼修改

### 1. UI 層修改 (MainWindow.xaml)

**檔案**: `MainWindow.xaml`

**修改位置**: 第 47-55 行

```xml
<ComboBox Grid.Column="1" Name="cmbSecurityPolicy"
          VerticalAlignment="Center" SelectedIndex="0"
          SelectionChanged="CmbSecurityPolicy_SelectionChanged">
    <ComboBoxItem Content="None"/>
    <ComboBoxItem Content="Basic256Sha256"/>
    <ComboBoxItem Content="Aes128_Sha256_RsaOaep"/>
    <ComboBoxItem Content="Aes256_Sha256_RsaPss"/>
</ComboBox>
```

**變更說明**:
- 新增 `Aes128_Sha256_RsaOaep` 選項
- 新增 `Aes256_Sha256_RsaPss` 選項
- 加入 `SelectionChanged` 事件處理器

### 2. UI 邏輯層修改 (MainWindow.xaml.cs)

**檔案**: `MainWindow.xaml.cs`

#### 修改 1: 加入安全設定初始化

**位置**: 第 16-23 行

```csharp
public MainWindow()
{
    InitializeComponent();
    Log("應用程式已啟動");

    // 初始化安全設定
    UpdateMessageSecurityModeOptions();
}
```

#### 修改 2: 實作 Security Policy 變更處理

**位置**: 第 25-52 行

```csharp
private void CmbSecurityPolicy_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    UpdateMessageSecurityModeOptions();
}

private void UpdateMessageSecurityModeOptions()
{
    if (cmbSecurityPolicy == null || cmbMessageSecurityMode == null)
        return;

    if (cmbSecurityPolicy.SelectedIndex == 0) // None
    {
        // 當 Security Policy 為 None 時，強制設定 Message Security Mode 為 None
        cmbMessageSecurityMode.SelectedIndex = 0; // None
        cmbMessageSecurityMode.IsEnabled = false;
    }
    else // 任何安全策略
    {
        // 當 Security Policy 不為 None 時，可選擇 Sign 或 SignAndEncrypt
        cmbMessageSecurityMode.IsEnabled = true;

        // 如果當前選擇的是 None，則自動切換到 SignAndEncrypt
        if (cmbMessageSecurityMode.SelectedIndex == 0)
        {
            cmbMessageSecurityMode.SelectedIndex = 2; // SignAndEncrypt
        }
    }
}
```

**功能說明**:
- Security Policy 為 None：Message Security Mode 強制為 None 且禁用選擇
- Security Policy 為其他：Message Security Mode 預設為 SignAndEncrypt，可手動調整

#### 修改 3: 更新連線邏輯

**位置**: 第 65-74 行

```csharp
// 讀取安全設定
string securityPolicy = ((ComboBoxItem)cmbSecurityPolicy.SelectedItem)?.Content?.ToString() ?? "None";
string messageSecurityMode = ((ComboBoxItem)cmbMessageSecurityMode.SelectedItem)?.Content?.ToString() ?? "None";

Log($"正在連線到: {endpointUrl}");
Log($"Security Policy: {securityPolicy}, Message Security Mode: {messageSecurityMode}");
SetStatusLight(Colors.Yellow);

_clientManager = new OpcUaClientManager();
bool success = await _clientManager.ConnectAsync(endpointUrl, securityPolicy, messageSecurityMode, Log);
```

**功能說明**:
- 讀取使用者選擇的安全設定
- 將設定傳遞給連線管理器
- 傳遞 Log 函數以接收詳細偵錯訊息

#### 修改 4: 更新斷線邏輯

**位置**: 第 117-121 行

```csharp
cmbSecurityPolicy.IsEnabled = true;

// 重新更新Message Security Mode的選項
UpdateMessageSecurityModeOptions();
```

**功能說明**:
- 斷線後重新啟用 Security Policy 選擇
- 更新 Message Security Mode 選項狀態

### 3. 連線管理層修改 (OpcUaClientManager.cs)

**檔案**: `OpcUaClientManager.cs`

#### 修改 1: 更新連線方法簽名

**位置**: 第 19 行

```csharp
public async Task<bool> ConnectAsync(string endpointUrl, string securityPolicy = "None",
    string messageSecurityMode = "None", Action<string>? log = null)
```

**變更說明**:
- 加入 `securityPolicy` 參數
- 加入 `messageSecurityMode` 參數
- 加入 `log` 回調函數參數

#### 修改 2: 加入應用程式配置

**位置**: 第 30-78 行

```csharp
_configuration = new ApplicationConfiguration
{
    ApplicationName = "OPC UA Client App",
    ApplicationUri = $"urn:{System.Net.Dns.GetHostName()}:OPCUAClientApp",
    ApplicationType = ApplicationType.Client,
    SecurityConfiguration = new SecurityConfiguration
    {
        ApplicationCertificate = new CertificateIdentifier
        {
            StoreType = @"Directory",
            StorePath = @"OPC Foundation/CertificateStores/MachineDefault",
            SubjectName = "CN=OPC UA Client App, O=OPC Foundation"
        },
        TrustedIssuerCertificates = new CertificateTrustList
        {
            StoreType = @"Directory",
            StorePath = @"OPC Foundation/CertificateStores/UA Certificate Authorities"
        },
        TrustedPeerCertificates = new CertificateTrustList
        {
            StoreType = @"Directory",
            StorePath = @"OPC Foundation/CertificateStores/UA Applications"
        },
        RejectedCertificateStore = new CertificateTrustList
        {
            StoreType = @"Directory",
            StorePath = @"OPC Foundation/CertificateStores/RejectedCertificates"
        },
        AutoAcceptUntrustedCertificates = true,
        RejectSHA1SignedCertificates = false,
        AddAppCertToTrustedStore = true
    },
    // ... 其他配置
};
```

**功能說明**:
- 配置應用程式證書存儲位置
- 配置信任的證書存儲
- 配置被拒絕的證書存儲
- 啟用自動接受未信任的證書（測試環境）

#### 修改 3: 實作自動證書創建

**位置**: 第 56-100 行

```csharp
if (securityPolicy != "None")
{
    log?.Invoke($"[偵錯] 需要安全連線，檢查應用程式證書...");
    var existingCertificate = await _configuration.SecurityConfiguration.ApplicationCertificate.Find(false);

    if (existingCertificate == null)
    {
        log?.Invoke($"[偵錯] 應用程式證書不存在，開始創建自簽名證書...");

        var certificate = CertificateFactory.CreateCertificate(
            _configuration.SecurityConfiguration.ApplicationCertificate.StoreType,
            _configuration.SecurityConfiguration.ApplicationCertificate.StorePath,
            null,
            _configuration.ApplicationUri,
            _configuration.ApplicationName,
            _configuration.SecurityConfiguration.ApplicationCertificate.SubjectName,
            null,
            2048,  // RSA 金鑰長度
            DateTime.UtcNow - TimeSpan.FromDays(1),
            60,    // 60 個月有效期
            256    // SHA256
        );

        _configuration.SecurityConfiguration.ApplicationCertificate.Certificate = certificate;
        log?.Invoke($"[偵錯] ✓ 已創建自簽名證書");
    }
    else
    {
        log?.Invoke($"[偵錯] ✓ 找到現有的應用程式證書");
        _configuration.SecurityConfiguration.ApplicationCertificate.Certificate = existingCertificate;
    }
}
```

**功能說明**:
- 僅在需要安全連線時檢查證書
- 自動創建或載入現有證書
- 輸出詳細的證書資訊

#### 修改 4: 實作 Security Policy 轉換

**位置**: 第 173-183 行

```csharp
private string ConvertSecurityPolicyToUri(string securityPolicy)
{
    return securityPolicy switch
    {
        "None" => SecurityPolicies.None,
        "Basic256Sha256" => SecurityPolicies.Basic256Sha256,
        "Aes128_Sha256_RsaOaep" => SecurityPolicies.Aes128_Sha256_RsaOaep,
        "Aes256_Sha256_RsaPss" => SecurityPolicies.Aes256_Sha256_RsaPss,
        _ => SecurityPolicies.None
    };
}
```

**功能說明**:
- 將 UI 選擇的字串轉換為 OPC UA Security Policy URI
- 支援所有四種安全策略

#### 修改 5: 實作 Message Security Mode 轉換

**位置**: 第 185-193 行

```csharp
private MessageSecurityMode ConvertToMessageSecurityMode(string mode)
{
    return mode switch
    {
        "None" => MessageSecurityMode.None,
        "Sign" => MessageSecurityMode.Sign,
        "SignAndEncrypt" => MessageSecurityMode.SignAndEncrypt,
        _ => MessageSecurityMode.None
    };
}
```

#### 修改 6: 實作端點選擇邏輯

**位置**: 第 196-245 行

```csharp
private EndpointDescription SelectEndpoint(string endpointUrl, string securityPolicyUri,
    MessageSecurityMode securityMode, Action<string>? log = null)
{
    var endpointConfiguration = EndpointConfiguration.Create();
    endpointConfiguration.OperationTimeout = 15000;

    using (var discoveryClient = DiscoveryClient.Create(new Uri(endpointUrl), endpointConfiguration))
    {
        var endpoints = discoveryClient.GetEndpoints(null);
        log?.Invoke($"[偵錯] 伺服器提供 {endpoints.Count} 個端點");

        // 列出所有可用端點
        for (int i = 0; i < endpoints.Count; i++)
        {
            var ep = endpoints[i];
            log?.Invoke($"[偵錯] 端點 #{i + 1}:");
            log?.Invoke($"[偵錯]   URL: {ep.EndpointUrl}");
            log?.Invoke($"[偵錯]   Security Policy: {ep.SecurityPolicyUri}");
            log?.Invoke($"[偵錯]   Security Mode: {ep.SecurityMode}");
            log?.Invoke($"[偵錯]   Security Level: {ep.SecurityLevel}");
        }

        // 根據安全策略和安全模式篩選端點
        var matchingEndpoint = endpoints.FirstOrDefault(e =>
            e.SecurityPolicyUri == securityPolicyUri &&
            e.SecurityMode == securityMode);

        if (matchingEndpoint != null)
        {
            log?.Invoke($"[偵錯] ✓ 找到匹配的端點！");
            return matchingEndpoint;
        }

        // 如果沒有找到完全匹配的端點，拋出異常
        throw new Exception($"找不到匹配的端點！");
    }
}
```

**功能說明**:
- 從伺服器發現所有可用端點
- 列出所有端點的詳細資訊
- 根據 Security Policy 和 Message Security Mode 精確匹配端點
- 如果找不到匹配的端點，拋出異常並顯示所有可用端點

#### 修改 7: 加入詳細偵錯日誌

整個連線過程加入了詳細的偵錯訊息：

```
[偵錯] 開始連線程序
[偵錯] 端點: opc.tcp://...
[偵錯] 要求的 Security Policy: Basic256Sha256
[偵錯] 要求的 Message Security Mode: SignAndEncrypt
[偵錯] 建立應用程式配置...
[偵錯] 應用程式配置驗證完成
[偵錯] 需要安全連線，檢查應用程式證書...
[偵錯] ✓ 已創建自簽名證書
[偵錯]   Subject: CN=OPC UA Client App, O=OPC Foundation
[偵錯]   Thumbprint: ...
[偵錯]   有效期至: ...
[偵錯] 轉換後的 Security Policy URI: http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256
[偵錯] 轉換後的 Message Security Mode: SignAndEncrypt
[偵錯] 開始發現並選擇端點...
[偵錯] 伺服器提供 6 個端點
[偵錯] 端點 #1: ...
[偵錯] ✓ 找到匹配的端點！
[偵錯] Session 建立成功
[偵錯] 使用的端點 Security Policy: ...
```

## 測試結果

### 測試環境

- **伺服器**: OPC UA Server (提供 6 個端點)
- **端點位址**: `opc.tcp://localhost:48030/`

### 測試案例

| Security Policy | Message Security Mode | 結果 | 備註 |
|----------------|----------------------|------|------|
| None | None (強制) | ✅ 通過 | 無安全連線 |
| Basic256Sha256 | Sign | ✅ 通過 | 僅簽名 |
| Basic256Sha256 | SignAndEncrypt | ✅ 通過 | 簽名+加密 |
| Aes128_Sha256_RsaOaep | Sign | ✅ 通過 | 僅簽名 |
| Aes128_Sha256_RsaOaep | SignAndEncrypt | ✅ 通過 | 簽名+加密 |
| Aes256_Sha256_RsaPss | Sign | ✅ 通過 | 僅簽名 |
| Aes256_Sha256_RsaPss | SignAndEncrypt | ✅ 通過 | 簽名+加密（最高安全性） |

### 測試日誌範例

```
[13:47:40.900] Security Policy: Basic256Sha256, Message Security Mode: SignAndEncrypt
[13:47:41.009] [偵錯] 轉換後的 Security Policy URI: http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256
[13:47:41.039] [偵錯] 轉換後的 Message Security Mode: SignAndEncrypt
[13:47:41.219] [偵錯] 伺服器提供 6 個端點
[13:47:41.262] [偵錯] ✓ 找到匹配的端點！
[13:47:41.275] [偵錯]   - Security Policy: http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256
[13:47:41.276] [偵錯]   - Security Mode: SignAndEncrypt
```

## 安全性考量

### 1. 證書管理

- **自動創建**: 首次使用安全連線時自動創建自簽名證書
- **證書存儲**: 證書儲存在標準的 OPC Foundation 目錄中
- **證書重用**: 後續連線會重用已創建的證書

### 2. 信任設定

- **AutoAcceptUntrustedCertificates**: 設為 `true`（測試環境）
  - 生產環境建議設為 `false`，手動管理信任的證書
- **RejectSHA1SignedCertificates**: 設為 `false`
  - 允許舊版 SHA1 簽名的證書（向下相容）

### 3. 端點驗證

- 嚴格驗證選擇的端點與要求的安全設定匹配
- 如果不匹配，拋出異常並顯示詳細資訊

## 使用建議

### 1. 選擇合適的 Security Policy

- **開發/測試環境**: 使用 `None` 或 `Basic256Sha256`
- **生產環境**: 使用 `Aes256_Sha256_RsaPss` + `SignAndEncrypt` 獲得最高安全性

### 2. Message Security Mode

- **Sign**: 確保訊息完整性，適用於內網環境
- **SignAndEncrypt**: 確保訊息完整性和機密性，適用於需要保護資料內容的場景

### 3. 證書管理

- 定期更新證書（有效期 60 個月）
- 在生產環境中使用經過認證的證書
- 妥善保管私鑰

## 已知限制

1. **證書格式**: 僅支援自簽名證書，不支援 CA 簽發的證書鏈
2. **證書撤銷**: 未實作 CRL (Certificate Revocation List) 檢查
3. **多證書**: 不支援多個應用程式證書
4. **硬編碼**: 證書存儲路徑為硬編碼，不可配置

## 未來改進方向

1. **證書配置化**: 允許使用者自定義證書存儲路徑
2. **CA 證書支援**: 支援 CA 簽發的證書
3. **CRL 檢查**: 實作證書撤銷列表檢查
4. **證書到期提醒**: 在證書即將到期時提醒使用者
5. **安全策略配置檔**: 將安全策略儲存為配置檔，方便管理

## 相關檔案

### 修改的檔案

1. `MainWindow.xaml` - UI 定義
2. `MainWindow.xaml.cs` - UI 邏輯
3. `OpcUaClientManager.cs` - 連線管理

### 新增的檔案

1. `SECURITY_POLICY_IMPLEMENTATION.md` - 本文件

## Git 提交歷史

```bash
# 初始提交
06c3287 - Add Security Policy and Message Security Mode options to OPC UA connection interface

# 修正端點選擇邏輯並加入偵錯訊息
53c5ae9 - Fix endpoint selection and add detailed debug logging

# 加入應用程式證書支援
10e1805 - Add application certificate support for secure OPC UA connections

# 修正類型轉換錯誤
3a373e8 - Fix certificate type conversion error

# 修改預設 Message Security Mode
d2496cb - Change default Message Security Mode to SignAndEncrypt for Basic256Sha256

# 加入所有 Security Policy 支援
3961703 - Add support for all OPC UA Security Policies
```

## 參考資料

1. [OPC Foundation - OPC UA Specification](https://opcfoundation.org/developer-tools/specifications-unified-architecture)
2. [OPC UA .NET Standard Stack](https://github.com/OPCFoundation/UA-.NETStandard)
3. [Security Policy Profiles](https://profiles.opcfoundation.org/)

## 結語

此次重構成功實作了完整的 OPC UA 安全連線功能，支援多種 Security Policy 和 Message Security Mode，並實作了自動證書管理。應用程式現在可以安全地連接到不同安全等級的 OPC UA 伺服器，適用於從開發測試到生產環境的各種場景。

# 端點設定自動記憶功能實作文件

## 功能概述

本次重構為 OPC UA Client 應用程式添加了端點設定的自動持久化功能，使用者在程式重新啟動後無需重新輸入端點位址和安全設定，提升使用體驗。

### 主要功能

- 自動記憶最後一次成功連線的端點位址
- 自動記憶最後一次使用的 Security Policy
- 自動記憶最後一次使用的 Message Security Mode
- 程式啟動時自動載入並填充上次的設定
- 連線成功後自動儲存當前設定
- 設定以 JSON 格式儲存，易於查看和除錯

## 實作日期

2025-11-20

## 技術架構

### 1. 配置管理類別 (AppSettings.cs)

#### 類別結構

```csharp
public class AppSettings
{
    public string? LastEndpointUrl { get; set; }
    public string? LastSecurityPolicy { get; set; }
    public string? LastMessageSecurityMode { get; set; }

    public static AppSettings Load()
    public void Save()
}
```

#### 儲存位置

- **Windows**: `%LocalAppData%\OpcUaClientApp\settings.json`
- **完整路徑範例**: `C:\Users\{Username}\AppData\Local\OpcUaClientApp\settings.json`

#### 設定檔格式

```json
{
  "LastEndpointUrl": "opc.tcp://localhost:62541/Quickstarts/ReferenceServer",
  "LastSecurityPolicy": "Basic256Sha256",
  "LastMessageSecurityMode": "SignAndEncrypt"
}
```

#### 錯誤處理

- 若設定檔不存在或讀取失敗，返回空的設定物件
- 若儲存失敗，靜默忽略錯誤（不影響程式正常運行）
- 自動建立設定資料夾（若不存在）

### 2. 主視窗整合 (MainWindow.xaml.cs)

#### 新增的私有欄位

```csharp
private AppSettings _appSettings;
```

#### 建構函式修改

```csharp
public MainWindow()
{
    InitializeComponent();
    Log("應用程式已啟動");

    // 載入應用程式設定
    _appSettings = AppSettings.Load();
    LoadLastEndpointSettings();

    // 初始化安全設定
    UpdateMessageSecurityModeOptions();
}
```

#### 新增的方法

**LoadLastEndpointSettings()**

功能：載入並應用上次儲存的端點設定

實作細節：
- 檢查並載入端點 URL
- 遍歷 ComboBox 項目尋找匹配的 Security Policy
- 遍歷 ComboBox 項目尋找匹配的 Message Security Mode
- 在日誌中顯示載入的設定資訊

```csharp
private void LoadLastEndpointSettings()
{
    // 載入端點 URL
    if (!string.IsNullOrEmpty(_appSettings.LastEndpointUrl))
    {
        txtEndpointUrl.Text = _appSettings.LastEndpointUrl;
        Log($"已載入上次使用的端點位址: {_appSettings.LastEndpointUrl}");
    }

    // 載入 Security Policy
    if (!string.IsNullOrEmpty(_appSettings.LastSecurityPolicy))
    {
        for (int i = 0; i < cmbSecurityPolicy.Items.Count; i++)
        {
            if (cmbSecurityPolicy.Items[i] is ComboBoxItem item &&
                item.Content?.ToString() == _appSettings.LastSecurityPolicy)
            {
                cmbSecurityPolicy.SelectedIndex = i;
                Log($"已載入上次使用的 Security Policy: {_appSettings.LastSecurityPolicy}");
                break;
            }
        }
    }

    // 載入 Message Security Mode
    if (!string.IsNullOrEmpty(_appSettings.LastMessageSecurityMode))
    {
        for (int i = 0; i < cmbMessageSecurityMode.Items.Count; i++)
        {
            if (cmbMessageSecurityMode.Items[i] is ComboBoxItem item &&
                item.Content?.ToString() == _appSettings.LastMessageSecurityMode)
            {
                cmbMessageSecurityMode.SelectedIndex = i;
                Log($"已載入上次使用的 Message Security Mode: {_appSettings.LastMessageSecurityMode}");
                break;
            }
        }
    }
}
```

#### 連線成功事件修改

在 `BtnConnect_Click` 方法的連線成功分支中添加設定儲存邏輯：

```csharp
if (success)
{
    Log("✓ 連線成功");
    SetStatusLight(Colors.Green);
    btnConnect.IsEnabled = false;
    btnDisconnect.IsEnabled = true;
    txtEndpointUrl.IsEnabled = false;
    cmbSecurityPolicy.IsEnabled = false;
    cmbMessageSecurityMode.IsEnabled = false;

    // 儲存端點設定
    _appSettings.LastEndpointUrl = endpointUrl;
    _appSettings.LastSecurityPolicy = securityPolicy;
    _appSettings.LastMessageSecurityMode = messageSecurityMode;
    _appSettings.Save();
    Log("✓ 已儲存端點設定");

    // 自動瀏覽根節點
    await BrowseRootNodes();
}
```

## 檔案變更清單

### 新增檔案

| 檔案名稱 | 說明 | 行數 |
|---------|------|------|
| AppSettings.cs | 配置管理類別 | 63 行 |

### 修改檔案

| 檔案名稱 | 修改內容 | 新增行數 |
|---------|---------|---------|
| MainWindow.xaml.cs | 添加設定載入和儲存邏輯 | +57 行 |

## 使用流程

### 第一次使用

1. 啟動應用程式
2. 手動輸入端點位址（例如：`opc.tcp://localhost:62541/Quickstarts/ReferenceServer`）
3. 選擇 Security Policy（例如：Basic256Sha256）
4. 選擇 Message Security Mode（例如：SignAndEncrypt）
5. 點擊「連線」按鈕
6. 連線成功後，設定自動儲存
7. 日誌顯示：「✓ 已儲存端點設定」

### 後續使用

1. 啟動應用程式
2. 程式自動載入上次的設定
3. 日誌顯示：
   - 「已載入上次使用的端點位址: [URL]」
   - 「已載入上次使用的 Security Policy: [Policy]」
   - 「已載入上次使用的 Message Security Mode: [Mode]」
4. 直接點擊「連線」即可使用相同設定

### 變更設定

1. 啟動應用程式（自動載入舊設定）
2. 修改端點位址或安全設定
3. 點擊「連線」
4. 連線成功後，新設定自動覆蓋舊設定

## 優點與效益

### 使用者體驗改善

- **節省時間**：無需每次啟動都重新輸入端點資訊
- **減少錯誤**：避免手動輸入時的拼寫錯誤
- **提升效率**：適合需要頻繁重啟應用程式的開發和測試場景

### 技術優勢

- **輕量級實作**：使用 .NET 內建的 JSON 序列化，無需額外依賴
- **容錯性強**：讀取或儲存失敗不影響程式正常運行
- **易於維護**：JSON 格式便於人工檢視和編輯
- **可擴展性**：未來可輕鬆添加更多配置項目

## 安全性考量

### 現有實作

- 僅儲存端點 URL 和安全設定名稱
- **不儲存**任何認證資訊（使用者名稱、密碼等）
- 設定檔位於使用者的本機應用資料夾，受作業系統權限保護

### 未來改進建議

若需要儲存敏感資訊，建議考慮：
- 使用 Windows DPAPI (Data Protection API) 加密敏感資料
- 使用 .NET 的 ProtectedData 類別
- 實作密鑰管理機制

## 測試建議

### 功能測試

1. **初次載入測試**
   - 刪除設定檔
   - 啟動程式
   - 確認端點位址為空
   - 輸入設定並連線
   - 確認設定已儲存

2. **自動載入測試**
   - 關閉程式
   - 重新啟動
   - 確認所有設定正確載入

3. **設定變更測試**
   - 修改端點位址或安全設定
   - 連線成功
   - 重啟程式
   - 確認新設定已生效

4. **錯誤處理測試**
   - 手動損壞設定檔
   - 啟動程式
   - 確認程式正常運行且回到預設狀態

### 邊界條件測試

- 空端點 URL
- 無效的 Security Policy 名稱
- 設定檔權限問題
- 磁碟空間不足

## 日誌訊息

### 啟動時的日誌

```
[HH:mm:ss.fff] 應用程式已啟動
[HH:mm:ss.fff] 已載入上次使用的端點位址: opc.tcp://localhost:62541/Quickstarts/ReferenceServer
[HH:mm:ss.fff] 已載入上次使用的 Security Policy: Basic256Sha256
[HH:mm:ss.fff] 已載入上次使用的 Message Security Mode: SignAndEncrypt
```

### 連線成功時的日誌

```
[HH:mm:ss.fff] 正在連線到: opc.tcp://localhost:62541/Quickstarts/ReferenceServer
[HH:mm:ss.fff] Security Policy: Basic256Sha256, Message Security Mode: SignAndEncrypt
[HH:mm:ss.fff] ✓ 連線成功
[HH:mm:ss.fff] ✓ 已儲存端點設定
```

## 相關提交

- **Commit Hash**: 23710b8
- **Commit Message**: "Add automatic endpoint settings persistence"
- **Branch**: `claude/auto-load-endpoint-01EbufaPhvbEUL4dTg25qPxn`
- **Changed Files**:
  - `AppSettings.cs` (新增)
  - `MainWindow.xaml.cs` (修改)

## 未來擴展建議

### 短期改進

1. **多端點管理**
   - 儲存多個常用端點
   - 提供快速切換功能
   - 端點清單管理介面

2. **匯入/匯出設定**
   - 匯出設定為檔案
   - 從檔案匯入設定
   - 團隊共享設定檔

### 長期改進

1. **進階配置選項**
   - 連線逾時設定
   - 重試策略
   - 日誌等級控制

2. **雲端同步**
   - 跨裝置同步設定
   - 團隊共享配置
   - 版本控制

3. **設定檔加密**
   - 敏感資料加密儲存
   - 安全的憑證管理

## 維護者注意事項

### 相容性

- 設定檔格式變更時，需考慮向後相容性
- 建議添加版本號欄位以支援未來的設定遷移

### 除錯

檢查設定檔位置：
```csharp
var settingsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "OpcUaClientApp",
    "settings.json"
);
Console.WriteLine($"Settings file: {settingsPath}");
```

手動查看設定檔：
```bash
# Windows
notepad %LOCALAPPDATA%\OpcUaClientApp\settings.json

# PowerShell
cat $env:LOCALAPPDATA\OpcUaClientApp\settings.json
```

## 結論

此次重構成功實現了端點設定的自動持久化功能，顯著提升了使用者體驗，特別是在開發和測試場景中。實作方式簡潔高效，具備良好的錯誤處理和可擴展性，為未來的功能擴展奠定了基礎。

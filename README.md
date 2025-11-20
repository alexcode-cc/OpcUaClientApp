# OPC UA 客戶端測試工具

這是一個基於 WPF 的 Windows Desktop 應用程式，用於測試和與 OPC UA Server 進行互動。

## 功能特點

- ✅ 連線管理（連線/斷線）
- ✅ 即時連線狀態顯示（紅/黃/綠燈號）
- ✅ 節點樹瀏覽器（TreeView）
- ✅ 讀取節點資料
- ✅ 寫入節點資料
- ✅ 訂閱節點變化（即時監控）
- ✅ 操作日誌記錄
- ✅ 支援多種資料型別

## 系統需求

- Windows 10/11
- .NET 8.0 或更高版本
- OPC UA Server（例如: OpcUaSqlServer）

## 安裝步驟

### 1. 下載專案

```bash
git clone git@github.com:alexcode-cc/OpcUaClientApp.git
cd OpcUaClientApp
```

### 2. 還原 NuGet 套件

```bash
dotnet restore
```

### 3. 編譯專案

```bash
dotnet build
```

### 4. 執行程式

```bash
dotnet run
```

或者在 Visual Studio 中開啟 `OpcUaClientApp.csproj` 並按 F5 執行。

## 使用說明

### 1. 連線到 OPC UA Server

1. 在「端點位址」欄位輸入 OPC UA Server 的 URL
   - 預設值: `opc.tcp://localhost:4840/OpcUaSqlServer`
2. 點選「連線」按鈕
3. 觀察右側的狀態燈號：
   - 🔴 紅色：未連線
   - 🟡 黃色：連線中
   - 🟢 綠色：已連線

### 2. 瀏覽節點樹

連線成功後，應用程式會自動載入根節點。您也可以：
- 點選「瀏覽節點」按鈕重新載入
- 在樹狀結構中展開節點以查看子節點
- 點選節點以查看詳細資訊

### 3. 讀取節點資料

1. 在左側節點樹中選擇一個「變數」節點
2. 節點資訊會顯示在右側面板
3. 點選「讀取」按鈕
4. 當前值和時間戳記會顯示在「讀取資料」區塊

### 4. 寫入節點資料

1. 在左側節點樹中選擇一個「變數」節點
2. 在「新值」欄位輸入要寫入的值
3. 點選「寫入」按鈕
4. 系統會自動確認寫入結果

### 5. 訂閱節點監控

1. 選擇要監控的變數節點
2. 設定「更新間隔(ms)」（建議 1000 毫秒以上）
3. 點選「訂閱節點」按鈕
4. 當節點值變化時，會即時顯示在「監控」列表中
5. 點選「取消訂閱」停止監控

### 6. 查看操作日誌

- 所有操作都會記錄在底部的「操作日誌」區域
- 點選「清除」按鈕可清空日誌

## 介面說明

### 連線區域（頂部）
- **端點位址**: 輸入 OPC UA Server 的 URL
- **連線/斷線按鈕**: 管理連線狀態
- **狀態燈號**: 顯示當前連線狀態

### 節點瀏覽器（左側）
- 以樹狀結構顯示 OPC UA 節點階層
- 節點類型標示（Object/Variable）
- 支援展開/收合子節點

### 操作區域（右側）

#### 節點資訊
- 顯示名稱
- 節點 ID
- 節點類別
- 資料型別

#### 讀取資料
- 當前值
- 時間戳記
- 讀取按鈕

#### 寫入資料
- 新值輸入框
- 寫入按鈕

#### 監控（訂閱）
- 更新間隔設定
- 訂閱/取消訂閱按鈕
- 即時資料變化列表

### 操作日誌（底部）
- 時間戳記
- 操作記錄
- 成功/失敗狀態

## 連線端點格式

標準 OPC UA 端點格式：
```
opc.tcp://[主機位址]:[埠號]/[伺服器名稱]
```

範例：
- `opc.tcp://localhost:4840/OpcUaSqlServer`
- `opc.tcp://192.168.1.100:4840/MyServer`
- `opc.tcp://server.example.com:4840/OPCUA`

## 支援的資料型別

應用程式支援以下 OPC UA 資料型別：
- Boolean
- Byte, SByte
- Int16, UInt16, Int32, UInt32, Int64, UInt64
- Float, Double
- String
- DateTime

## 疑難排解

### 問題：無法連線到伺服器

**可能原因與解決方式：**
1. **伺服器未啟動**
   - 確認 OPC UA Server 正在執行
   - 檢查伺服器日誌是否有錯誤

2. **端點位址錯誤**
   - 確認主機位址、埠號和伺服器名稱正確
   - 嘗試使用 `localhost` 或 `127.0.0.1`

3. **防火牆阻擋**
   - 檢查防火牆設定
   - 確認埠號（預設 4840）已開放

4. **憑證問題**
   - 應用程式會自動接受不受信任的憑證
   - 如果仍有問題，檢查伺服器的安全性設定

### 問題：節點樹為空

**解決方式：**
1. 確認已成功連線（綠色燈號）
2. 點選「瀏覽節點」按鈕重新載入
3. 檢查伺服器是否有公開的節點

### 問題：無法寫入資料

**可能原因：**
1. 節點為唯讀
2. 資料型別不匹配
3. 沒有寫入權限
4. 伺服器拒絕寫入

**解決方式：**
- 確認節點支援寫入
- 檢查輸入的值格式是否正確
- 查看操作日誌中的錯誤訊息

### 問題：訂閱沒有收到更新

**解決方式：**
1. 確認更新間隔設定合理（建議 ≥ 1000ms）
2. 檢查伺服器端資料是否真的在變化
3. 嘗試取消訂閱後重新訂閱

## 專案結構

```
OpcUaClientApp/
├── App.xaml                    # 應用程式定義
├── App.xaml.cs                 # 應用程式邏輯
├── MainWindow.xaml             # 主視窗 UI
├── MainWindow.xaml.cs          # 主視窗邏輯
├── OpcUaClientManager.cs       # OPC UA 客戶端管理器
├── OpcUaNodeItem.cs            # 節點資料模型
└── OpcUaClientApp.csproj       # 專案檔
```

## 技術細節

### 使用的 NuGet 套件

- `OPCFoundation.NetStandard.Opc.Ua` - OPC UA 核心功能
- `OPCFoundation.NetStandard.Opc.Ua.Client` - OPC UA 客戶端

### 架構

- **MVVM 模式**: 使用 WPF 的資料繫結
- **非同步操作**: 所有網路操作都是非同步的
- **事件驅動**: 訂閱機制使用事件處理

### 安全性

- 自動接受不受信任的憑證（僅供開發測試使用）
- 使用匿名身份驗證
- 支援 SHA-256 安全策略

## 開發建議

### 擴展功能

您可以基於此專案擴展以下功能：

1. **身份驗證**
   - 加入使用者名稱/密碼登入
   - 支援憑證身份驗證

2. **歷史資料**
   - 讀取歷史資料
   - 資料圖表顯示

3. **批次操作**
   - 批次讀取多個節點
   - 批次寫入

4. **匯出功能**
   - 匯出節點樹為 XML/JSON
   - 匯出監控資料為 CSV

5. **連線設定檔**
   - 儲存常用的端點設定
   - 快速切換不同伺服器

### 除錯技巧

1. 啟用 OPC UA 日誌以查看詳細資訊
2. 使用 Wireshark 抓取 OPC UA 封包
3. 檢查伺服器端日誌

## 授權

MIT License

## 貢獻

歡迎提交 Issue 和 Pull Request！

## 相關資源

- [OPC Foundation](https://opcfoundation.org/)
- [OPC UA .NET Standard](https://github.com/OPCFoundation/UA-.NETStandard)
- [UaExpert](https://www.unified-automation.com/products/development-tools/uaexpert.html) - 官方測試工具

## 聯絡方式

如有問題，請透過 GitHub Issues 聯繫。

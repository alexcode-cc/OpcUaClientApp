# OPC UA 客戶端應用程式使用指南

## 專案概述

這是一個完整的 Windows Desktop App (WPF) 來測試 OPC UA Server。

**檔案大小**: 15 KB

## ✨ 功能特點

### 1. 連線管理
- 輸入 OPC UA Server 端點位址
- 一鍵連線/斷線
- 即時連線狀態燈號顯示：
  - 🔴 紅色：未連線
  - 🟡 黃色：連線中
  - 🟢 綠色：已連線成功

### 2. 節點瀏覽器
- TreeView 樹狀結構顯示所有節點
- 自動載入子節點
- 顯示節點類型（Object/Variable）
- 雙擊展開節點

### 3. 節點資訊顯示
- 顯示名稱 (Display Name)
- 節點 ID (Node ID)
- 節點類別 (Node Class)
- 資料型別 (Data Type)

### 4. 讀取功能
- 讀取選中節點的當前值
- 顯示時間戳記
- 支援多種資料型別

### 5. 寫入功能
- 寫入新值到選中的節點
- 自動型別轉換
- 寫入後自動確認

### 6. 訂閱監控
- 即時監控節點值變化
- 可設定更新間隔
- 顯示最新 100 筆變化記錄
- 支援多節點同時監控

### 7. 操作日誌
- 記錄所有操作
- 時間戳記
- 成功/失敗狀態顯示
- 可清除日誌

## 🚀 快速開始

### 步驟 1: 解壓縮並編譯

```bash
cd OpcUaClientApp

# 還原 NuGet 套件
dotnet restore

# 編譯專案
dotnet build

# 執行程式
dotnet run
```

### 步驟 2: 啟動 OPC UA Server

確保您的 OPC UA Server (例如 OpcUaSqlServer) 正在執行：

```bash
cd OpcUaSqlServer
dotnet run
```

Server 應該顯示：
```
OPC UA Server 已啟動
端點: opc.tcp://localhost:4840/OpcUaSqlServer
```

### 步驟 3: 連線測試

1. 在客戶端應用程式中，端點位址預設為：
   ```
   opc.tcp://localhost:4840/OpcUaSqlServer
   ```

2. 點選「連線」按鈕

3. 觀察狀態燈號變化：
   - 紅色 → 黃色 → 綠色 ✓

4. 連線成功後，節點樹會自動載入

### 步驟 4: 測試讀取

1. 在左側節點樹中展開：
   ```
   Objects → SqlData → Products
   ```

2. 選擇任一變數節點（例如：Products_ProductName）

3. 點選右側「讀取」按鈕

4. 查看當前值和時間戳記

### 步驟 5: 測試寫入

1. 選擇一個可寫入的變數節點

2. 在「新值」欄位輸入測試值，例如：
   ```
   Test Product
   ```

3. 點選「寫入」按鈕

4. 確認寫入成功訊息

5. 再次點選「讀取」確認值已更新

### 步驟 6: 測試監控

1. 選擇要監控的節點

2. 設定「更新間隔(ms)」為 `1000`

3. 點選「訂閱節點」按鈕

4. 在監控區域會看到即時更新的資料

5. 您可以在 Server 端修改資料，客戶端會即時顯示變化

## 📋 完整操作流程示範

### 示範 1: 連線並讀取資料庫資料

```
1. 啟動 OpcUaSqlServer
   ✓ Server 在 opc.tcp://localhost:4840/OpcUaSqlServer 啟動

2. 啟動 OpcUaClientApp
   ✓ 應用程式開啟

3. 點選「連線」
   ✓ 狀態燈變綠色
   ✓ 日誌顯示：[13:45:23] ✓ 連線成功

4. 瀏覽節點樹
   ✓ 展開 Objects → SqlData → Products

5. 選擇 Products_ProductName
   ✓ 節點資訊顯示在右側

6. 點選「讀取」
   ✓ 當前值顯示：Product A
   ✓ 時間戳記顯示：2025-11-20 13:45:30.123
```

### 示範 2: 寫入資料到資料庫

```
1. 選擇 Products_ProductName 節點

2. 在「新值」輸入：New Product Name

3. 點選「寫入」
   ✓ 訊息框顯示：寫入成功
   ✓ 日誌顯示：[13:46:15] ✓ 寫入成功: New Product Name

4. 點選「讀取」確認
   ✓ 當前值顯示：New Product Name

5. 在 SQL Server 中查詢確認
   ✓ 資料庫中的值已更新
```

### 示範 3: 即時監控資料變化

```
1. 選擇要監控的節點（例如：Products_Price）

2. 設定更新間隔：1000ms

3. 點選「訂閱節點」
   ✓ 日誌顯示：[13:47:00] ✓ 已訂閱節點: Products_Price

4. 在 SQL Server Management Studio 中更新資料：
   UPDATE Products SET Price = 1999 WHERE ProductID = 1

5. 觀察監控區域
   ✓ 立即顯示：[13:47:05.234] Products_Price = 1999
   ✓ 當前值自動更新為：1999

6. 再次更新資料庫
   UPDATE Products SET Price = 2499 WHERE ProductID = 1

7. 觀察監控區域
   ✓ 顯示：[13:47:10.456] Products_Price = 2499
```

## 🎨 介面佈局說明

```
┌────────────────────────────────────────────────────────────────────┐
│ OPC UA 客戶端測試工具                                          [_][□][X]│
├────────────────────────────────────────────────────────────────────┤
│ 端點位址: [opc.tcp://localhost:4840/OpcUaSqlServer] [連線] [斷線] 🟢│
├──────────────────────┬─────────────────────────────────────────────┤
│ OPC UA 節點樹        │ 節點資訊                                    │
│ [瀏覽節點]           │ ┌─────────────────────────────────────────┐│
│                      │ │ 顯示名稱: Products_ProductName          ││
│ ▼ Objects            │ │ 節點 ID: ns=2;s=Products_ProductName    ││
│   ▼ SqlData          │ │ 節點類別: Variable                      ││
│     ▼ Products       │ │ 資料型別: String                        ││
│       • ProductName  │ └─────────────────────────────────────────┘│
│       • Price        │                                             │
│     ▼ Orders         │ 讀取資料                                    │
│       • OrderID      │ ┌─────────────────────────────────────────┐│
│       • CustomerName │ │ 當前值: [Product A]          [讀取]     ││
│                      │ │ 時間戳記: 2025-11-20 13:45:30.123       ││
│                      │ └─────────────────────────────────────────┘│
│                      │                                             │
│                      │ 寫入資料                                    │
│                      │ ┌─────────────────────────────────────────┐│
│                      │ │ 新值: [New Value]            [寫入]     ││
│                      │ └─────────────────────────────────────────┘│
│                      │                                             │
│                      │ 監控 (Subscription)                         │
│                      │ [訂閱節點] [取消訂閱] 更新間隔: [1000] ms  │
│                      │ ┌─────────────────────────────────────────┐│
│                      │ │ [13:47:05] Products_Price = 1999        ││
│                      │ │ [13:47:00] Products_Price = 1500        ││
│                      │ │ [13:46:55] Products_Price = 1200        ││
│                      │ └─────────────────────────────────────────┘│
├──────────────────────┴─────────────────────────────────────────────┤
│ 操作日誌                                              [清除]        │
│ ┌──────────────────────────────────────────────────────────────┐  │
│ │ [13:45:23.456] OPC UA Server 啟動中...                      │  │
│ │ [13:45:25.789] ✓ 連線成功                                   │  │
│ │ [13:45:26.123] 正在瀏覽根節點...                            │  │
│ │ [13:45:26.456] ✓ 已載入 10 個節點                           │  │
│ │ [13:45:30.123] 正在讀取節點: Products_ProductName           │  │
│ │ [13:45:30.234] ✓ 讀取成功: Product A                        │  │
│ │ [13:46:15.567] 正在寫入節點: Products_ProductName           │  │
│ │ [13:46:15.678] ✓ 寫入成功: New Product Name                 │  │
│ └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────┘
```

## 🔧 技術規格

### 開發框架
- **.NET 8.0** - 最新的 .NET 框架
- **WPF** - Windows Presentation Foundation
- **OPC Foundation SDK** - 官方 OPC UA SDK

### NuGet 套件
- `OPCFoundation.NetStandard.Opc.Ua` (1.5.374.54)
- `OPCFoundation.NetStandard.Opc.Ua.Client` (1.5.374.54)

### 支援的作業系統
- Windows 10
- Windows 11
- Windows Server 2019/2022

## 📁 專案檔案結構

```
OpcUaClientApp/
├── App.xaml                    # WPF 應用程式定義
├── App.xaml.cs                 # 應用程式啟動邏輯
├── MainWindow.xaml             # 主視窗 UI 設計
├── MainWindow.xaml.cs          # 主視窗業務邏輯
├── OpcUaClientManager.cs       # OPC UA 客戶端管理器
├── OpcUaNodeItem.cs            # 節點資料模型
├── OpcUaClientApp.csproj       # 專案配置檔
├── README.md                   # 完整說明文件
├── QUICKSTART.md               # 快速開始指南
└── .gitignore                  # Git 忽略檔案
```

## ⚠️ 注意事項

### 安全性
- 此應用程式預設自動接受不受信任的憑證
- 僅供開發和測試使用
- 生產環境請實作適當的憑證驗證

### 效能
- 建議監控間隔 ≥ 1000ms
- 監控列表最多保留 100 筆記錄
- 大量節點時請謹慎使用訂閱功能

### 相容性
- 需要 Windows 作業系統
- 需要 .NET 8.0 Runtime
- 與標準 OPC UA Server 相容

## 🐛 疑難排解

### 問題 1: 編譯錯誤

**錯誤訊息**: 找不到 SDK 或套件

**解決方式**:
```bash
# 確認 .NET SDK 版本
dotnet --version

# 清理並重建
dotnet clean
dotnet restore
dotnet build
```

### 問題 2: 無法連線

**可能原因**:
1. Server 未啟動
2. 端點位址錯誤
3. 防火牆阻擋
4. 埠號被佔用

**解決方式**:
```bash
# 檢查 Server 是否執行
netstat -an | findstr 4840

# 測試網路連線
telnet localhost 4840

# 檢查防火牆規則
netsh advfirewall firewall show rule name=all | findstr 4840
```

### 問題 3: 應用程式無法啟動

**錯誤訊息**: 缺少 Runtime

**解決方式**:
1. 下載並安裝 .NET 8.0 Desktop Runtime
2. 網址: https://dotnet.microsoft.com/download/dotnet/8.0

## 📚 延伸閱讀

- [OPC UA 規範](https://opcfoundation.org/developer-tools/specifications-unified-architecture)
- [OPC Foundation .NET SDK](https://github.com/OPCFoundation/UA-.NETStandard)
- [WPF 官方文件](https://docs.microsoft.com/zh-tw/dotnet/desktop/wpf/)

## 🎯 下一步建議

1. **熟悉介面**: 花 10 分鐘探索所有功能
2. **測試連線**: 確保能成功連線到 Server
3. **測試讀寫**: 驗證資料讀取和寫入功能
4. **測試監控**: 體驗即時資料監控功能
5. **查看日誌**: 了解操作流程和錯誤訊息
6. **擴展功能**: 根據需求增加新功能

## 💡 使用技巧

1. **快速定位節點**: 使用節點樹的搜尋功能（待實作）
2. **批次操作**: 選擇多個節點進行批次讀取（待實作）
3. **匯出資料**: 將監控資料匯出為 CSV（待實作）
4. **儲存設定**: 儲存常用的連線設定（待實作）

## 📞 支援

如有問題或建議，請：
1. 查看 README.md 的疑難排解章節
2. 檢查操作日誌中的錯誤訊息
3. 在 GitHub 上提交 Issue

祝您使用愉快！🚀

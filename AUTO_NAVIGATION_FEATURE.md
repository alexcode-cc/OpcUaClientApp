# 自動導航到最後查看節點功能實現

## 功能概述

此次重構在原有的端點位址、Security Policy 和 Message Security Mode 自動記憶功能基礎上，新增了以下功能：

1. **自動記憶最後查看的節點**
2. **連接成功後自動導航到該節點**
3. **自動展開 OPC UA 節點樹到目標節點**
4. **自動讀取節點資料**
5. **自動恢復訂閱功能**

## 實現細節

### 1. 擴展 AppSettings 類別

在 `AppSettings.cs` 中新增以下屬性：

```csharp
// 最後查看的節點資訊
public string? LastViewedNodeId { get; set; }
public string? LastViewedNodeDisplayName { get; set; }
public string? LastViewedNodeClass { get; set; }
public string? LastViewedNodePath { get; set; }
public bool LastViewedNodeIsSubscribed { get; set; }
public int? LastViewedSubscriptionInterval { get; set; }
```

這些屬性會自動序列化為 JSON 並保存在：
- Windows: `%LocalAppData%\OpcUaClientApp\settings.json`
- Linux: `~/.local/share/OpcUaClientApp/settings.json`

### 2. 自動保存節點資訊

在 `TreeNodes_SelectedItemChanged` 事件處理器中，當用戶選擇節點時自動保存：

```csharp
private void SaveLastViewedNode(OpcUaNodeItem node)
{
    _appSettings.LastViewedNodeId = node.NodeId.ToString();
    _appSettings.LastViewedNodeDisplayName = node.DisplayName;
    _appSettings.LastViewedNodeClass = node.NodeClass.ToString();
    _appSettings.LastViewedNodePath = BuildNodePath(node);
    _appSettings.Save();
}
```

### 3. 自動導航功能

連接成功後，`BtnConnect_Click` 會調用 `NavigateToLastViewedNode()` 方法：

#### 3.1 查找節點

使用遞歸搜索算法 `SearchAndExpandNode()` 在整個 OPC UA 樹中查找目標節點：

- 從根節點（Objects）開始
- 遍歷所有子節點
- 如果遇到未載入的對象節點，自動調用 `BrowseAsync()` 載入其子節點
- 遞歸搜索直到找到目標節點

#### 3.2 展開節點樹

`FindTreeViewItem()` 方法負責：
- 在 WPF TreeView 中找到對應的 TreeViewItem
- 自動展開從根到目標節點的所有父節點
- 調用 `IsExpanded = true` 和 `UpdateLayout()` 確保節點可見

#### 3.3 選中節點

`SelectTreeViewItem()` 方法：
- 設置 `IsSelected = true` 選中目標節點
- 調用 `BringIntoView()` 將節點滾動到可視區域
- 自動觸發 `TreeNodes_SelectedItemChanged` 事件顯示節點詳情

### 4. 自動讀取節點資料

如果目標節點是變數（Variable）類型：

```csharp
await AutoReadNodeValue(foundNode);
```

此方法會：
- 調用 `ReadValueAsync()` 讀取節點當前值
- 更新 UI 顯示值和時間戳
- 在日誌中記錄讀取結果

### 5. 自動恢復訂閱

如果該節點之前有訂閱狀態：

```csharp
if (_appSettings.LastViewedNodeIsSubscribed &&
    _appSettings.LastViewedSubscriptionInterval.HasValue)
{
    await AutoSubscribeNode(foundNode, interval);
}
```

`AutoSubscribeNode()` 方法會：
- 使用保存的訂閱間隔創建訂閱
- 設置資料變化回調函數
- 自動更新訂閱間隔 UI 輸入框
- 開始接收即時資料更新

### 6. 訂閱狀態管理

#### 訂閱時保存狀態

在 `BtnSubscribe_Click` 中：
```csharp
_appSettings.LastViewedNodeIsSubscribed = true;
_appSettings.LastViewedSubscriptionInterval = interval;
_appSettings.Save();
```

#### 取消訂閱時清除狀態

在 `BtnUnsubscribe_Click` 中：
```csharp
_appSettings.LastViewedNodeIsSubscribed = false;
_appSettings.LastViewedSubscriptionInterval = null;
_appSettings.Save();
```

## 使用流程

### 正常使用流程

1. **首次使用**：
   - 連接到 OPC UA 伺服器
   - 瀏覽節點樹並選擇一個節點
   - 讀取節點資料或創建訂閱
   - 關閉應用程式

2. **再次使用**：
   - 啟動應用程式
   - 點擊「連線」按鈕
   - **自動執行**：
     - ✓ 連接到上次的端點
     - ✓ 瀏覽根節點
     - ✓ 自動展開樹狀結構到上次查看的節點
     - ✓ 選中該節點
     - ✓ 讀取節點資料
     - ✓ 如果之前有訂閱，自動啟動訂閱

### 日誌輸出範例

```
[10:30:15.123] 正在連線到: opc.tcp://localhost:4840
[10:30:15.456] Security Policy: Basic256Sha256, Message Security Mode: SignAndEncrypt
[10:30:16.789] ✓ 連線成功
[10:30:16.890] ✓ 已儲存端點設定
[10:30:16.991] 正在瀏覽根節點...
[10:30:17.234] ✓ 已載入 5 個節點
[10:30:17.345] 正在導航到最後查看的節點: Temperature
[10:30:18.567] ✓ 已導航到節點: Temperature
[10:30:18.890] 正在自動讀取節點值: Temperature
[10:30:19.012] ✓ 自動讀取成功: 25.5
[10:30:19.345] 正在自動訂閱節點: Temperature, 間隔: 1000ms
[10:30:19.678] ✓ 已自動訂閱節點: Temperature
```

## 技術亮點

### 1. 遞歸搜索算法

使用深度優先搜索（DFS）遍歷整個 OPC UA 樹：
- 時間複雜度：O(n)，n 為節點總數
- 空間複雜度：O(h)，h 為樹的高度（遞歸調用棧）

### 2. 延遲加載機制

只在需要時載入子節點，減少網路請求：
```csharp
if (parentNode.NodeClass == NodeClass.Object && parentNode.Children.Count == 0)
{
    var children = await _clientManager!.BrowseAsync(parentNode.NodeId);
    // ...
}
```

### 3. UI 線程同步

使用 `Dispatcher.Invoke()` 確保訂閱回調在 UI 線程執行：
```csharp
Dispatcher.Invoke(() =>
{
    txtCurrentValue.Text = value?.ToString() ?? "null";
    // ...
});
```

### 4. 優雅的錯誤處理

所有自動操作都包含 try-catch 區塊，確保單個功能失敗不會影響整體流程：
```csharp
try
{
    await AutoReadNodeValue(foundNode);
}
catch (Exception ex)
{
    Log($"✗ 自動讀取錯誤: {ex.Message}");
    // 繼續執行，不中斷流程
}
```

## 改進的用戶體驗

### 節省時間

用戶不再需要每次連接後：
- ❌ 手動展開多層節點樹
- ❌ 記憶上次查看的節點位置
- ❌ 重新設置訂閱參數

### 工作流程優化

特別適用於以下場景：
1. **調試和測試**：頻繁重啟應用程式查看同一個變數
2. **監控特定節點**：需要長期監控某個關鍵參數
3. **快速恢復工作狀態**：中斷工作後快速恢復到之前的狀態

## 相容性

### 向後相容

- 如果 `settings.json` 中沒有節點資訊，功能會靜默跳過
- 不影響現有的端點保存功能
- 舊的設定檔案可以無縫升級

### 錯誤容錯

- 如果節點不存在（伺服器結構改變），會在日誌中提示但不會崩潰
- 如果訂閱失敗，會記錄錯誤但不影響其他功能
- 網路異常會被捕獲並顯示友好的錯誤訊息

## 未來改進方向

1. **支援多個節點書籤**：保存多個常用節點的快速訪問列表
2. **節點訪問歷史**：記錄最近訪問的 10 個節點
3. **智能節點建議**：根據訪問頻率推薦節點
4. **跨伺服器記憶**：為不同的端點保存不同的最後節點
5. **節點路徑優化**：使用完整路徑加速查找（避免遞歸搜索）

## 測試建議

### 功能測試

1. **基本導航測試**：
   - 選擇一個深層節點
   - 關閉並重新連接
   - 驗證是否正確導航到該節點

2. **訂閱恢復測試**：
   - 訂閱一個變數節點
   - 關閉應用程式
   - 重新連接
   - 驗證訂閱是否自動恢復

3. **錯誤處理測試**：
   - 刪除或修改伺服器上的節點
   - 驗證應用程式是否能優雅處理
   - 檢查日誌中的錯誤訊息

4. **效能測試**：
   - 測試在大型節點樹（1000+ 節點）中的搜索速度
   - 監控記憶體使用情況

### 用戶接受測試

- 邀請實際用戶測試工作流程改進
- 收集關於自動導航速度的反饋
- 評估是否需要添加「禁用自動導航」的選項

## 總結

此次重構大幅提升了 OPC UA 客戶端應用程式的用戶體驗，通過自動記憶和恢復用戶的工作狀態，減少了重複操作，提高了工作效率。所有功能都經過精心設計，確保穩定性和向後相容性。

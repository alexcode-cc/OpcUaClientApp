using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Opc.Ua;
using Opc.Ua.Client;

namespace OpcUaClientApp
{
    public partial class MainWindow : Window
    {
        private OpcUaClientManager? _clientManager;
        private OpcUaNodeItem? _selectedNode;
        private AppSettings _appSettings;

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

        /// <summary>
        /// 載入上次使用的端點設定
        /// </summary>
        private void LoadLastEndpointSettings()
        {
            if (!string.IsNullOrEmpty(_appSettings.LastEndpointUrl))
            {
                txtEndpointUrl.Text = _appSettings.LastEndpointUrl;
                Log($"已載入上次使用的端點位址: {_appSettings.LastEndpointUrl}");
            }

            // 載入上次的 Security Policy
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

            // 載入上次的 Message Security Mode
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
            else // 任何安全策略（Basic256Sha256, Aes128_Sha256_RsaOaep, Aes256_Sha256_RsaPss）
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

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string endpointUrl = txtEndpointUrl.Text.Trim();
                if (string.IsNullOrEmpty(endpointUrl))
                {
                    MessageBox.Show("請輸入端點位址", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 讀取安全設定
                string securityPolicy = ((ComboBoxItem)cmbSecurityPolicy.SelectedItem)?.Content?.ToString() ?? "None";
                string messageSecurityMode = ((ComboBoxItem)cmbMessageSecurityMode.SelectedItem)?.Content?.ToString() ?? "None";

                Log($"正在連線到: {endpointUrl}");
                Log($"Security Policy: {securityPolicy}, Message Security Mode: {messageSecurityMode}");
                SetStatusLight(Colors.Yellow); // 連線中

                _clientManager = new OpcUaClientManager();
                bool success = await _clientManager.ConnectAsync(endpointUrl, securityPolicy, messageSecurityMode, Log);

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

                    // 自動導航到最後查看的節點
                    await NavigateToLastViewedNode();
                }
                else
                {
                    Log("✗ 連線失敗");
                    SetStatusLight(Colors.Red);
                    MessageBox.Show("連線失敗，請檢查伺服器是否正在執行及安全設定是否正確", "錯誤",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 連線錯誤: {ex.Message}");
                SetStatusLight(Colors.Red);
                MessageBox.Show($"連線錯誤: {ex.Message}", "錯誤",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _clientManager?.Disconnect();
                _clientManager = null;

                Log("已斷線");
                SetStatusLight(Colors.Red);
                btnConnect.IsEnabled = true;
                btnDisconnect.IsEnabled = false;
                txtEndpointUrl.IsEnabled = true;
                cmbSecurityPolicy.IsEnabled = true;

                // 重新更新Message Security Mode的選項
                UpdateMessageSecurityModeOptions();

                treeNodes.Items.Clear();
                ClearNodeDetails();
            }
            catch (Exception ex)
            {
                Log($"✗ 斷線錯誤: {ex.Message}");
            }
        }

        private async void BtnBrowseNodes_Click(object sender, RoutedEventArgs e)
        {
            await BrowseRootNodes();
        }

        private async System.Threading.Tasks.Task BrowseRootNodes()
        {
            if (_clientManager == null || !_clientManager.IsConnected)
            {
                MessageBox.Show("請先連線到伺服器", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Log("正在瀏覽根節點...");
                treeNodes.Items.Clear();

                // 瀏覽 Objects 節點
                var objectsNode = new OpcUaNodeItem
                {
                    DisplayName = "Objects",
                    NodeId = ObjectIds.ObjectsFolder,
                    NodeClass = NodeClass.Object
                };

                var children = await _clientManager.BrowseAsync(ObjectIds.ObjectsFolder);
                foreach (var child in children)
                {
                    objectsNode.Children.Add(child);
                }

                treeNodes.Items.Add(objectsNode);
                Log($"✓ 已載入 {children.Count} 個節點");
            }
            catch (Exception ex)
            {
                Log($"✗ 瀏覽節點錯誤: {ex.Message}");
                MessageBox.Show($"瀏覽節點錯誤: {ex.Message}", "錯誤", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TreeNodes_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is OpcUaNodeItem node)
            {
                _selectedNode = node;
                DisplayNodeDetails(node);

                // 保存最後查看的節點資訊
                SaveLastViewedNode(node);

                // 如果節點尚未載入子節點，則載入
                if (node.Children.Count == 0 && node.NodeClass == NodeClass.Object)
                {
                    try
                    {
                        var children = await _clientManager!.BrowseAsync(node.NodeId);
                        foreach (var child in children)
                        {
                            node.Children.Add(child);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"✗ 載入子節點錯誤: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 保存最後查看的節點資訊
        /// </summary>
        private void SaveLastViewedNode(OpcUaNodeItem node)
        {
            try
            {
                _appSettings.LastViewedNodeId = node.NodeId.ToString();
                _appSettings.LastViewedNodeDisplayName = node.DisplayName;
                _appSettings.LastViewedNodeClass = node.NodeClass.ToString();

                // 構建節點路徑（從根到當前節點的路徑）
                _appSettings.LastViewedNodePath = BuildNodePath(node);

                _appSettings.Save();
            }
            catch (Exception ex)
            {
                Log($"✗ 保存節點資訊錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 構建節點路徑
        /// </summary>
        private string BuildNodePath(OpcUaNodeItem node)
        {
            // 使用節點的 DisplayName 和 NodeId 來構建路徑
            // 格式: "DisplayName|NodeId"
            return $"{node.DisplayName}|{node.NodeId}";
        }

        /// <summary>
        /// 自動導航到最後查看的節點
        /// </summary>
        private async System.Threading.Tasks.Task NavigateToLastViewedNode()
        {
            if (string.IsNullOrEmpty(_appSettings.LastViewedNodeId))
            {
                return; // 沒有保存的節點資訊
            }

            try
            {
                Log($"正在導航到最後查看的節點: {_appSettings.LastViewedNodeDisplayName}");

                // 解析 NodeId
                var nodeId = NodeId.Parse(_appSettings.LastViewedNodeId);

                // 在樹狀結構中查找並展開到該節點
                var foundNode = await FindAndExpandToNode(nodeId);

                if (foundNode != null)
                {
                    // 選中該節點
                    SelectTreeViewItem(foundNode);

                    Log($"✓ 已導航到節點: {foundNode.DisplayName}");

                    // 如果是變數節點，自動讀取資料
                    if (foundNode.NodeClass == NodeClass.Variable)
                    {
                        await System.Threading.Tasks.Task.Delay(300); // 稍微延遲以確保UI更新完成
                        await AutoReadNodeValue(foundNode);

                        // 如果有訂閱狀態，自動啟動訂閱
                        if (_appSettings.LastViewedNodeIsSubscribed &&
                            _appSettings.LastViewedSubscriptionInterval.HasValue)
                        {
                            await System.Threading.Tasks.Task.Delay(300);
                            await AutoSubscribeNode(foundNode, _appSettings.LastViewedSubscriptionInterval.Value);
                        }
                    }
                }
                else
                {
                    Log($"✗ 無法找到節點: {_appSettings.LastViewedNodeDisplayName}");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 導航到節點錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找並展開到指定節點
        /// </summary>
        private async System.Threading.Tasks.Task<OpcUaNodeItem?> FindAndExpandToNode(NodeId targetNodeId)
        {
            try
            {
                // 從根節點開始搜索
                if (treeNodes.Items.Count == 0)
                {
                    return null;
                }

                var rootNode = treeNodes.Items[0] as OpcUaNodeItem;
                if (rootNode == null)
                {
                    return null;
                }

                // 檢查根節點是否就是目標
                if (rootNode.NodeId == targetNodeId)
                {
                    return rootNode;
                }

                // 遞歸搜索子節點
                return await SearchAndExpandNode(rootNode, targetNodeId);
            }
            catch (Exception ex)
            {
                Log($"✗ 查找節點錯誤: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 遞歸搜索並展開節點
        /// </summary>
        private async System.Threading.Tasks.Task<OpcUaNodeItem?> SearchAndExpandNode(OpcUaNodeItem parentNode, NodeId targetNodeId)
        {
            try
            {
                // 如果父節點是對象且尚未載入子節點，則載入
                if (parentNode.NodeClass == NodeClass.Object && parentNode.Children.Count == 0)
                {
                    var children = await _clientManager!.BrowseAsync(parentNode.NodeId);
                    foreach (var child in children)
                    {
                        parentNode.Children.Add(child);
                    }
                }

                // 搜索子節點
                foreach (var child in parentNode.Children)
                {
                    if (child.NodeId == targetNodeId)
                    {
                        return child; // 找到目標節點
                    }

                    // 如果子節點是對象，遞歸搜索
                    if (child.NodeClass == NodeClass.Object)
                    {
                        var result = await SearchAndExpandNode(child, targetNodeId);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 選中 TreeView 中的項目
        /// </summary>
        private void SelectTreeViewItem(OpcUaNodeItem node)
        {
            try
            {
                // 使用 TreeView 的選擇機制
                var item = FindTreeViewItem(treeNodes, node);
                if (item != null)
                {
                    item.IsSelected = true;
                    item.BringIntoView();
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 選中節點錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 在 TreeView 中查找對應的 TreeViewItem
        /// </summary>
        private TreeViewItem? FindTreeViewItem(ItemsControl container, OpcUaNodeItem node)
        {
            if (container == null)
                return null;

            foreach (var item in container.Items)
            {
                var treeViewItem = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeViewItem == null)
                    continue;

                if (item is OpcUaNodeItem currentNode && currentNode.NodeId == node.NodeId)
                {
                    return treeViewItem;
                }

                // 展開並搜索子項
                treeViewItem.IsExpanded = true;
                treeViewItem.UpdateLayout();

                var result = FindTreeViewItem(treeViewItem, node);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 自動讀取節點值
        /// </summary>
        private async System.Threading.Tasks.Task AutoReadNodeValue(OpcUaNodeItem node)
        {
            try
            {
                Log($"正在自動讀取節點值: {node.DisplayName}");
                var result = await _clientManager!.ReadValueAsync(node.NodeId);

                if (result != null)
                {
                    txtCurrentValue.Text = result.Value?.ToString() ?? "null";
                    txtTimestamp.Text = result.SourceTimestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                    Log($"✓ 自動讀取成功: {txtCurrentValue.Text}");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 自動讀取錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 自動訂閱節點
        /// </summary>
        private async System.Threading.Tasks.Task AutoSubscribeNode(OpcUaNodeItem node, int interval)
        {
            try
            {
                Log($"正在自動訂閱節點: {node.DisplayName}, 間隔: {interval}ms");

                _clientManager!.Subscribe(node.NodeId, interval, (nodeId, value, timestamp) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var message = $"[{timestamp:HH:mm:ss.fff}] {node.DisplayName} = {value}";
                        lstMonitoredItems.Items.Insert(0, new { Message = message });

                        // 限制列表項目數量
                        if (lstMonitoredItems.Items.Count > 100)
                        {
                            lstMonitoredItems.Items.RemoveAt(lstMonitoredItems.Items.Count - 1);
                        }

                        // 如果當前選中的節點正在被監控，更新顯示
                        if (_selectedNode != null && _selectedNode.NodeId == nodeId)
                        {
                            txtCurrentValue.Text = value?.ToString() ?? "null";
                            txtTimestamp.Text = timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                        }
                    });
                });

                // 更新訂閱間隔 UI
                txtPublishingInterval.Text = interval.ToString();

                Log($"✓ 已自動訂閱節點: {node.DisplayName}");

                await System.Threading.Tasks.Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log($"✗ 自動訂閱錯誤: {ex.Message}");
            }
        }

        private void DisplayNodeDetails(OpcUaNodeItem node)
        {
            txtDisplayName.Text = node.DisplayName;
            txtNodeId.Text = node.NodeId.ToString();
            txtNodeClass.Text = node.NodeClass.ToString();
            txtDataType.Text = node.DataType ?? "N/A";
            txtCurrentValue.Text = "";
            txtTimestamp.Text = "";
        }

        private void ClearNodeDetails()
        {
            txtDisplayName.Text = "";
            txtNodeId.Text = "";
            txtNodeClass.Text = "";
            txtDataType.Text = "";
            txtCurrentValue.Text = "";
            txtTimestamp.Text = "";
            txtNewValue.Text = "";
        }

        private async void BtnRead_Click(object sender, RoutedEventArgs e)
        {
            if (_clientManager == null || !_clientManager.IsConnected)
            {
                MessageBox.Show("請先連線到伺服器", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedNode == null || _selectedNode.NodeClass != NodeClass.Variable)
            {
                MessageBox.Show("請選擇一個變數節點", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Log($"正在讀取節點: {_selectedNode.DisplayName}");
                var result = await _clientManager.ReadValueAsync(_selectedNode.NodeId);

                if (result != null)
                {
                    txtCurrentValue.Text = result.Value?.ToString() ?? "null";
                    txtTimestamp.Text = result.SourceTimestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                    Log($"✓ 讀取成功: {txtCurrentValue.Text}");
                }
                else
                {
                    Log("✗ 讀取失敗");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 讀取錯誤: {ex.Message}");
                MessageBox.Show($"讀取錯誤: {ex.Message}", "錯誤", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnWrite_Click(object sender, RoutedEventArgs e)
        {
            if (_clientManager == null || !_clientManager.IsConnected)
            {
                MessageBox.Show("請先連線到伺服器", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedNode == null || _selectedNode.NodeClass != NodeClass.Variable)
            {
                MessageBox.Show("請選擇一個變數節點", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNewValue.Text))
            {
                MessageBox.Show("請輸入要寫入的值", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Log($"正在寫入節點: {_selectedNode.DisplayName}");
                bool success = await _clientManager.WriteValueAsync(_selectedNode.NodeId, txtNewValue.Text);

                if (success)
                {
                    Log($"✓ 寫入成功: {txtNewValue.Text}");
                    MessageBox.Show("寫入成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // 自動讀取以確認
                    await System.Threading.Tasks.Task.Delay(100);
                    BtnRead_Click(sender, e);
                }
                else
                {
                    Log("✗ 寫入失敗");
                    MessageBox.Show("寫入失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 寫入錯誤: {ex.Message}");
                MessageBox.Show($"寫入錯誤: {ex.Message}", "錯誤", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            if (_clientManager == null || !_clientManager.IsConnected)
            {
                MessageBox.Show("請先連線到伺服器", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedNode == null || _selectedNode.NodeClass != NodeClass.Variable)
            {
                MessageBox.Show("請選擇一個變數節點", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                if (!int.TryParse(txtPublishingInterval.Text, out int interval) || interval < 100)
                {
                    MessageBox.Show("請輸入有效的更新間隔（至少 100 毫秒）", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _clientManager.Subscribe(_selectedNode.NodeId, interval, (nodeId, value, timestamp) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var message = $"[{timestamp:HH:mm:ss.fff}] {_selectedNode.DisplayName} = {value}";
                        lstMonitoredItems.Items.Insert(0, new { Message = message });

                        // 限制列表項目數量
                        if (lstMonitoredItems.Items.Count > 100)
                        {
                            lstMonitoredItems.Items.RemoveAt(lstMonitoredItems.Items.Count - 1);
                        }

                        // 如果當前選中的節點正在被監控，更新顯示
                        if (_selectedNode != null && _selectedNode.NodeId == nodeId)
                        {
                            txtCurrentValue.Text = value?.ToString() ?? "null";
                            txtTimestamp.Text = timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                        }
                    });
                });

                // 保存訂閱狀態
                _appSettings.LastViewedNodeIsSubscribed = true;
                _appSettings.LastViewedSubscriptionInterval = interval;
                _appSettings.Save();

                Log($"✓ 已訂閱節點: {_selectedNode.DisplayName}");
                MessageBox.Show("訂閱成功，資料變化將即時顯示", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"✗ 訂閱錯誤: {ex.Message}");
                MessageBox.Show($"訂閱錯誤: {ex.Message}", "錯誤", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUnsubscribe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _clientManager?.UnsubscribeAll();
                lstMonitoredItems.Items.Clear();

                // 清除訂閱狀態
                _appSettings.LastViewedNodeIsSubscribed = false;
                _appSettings.LastViewedSubscriptionInterval = null;
                _appSettings.Save();

                Log("✓ 已取消所有訂閱");
            }
            catch (Exception ex)
            {
                Log($"✗ 取消訂閱錯誤: {ex.Message}");
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
        }

        private void SetStatusLight(Color color)
        {
            statusLight.Fill = new SolidColorBrush(color);
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            txtLog.AppendText($"[{timestamp}] {message}\n");
            txtLog.ScrollToEnd();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _clientManager?.Disconnect();
            base.OnClosing(e);
        }
    }
}

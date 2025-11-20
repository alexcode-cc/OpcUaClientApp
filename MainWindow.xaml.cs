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

        public MainWindow()
        {
            InitializeComponent();
            Log("應用程式已啟動");

            // 初始化安全設定
            UpdateMessageSecurityModeOptions();
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
            else // Basic256Sha256
            {
                // 當 Security Policy 為 Basic256Sha256 時，可選擇 Sign 或 SignAndEncrypt
                cmbMessageSecurityMode.IsEnabled = true;

                // 如果當前選擇的是 None，則自動切換到 Sign
                if (cmbMessageSecurityMode.SelectedIndex == 0)
                {
                    cmbMessageSecurityMode.SelectedIndex = 1; // Sign
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
                bool success = await _clientManager.ConnectAsync(endpointUrl, securityPolicy, messageSecurityMode);

                if (success)
                {
                    Log("✓ 連線成功");
                    SetStatusLight(Colors.Green);
                    btnConnect.IsEnabled = false;
                    btnDisconnect.IsEnabled = true;
                    txtEndpointUrl.IsEnabled = false;
                    cmbSecurityPolicy.IsEnabled = false;
                    cmbMessageSecurityMode.IsEnabled = false;

                    // 自動瀏覽根節點
                    await BrowseRootNodes();
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

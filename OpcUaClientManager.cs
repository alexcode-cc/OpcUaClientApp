using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace OpcUaClientApp
{
    public class OpcUaClientManager
    {
        private ISession? _session;
        private Subscription? _subscription;
        private ApplicationConfiguration? _configuration;

        public bool IsConnected => _session != null && _session.Connected;

        public async Task<bool> ConnectAsync(string endpointUrl, string securityPolicy = "None", string messageSecurityMode = "None", Action<string>? log = null)
        {
            try
            {
                log?.Invoke($"[偵錯] 開始連線程序");
                log?.Invoke($"[偵錯] 端點: {endpointUrl}");
                log?.Invoke($"[偵錯] 要求的 Security Policy: {securityPolicy}");
                log?.Invoke($"[偵錯] 要求的 Message Security Mode: {messageSecurityMode}");

                // 建立應用程式配置
                log?.Invoke($"[偵錯] 建立應用程式配置...");
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
                    TransportQuotas = new TransportQuotas
                    {
                        OperationTimeout = 30000,
                        MaxStringLength = 1048576,
                        MaxByteStringLength = 1048576,
                        MaxArrayLength = 65535,
                        MaxMessageSize = 4194304,
                        MaxBufferSize = 65535,
                        ChannelLifetime = 300000,
                        SecurityTokenLifetime = 3600000
                    },
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = 60000,
                        MinSubscriptionLifetime = 10000
                    }
                };

                await _configuration.Validate(ApplicationType.Client);
                log?.Invoke($"[偵錯] 應用程式配置驗證完成");

                // 如果需要安全連線，檢查並創建應用程式證書
                if (securityPolicy != "None")
                {
                    log?.Invoke($"[偵錯] 需要安全連線，檢查應用程式證書...");
                    var existingCertificate = await _configuration.SecurityConfiguration.ApplicationCertificate.Find(false);

                    if (existingCertificate == null)
                    {
                        log?.Invoke($"[偵錯] 應用程式證書不存在，開始創建自簽名證書...");

                        // 創建自簽名證書
                        var certificate = CertificateFactory.CreateCertificate(
                            _configuration.SecurityConfiguration.ApplicationCertificate.StoreType,
                            _configuration.SecurityConfiguration.ApplicationCertificate.StorePath,
                            null,
                            _configuration.ApplicationUri,
                            _configuration.ApplicationName,
                            _configuration.SecurityConfiguration.ApplicationCertificate.SubjectName,
                            null,
                            2048,
                            DateTime.UtcNow - TimeSpan.FromDays(1),
                            60, // 60 個月有效期
                            256  // SHA256
                        );

                        _configuration.SecurityConfiguration.ApplicationCertificate.Certificate = certificate;
                        log?.Invoke($"[偵錯] ✓ 已創建自簽名證書");
                        log?.Invoke($"[偵錯]   Subject: {certificate.Subject}");
                        log?.Invoke($"[偵錯]   Thumbprint: {certificate.Thumbprint}");
                        log?.Invoke($"[偵錯]   有效期至: {certificate.NotAfter}");
                    }
                    else
                    {
                        log?.Invoke($"[偵錯] ✓ 找到現有的應用程式證書");
                        log?.Invoke($"[偵錯]   Subject: {existingCertificate.Subject}");
                        log?.Invoke($"[偵錯]   Thumbprint: {existingCertificate.Thumbprint}");
                        log?.Invoke($"[偵錯]   有效期至: {existingCertificate.NotAfter}");

                        // 確保證書已經設置到配置中
                        _configuration.SecurityConfiguration.ApplicationCertificate.Certificate = existingCertificate;
                    }
                }
                else
                {
                    log?.Invoke($"[偵錯] Security Policy 為 None，不需要應用程式證書");
                }

                // 將字串轉換為對應的 SecurityPolicy URI 和 MessageSecurityMode 枚舉
                string securityPolicyUri = ConvertSecurityPolicyToUri(securityPolicy);
                MessageSecurityMode securityMode = ConvertToMessageSecurityMode(messageSecurityMode);

                log?.Invoke($"[偵錯] 轉換後的 Security Policy URI: {securityPolicyUri}");
                log?.Invoke($"[偵錯] 轉換後的 Message Security Mode: {securityMode}");

                // 選擇端點
                log?.Invoke($"[偵錯] 開始發現並選擇端點...");
                var endpointDescription = SelectEndpoint(endpointUrl, securityPolicyUri, securityMode, log);

                log?.Invoke($"[偵錯] 已選擇端點:");
                log?.Invoke($"[偵錯]   - 端點 URL: {endpointDescription.EndpointUrl}");
                log?.Invoke($"[偵錯]   - Security Policy: {endpointDescription.SecurityPolicyUri}");
                log?.Invoke($"[偵錯]   - Security Mode: {endpointDescription.SecurityMode}");
                log?.Invoke($"[偵錯]   - Security Level: {endpointDescription.SecurityLevel}");

                // 驗證選擇的端點
                if (endpointDescription.SecurityPolicyUri != securityPolicyUri ||
                    endpointDescription.SecurityMode != securityMode)
                {
                    throw new Exception(
                        $"端點選擇錯誤！\n" +
                        $"期望: Policy={securityPolicyUri}, Mode={securityMode}\n" +
                        $"實際: Policy={endpointDescription.SecurityPolicyUri}, Mode={endpointDescription.SecurityMode}");
                }

                log?.Invoke($"[偵錯] 端點驗證通過");

                var endpointConfiguration = EndpointConfiguration.Create(_configuration);
                var endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

                log?.Invoke($"[偵錯] 建立 ConfiguredEndpoint 完成");

                // 建立 Session
                log?.Invoke($"[偵錯] 開始建立 Session...");
                _session = await Session.Create(
                    _configuration,
                    endpoint,
                    false,
                    "OPC UA Client Session",
                    60000,
                    new UserIdentity(new AnonymousIdentityToken()),
                    null
                );

                if (_session != null && _session.Connected)
                {
                    log?.Invoke($"[偵錯] Session 建立成功");
                    log?.Invoke($"[偵錯] Session ID: {_session.SessionId}");
                    log?.Invoke($"[偵錯] 使用的端點 Security Policy: {_session.Endpoint.SecurityPolicyUri}");
                    log?.Invoke($"[偵錯] 使用的端點 Security Mode: {_session.Endpoint.SecurityMode}");
                    return true;
                }

                log?.Invoke($"[偵錯] Session 建立失敗");
                return false;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[錯誤] 連線異常: {ex.GetType().Name}");
                log?.Invoke($"[錯誤] 錯誤訊息: {ex.Message}");
                if (ex.InnerException != null)
                {
                    log?.Invoke($"[錯誤] 內部異常: {ex.InnerException.Message}");
                }
                log?.Invoke($"[錯誤] 堆疊追蹤: {ex.StackTrace}");
                return false;
            }
        }

        private string ConvertSecurityPolicyToUri(string securityPolicy)
        {
            return securityPolicy switch
            {
                "None" => SecurityPolicies.None,
                "Basic256Sha256" => SecurityPolicies.Basic256Sha256,
                _ => SecurityPolicies.None
            };
        }

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

        private EndpointDescription SelectEndpoint(string endpointUrl, string securityPolicyUri, MessageSecurityMode securityMode, Action<string>? log = null)
        {
            // 發現所有端點
            log?.Invoke($"[偵錯] 建立 DiscoveryClient...");
            var endpointConfiguration = EndpointConfiguration.Create();
            endpointConfiguration.OperationTimeout = 15000;

            using (var discoveryClient = DiscoveryClient.Create(new Uri(endpointUrl), endpointConfiguration))
            {
                log?.Invoke($"[偵錯] 正在取得所有可用端點...");
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

                log?.Invoke($"[偵錯] 開始尋找匹配的端點...");
                log?.Invoke($"[偵錯] 要求的 Security Policy URI: {securityPolicyUri}");
                log?.Invoke($"[偵錯] 要求的 Security Mode: {securityMode}");

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
                log?.Invoke($"[錯誤] ✗ 找不到匹配的端點！");
                var availableEndpoints = string.Join("\n", endpoints.Select(e =>
                    $"  - Policy: {e.SecurityPolicyUri}, Mode: {e.SecurityMode}"));

                throw new Exception(
                    $"找不到匹配的端點！\n" +
                    $"要求: Policy={securityPolicyUri}, Mode={securityMode}\n" +
                    $"可用的端點:\n{availableEndpoints}");
            }
        }

        public void Disconnect()
        {
            if (_subscription != null)
            {
                _subscription.Delete(true);
                _subscription = null;
            }

            if (_session != null)
            {
                _session.Close();
                _session.Dispose();
                _session = null;
            }
        }

        public async Task<List<OpcUaNodeItem>> BrowseAsync(NodeId nodeId)
        {
            var result = new List<OpcUaNodeItem>();

            if (_session == null || !_session.Connected)
                return result;

            try
            {
                // 建立瀏覽描述
                var browseDescription = new BrowseDescription
                {
                    NodeId = nodeId,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                    ResultMask = (uint)BrowseResultMask.All
                };

                var browseDescriptions = new BrowseDescriptionCollection { browseDescription };

                // 執行瀏覽
                _session.Browse(
                    null,
                    null,
                    0,
                    browseDescriptions,
                    out var results,
                    out _
                );

                if (results != null && results.Count > 0)
                {
                    var browseResult = results[0];

                    foreach (var reference in browseResult.References)
                    {
                        var nodeItem = new OpcUaNodeItem
                        {
                            DisplayName = reference.DisplayName.Text,
                            NodeId = ExpandedNodeId.ToNodeId(reference.NodeId, _session.NamespaceUris),
                            NodeClass = reference.NodeClass
                        };

                        // 如果是變數節點，讀取資料型別
                        if (reference.NodeClass == NodeClass.Variable)
                        {
                            try
                            {
                                var node = _session.ReadNode(nodeItem.NodeId) as VariableNode;
                                if (node != null)
                                {
                                    nodeItem.DataType = _session.NodeCache.Find(node.DataType)?.DisplayName.Text;
                                }
                            }
                            catch
                            {
                                // 忽略錯誤
                            }
                        }

                        result.Add(nodeItem);
                    }
                }
            }
            catch (Exception)
            {
                // 忽略錯誤
            }

            return result;
        }

        public async Task<DataValue?> ReadValueAsync(NodeId nodeId)
        {
            if (_session == null || !_session.Connected)
                return null;

            try
            {
                var nodesToRead = new ReadValueIdCollection
                {
                    new ReadValueId
                    {
                        NodeId = nodeId,
                        AttributeId = Attributes.Value
                    }
                };

                _session.Read(
                    null,
                    0,
                    TimestampsToReturn.Both,
                    nodesToRead,
                    out var results,
                    out _
                );

                if (results != null && results.Count > 0)
                {
                    return results[0];
                }
            }
            catch (Exception)
            {
                // 忽略錯誤
            }

            return null;
        }

        public async Task<bool> WriteValueAsync(NodeId nodeId, string value)
        {
            if (_session == null || !_session.Connected)
                return false;

            try
            {
                // 首先讀取節點以確定資料型別
                var node = _session.ReadNode(nodeId) as VariableNode;
                if (node == null)
                    return false;

                // 轉換值為正確的型別
                object? convertedValue = ConvertValue(value, node.DataType);
                if (convertedValue == null)
                    return false;

                var nodesToWrite = new WriteValueCollection
                {
                    new WriteValue
                    {
                        NodeId = nodeId,
                        AttributeId = Attributes.Value,
                        Value = new DataValue(new Variant(convertedValue))
                    }
                };

                _session.Write(
                    null,
                    nodesToWrite,
                    out var results,
                    out _
                );

                if (results != null && results.Count > 0)
                {
                    return StatusCode.IsGood(results[0]);
                }
            }
            catch (Exception)
            {
                // 忽略錯誤
            }

            return false;
        }

        public void Subscribe(NodeId nodeId, int publishingInterval, Action<NodeId, object?, DateTime> onDataChanged)
        {
            if (_session == null || !_session.Connected)
                return;

            try
            {
                // 建立 Subscription
                if (_subscription == null)
                {
                    _subscription = new Subscription(_session.DefaultSubscription)
                    {
                        PublishingInterval = publishingInterval,
                        PublishingEnabled = true
                    };

                    _session.AddSubscription(_subscription);
                    _subscription.Create();
                }

                // 建立 Monitored Item
                var monitoredItem = new MonitoredItem(_subscription.DefaultItem)
                {
                    StartNodeId = nodeId,
                    AttributeId = Attributes.Value,
                    SamplingInterval = publishingInterval,
                    QueueSize = 10,
                    DiscardOldest = true
                };

                monitoredItem.Notification += (item, e) =>
                {
                    if (e.NotificationValue is MonitoredItemNotification notification)
                    {
                        onDataChanged(nodeId, notification.Value.Value, notification.Value.SourceTimestamp);
                    }
                };

                _subscription.AddItem(monitoredItem);
                _subscription.ApplyChanges();
            }
            catch (Exception)
            {
                // 忽略錯誤
            }
        }

        public void UnsubscribeAll()
        {
            if (_subscription != null)
            {
                _subscription.RemoveItems(_subscription.MonitoredItems);
                _subscription.ApplyChanges();
            }
        }

        private object? ConvertValue(string value, NodeId dataTypeId)
        {
            try
            {
                var builtInType = DataTypes.GetBuiltInType(dataTypeId);

                return builtInType switch
                {
                    BuiltInType.Boolean => bool.Parse(value),
                    BuiltInType.SByte => sbyte.Parse(value),
                    BuiltInType.Byte => byte.Parse(value),
                    BuiltInType.Int16 => short.Parse(value),
                    BuiltInType.UInt16 => ushort.Parse(value),
                    BuiltInType.Int32 => int.Parse(value),
                    BuiltInType.UInt32 => uint.Parse(value),
                    BuiltInType.Int64 => long.Parse(value),
                    BuiltInType.UInt64 => ulong.Parse(value),
                    BuiltInType.Float => float.Parse(value),
                    BuiltInType.Double => double.Parse(value),
                    BuiltInType.String => value,
                    BuiltInType.DateTime => DateTime.Parse(value),
                    _ => value
                };
            }
            catch
            {
                return value;
            }
        }
    }
}

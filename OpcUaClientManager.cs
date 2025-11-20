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

        public async Task<bool> ConnectAsync(string endpointUrl)
        {
            try
            {
                // 建立應用程式配置
                _configuration = new ApplicationConfiguration
                {
                    ApplicationName = "OPC UA Client App",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier(),
                        AutoAcceptUntrustedCertificates = true,
                        RejectSHA1SignedCertificates = false
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

                // 選擇端點
                var endpointDescription = CoreClientUtils.SelectEndpoint(endpointUrl, false, 15000);
                var endpointConfiguration = EndpointConfiguration.Create(_configuration);
                var endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

                // 建立 Session
                _session = await Session.Create(
                    _configuration,
                    endpoint,
                    false,
                    "OPC UA Client Session",
                    60000,
                    new UserIdentity(new AnonymousIdentityToken()),
                    null
                );

                return _session != null && _session.Connected;
            }
            catch (Exception)
            {
                return false;
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

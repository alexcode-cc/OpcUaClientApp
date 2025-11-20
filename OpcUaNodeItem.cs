using System.Collections.ObjectModel;
using Opc.Ua;

namespace OpcUaClientApp
{
    public class OpcUaNodeItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public NodeId NodeId { get; set; } = NodeId.Null;
        public NodeClass NodeClass { get; set; }
        public string? DataType { get; set; }
        public ObservableCollection<OpcUaNodeItem> Children { get; set; } = new();
    }
}

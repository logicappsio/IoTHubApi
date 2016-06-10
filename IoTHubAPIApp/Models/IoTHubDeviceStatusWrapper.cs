using System.ComponentModel.DataAnnotations;
using TRex.Metadata;
using Microsoft.Azure.Devices;

namespace IoTHubAPIApp.Models
{
    public class IoTHubDeviceStatusWrapper
    {
        [Metadata("Connection String", "IoT hub connection string.")]
        [Required(AllowEmptyStrings = false)]
        public string ConnectionString { get; set; }

        [Metadata("IoT device ids", "Comma separated list of device ids previously registered with the Azure IoT hub.")]
        [Required(AllowEmptyStrings = false)]
        public string DeviceIds { get; set; }

        [Metadata("DeviceStatus", "Enable or disable?")]
        [Required]
        public DeviceStatus DeviceStatus { get; set; }
    }
}

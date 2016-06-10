using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using TRex.Metadata;

namespace IoTHubAPIApp.Models
{
    public class IoTHubMessageWrapper
    {
        [Metadata("Connection String", "IoT hub connection string.")]
        [Required(AllowEmptyStrings = false)]
        public string ConnectionString { get; set; }

        [Metadata("IoT device id", "Device id registered with the Azure IoT hub.")]
        [Required(AllowEmptyStrings = false)]
        public string DeviceId { get; set; }

        [Metadata("Message", "A serialized message string to send to the device.")]
        public string Message { get; set; }
    }
}

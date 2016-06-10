using System.ComponentModel.DataAnnotations;
using TRex.Metadata;

namespace IoTHubAPIApp.Models
{
    public class IoTHubRegisterWrapper
    {
        [Metadata("Connection String", "IoT hub connection string.")]
        [Required(AllowEmptyStrings = false)]
        public string ConnectionString { get; set; }

        [Metadata("IoT device ids", "Comma separated list of device ids.")]
        [Required(AllowEmptyStrings = false)]
        public string DeviceIds { get; set; }
    }
}

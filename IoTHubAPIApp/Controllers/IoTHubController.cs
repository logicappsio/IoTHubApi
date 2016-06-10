namespace IoTHubAPIApp.Controllers
{
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Models;
    using Localization;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web.Hosting;
    using System.Web.Http;
    using TRex.Metadata;

    /// TODO: Localize any error messages if Logic Apps platform surfaces these errors to business users.
    /// <summary>
    /// ApiController for connecting with IoT hub.
    /// </summary>
    public class IoTHubController : ApiController
    {
        private static readonly Regex AzureIoTDeviceIdRegex = new Regex(@"^[A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128}$");

        #region Action - Send message
        [Metadata("Send message to an IoT hub device")]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.BadRequest, "An exception occured", typeof(Exception))]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.Created)]
        [Route("SendMessage")]
        public HttpResponseMessage SendMessage([FromBody]IoTHubMessageWrapper input)
        {
            try
            {
                ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(input.ConnectionString);
                var message = new Message(Encoding.UTF8.GetBytes(input.Message));
                HostingEnvironment.QueueBackgroundWorkItem(async ct => await serviceClient.SendAsync(input.DeviceId, message));

                return Request.CreateResponse(HttpStatusCode.Created);
            }
            catch (NullReferenceException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, @"The input Received by the API was null. This sometimes happens if the message in the Logic App is malformed. Check the message to make sure there are no escape characters like '\'.", ex);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }
        #endregion

        #region Action - Register device
        /// <summary>
        /// Registers the given list of devices. If enable is set to true, this will register all the devices in enabled state and for those devices
        /// that were already registered, it will update the state to enabled.
        /// </summary>
        [Metadata("Register devices with IoT hub")]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.BadRequest, "An exception occured", typeof(Exception))]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.Created)]
        [Route("RegisterDevices")]
        public async Task<HttpResponseMessage> RegisterDevices([FromBody]IoTHubRegisterWrapper input,
                                                                [Metadata(null, null, VisibilityType.Internal)]bool enable = false)
        {
            try
            {
                RegistryManager registryManager = RegistryManager.CreateFromConnectionString(input.ConnectionString);

                List<string> deviceIds = input.DeviceIds.Split(',').Select(x => x.Trim()).ToList();

                IEnumerable<DeviceRegistryOperationError> errors = await RegisterDevices(registryManager, deviceIds, enable);
                return Request.CreateResponse(HttpStatusCode.Created, errors);
            }
            // TODO: Move exception handling for all these apis to a common method.
            catch (NullReferenceException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, @"The input Received by the API was null. This sometimes happens if the message in the Logic App is malformed. Check the message to make sure there are no escape characters like '\'.", ex);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }
        #endregion

        #region Action - Register and enable device
        /// <summary>
        /// Registers and enables the given list of devices.
        /// </summary>
        [Metadata("Register and enable devices with IoT hub")]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.BadRequest, "An exception occured", typeof(Exception))]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.Created)]
        [Route("RegisterAndEnableDevices")]
        public async Task<HttpResponseMessage> RegisterAndEnableDevices([FromBody]IoTHubRegisterWrapper input)
        {
            return await RegisterDevices(input, true);
        }
        #endregion

        #region Action - Set device status
        /// <summary>
        /// Enable or disable the given list of devices.
        /// </summary>
        [Metadata("Enable or disable devices")]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.BadRequest, "An exception occured", typeof(Exception))]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.Created)]
        [Route("SetDeviceStatus")]
        public async Task<HttpResponseMessage> SetDeviceStatus([FromBody]IoTHubDeviceStatusWrapper input)
        {
            try
            {
                IEnumerable<string> deviceIds = input.DeviceIds.Split(',').Select(x => x.Trim());

                RegistryManager registryManager = RegistryManager.CreateFromConnectionString(input.ConnectionString);
                IEnumerable<DeviceRegistryOperationError> errors = await SetStatuses(registryManager, deviceIds, input.DeviceStatus);

                return Request.CreateResponse(HttpStatusCode.Created, errors);
            }
            catch (NullReferenceException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, @"The input Received by the API was null. This sometimes happens if the message in the Logic App is malformed. Check the message to make sure there are no escape characters like '\'.", ex);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }
        #endregion

        #region Action - Unregister device
        /// <summary>
        /// Unregisters the given list of devices.
        /// </summary>
        [Metadata("Unregister devices from IoT hub")]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.BadRequest, "An exception occured", typeof(Exception))]
        [Swashbuckle.Swagger.Annotations.SwaggerResponse(HttpStatusCode.Created)]
        [Route("UnregisterDevices")]
        public async Task<HttpResponseMessage> UnregisterDevices([FromBody]IoTHubRegisterWrapper input)
        {
            try
            {
                RegistryManager registryManager = RegistryManager.CreateFromConnectionString(input.ConnectionString);

                List<string> deviceIds = input.DeviceIds.Split(',').Select(x => x.Trim()).ToList();

                IEnumerable<DeviceRegistryOperationError> errors = await UnregisterDevices(registryManager, deviceIds);
                return Request.CreateResponse(HttpStatusCode.Created, errors);
            }
            catch (NullReferenceException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, @"The input Received by the API was null. This sometimes happens if the message in the Logic App is malformed. Check the message to make sure there are no escape characters like '\'.", ex);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }
        #endregion

        #region Private Helpers

        /// <summary>
        /// Unregisters devices from the IoT Hub.
        /// </summary>
        /// <param name="registryManager">An instance of <c>RegistryManager</c></param>
        /// <param name="deviceIds">The list of device ids to unregister.</param>
        /// <returns>Any errors encountered during the bulk remove operation.</returns>
        /// <remarks>Ignores DeviceNotFound errors</remarks>
        private async Task<IEnumerable<DeviceRegistryOperationError>> UnregisterDevices(RegistryManager registryManager, List<string> deviceIds)
        {
            IEnumerable<Task<Device>> getDeviceTasks = deviceIds.Select(
                                                                    async deviceId => await registryManager.GetDeviceAsync(deviceId)
                                                                );

            IEnumerable<Device> deviceList = await Task.WhenAll(getDeviceTasks);

            BulkRegistryOperationResult unregisterResult = await registryManager.RemoveDevices2Async(deviceList);
            return GetRegistryOperationErrors(unregisterResult, ErrorCode.DeviceNotFound);
        }

        /// <summary>
        /// Registers devices with the IoT Hub.
        /// </summary>
        /// <param name="registryManager">An instance of <c>RegistryManager</c></param>
        /// <param name="deviceIds">The list of device ids to register.</param>
        /// <param name="enable">Devices will be registered with enabled state if this is set to true.</param>
        /// <returns>Any errors returned during the bulk register operation (and bulk update operation if devices are being enabled also).</returns>
        private async Task<IEnumerable<DeviceRegistryOperationError>> RegisterDevices(RegistryManager registryManager, List<string> deviceIds, bool enable = false)
        {
            IEnumerable<DeviceRegistryOperationError> registerErrors = deviceIds.Where(
                                                                                    id => !AzureIoTDeviceIdRegex.IsMatch(id)
                                                                                )
                                                                                .Select(
                                                                                    id => new DeviceRegistryOperationError()
                                                                                    {
                                                                                        ErrorCode = ErrorCode.ArgumentInvalid,
                                                                                        DeviceId = id,
                                                                                        ErrorStatus = String.Format(Labels.AzureIoTDeviceIdInvalid, id)
                                                                                    }
                                                                                );
            IEnumerable<DeviceRegistryOperationError> updateErrors = Enumerable.Empty<DeviceRegistryOperationError>();
            List<Device> deviceList = deviceIds.Where(
                                                id => AzureIoTDeviceIdRegex.IsMatch(id)
                                            ).Select(
                                                id => { return new Device(id) { Status = enable ? DeviceStatus.Enabled : DeviceStatus.Disabled }; }
                                            ).ToList();

            if (deviceList.Count > 0)
            {
                BulkRegistryOperationResult registerResult = await registryManager.AddDevices2Async(deviceList);

                if (!registerResult.IsSuccessful)
                {
                    // Some devices were already registered.
                    // Explicity enable those devices only if the argument is set to true, otherwise skip this step.
                    if (enable)
                    {
                        IEnumerable<string> deviceIdsToUpdate = registerResult.Errors
                                                                            .Where(
                                                                                error => error.ErrorCode == ErrorCode.DeviceAlreadyExists
                                                                            )
                                                                            .Select(
                                                                                error => error.DeviceId
                                                                            );

                        updateErrors = await SetStatuses(registryManager, deviceIdsToUpdate, DeviceStatus.Enabled);
                    }
                }

                registerErrors = registerErrors.Concat(GetRegistryOperationErrors(registerResult, ErrorCode.DeviceAlreadyExists));
            }

            return registerErrors.Concat(updateErrors);
        }

        /// <summary>
        /// Enable or disable devices.
        /// </summary>
        /// <param name="registryManager">An instance of <c>RegistryManager</c></param>
        /// <param name="deviceIds">The list of device ids to set the status for.</param>
        /// <param name="status">DeviceStatus enum to update to.</param>
        /// <returns>Any errors returned during the bulk update operation.</returns>
        private async Task<IEnumerable<DeviceRegistryOperationError>> SetStatuses(RegistryManager registryManager, IEnumerable<string> deviceIds, DeviceStatus status)
        {
            IEnumerable<Task<Device>> getDeviceTasks = deviceIds.Select(
                                                                    async deviceId => await registryManager.GetDeviceAsync(deviceId)
                                                                );

            IEnumerable<Device> deviceListToUpdate = await Task.WhenAll(getDeviceTasks);
            deviceListToUpdate = deviceListToUpdate
                                        .Where(device => device.Status != status)
                                        .Select(device => { device.Status = status; return device; }).ToList();

            if (deviceListToUpdate.Count() > 0)
            {
                BulkRegistryOperationResult updateResult = await registryManager.UpdateDevices2Async(deviceListToUpdate);
                return GetRegistryOperationErrors(updateResult);
            }
            else
            {
                return Enumerable.Empty<DeviceRegistryOperationError>();
            }
        }

        /// <summary>
        /// Returns the list of <c>DeviceRegistryOperationError</c>.
        /// </summary>
        /// <param name="result">An instance of <c>BulkRegistryOperationResult</c> to read the errors from.</param>
        /// <param name="ignoreError">Optionally set this to ignore an error type.</param>
        /// <returns></returns>
        private IEnumerable<DeviceRegistryOperationError> GetRegistryOperationErrors(BulkRegistryOperationResult result, ErrorCode ignoreError = ErrorCode.InvalidErrorCode)
        {
            if (result.Errors != null && result.Errors.Length > 0)
            {
                return result.Errors.Where(
                                            error => error.ErrorCode != ignoreError
                                        );
            }
            else
            {
                return Enumerable.Empty<DeviceRegistryOperationError>();
            }
        }

        // DEMOTED
        ///// <summary>
        ///// Register a single device and return the primary key.
        ///// If the device is already registered, get the device and return the primary key.
        ///// If enable is set to true, the device is enabled whether its a new device or an existing device.
        ///// </summary>
        ///// <returns></returns>
        //private async Task<Device> RegisterOrGetDevice(RegistryManager registryManager, string deviceId, bool enable = false)
        //{
        //    Device device = null;

        //    try
        //    {
        //        Device newDevice = new Device(deviceId);
        //        newDevice.Status = enable ? DeviceStatus.Enabled : DeviceStatus.Disabled;
        //        device = await registryManager.AddDeviceAsync(newDevice);
        //    }
        //    catch (DeviceAlreadyExistsException)
        //    {
        //        if (enable)
        //        {
        //            device = await SetStatus(registryManager, deviceId, DeviceStatus.Enabled);
        //        }
        //        else
        //        {
        //            device = await registryManager.GetDeviceAsync(deviceId);
        //        }
        //    }

        //    return device;
        //}

        //private async Task<Device> SetStatus(RegistryManager registryManager, string deviceId, DeviceStatus status)
        //{
        //    Device device = await registryManager.GetDeviceAsync(deviceId);

        //    if (device.Status != status)
        //    {
        //        device.Status = status;
        //        device = await registryManager.UpdateDeviceAsync(device);
        //    }

        //    return device;
        //}
        #endregion
    }
}

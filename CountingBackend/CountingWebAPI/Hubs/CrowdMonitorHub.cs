using Microsoft.AspNetCore.SignalR;
using CountingWebAPI.Services;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CountingWebAPI.Hubs
{
    public class CrowdMonitorHub : Hub
    {
        private readonly ILogger<CrowdMonitorHub> _log;
        private readonly VideoProcessingManager _vpm;

        public CrowdMonitorHub(ILogger<CrowdMonitorHub> logger, VideoProcessingManager vpm)
        {
            _log = logger; 
            _vpm = vpm;
        }
        
        public override async Task OnConnectedAsync()
        {
            _log.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? ex)
        {
            _log.LogInformation($"Client disconnected: {Context.ConnectionId}. Reason: {ex?.Message}");
            return base.OnDisconnectedAsync(ex);
        }

        public async Task RequestInitialState()
        {
            _log.LogInformation($"Client {Context.ConnectionId} requested initial state.");
            await _vpm.InitializationTask; // Await initialization before proceeding

            var cameraStatuses = await _vpm.GetAllCameraStatusesAsync();
            var zoneStatuses = await _vpm.GetAllZoneStatusesAsync();
            var locationStatuses = await _vpm.GetAllLocationStatusesAsync();
            await Clients.Caller.SendAsync("initial_state", new 
            { 
                cameras = cameraStatuses,
                zones = zoneStatuses,
                locations = locationStatuses
            });
        }

        public async Task SetRoi(RoiDataDto roiDto)
        {
            _log.LogInformation($"Hub received SetRoi for Camera ID {roiDto.CameraIndex} from {Context.ConnectionId}.");
            var processor = _vpm.GetCameraProcessor(roiDto.CameraIndex);
            if (processor != null)
            {
                await processor.SetRoiAsync(roiDto.Roi);
                await Clients.Caller.SendAsync("roi_update_ack", new { cameraIndex = roiDto.CameraIndex, message = "ROI update received and saved" });
            }
            else
            {
                _log.LogWarning($"Could not set ROI for Camera ID {roiDto.CameraIndex}: processor not found or not active.");
                await Clients.Caller.SendAsync("error_msg", new { message = $"Camera {roiDto.CameraIndex} not active/available for ROI." });
            }
        }
    }
}
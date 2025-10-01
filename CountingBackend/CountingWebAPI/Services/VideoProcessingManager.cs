using CountingWebAPI.Hubs;
using CountingWebAPI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using CountingWebAPI.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CountingWebAPI.Models.Database;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Configuration;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace CountingWebAPI.Services
{
    public class VideoProcessingManager : IDisposable
    {
        private readonly TaskCompletionSource _initializationComplete = new TaskCompletionSource();
        public Task InitializationTask => _initializationComplete.Task;

        private readonly ILogger<VideoProcessingManager> _log;
        private readonly IHubContext<CrowdMonitorHub> _hub;
        private readonly IOptions<AppSettings> _appOpt;
        private readonly SettingsService _sets;
        private readonly IServiceScopeFactory _scopeFact;
        private readonly ILoggerFactory _logFact;
        private readonly IConfiguration _config;
        private readonly RoiService _roiService;

        private readonly ConcurrentDictionary<int, CameraProcessor> _processors = new();
        private readonly ConcurrentDictionary<int, ZoneDto> _zones = new();
        private readonly ConcurrentDictionary<int, LocationDto> _locations = new();
        private readonly ConcurrentDictionary<int, Camera> _configuredCameras = new();

        public VideoProcessingManager(ILogger<VideoProcessingManager> l, IHubContext<CrowdMonitorHub> h, IOptions<AppSettings> ao, SettingsService s, IServiceScopeFactory sf, ILoggerFactory lf, IConfiguration config, RoiService roiService)
        {
            _log = l; _hub = h; _appOpt = ao; _sets = s; _scopeFact = sf; _logFact = lf; _config = config; _roiService = roiService;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            try
            {
                _log.LogInformation("VideoProcessingManager starting up...");
                await LoadConfigurationAndStartCamerasAsync();
                _log.LogInformation("VideoProcessingManager started.");
                _initializationComplete.SetResult();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "A critical error occurred during VideoProcessingManager startup.");
                _initializationComplete.SetException(ex);
            }
        }

        private async Task LoadConfigurationAndStartCamerasAsync()
        {
            using var scope = _scopeFact.CreateScope();
            var dbHelper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();

            var camerasFromDb = await dbHelper.QueryAsync("SELECT Id, Name, RtspUrl, IsEnabled FROM Cameras WHERE IsEnabled = 1",
                r => new Camera
                {
                    Id = r.GetInt32Safe("Id"),
                    Name = r.GetStringSafe("Name"),
                    RtspUrl = r.GetStringSafe("RtspUrl"),
                    IsEnabled = r.GetBooleanSafe("IsEnabled")
                });
            
            _configuredCameras.Clear();
            foreach(var cam in camerasFromDb)
            {
                _configuredCameras.TryAdd(cam.Id, cam);
            }

            var zones = await dbHelper.QueryAsync("SELECT Id, Name FROM Zones", r => new ZoneDto { Id = r.GetInt32Safe("Id"), Name = r.GetStringSafe("Name") });
            foreach (var zone in zones)
            {
                var zoneCamerasSql = "SELECT CameraId FROM ZoneCameras WHERE ZoneId = @ZoneId";
                var cameraIds = await dbHelper.QueryAsync(zoneCamerasSql, r => r.GetInt32Safe("CameraId"), dbHelper.CreateParameter("@ZoneId", zone.Id));
                zone.Cameras = camerasFromDb.Where(c => cameraIds.Contains(c.Id)).Select(c => new CameraDto { Id = c.Id, Name = c.Name }).ToList();
                _zones[zone.Id] = zone;
            }

            var locations = await dbHelper.QueryAsync("SELECT Id, Name FROM Locations", r => new LocationDto { Id = r.GetInt32Safe("Id"), Name = r.GetStringSafe("Name") });
            foreach (var location in locations)
            {
                var locationZonesSql = "SELECT ZoneId FROM LocationZones WHERE LocationId = @LocationId";
                var zoneIds = await dbHelper.QueryAsync(locationZonesSql, r => r.GetInt32Safe("ZoneId"), dbHelper.CreateParameter("@LocationId", location.Id));
                location.Zones = zones.Where(z => zoneIds.Contains(z.Id)).ToList();
                _locations[location.Id] = location;
            }

            _log.LogInformation($"Loaded {camerasFromDb.Count} enabled cameras, {_zones.Count} zones, and {_locations.Count} locations.");
            
            var processingTasks = camerasFromDb.Select(InitializeCameraProcessorAsync).ToList();
            await Task.WhenAll(processingTasks);
        }
        
        private async Task InitializeCameraProcessorAsync(Camera camera)
        {
            if (_processors.ContainsKey(camera.Id)) return;

            bool isReachable = true;
            string unreachableReason = string.Empty;

            if (string.IsNullOrEmpty(camera.RtspUrl))
            {
                isReachable = false;
                unreachableReason = "RTSP URL is not configured.";
                _log.LogWarning($"Camera '{camera.Name}' (ID: {camera.Id}) has no valid source. Skipping.");
            }
            else
            {
                var (host, port) = ParseRtspUrl(camera.RtspUrl);
                if (host == null || !await IsHostReachableAsync(host, port))
                {
                    isReachable = false;
                    unreachableReason = $"Host unreachable at {host}:{port}";
                    _log.LogWarning($"Camera '{camera.Name}' (ID: {camera.Id}) is unreachable. {unreachableReason}");
                }
                else
                {
                     _log.LogInformation($"Camera '{camera.Name}' (ID: {camera.Id}) at {host}:{port} is reachable. Proceeding with initialization.");
                }
            }

            if (!isReachable)
            {
                await _hub.Clients.All.SendAsync("camera_status", new {
                    cameraIndex = camera.Id,
                    name = camera.Name,
                    status = "Error",
                    message = unreachableReason,
                    frameUrl = "",
                    totalTrackedCount = 0,
                    roi = new List<double[]>()
                });
                return;
            }

            var initialRoi = await _roiService.GetRoiAsync(camera.Id);
            var processor = new CameraProcessor(camera, _hub, _appOpt, _sets, _scopeFact, _logFact, _config, _roiService, initialRoi);
            processor.StateUpdated += OnCameraStateUpdated;

            if (_processors.TryAdd(camera.Id, processor))
            {
                _ = processor.StartAsync(); 
            }
        }
        
        private async Task<bool> IsHostReachableAsync(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);
                if (await Task.WhenAny(task, Task.Delay(2000)) == task)
                {
                    await task;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private (string? host, int port) ParseRtspUrl(string rtspUrl)
        {
            try
            {
                var match = Regex.Match(rtspUrl, @"rtsp:\/\/.*?@?([a-zA-Z0-9\.\-]+):?(\d+)?\/");
                if (match.Success)
                {
                    string host = match.Groups[1].Value;
                    int port = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 554;
                    return (host, port);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse RTSP URL: {Url}", rtspUrl);
            }
            return (null, 0);
        }

        private void OnCameraStateUpdated(int cameraId, int totalCount)
        {
            foreach (var zone in _zones.Values.Where(z => z.Cameras.Any(c => c.Id == cameraId)))
            {
                UpdateZoneCounts(zone);
                var zonePayload = new { zone.Id, zone.Name, zone.TotalTrackedCount };
                _hub.Clients.All.SendAsync("zone_status", zonePayload);
            }

            foreach (var location in _locations.Values)
            {
                bool isAffected = location.Zones.Any(lz => _zones.ContainsKey(lz.Id) && _zones[lz.Id].Cameras.Any(c => c.Id == cameraId));
                if (isAffected)
                {
                    var locationCounts = CalculateLocationCounts(location);
                    var locationPayload = new { Id = location.Id, Name = location.Name, TotalTrackedCount = locationCounts.TotalTrackedCount };
                    _hub.Clients.All.SendAsync("location_status", locationPayload);
                }
            }
        }

        private void UpdateZoneCounts(ZoneDto zone)
        {
            zone.TotalTrackedCount = zone.Cameras.Sum(c => _processors.TryGetValue(c.Id, out var p) ? p.TotalTrackedCount : 0);
        }
        
        public (int TotalTrackedCount, int Placeholder) CalculateLocationCounts(LocationDto location)
        {
            int totalTracked = location.Zones.Sum(z => _zones.TryGetValue(z.Id, out var zs) ? zs.TotalTrackedCount : 0);
            return (totalTracked, 0);
        }

        public Task StopAsync(CancellationToken ct)
        {
            _log.LogInformation("VideoProcessingManager stopping...");
            foreach (var processor in _processors.Values)
            {
                processor.Stop();
            }
            _log.LogInformation("VideoProcessingManager stopped.");
            return Task.CompletedTask;
        }

        public async Task<List<dynamic>> GetAllCameraStatusesAsync()
        {
            await InitializationTask;
            var statuses = new List<dynamic>();

            foreach (var cam in _configuredCameras.Values)
            {
                if (_processors.TryGetValue(cam.Id, out var p))
                {
                    statuses.Add(new
                    {
                        cameraIndex = p.Id,
                        name = p.Name,
                        status = p.GetCurrentStatus(),
                        frameUrl = p.GetLastFrameDataUrl() ?? "",
                        totalTrackedCount = p.TotalTrackedCount,
                        roi = p.GetRoi()
                    });
                }
                else
                {
                    statuses.Add(new
                    {
                        cameraIndex = cam.Id,
                        name = cam.Name,
                        status = "Error",
                        message = "Host unreachable on startup",
                        frameUrl = "",
                        totalTrackedCount = 0,
                        roi = new List<double[]>()
                    });
                }
            }
            return statuses;
        }

        public async Task<List<ZoneDto>> GetAllZoneStatusesAsync()
        {
            await InitializationTask;
            return _zones.Values.ToList();
        }
        
        public async Task<List<LocationDto>> GetAllLocationStatusesAsync()
        {
            await InitializationTask;
            return _locations.Values.ToList();
        }

        public async Task<List<LocationCountDto>> GetAllLocationCountsAsync()
        {
            await InitializationTask;
            var locationCounts = new List<LocationCountDto>();
            foreach (var location in _locations.Values)
            {
                var counts = CalculateLocationCounts(location);
                locationCounts.Add(new LocationCountDto
                {
                    LocationId = location.Id,
                    LocationName = location.Name,
                    TotalCount = counts.TotalTrackedCount,
                });
            }
            return locationCounts;
        }

        public CameraProcessor? GetCameraProcessor(int cameraId)
        {
            _processors.TryGetValue(cameraId, out var p);
            return p;
        }

        public ICollection<CameraProcessor> GetActiveProcessors()
        {
            return _processors.Values;
        }

        public async Task LogDbEventAsync(string eventText)
        {
            try
            {
                using var scope = _scopeFact.CreateScope();
                var dbHelper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();
                string sql = "INSERT INTO Logs (Timestamp, Event) VALUES (datetime('now'), @Event)";
                await dbHelper.ExecuteNonQueryAsync(sql, dbHelper.CreateParameter("@Event", eventText));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "VPM Failed to log DB event: {event}", eventText);
            }
        }

        public void Dispose()
        {
            _log.LogInformation("Disposing VideoProcessingManager.");
            foreach (var p in _processors.Values) p.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
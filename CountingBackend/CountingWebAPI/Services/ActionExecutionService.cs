using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CountingWebAPI.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CountingWebAPI.Services
{
    public class ActionExecutionService : IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ActionExecutionService> _logger;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ConcurrentDictionary<int, Timer> _timers = new();
        private readonly ConcurrentDictionary<int, Models.Database.Action> _actions = new();

        public ActionExecutionService(IServiceScopeFactory scopeFactory, ILogger<ActionExecutionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await ReloadConfigurationAsync();
            _logger.LogInformation("ActionExecutionService started.");
        }

        public async Task ReloadConfigurationAsync()
        {
            _logger.LogInformation("Reloading action configurations...");
            foreach (var timer in _timers.Values)
            {
                await timer.DisposeAsync();
            }
            _timers.Clear();
            _actions.Clear();

            using var scope = _scopeFactory.CreateScope();
            var dbHelper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();

            var enabledActions = await dbHelper.QueryAsync(
                "SELECT Id, Name, IpAddress, Port, IntervalMilliseconds, Protocol FROM Actions WHERE IsEnabled = 1",
                r => new Models.Database.Action
                {
                    Id = r.GetInt32Safe("Id"),
                    Name = r.GetStringSafe("Name"),
                    IpAddress = r.GetStringSafe("IpAddress"),
                    Port = r.GetInt32Safe("Port"),
                    IntervalMilliseconds = r.GetInt32Safe("IntervalMilliseconds"),
                    Protocol = r.GetStringSafe("Protocol")
                });

            foreach (var action in enabledActions)
            {
                if (_actions.TryAdd(action.Id, action))
                {
                    var timer = new Timer(ExecuteActionCallback, action.Id, 0, action.IntervalMilliseconds);
                    _timers[action.Id] = timer;
                    _logger.LogInformation($"Scheduled action '{action.Name}' (ID: {action.Id}) to run every {action.IntervalMilliseconds}ms.");
                }
            }
             _logger.LogInformation($"Loaded and scheduled {enabledActions.Count} actions.");
        }

        private void ExecuteActionCallback(object? state)
        {
            if (state is not int actionId) return;
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested) return;

            _ = Task.Run(async () => {
                try 
                {
                    await ExecuteActionAsync(actionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in ExecuteActionCallback for Action ID {ActionId}.", actionId);
                }
            }, _cancellationTokenSource.Token);
        }

        private async Task ExecuteActionAsync(int actionId)
        {
            if (!_actions.TryGetValue(actionId, out var action))
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var videoManager = scope.ServiceProvider.GetRequiredService<VideoProcessingManager>();
                
                var locationsData = await videoManager.GetAllLocationStatusesAsync();
                var allZonesData = await videoManager.GetAllZoneStatusesAsync();

                var actionPayloadData = locationsData.Select(loc => {
                    var locationCounts = videoManager.CalculateLocationCounts(loc);
                    
                    var zonesInLocation = loc.Zones.Select(zoneSummary => {
                        var zoneDetail = allZonesData.FirstOrDefault(z => z.Id == zoneSummary.Id);
                        return new {
                            ZoneName = zoneDetail?.Name ?? "Unknown Zone",
                            Total = zoneDetail?.TotalTrackedCount ?? 0
                        };
                    });

                    return new {
                        LocationName = loc.Name,
                        Total = locationCounts.TotalTrackedCount,
                        Zones = zonesInLocation
                    };
                }).ToList();


                var dataPayload = JsonSerializer.Serialize(actionPayloadData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var dataBytes = Encoding.UTF8.GetBytes(dataPayload);

                _logger.LogDebug($"Executing action '{action.Name}': Sending {dataBytes.Length} bytes to {action.IpAddress}:{action.Port} via {action.Protocol}");

                if (action.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    using var tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(action.IpAddress, action.Port, _cancellationTokenSource.Token);
                    await using var stream = tcpClient.GetStream();
                    await stream.WriteAsync(dataBytes, 0, dataBytes.Length, _cancellationTokenSource.Token);
                }
                else // UDP
                {
                    using var udpClient = new UdpClient();
                    await udpClient.SendAsync(dataBytes, dataBytes.Length, action.IpAddress, action.Port);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to execute action '{action.Name}' (ID: {actionId})");
            }
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }
            _logger.LogInformation("ActionExecutionService stopped.");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
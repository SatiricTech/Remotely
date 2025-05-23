﻿using Remotely.Shared.Utilities;

namespace Remotely.Server.Services;

public class ScriptScheduler : IHostedService, IDisposable
{
    private static readonly SemaphoreSlim _dispatchLock = new(1, 1);

    private readonly TimeSpan _timerInterval = EnvironmentHelper.IsDebug ?
        TimeSpan.FromSeconds(30) :
        TimeSpan.FromMinutes(10);

    private readonly IServiceProvider _serviceProvider;
    private System.Timers.Timer? _schedulerTimer;


    public ScriptScheduler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }


    public void Dispose()
    {
        _schedulerTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _schedulerTimer?.Dispose();
        _schedulerTimer = new System.Timers.Timer(_timerInterval);
        _schedulerTimer.Elapsed += SchedulerTimer_Elapsed;
        _schedulerTimer.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _schedulerTimer?.Dispose();
        return Task.CompletedTask;
    }

    private void SchedulerTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _ = DispatchScriptRuns();
    }

    public async Task DispatchScriptRuns()
    {
        using var scope = _serviceProvider.CreateScope();
        var scriptScheduleDispatcher = scope.ServiceProvider.GetRequiredService<IScriptScheduleDispatcher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ScriptScheduler>>();

        if (!await _dispatchLock.WaitAsync(0))
        {
            logger.LogWarning("Script schedule dispatcher is already running.  Returning.");
            return;
        }

        try
        {

            await scriptScheduleDispatcher.DispatchPendingScriptRuns();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while dispatching script runs.");
        }
        finally
        {
            _dispatchLock.Release();
        }
    }
}

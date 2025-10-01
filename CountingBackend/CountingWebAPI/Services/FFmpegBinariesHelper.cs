

﻿using FFmpeg.AutoGen;
using Serilog;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CountingWebAPI.Services
{
    public static class FFmpegBinariesHelper
    {
        public static void RegisterFFmpegBinaries(string configuredPath)
        {
            var logger = Log.ForContext("SourceContext", "CountingWebAPI.Services.FFmpegBinariesHelper");
            bool pathFoundAndSet = false;

            // 1. Prioritize the local path configured in appsettings.json
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                string resolvedPath = configuredPath;
                if (!Path.IsPathRooted(configuredPath))
                {
                    resolvedPath = Path.Combine(AppContext.BaseDirectory, configuredPath);
                }

                logger.Information("Checking for local FFmpeg binaries at: {ResolvedPath}", resolvedPath);
                if (Directory.Exists(resolvedPath))
                {
                    logger.Information("Local FFmpeg directory found. Setting FFmpeg.RootPath to: {Path}", resolvedPath);
                    ffmpeg.RootPath = resolvedPath;
                    pathFoundAndSet = true;
                }
            }

            // 2. If not found locally, search the system PATH for ffmpeg.
            if (!pathFoundAndSet)
            {
                logger.Information("Local FFmpeg directory not found. Searching system PATH...");
                string? ffmpegSystemDir = FindFFmpegDirectoryInPath(logger);
                if (ffmpegSystemDir != null)
                {
                    logger.Information("Found FFmpeg in system PATH at: {Path}. Setting FFmpeg.RootPath.", ffmpegSystemDir);
                    ffmpeg.RootPath = ffmpegSystemDir;
                    pathFoundAndSet = true;
                }
                else
                {
                    logger.Warning("Could not find FFmpeg in the system PATH.");
                }
            }

            // 3. Final verification.
            try
            {
                var version = ffmpeg.av_version_info();
                logger.Information("FFmpeg version info retrieved successfully: {Version}", version);
            }
            catch (Exception ex)
            {
                string errorMessage = "Failed to load FFmpeg libraries. " +
                    "This can happen if FFmpeg is not in the system PATH and not found in the local 'runtime_assets/ffmpeg/bin' directory. " +
                    "Please either install FFmpeg and add it to your PATH, or place the binaries in the local directory. " +
                    "On Windows, downloaded DLLs may also be 'blocked' by the OS. " +
                    "If you have local binaries, right-click each .dll, go to Properties, and click 'Unblock'.";

                logger.Error(ex, errorMessage);
                throw new InvalidOperationException("Failed to load essential FFmpeg libraries. See log for details.", ex);
            }
        }

        /// <summary>
        /// Searches the directories in the PATH environment variable to find the location of the FFmpeg executable.
        /// </summary>
        /// <param name="logger">The logger instance to use for diagnostic messages.</param>
        /// <returns>The directory containing FFmpeg, or null if not found.</returns>
        private static string? FindFFmpegDirectoryInPath(Serilog.ILogger logger)
        {
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) return null;

            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

            foreach (var path in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;

                    var fullPath = Path.Combine(path, executableName);
                    if (File.Exists(fullPath))
                    {
                        // The RootPath needs to be the directory containing the executable and its DLLs.
                        return Path.GetDirectoryName(fullPath);
                    }
                }
                catch (Exception ex)
                {
                    // Ignore exceptions from invalid path entries.
                    logger.Debug(ex, "Could not check path entry '{Path}' while searching for FFmpeg.", path);
                }
            }
            return null;
        }
    }
}
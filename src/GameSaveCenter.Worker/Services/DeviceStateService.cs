using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Models;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Publishes and compares small, content-free device backup summaries. It never restores or overwrites a save.</summary>
public sealed class DeviceStateService
{
    private readonly WorkerOptions _options;
    private readonly SqliteStateStore _store;
    private readonly RcloneClient _rclone;
    private readonly CloudTransferCoordinator _cloudTransfers;
    private readonly ILogger<DeviceStateService> _logger;
    private readonly DeviceConflictDetector _detector=new();
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web){WriteIndented=true};

    public DeviceStateService(WorkerOptions options,SqliteStateStore store,RcloneClient rclone,CloudTransferCoordinator cloudTransfers,ILogger<DeviceStateService> logger)
    { _options=options;_store=store;_rclone=rclone;_cloudTransfers=cloudTransfers;_logger=logger; }

    public async Task<DeviceStateSyncResultDto> SyncAsync(CancellationToken token)
    {
        var device=Environment.MachineName;
        var sidecar=new DeviceStateSidecarDto{DeviceName=device,GeneratedUtc=DateTime.UtcNow,
            Backups=await _store.GetLatestBackupSummariesAsync(token).ConfigureAwait(false)};
        var directory=Path.Combine(_options.DataDirectory,"DeviceState");Directory.CreateDirectory(directory);
        var localPath=Path.Combine(directory,SafeSegment(device)+".json");
        var temporary=localPath+".tmp";
        await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(sidecar,JsonOptions),token).ConfigureAwait(false);
        File.Move(temporary,localPath,true);

        var result=new DeviceStateSyncResultDto{LocalDevice=device,GeneratedUtc=sidecar.GeneratedUtc,
            StatusMessage="本地设备摘要已更新；尚未配置云端，未读取其他设备。"};
        if(!_options.EnableCloudUpload||!_rclone.IsConfigured)return result;

        var upload=await _cloudTransfers.RunUploadAsync("device state",ct=>_rclone.CopyAsync(directory,Path.Combine(device,"DeviceState"),ct),token).ConfigureAwait(false);
        if(!upload.Success)
        {
            result.StatusMessage="本地设备摘要已更新，但上传失败："+upload.StandardError;
            return result;
        }
        result.Uploaded=true;
        var localByGame=sidecar.Backups.ToDictionary(x=>x.PlayniteId,StringComparer.OrdinalIgnoreCase);
        foreach(var remotePath in await _rclone.ListDeviceStateFilesAsync(token).ConfigureAwait(false))
        {
            var text=await _rclone.ReadRemoteTextAsync(remotePath,token).ConfigureAwait(false);
            if(string.IsNullOrWhiteSpace(text))continue;
            try
            {
                var remote=JsonSerializer.Deserialize<DeviceStateSidecarDto>(text,JsonOptions);
                if(remote==null||remote.SchemaVersion!=1||string.IsNullOrWhiteSpace(remote.DeviceName)||
                    string.Equals(remote.DeviceName,device,StringComparison.OrdinalIgnoreCase))continue;
                result.RemoteSidecarsRead++;
                foreach(var item in remote.Backups.Take(500))
                {
                    localByGame.TryGetValue(item.PlayniteId,out var local);
                    var conflict=_detector.Detect(ToSnapshot(local,device),ToSnapshot(item,remote.DeviceName));
                    result.Comparisons.Add(new DeviceConflictStatusDto
                    {
                        PlayniteId=item.PlayniteId,GameName=local?.GameName??item.GameName,RemoteDevice=remote.DeviceName,
                        LocalBackupId=local?.BackupId??string.Empty,RemoteBackupId=item.BackupId,HasConflict=conflict.HasConflict,
                        Reason=conflict.Reason,SuggestedBackupId=conflict.PreferredBackupId,Confidence=conflict.Confidence,
                        LocalCreatedUtc=local?.CreatedUtc??default,RemoteCreatedUtc=item.CreatedUtc
                    });
                }
            }
            catch(JsonException ex){_logger.LogWarning(ex,"Ignoring invalid device-state sidecar {Path}",remotePath);}
        }
        result.Comparisons=result.Comparisons.OrderByDescending(x=>x.HasConflict).ThenBy(x=>x.GameName,StringComparer.OrdinalIgnoreCase).ToList();
        foreach(var comparison in result.Comparisons)
        {
            var decision=await _store.GetDeviceConflictDecisionAsync(comparison.PlayniteId,comparison.RemoteDevice,token).ConfigureAwait(false);
            if(decision==null)continue;
            comparison.Decision=decision.Decision;
            comparison.DecisionComment=decision.Comment;
            comparison.DecidedUtc=decision.DecidedUtc;
        }
        result.StatusMessage=result.RemoteSidecarsRead==0?"已上传本机摘要；云端暂未发现其他设备摘要。":
            result.Comparisons.Any(x=>x.HasConflict)?"发现需要人工决定的设备分叉；未自动恢复或覆盖任何存档。":"设备摘要已比较，未发现需要人工决定的分叉。";
        return result;
    }

    private static BackupSnapshot? ToSnapshot(DeviceBackupSummaryDto? value,string device)=>value==null?null:new BackupSnapshot
    { BackupId=value.BackupId,SourceDevice=device,CreatedUtc=value.CreatedUtc,TotalBytes=value.TotalBytes,FileCount=value.FileCount };
    private static string SafeSegment(string value)=>string.Concat(value.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
}

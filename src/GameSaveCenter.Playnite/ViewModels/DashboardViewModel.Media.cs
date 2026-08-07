using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameSaveCenter.Contracts;

namespace GameSaveCenter.Playnite.ViewModels
{
    public sealed partial class DashboardViewModel
    {
        private async Task LoadMediaWorkspaceAsync()
        {
            await LoadInboxAsync();
            if (SelectedGame != null) await LoadDetailsAsync();
        }

        private async Task LoadInboxAsync()
        {
            var selectedId = SelectedInboxMedia?.MediaId;
            var targetId = InboxTargetGame?.PlayniteId;
            var inbox = (await plugin.RequestAsync<MediaItemDto[]>(MessageTypes.ListUnassignedMedia, new GameQueryDto { Limit = 5000 }))
                ?? Array.Empty<MediaItemDto>();
            ApplyOnUi(() =>
            {
                if (!InboxEquals(UnassignedMedia, inbox))
                    UnassignedMedia = new System.Collections.ObjectModel.ObservableCollection<MediaItemDto>(inbox);

                SelectedInboxMedia = UnassignedMedia.FirstOrDefault(x => string.Equals(x.MediaId, selectedId, StringComparison.OrdinalIgnoreCase))
                                     ?? UnassignedMedia.FirstOrDefault();
                InboxTargetGame = Games.FirstOrDefault(x => string.Equals(x.PlayniteId, targetId, StringComparison.OrdinalIgnoreCase))
                                  ?? SelectedGame
                                  ?? Games.FirstOrDefault();
                RaiseCommandStates();
            });
        }

        private static bool InboxEquals(IReadOnlyList<MediaItemDto> current, IReadOnlyList<MediaItemDto> incoming)
        {
            if (current.Count != incoming.Count)
                return false;

            for (var index = 0; index < current.Count; index++)
            {
                var left = current[index];
                var right = incoming[index];
                if (!string.Equals(left.MediaId, right.MediaId, StringComparison.OrdinalIgnoreCase)
                    || left.CapturedUtc != right.CapturedUtc
                    || !string.Equals(left.ClassificationReason, right.ClassificationReason, StringComparison.Ordinal)
                    || !string.Equals(left.OriginalPath, right.OriginalPath, StringComparison.Ordinal)
                    || left.SizeBytes != right.SizeBytes)
                    return false;
            }

            return true;
        }

        private async Task SyncMediaAsync()
        {
            if (!plugin.Settings.EnableMediaSync)
            {
                StatusMessage = "全局媒体归档已关闭；请在插件设置中启用后再同步。";
                return;
            }

            var ids = SelectedGame == null ? new string[0] : new[] { SelectedGame.PlayniteId };
            var request = new MediaSyncRequestDto { UploadAfterSync = plugin.Settings.EnableCloudUpload };
            foreach (var id in ids) request.PlayniteIds.Add(id);
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.SyncMedia, request, TimeSpan.FromMinutes(60));
            await RefreshCoreAsync(false);
            NotifyTaskResults(tasks);
        }

        private async Task ReassignMediaAsync()
        {
            await plugin.RequestAsync<MediaItemDto>(MessageTypes.ReassignMedia, new ReassignMediaRequestDto { MediaId = SelectedMedia.MediaId, TargetPlayniteId = MediaTargetGame.PlayniteId });
            ConfirmSuccess($"媒体已重新归类到 {MediaTargetGame.Name}");
            await LoadDetailsAsync();
            await LoadInboxAsync();
        }

        private async Task UpdateMediaMetadataAsync()
        {
            var selected=SelectedMedia??throw new InvalidOperationException("请先选择媒体。");
            var updated=await plugin.RequestAsync<MediaItemDto>(MessageTypes.UpdateMediaMetadata,new MediaMetadataUpdateDto
            {
                MediaId=selected.MediaId,
                IsFavorite=MediaFavorite,
                Comment=MediaComment
            });
            var index=Media.IndexOf(selected);
            if(index>=0)Media[index]=updated;
            SelectedMedia=updated;
            MediaView.Refresh();
            if(SelectedGame!=null)
                MediaSummary=await plugin.RequestAsync<MediaStorageSummaryDto>(MessageTypes.GetMediaSummary,new GameQueryDto{PlayniteId=SelectedGame.PlayniteId});
            ConfirmSuccess("媒体备注与收藏状态已保存");
        }

        private async Task UpdateMediaMetadataBatchAsync(object? value,bool? favorite,bool updateComment)
        {
            var selected=(value as IList)?.Cast<object>().OfType<MediaItemDto>()
                .GroupBy(x=>x.MediaId,StringComparer.OrdinalIgnoreCase)
                .Select(x=>x.First())
                .ToList()??new List<MediaItemDto>();
            if(selected.Count==0)throw new InvalidOperationException("请先在媒体列表中选择一个或多个项目。");
            var updated=await plugin.RequestAsync<MediaItemDto[]>(MessageTypes.UpdateMediaMetadataBatch,new MediaMetadataBatchUpdateDto
            {
                MediaIds=selected.Select(x=>x.MediaId).ToList(),
                IsFavorite=favorite,
                UpdateComment=updateComment,
                Comment=MediaComment
            });
            var byId=updated.ToDictionary(x=>x.MediaId,StringComparer.OrdinalIgnoreCase);
            for(var index=0;index<Media.Count;index++)
                if(byId.TryGetValue(Media[index].MediaId,out var replacement))Media[index]=replacement;
            MediaView.Refresh();
            if(SelectedMedia!=null&&byId.TryGetValue(SelectedMedia.MediaId,out var selectedReplacement))
                SelectedMedia=selectedReplacement;
            if(SelectedGame!=null)
                MediaSummary=await plugin.RequestAsync<MediaStorageSummaryDto>(MessageTypes.GetMediaSummary,new GameQueryDto{PlayniteId=SelectedGame.PlayniteId});
            ConfirmSuccess(updateComment?$"已为 {updated.Length} 个媒体文件更新备注":favorite==true?$"已收藏 {updated.Length} 个媒体文件":$"已取消收藏 {updated.Length} 个媒体文件");
        }

        private void OpenSelectedMedia()
        {
            var path=SelectedMedia?.ArchivePath;
            if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))throw new FileNotFoundException("归档媒体文件不存在。",path);
            Process.Start(new ProcessStartInfo{FileName=path,UseShellExecute=true});
        }

        private async Task AssignInboxMediaAsync()
        {
            var media = SelectedInboxMedia ?? throw new InvalidOperationException("请先选择待归类媒体。");
            var target = InboxTargetGame ?? throw new InvalidOperationException("请选择目标游戏。");
            await plugin.RequestAsync<MediaItemDto>(MessageTypes.ReassignMedia, new ReassignMediaRequestDto { MediaId = media.MediaId, TargetPlayniteId = target.PlayniteId });
            ConfirmSuccess($"已将 {media.FileName} 归类到 {target.Name}");
            await RefreshDashboardAsync(false, false);
            await LoadInboxAsync();
            if (SelectedGame != null && string.Equals(SelectedGame.PlayniteId, target.PlayniteId, StringComparison.OrdinalIgnoreCase))
                await LoadDetailsAsync();
        }

        private async Task IgnoreInboxMediaAsync()
        {
            var media = SelectedInboxMedia ?? throw new InvalidOperationException("请先选择待归类媒体。");
            if (!await plugin.ConfirmAsync(
                    "忽略待归类媒体",
                    $"确认忽略“{media.FileName}”？\n\n归档副本仍会保留在媒体目录中。",
                    "忽略并保留副本",
                    "取消")) return;
            await plugin.RequestAsync<MediaItemDto>(MessageTypes.IgnoreMedia, new IgnoreMediaRequestDto { MediaId = media.MediaId });
            ConfirmSuccess($"已忽略 {media.FileName}；归档副本仍保留在媒体目录");
            await RefreshDashboardAsync(false, false);
            await LoadInboxAsync();
        }

        private bool FilterMedia(object value)
        {
            if(value is not MediaItemDto item)return false;
            if(string.Equals(MediaFilter,"截图",StringComparison.Ordinal)&&item.Kind!=MediaKind.Screenshot)return false;
            if(string.Equals(MediaFilter,"录像",StringComparison.Ordinal)&&item.Kind!=MediaKind.VideoClip)return false;
            if(string.Equals(MediaFilter,"收藏",StringComparison.Ordinal)&&!item.IsFavorite)return false;
            if(string.IsNullOrWhiteSpace(MediaSearchText))return true;
            return (item.FileName??string.Empty).IndexOf(MediaSearchText,StringComparison.OrdinalIgnoreCase)>=0
                   || (item.Comment??string.Empty).IndexOf(MediaSearchText,StringComparison.OrdinalIgnoreCase)>=0
                   || (item.SourceDisplay??string.Empty).IndexOf(MediaSearchText,StringComparison.OrdinalIgnoreCase)>=0;
        }
    }
}

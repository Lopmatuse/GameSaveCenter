using System;
using System.Threading.Tasks;
using GameSaveCenter.Contracts;

namespace GameSaveCenter.Playnite.ViewModels
{
    /// <summary>Owns user-configured media source rules and keeps the media workspace focused on browsing.</summary>
    public sealed partial class DashboardViewModel
    {
        private string customMediaSourcePath = string.Empty;
        private string customMediaPattern = "*";
        private bool customMediaShared;

        public string CustomMediaSourcePath
        {
            get => customMediaSourcePath;
            set => SetValue(ref customMediaSourcePath, value);
        }

        public string CustomMediaPattern
        {
            get => customMediaPattern;
            set => SetValue(ref customMediaPattern, value);
        }

        public bool CustomMediaShared
        {
            get => customMediaShared;
            set => SetValue(ref customMediaShared, value);
        }

        private async Task AddMediaSourceAsync()
        {
            if (SelectedGame == null) throw new InvalidOperationException("请先选择游戏。");
            if (string.IsNullOrWhiteSpace(CustomMediaSourcePath)) throw new InvalidOperationException("请输入截图或录像目录。");
            await plugin.RequestAsync<MediaSourceRuleDto>(MessageTypes.AddMediaSource, new MediaSourceRuleDto
            {
                PlayniteId = CustomMediaShared ? string.Empty : SelectedGame.PlayniteId,
                RootPath = CustomMediaSourcePath,
                IncludePattern = string.IsNullOrWhiteSpace(CustomMediaPattern) ? "*" : CustomMediaPattern,
                SharedDirectory = CustomMediaShared,
                SourceKind = MediaSourceKind.Custom,
                Enabled = true
            });
            CustomMediaSourcePath = string.Empty;
            ConfirmSuccess("自定义媒体来源已添加");
            await LoadDetailsAsync();
        }

        private async Task UpdateMediaSourceAsync(MediaSourceRuleDto? source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.SourceId)) throw new InvalidOperationException("请选择需要更新的媒体来源。");
            await plugin.RequestAsync<MediaSourceRuleDto>(MessageTypes.UpdateMediaSource, source);
            ConfirmSuccess(source.Enabled ? "媒体来源已启用" : "媒体来源已暂停");
        }

        private async Task DeleteMediaSourceAsync(MediaSourceRuleDto? source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.SourceId)) throw new InvalidOperationException("请选择需要移除的媒体来源。");
            if (!await plugin.ConfirmAsync(
                    "移除媒体来源",
                    $"停止扫描“{source.RootPath}”？\n\n已经归档的媒体不会被删除。",
                    "移除来源",
                    "取消")) return;
            await plugin.RequestAsync<object>(MessageTypes.DeleteMediaSource, source);
            ConfirmSuccess("媒体来源已移除；现有归档媒体保持不变");
            await LoadDetailsAsync();
        }
    }
}

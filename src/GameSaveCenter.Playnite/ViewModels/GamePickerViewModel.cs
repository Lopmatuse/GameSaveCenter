using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using GameSaveCenter.Contracts;

namespace GameSaveCenter.Playnite.ViewModels
{
    /// <summary>
    /// Shared local game-picker state. Search and filtering are intentionally local and
    /// debounced; this type never calls the Worker while the user is typing.
    /// </summary>
    public sealed class GamePickerViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SynchronizationContext? synchronizationContext;
        private CancellationTokenSource? refreshCancellation;
        private GamePickerItem? selectedItem;
        private string searchText = string.Empty;
        private string statusFilter = "已安装";
        private string platformFilter = "全部";
        private string sortMode = "名称";
        private int filteredCount;
        private bool disposed;
        private bool rebuildingItems;

        public GamePickerViewModel()
        {
            synchronizationContext = SynchronizationContext.Current;
            ItemsView = CollectionViewSource.GetDefaultView(Items);
            ItemsView.Filter = FilterItem;
            Items.CollectionChanged += OnItemsChanged;
            RebuildSortDescriptions();
        }

        public ObservableCollection<GamePickerItem> Items { get; } = new BatchObservableCollection<GamePickerItem>();
        public ICollectionView ItemsView { get; }
        public IReadOnlyList<string> StatusFilterOptions { get; } = new[] { "已安装", "全部", "已匹配", "有备份", "需处理", "未匹配" };
        public IReadOnlyList<string> SortOptions { get; } = new[] { "名称", "最近游玩", "最近备份" };
        public ObservableCollection<string> PlatformFilterOptions { get; } = new ObservableCollection<string> { "全部" };

        public GamePickerItem? SelectedItem
        {
            get => selectedItem;
            set
            {
                if (ReferenceEquals(selectedItem, value)) return;
                selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                OnPropertyChanged(nameof(SelectedGame));
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public GameStatusDto? SelectedGame => SelectedItem?.Game;

        public string SearchText
        {
            get => searchText;
            set
            {
                value ??= string.Empty;
                if (string.Equals(searchText, value, StringComparison.Ordinal)) return;
                searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ScheduleRefresh();
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string StatusFilter
        {
            get => statusFilter;
            set
            {
                value = string.IsNullOrWhiteSpace(value) ? "已安装" : value;
                if (string.Equals(statusFilter, value, StringComparison.Ordinal)) return;
                statusFilter = value;
                OnPropertyChanged(nameof(StatusFilter));
                RefreshNow();
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string PlatformFilter
        {
            get => platformFilter;
            set
            {
                value = string.IsNullOrWhiteSpace(value) ? "全部" : value;
                if (string.Equals(platformFilter, value, StringComparison.Ordinal)) return;
                platformFilter = value;
                OnPropertyChanged(nameof(PlatformFilter));
                RefreshNow();
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string SortMode
        {
            get => sortMode;
            set
            {
                value = string.IsNullOrWhiteSpace(value) ? "名称" : value;
                if (string.Equals(sortMode, value, StringComparison.Ordinal)) return;
                sortMode = value;
                OnPropertyChanged(nameof(SortMode));
                RebuildSortDescriptions();
                RefreshNow();
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int FilteredCount
        {
            get => filteredCount;
            private set
            {
                if (filteredCount == value) return;
                filteredCount = value;
                OnPropertyChanged(nameof(FilteredCount));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? StateChanged;

        public void ApplyPersistedState(string? search, string? status, string? platform, string? sort)
        {
            searchText = search ?? string.Empty;
            statusFilter = string.IsNullOrWhiteSpace(status) ? "已安装" : status!;
            platformFilter = string.IsNullOrWhiteSpace(platform) ? "全部" : platform!;
            sortMode = string.IsNullOrWhiteSpace(sort) ? "名称" : sort!;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(PlatformFilter));
            OnPropertyChanged(nameof(SortMode));
            RebuildSortDescriptions();
            RefreshNow();
        }

        public void SetItems(IEnumerable<GameStatusDto> games, string? preferredGameId = null)
        {
            if (disposed) return;
            var previousId = preferredGameId ?? SelectedGame?.PlayniteId;
            rebuildingItems = true;
            try
            {
                // A dashboard refresh can replace hundreds or thousands of lightweight
                // summaries. Batch the collection notifications so WPF does not repeatedly
                // measure and filter the same list for every item. A ListCollectionView cannot
                // safely process ObservableCollection changes while DeferRefresh is active, so
                // the collection itself suppresses intermediate notifications and emits one
                // Reset after the replacement is complete.
                var batch = Items as BatchObservableCollection<GamePickerItem>;
                batch?.BeginUpdate();
                try
                {
                    Items.Clear();
                    foreach (var game in games ?? Enumerable.Empty<GameStatusDto>())
                        Items.Add(new GamePickerItem(game));
                    RebuildPlatformOptions();
                }
                finally
                {
                    batch?.EndUpdate();
                }
                RefreshNow();
            }
            finally { rebuildingItems = false; }
            var candidate = Items.FirstOrDefault(x => string.Equals(x.PlayniteId, previousId, StringComparison.OrdinalIgnoreCase) && ItemsView.Contains(x))
                            ?? ItemsView.Cast<GamePickerItem>().FirstOrDefault()
                            ?? null;
            SelectedItem = candidate;
        }

        public void SelectGame(GameStatusDto? game)
        {
            var item = game == null ? null : Items.FirstOrDefault(x => string.Equals(x.PlayniteId, game.PlayniteId, StringComparison.OrdinalIgnoreCase));
            SelectedItem = item;
        }

        public void RefreshNow()
        {
            if (disposed) return;
            ApplyViewRefresh();
        }

        /// <summary>Stops a pending debounce when the owning WPF view is unloaded.</summary>
        public void CancelPendingRefresh()
        {
            refreshCancellation?.Cancel();
        }

        public bool MatchesCurrentFilter(GamePickerItem item) => FilterItem(item);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
            Items.CollectionChanged -= OnItemsChanged;
        }

        private bool FilterItem(object item)
        {
            var game = item as GamePickerItem;
            if (game == null) return false;
            if (!string.Equals(PlatformFilter, "全部", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(game.PlatformDisplay, PlatformFilter, StringComparison.OrdinalIgnoreCase)) return false;
            var query = SearchText.Trim();
            if (query.Length > 0 && game.SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) return false;
            switch (StatusFilter)
            {
                case "已安装": return game.IsInstalled;
                case "已匹配": return game.IsMatched;
                case "有备份": return game.HasBackups;
                case "需处理": return game.NeedsAttention;
                case "未匹配": return !game.IsMatched;
                default: return true;
            }
        }

        private void RebuildSortDescriptions()
        {
            ItemsView.SortDescriptions.Clear();
            if (string.Equals(SortMode, "最近游玩", StringComparison.OrdinalIgnoreCase))
                ItemsView.SortDescriptions.Add(new SortDescription(nameof(GamePickerItem.RecentActivityUtc), ListSortDirection.Descending));
            else if (string.Equals(SortMode, "最近备份", StringComparison.OrdinalIgnoreCase))
                ItemsView.SortDescriptions.Add(new SortDescription(nameof(GamePickerItem.LastBackupUtc), ListSortDirection.Descending));
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(GamePickerItem.Name), ListSortDirection.Ascending));
        }

        private void RebuildPlatformOptions()
        {
            var current = PlatformFilter;
            PlatformFilterOptions.Clear();
            PlatformFilterOptions.Add("全部");
            foreach (var platform in Items.Select(x => x.PlatformDisplay).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
                PlatformFilterOptions.Add(platform);
            if (!PlatformFilterOptions.Contains(current)) platformFilter = "全部";
            OnPropertyChanged(nameof(PlatformFilter));
        }

        private void ScheduleRefresh()
        {
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
            refreshCancellation = new CancellationTokenSource();
            var token = refreshCancellation.Token;
            _ = DebouncedRefreshAsync(token);
        }

        private async Task DebouncedRefreshAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(180, token).ConfigureAwait(false);
                if (token.IsCancellationRequested || disposed) return;
                if (synchronizationContext == null) ApplyViewRefresh();
                else synchronizationContext.Post(_ =>
                {
                    if (!token.IsCancellationRequested) ApplyViewRefresh();
                }, null);
            }
            catch (OperationCanceledException) { }
        }

        private void ApplyViewRefresh()
        {
            if (disposed) return;
            ItemsView.Refresh();
            FilteredCount = ItemsView.Cast<object>().Count();
            if (!rebuildingItems && SelectedItem != null && !ItemsView.Contains(SelectedItem))
                SelectedItem = ItemsView.Cast<GamePickerItem>().FirstOrDefault();
        }

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) { }
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Keeps a large game-picker replacement from raising one WPF collection change per
        /// game. The public type remains ObservableCollection for binding compatibility.
        /// </summary>
        private sealed class BatchObservableCollection<T> : ObservableCollection<T>
        {
            private bool batching;
            private bool collectionChanged;
            private bool propertyChanged;

            public void BeginUpdate()
            {
                batching = true;
                collectionChanged = false;
                propertyChanged = false;
            }

            public void EndUpdate()
            {
                if (!batching) return;
                batching = false;
                if (propertyChanged)
                {
                    base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                    base.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                }
                if (collectionChanged)
                    base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                collectionChanged = false;
                propertyChanged = false;
            }

            protected override void OnPropertyChanged(PropertyChangedEventArgs e)
            {
                if (batching)
                {
                    propertyChanged = true;
                    return;
                }
                base.OnPropertyChanged(e);
            }

            protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            {
                if (batching)
                {
                    collectionChanged = true;
                    return;
                }
                base.OnCollectionChanged(e);
            }
        }
    }
}

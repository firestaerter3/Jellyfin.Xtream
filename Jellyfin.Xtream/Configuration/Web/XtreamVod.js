export default function (view) {
  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(3);

    const getConfig = ApiClient.getPluginConfiguration(pluginId);
    const visible = view.querySelector("#Visible");
    const flattenVodView = view.querySelector("#FlattenVodView");
    const tmdbOverride = view.querySelector("#TmdbOverride");

    // Cache settings elements
    const enableVodCaching = view.querySelector("#EnableVodCaching");
    const vodCacheOptionsContainer = view.querySelector("#VodCacheOptionsContainer");
    const vodCacheRefreshMinutes = view.querySelector("#VodCacheRefreshMinutes");
    const vodRefreshCacheBtn = view.querySelector("#VodRefreshCacheBtn");
    const vodClearCacheBtn = view.querySelector("#VodClearCacheBtn");
    const vodCacheStatusContainer = view.querySelector("#VodCacheStatusContainer");
    const vodCacheProgressFill = view.querySelector("#VodCacheProgressFill");
    const vodCacheStatusText = view.querySelector("#VodCacheStatusText");
    const vodCacheParallelism = view.querySelector("#VodCacheRefreshParallelism");
    const vodCacheParallelismValue = view.querySelector("#VodCacheParallelismValue");
    const vodCacheMinDelay = view.querySelector("#VodCacheRefreshMinDelayMs");
    const vodCacheMinDelayValue = view.querySelector("#VodCacheMinDelayValue");

    // Metadata Lookup settings
    const enableTmdbLookup = view.querySelector("#EnableTmdbLookup");

    // Artwork Injector settings
    const useTmdbForVodMetadata = view.querySelector("#UseTmdbForVodMetadata");
    const tmdbOptionsContainer = view.querySelector("#TmdbOptionsContainer");
    const tmdbTitleOverrides = view.querySelector("#TmdbTitleOverrides");

    // Toggle cache options visibility
    function updateCacheOptionsVisibility() {
      vodCacheOptionsContainer.style.display = enableVodCaching.checked ? 'block' : 'none';
    }

    // Toggle TMDB options visibility
    function updateTmdbOptionsVisibility() {
      tmdbOptionsContainer.style.display = useTmdbForVodMetadata.checked ? 'block' : 'none';
    }

    // Update parallelism display value
    function updateParallelismDisplay() {
      vodCacheParallelismValue.textContent = vodCacheParallelism.value;
    }

    // Update min delay display value
    function updateMinDelayDisplay() {
      vodCacheMinDelayValue.textContent = vodCacheMinDelay.value;
    }

    enableVodCaching.addEventListener('change', updateCacheOptionsVisibility);
    vodCacheParallelism.addEventListener('input', updateParallelismDisplay);
    vodCacheMinDelay.addEventListener('input', updateMinDelayDisplay);
    useTmdbForVodMetadata.addEventListener('change', updateTmdbOptionsVisibility);

    getConfig.then((config) => {
      visible.checked = config.IsVodVisible;
      flattenVodView.checked = config.FlattenVodView || false;
      tmdbOverride.checked = config.IsTmdbVodOverride;

      // Cache settings
      enableVodCaching.checked = config.EnableVodCaching !== false;
      vodCacheRefreshMinutes.value = config.VodCacheExpirationMinutes || 600;
      vodCacheParallelism.value = config.CacheRefreshParallelism || 3;
      vodCacheMinDelay.value = config.CacheRefreshMinDelayMs !== undefined ? config.CacheRefreshMinDelayMs : 100;

      // Metadata Lookup settings
      enableTmdbLookup.checked = config.EnableTmdbLookup !== false;

      // Artwork Injector settings
      useTmdbForVodMetadata.checked = config.UseTmdbForVodMetadata !== false;
      tmdbTitleOverrides.value = config.TmdbTitleOverrides || '';

      updateCacheOptionsVisibility();
      updateParallelismDisplay();
      updateMinDelayDisplay();
      updateTmdbOptionsVisibility();
    });

    // Refresh Now button handler
    vodRefreshCacheBtn.addEventListener('click', () => {
      if (vodRefreshCacheBtn.disabled) {
        Dashboard.alert('VOD cache refresh is already in progress. Please wait for it to complete.');
        return;
      }

      vodRefreshCacheBtn.disabled = true;
      vodRefreshCacheBtn.querySelector('span').textContent = 'Starting...';

      ApiClient.fetch({
        url: ApiClient.getUrl('Xtream/VodCacheRefresh'),
        type: 'POST',
        dataType: 'json'
      })
        .then(result => {
          if (result && typeof result.json === 'function') {
            return result.json();
          }
          return result;
        })
        .then(result => {
          if (result.Success) {
            vodCacheStatusText.textContent = 'Refresh started...';
            vodCacheStatusText.style.color = '#00a4dc';
            vodCacheStatusContainer.style.display = 'block';
          } else {
            Dashboard.alert(result.Message || 'Failed to start VOD refresh');
          }
        })
        .catch(err => {
          console.error('Failed to trigger VOD cache refresh:', err);
          Dashboard.alert('Failed to trigger VOD cache refresh: ' + err.message);
        })
        .finally(() => {
          vodRefreshCacheBtn.disabled = false;
          vodRefreshCacheBtn.querySelector('span').textContent = 'Refresh Now';
        });
    });

    // Clear Cache button handler
    vodClearCacheBtn.addEventListener('click', () => {
      Xtream.fetchJson('Xtream/VodCacheStatus')
        .then((status) => {
          let confirmMessage = 'Are you sure you want to clear the VOD cache? Next refresh will fetch all data from scratch.';
          if (status.IsRefreshing) {
            confirmMessage = 'A VOD cache refresh is currently in progress. Clearing the cache will stop the refresh. Are you sure you want to continue?';
          }

          if (!confirm(confirmMessage)) {
            return;
          }

          clearVodCache();
        })
        .catch((err) => {
          console.error('Failed to check VOD cache status:', err);
          if (confirm('Are you sure you want to clear the VOD cache? Next refresh will fetch all data from scratch.')) {
            clearVodCache();
          }
        });
    });

    function clearVodCache() {
      vodClearCacheBtn.disabled = true;
      vodClearCacheBtn.querySelector('span').textContent = 'Clearing...';

      fetch(ApiClient.getUrl('Xtream/VodCacheClear'), {
        method: 'POST',
        headers: ApiClient.defaultRequestHeaders()
      })
        .then(response => {
          if (!response.ok) {
            throw new Error('Server returned ' + response.status);
          }
          return response.json();
        })
        .then(result => {
          if (result.Success) {
            Dashboard.alert(result.Message || 'VOD cache cleared successfully');
            vodCacheStatusText.textContent = 'Cache cleared';
            vodCacheStatusText.style.color = '#a0a0a0';
            vodCacheProgressFill.style.width = '0%';
          } else {
            Dashboard.alert(result.Message || 'Failed to clear VOD cache');
          }
        })
        .catch(err => {
          console.error('Failed to clear VOD cache:', err);
          Dashboard.alert('Failed to clear VOD cache: ' + err.message);
        })
        .finally(() => {
          vodClearCacheBtn.disabled = false;
          vodClearCacheBtn.querySelector('span').textContent = 'Clear Cache';
        });
    }

    // Poll cache status every 2 seconds
    let statusPollInterval;
    function updateVodCacheStatus() {
      Xtream.fetchJson('Xtream/VodCacheStatus')
        .then((status) => {
          if (status.IsRefreshing || status.Progress > 0 || status.IsCachePopulated) {
            vodCacheStatusContainer.style.display = 'block';
            const progressPercent = Math.round(status.Progress * 100);
            vodCacheProgressFill.style.width = progressPercent + '%';
            vodCacheStatusText.textContent = status.Status || 'Idle';

            if (status.IsRefreshing) {
              vodCacheStatusText.style.color = '#00a4dc';
              vodRefreshCacheBtn.disabled = true;
            } else {
              vodRefreshCacheBtn.disabled = false;
              if (status.Progress >= 1.0) {
                vodCacheStatusText.style.color = '#4caf50';
              } else {
                vodCacheStatusText.style.color = '#a0a0a0';
              }
            }
          } else {
            vodCacheStatusContainer.style.display = 'none';
            vodRefreshCacheBtn.disabled = false;
          }
        })
        .catch(() => {
          // Silently fail if API is not available
        });
    }

    // Start polling when view is shown
    updateVodCacheStatus();
    statusPollInterval = setInterval(updateVodCacheStatus, 2000);

    // Clean up interval when view is hidden
    view.addEventListener("viewhide", () => {
      if (statusPollInterval) {
        clearInterval(statusPollInterval);
      }
    });

    const table = view.querySelector('#VodContent');
    Xtream.populateCategoriesTable(
      table,
      () => getConfig.then((config) => config.Vod),
      () => Xtream.fetchJson('Xtream/VodCategories'),
      (categoryId) => Xtream.fetchJson(`Xtream/VodCategories/${categoryId}`),
    ).then((data) => {
      view.querySelector('#XtreamVodForm').addEventListener('submit', (e) => {
        e.preventDefault();

        // Guard: only save if categories actually loaded into the table
        if (table.querySelectorAll('tr[data-category-id]').length === 0) {
          Dashboard.alert('Cannot save: VOD categories failed to load. Please check your credentials and refresh the page.');
          return false;
        }

        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then((config) => {
          config.IsVodVisible = visible.checked;
          config.IsTmdbVodOverride = tmdbOverride.checked;
          config.FlattenVodView = flattenVodView.checked;

          // Cache settings
          config.EnableVodCaching = enableVodCaching.checked;

          // Validate refresh frequency
          let refreshMinutes = parseInt(vodCacheRefreshMinutes.value, 10) || 600;
          if (refreshMinutes < 10) refreshMinutes = 10;
          if (refreshMinutes > 1380) refreshMinutes = 1380;
          config.VodCacheExpirationMinutes = refreshMinutes;

          // Validate parallelism (shared with series - these will sync)
          let parallelism = parseInt(vodCacheParallelism.value, 10) || 3;
          if (parallelism < 1) parallelism = 1;
          if (parallelism > 10) parallelism = 10;
          config.CacheRefreshParallelism = parallelism;

          // Validate min delay (shared with series - these will sync)
          let minDelay = parseInt(vodCacheMinDelay.value, 10) || 100;
          if (minDelay < 0) minDelay = 0;
          if (minDelay > 1000) minDelay = 1000;
          config.CacheRefreshMinDelayMs = minDelay;

          // Metadata Lookup settings
          config.EnableTmdbLookup = enableTmdbLookup.checked;

          // Artwork Injector settings
          config.UseTmdbForVodMetadata = useTmdbForVodMetadata.checked;
          config.TmdbTitleOverrides = tmdbTitleOverrides.value;

          config.Vod = data;
          ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
            Dashboard.processPluginConfigurationUpdateResult(result);
          });
        });

        return false;
      });
    }).catch((error) => {
      console.error('Failed to load VOD categories:', error);
      Dashboard.hideLoadingMsg();
      table.innerHTML = '';
      const errorRow = document.createElement('tr');
      const errorCell = document.createElement('td');
      errorCell.colSpan = 3;
      errorCell.style.color = '#ff6b6b';
      errorCell.style.padding = '16px';
      errorCell.innerHTML = 'Failed to load categories. Please check:<br>' +
        '1. Xtream credentials are configured (Credentials tab)<br>' +
        '2. Xtream server is accessible<br>' +
        '3. Browser console for detailed errors';
      errorRow.appendChild(errorCell);
      table.appendChild(errorRow);
    });
  }));
}

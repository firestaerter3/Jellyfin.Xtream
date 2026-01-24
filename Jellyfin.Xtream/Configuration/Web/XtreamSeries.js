export default function (view) {
  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(4);

    const getConfig = ApiClient.getPluginConfiguration(pluginId);
    const visible = view.querySelector("#Visible");
    const flattenSeriesView = view.querySelector("#FlattenSeriesView");
    const cacheExpirationMinutes = view.querySelector("#SeriesCacheExpirationMinutes");
    const cacheStatusContainer = view.querySelector("#CacheStatusContainer");
    const cacheProgressFill = view.querySelector("#CacheProgressFill");
    const cacheStatusText = view.querySelector("#CacheStatusText");
    
    getConfig.then((config) => {
      visible.checked = config.IsSeriesVisible;
      flattenSeriesView.checked = config.FlattenSeriesView || false;
      cacheExpirationMinutes.value = config.SeriesCacheExpirationMinutes || 60;
    });

    // Poll cache status every 2 seconds
    let statusPollInterval;
    function updateCacheStatus() {
      Xtream.fetchJson('Xtream/SeriesCacheStatus')
        .then((status) => {
          if (status.IsRefreshing || status.Progress > 0 || status.IsCachePopulated) {
            cacheStatusContainer.style.display = 'block';
            const progressPercent = Math.round(status.Progress * 100);
            cacheProgressFill.style.width = progressPercent + '%';
            cacheStatusText.textContent = status.Status || 'Idle';

            if (status.IsRefreshing) {
              cacheStatusText.style.color = '#00a4dc';
            } else if (status.Progress >= 1.0) {
              cacheStatusText.style.color = '#4caf50';
            } else {
              cacheStatusText.style.color = '#a0a0a0';
            }
          } else {
            cacheStatusContainer.style.display = 'none';
          }
        })
        .catch(() => {
          // Silently fail if API is not available
        });
    }

    // Start polling when view is shown (after table loads to ensure Xtream is available)
    statusPollInterval = setInterval(updateCacheStatus, 2000);

    // Clean up interval when view is hidden
    view.addEventListener("viewhide", () => {
      if (statusPollInterval) {
        clearInterval(statusPollInterval);
      }
    });
    const table = view.querySelector('#SeriesContent');
    Xtream.populateCategoriesTable(
      table,
      () => getConfig.then((config) => config.Series),
      () => Xtream.fetchJson('Xtream/SeriesCategories'),
      (categoryId) => Xtream.fetchJson(`Xtream/SeriesCategories/${categoryId}`),
    ).then((data) => {
      // Start cache status polling after table is loaded
      updateCacheStatus();

      view.querySelector('#XtreamSeriesForm').addEventListener('submit', (e) => {
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then((config) => {
          config.IsSeriesVisible = visible.checked;
          config.FlattenSeriesView = flattenSeriesView.checked;
          config.SeriesCacheExpirationMinutes = parseInt(cacheExpirationMinutes.value, 10) || 60;
          config.Series = data;
          ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
            Dashboard.processPluginConfigurationUpdateResult(result);
          });
        });

        e.preventDefault();
        return false;
      });
    }).catch((error) => {
      console.error('Failed to load series categories:', error);
      Dashboard.hideLoadingMsg();
    });
  }));
}
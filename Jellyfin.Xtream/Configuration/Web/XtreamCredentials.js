export default function (view) {
  view.addEventListener("viewshow", () => import(
    window.ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(0);

    Dashboard.showLoadingMsg();
    ApiClient.getPluginConfiguration(pluginId).then(function (config) {
      view.querySelector('#BaseUrl').value = config.BaseUrl;
      view.querySelector('#Username').value = config.Username;
      view.querySelector('#Password').value = config.Password;
      view.querySelector('#UserAgent').value = config.UserAgent;
      Dashboard.hideLoadingMsg();
    });

    const reloadStatus = () => {
      const status = view.querySelector("#ProviderStatus");
      const expiry = view.querySelector("#ProviderExpiry");
      const cons = view.querySelector("#ProviderConnections");
      const maxCons = view.querySelector("#ProviderMaxConnections");
      const time = view.querySelector("#ProviderTime");
      const timezone = view.querySelector("#ProviderTimezone");
      const mpegTs = view.querySelector("#ProviderMpegTs");

      Xtream.fetchJson('Xtream/TestProvider').then(response => {
        // Always display the Status field, even if it contains an error message
        status.innerText = response.Status || "Unknown status";
        // Only show other fields if status doesn't start with "Failed"
        if (response.Status && !response.Status.toLowerCase().startsWith("failed")) {
          expiry.innerText = response.ExpiryDate || "";
          cons.innerText = response.ActiveConnections || "";
          maxCons.innerText = response.MaxConnections || "";
          time.innerText = response.ServerTime || "";
          timezone.innerText = response.ServerTimezone || "";
          mpegTs.innerText = response.SupportsMpegTs || "";
        } else {
          // Clear fields on error
          expiry.innerText = "";
          cons.innerText = "";
          maxCons.innerText = "";
          time.innerText = "";
          timezone.innerText = "";
          mpegTs.innerText = "";
        }
      }).catch((error) => {
        // If the request fails completely (network error, etc.), show generic message
        // But first try to extract any error details
        let errorMessage = "Failed. Check server logs.";
        if (error && error.response && error.response.data) {
          const errorData = error.response.data;
          if (errorData.Status) {
            errorMessage = errorData.Status;
          } else if (errorData.message) {
            errorMessage = `Failed: ${errorData.message}`;
          }
        } else if (error && error.message) {
          errorMessage = `Failed: ${error.message}`;
        }
        status.innerText = errorMessage;
        expiry.innerText = "";
        cons.innerText = "";
        maxCons.innerText = "";
        time.innerText = "";
        timezone.innerText = "";
        mpegTs.innerText = "";
      });
    };
    reloadStatus();

    view.querySelector('#UserAgentFromBrowser').addEventListener('click', (e) => {
      e.preventDefault();
      view.querySelector('#UserAgent').value = navigator.userAgent;
    });

    view.querySelector('#XtreamCredentialsForm').addEventListener('submit', (e) => {
      Dashboard.showLoadingMsg();

      ApiClient.getPluginConfiguration(pluginId).then((config) => {
        config.BaseUrl = view.querySelector('#BaseUrl').value;
        config.Username = view.querySelector('#Username').value;
        config.Password = view.querySelector('#Password').value;
        config.UserAgent = view.querySelector('#UserAgent').value;
        ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
          reloadStatus();
          Dashboard.processPluginConfigurationUpdateResult(result);
        });
      });

      e.preventDefault();
      return false;
    });
  }));
}
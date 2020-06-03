using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Notifications.Plex.PlexTv;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Plex.Server
{
    public class PlexServer : NotificationBase<PlexServerSettings>
    {
        private readonly IPlexServerService _plexServerService;
        private readonly IPlexTvService _plexTvService;

        class PlexUpdateQueue
        {
            public Dictionary<int, Series> Pending { get; } = new Dictionary<int, Series>();
            public bool Refreshing { get; set; }
        }

        private readonly ICached<PlexUpdateQueue> _pendingSeriesCache;

        public PlexServer(IPlexServerService plexServerService, IPlexTvService plexTvService, ICacheManager cacheManager)
        {
            _plexServerService = plexServerService;
            _plexTvService = plexTvService;

            _pendingSeriesCache = cacheManager.GetCache<PlexUpdateQueue>(GetType(), "pendingSeries");
        }

        public override string Link => "https://www.plex.tv/";
        public override string Name => "Plex Media Server";

        public override void OnDownload(DownloadMessage message)
        {
            UpdateIfEnabled(message.Series);
        }

        public override void OnRename(Series series)
        {
            UpdateIfEnabled(series);
        }

        private void UpdateIfEnabled(Series series)
        {
            if (Settings.UpdateLibrary)
            {
                lock (_pendingSeriesCache)
                {
                    var queue = _pendingSeriesCache.Get(Settings.Host, () => new PlexUpdateQueue(), TimeSpan.FromDays(1));
                    queue.Pending[series.Id] = series;
                }
            }
        }

        public override void ProcessQueue()
        {
            PlexUpdateQueue queue = null;
            var refreshing = false;
            try
            {
                lock (_pendingSeriesCache)
                {
                    queue = _pendingSeriesCache.Find(Settings.Host);
                    if (queue == null || queue.Refreshing)
                    {
                        return;
                    }
                    queue.Refreshing = refreshing = true;
                    _pendingSeriesCache.Set(Settings.Host, queue, TimeSpan.FromDays(1));
                }

                while (true)
                {
                    List<Series> refreshingSeries;
                    lock (_pendingSeriesCache)
                    {
                        if (queue.Pending.Empty())
                        {
                            return;
                        }

                        refreshingSeries = queue.Pending.Values.ToList();
                        queue.Pending.Clear();
                    }

                    if (Settings.UpdateLibrary)
                    {
                        _plexServerService.UpdateLibrary(refreshingSeries, Settings);
                    }
                }
            }
            finally
            {
                lock (_pendingSeriesCache)
                {
                    if (refreshing && queue != null)
                    {
                        queue.Refreshing = false;
                    }
                }
            }
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_plexServerService.Test(Settings));

            return new ValidationResult(failures);
        }

        public override object RequestAction(string action, IDictionary<string, string> query)
        {
            if (action == "startOAuth")
            {
                Settings.Validate().Filter("ConsumerKey", "ConsumerSecret").ThrowOnError();

                return _plexTvService.GetPinUrl();
            }
            else if (action == "continueOAuth")
            {
                Settings.Validate().Filter("ConsumerKey", "ConsumerSecret").ThrowOnError();

                if (query["callbackUrl"].IsNullOrWhiteSpace())
                {
                    throw new BadRequestException("QueryParam callbackUrl invalid.");
                }

                if (query["id"].IsNullOrWhiteSpace())
                {
                    throw new BadRequestException("QueryParam id invalid.");
                }

                if (query["code"].IsNullOrWhiteSpace())
                {
                    throw new BadRequestException("QueryParam code invalid.");
                }

                return _plexTvService.GetSignInUrl(query["callbackUrl"], Convert.ToInt32(query["id"]), query["code"]);
            }
            else if (action == "getOAuthToken")
            {
                Settings.Validate().Filter("ConsumerKey", "ConsumerSecret").ThrowOnError();

                if (query["pinId"].IsNullOrWhiteSpace())
                {
                    throw new BadRequestException("QueryParam pinId invalid.");
                }

                var authToken = _plexTvService.GetAuthToken(Convert.ToInt32(query["pinId"]));

                return new
                       {
                           authToken
                       };
            }

            return new { };
        }
    }
}

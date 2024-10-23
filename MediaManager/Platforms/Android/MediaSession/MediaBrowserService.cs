﻿using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Media;
using AndroidX.Media.Session;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.UI;
using MediaManager.Platforms.Android.Media;

namespace MediaManager.Platforms.Android.MediaSession
{
    [Service(Exported = true, Enabled = true, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
    [IntentFilter(new[] { global::Android.Service.Media.MediaBrowserService.ServiceInterface })]
    public class MediaBrowserService : MediaBrowserServiceCompat
    {
        private readonly MediaManagerLogger _logger = new MediaManagerLogger(nameof(MediaBrowserService));

        protected MediaManagerImplementation MediaManager => CrossMediaManager.Android;
        protected MediaDescriptionAdapter MediaDescriptionAdapter { get; set; }
        protected PlayerNotificationManager PlayerNotificationManager
        {
            get => (MediaManager.Notification as Notifications.NotificationManager).PlayerNotificationManager;
            set => (MediaManager.Notification as Notifications.NotificationManager).PlayerNotificationManager = value;
        }
        protected MediaControllerCompat MediaController => MediaManager.MediaController;

        protected NotificationListener NotificationListener { get; set; }

        public readonly string ChannelId = "audio_channel";
        public readonly int ForegroundNotificationId = 1;
        public bool IsForeground = false;

        public MediaBrowserService()
        {
        }

        protected MediaBrowserService(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();

            try
            {
                var exoPlayerVersion = typeof(ExoPlayer).Assembly.GetName().Version;
                _logger.Info($"ExoPlayer version: {exoPlayerVersion}");

                PrepareMediaSession();
                PrepareNotificationManager();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            MediaManager.StateChanged += MediaManager_StateChanged;
        }

        private void MediaManager_StateChanged(object sender, MediaManager.Playback.StateChangedEventArgs e)
        {
            switch (e.State)
            {
                case global::MediaManager.Player.MediaPlayerState.Failed:
                case global::MediaManager.Player.MediaPlayerState.Stopped:
                    if (IsForeground && MediaController.PlaybackState.State == PlaybackStateCompat.StateNone)
                    {
                        //ServiceCompat.StopForeground(this, ServiceCompat.StopForegroundRemove);
                        StopForeground(true);
                        StopSelf();
                        IsForeground = false;
                    }
                    break;
                case global::MediaManager.Player.MediaPlayerState.Paused:
                    if (IsForeground)
                    {
                        //ServiceCompat.StopForeground(this, ServiceCompat.StopForegroundDetach);
                        StopForeground(false);
                        //PlayerNotificationManager?.SetOngoing(false);
                        PlayerNotificationManager?.Invalidate();
                        IsForeground = false;
                    }
                    break;
                default:
                    break;
            }
        }

        protected virtual void PrepareMediaSession()
        {
            var mediaSession = MediaManager.MediaSession = new MediaSessionCompat(this, nameof(MediaBrowserService));
            mediaSession.SetSessionActivity(MediaManager.SessionActivityPendingIntent);
            mediaSession.Active = true;

            SessionToken = mediaSession.SessionToken;

            mediaSession.SetFlags(MediaSessionCompat.FlagHandlesMediaButtons |
                                   MediaSessionCompat.FlagHandlesTransportControls);
        }

        protected virtual void PrepareNotificationManager()
        {
            MediaDescriptionAdapter = new MediaDescriptionAdapter();
            // PlayerNotificationManager = Com.Google.Android.Exoplayer2.UI.PlayerNotificationManager.CreateWithNotificationChannel(
            //     this,
            //     ChannelId,
            //     Resource.String.exo_download_notification_channel_name,
            //     ForegroundNotificationId,
            //     MediaDescriptionAdapter);

            PlayerNotificationManager = new Com.Google.Android.Exoplayer2.UI.PlayerNotificationManager.Builder(
                this,
                ForegroundNotificationId,
                ChannelId)
                // .SetChannelNameResourceId(Resource.String.XamarinMediaManagerName)
                // .SetChannelDescriptionResourceId(Resource.String.XamarinMediaManagerDescription)
                .SetMediaDescriptionAdapter(MediaDescriptionAdapter)
                .SetNotificationListener(NotificationListener)
                .Build();

            //Needed for enabling the notification as a mediabrowser.
            NotificationListener = new NotificationListener();
            NotificationListener.OnNotificationCancelledImpl = (notificationId, dismissedByUser) =>
            {
                StopForeground(dismissedByUser);
                //ServiceCompat.StopForeground(this, ServiceCompat.StopForegroundRemove);

                StopSelf();
                IsForeground = false;
            };
            NotificationListener.OnNotificationPostedImpl = (notificationId, notification, ongoing) =>
            {
                if (ongoing && !IsForeground)
                {
                    ContextCompat.StartForegroundService(ApplicationContext, new Intent(ApplicationContext, Java.Lang.Class.FromType(typeof(MediaBrowserService))));
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                    {
                        _logger.Info("Starting service in foreground (above 31)");
                        StartForeground(notificationId, notification, ForegroundService.TypeMediaPlayback);
                    }
                    else
                    {
                        _logger.Info("Starting service in foreground");
                        StartForeground(notificationId, notification);
                    }

                    IsForeground = true;
                }
            };

            // PlayerNotificationManager.SetFastForwardIncrementMs((long)MediaManager.StepSizeForward.TotalMilliseconds);
            // PlayerNotificationManager.SetRewindIncrementMs((long)MediaManager.StepSizeBackward.TotalMilliseconds);

            //TODO: not sure why this is broken? Maybe in the binding
            //PlayerNotificationManager.SetNotificationListener(NotificationListener);

            PlayerNotificationManager.SetMediaSessionToken(SessionToken);
            //PlayerNotificationManager.SetOngoing(true);
            PlayerNotificationManager.SetUsePlayPauseActions(MediaManager.Notification.ShowPlayPauseControls);
            // PlayerNotificationManager.SetUseNavigationActions(MediaManager.Notification.ShowNavigationControls);
            PlayerNotificationManager.SetSmallIcon(MediaManager.NotificationIconResource);
            PlayerNotificationManager.SetPriority(NotificationCompat.PriorityLow);

            //Must be called to start the connection
            if (MediaManager.Notification is Notifications.NotificationManager notificationManager)
                notificationManager.Player = MediaManager.Player;

            // (MediaManager.Notification as Notifications.NotificationManager).Player = MediaManager.Player;
            //PlayerNotificationManager.SetPlayer(MediaManager.AndroidMediaPlayer.Player);
        }

        public override StartCommandResult OnStartCommand(Intent startIntent, StartCommandFlags flags, int startId)
        {
            if (startIntent != null)
            {
                MediaButtonReceiver.HandleIntent(MediaManager.MediaSession, startIntent);
            }
            return StartCommandResult.Sticky;
        }

        public override async void OnTaskRemoved(Intent rootIntent)
        {
            StopForeground(true);
            await MediaManager.Stop();
            base.OnTaskRemoved(rootIntent);
        }

        public override void OnDestroy()
        {
            //ServiceCompat.StopForeground(this, ServiceCompat.StopForegroundDetach);

            _logger.Info("Destroying MediaBrowserService");
            StopForeground(true);

            if (MediaManager != null)
            {
                MediaManager.StateChanged -= MediaManager_StateChanged;
                if (MediaManager?.Notification is Notifications.NotificationManager notification)
                {
                    notification.Player = null;
                }
            }

            if (MediaDescriptionAdapter != null)
            {
                MediaDescriptionAdapter.Dispose();
                MediaDescriptionAdapter = null;
            }

            if (PlayerNotificationManager != null)
            {
                // Service is being killed, so make sure we release our resources
                // PlayerNotificationManager.SetNotificationListener(null);

                PlayerNotificationManager.SetPlayer(null);
                PlayerNotificationManager.Dispose();
                PlayerNotificationManager = null;
            }

            if (NotificationListener != null)
            {
                NotificationListener.Dispose();
                NotificationListener = null;
            }

            if (MediaManager?.MediaSession != null)
            {
                MediaManager.MediaSession.Active = false;
                MediaManager.MediaSession.Release();
                //MediaManager.MediaSession.Dispose();
                MediaManager.MediaSession = null;
            }

            IsForeground = false;
            base.OnDestroy();
        }

        public override BrowserRoot OnGetRoot(string clientPackageName, int clientUid, Bundle rootHints)
        {
            return new BrowserRoot(nameof(ApplicationContext.ApplicationInfo.Name), null);
        }

        public override void OnLoadChildren(string parentId, Result result)
        {
            var mediaItems = new JavaList<MediaBrowserCompat.MediaItem>();

            foreach (var item in MediaManager.Queue)
                mediaItems.Add(item.ToMediaBrowserMediaItem());

            result.SendResult(mediaItems);
        }
    }
}

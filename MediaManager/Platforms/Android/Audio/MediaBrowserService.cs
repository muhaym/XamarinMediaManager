﻿using System;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.UI;
using MediaManager.Audio;
using MediaManager.Media;
using MediaManager.Platforms.Android.Audio;

namespace MediaManager.Platforms.Android
{
    [Service(Exported = true)]
    [IntentFilter(new[] { global::Android.Service.Media.MediaBrowserService.ServiceInterface })]
    public class MediaBrowserService : MediaBrowserServiceCompat
    {
        private IMediaManager mediaManager = CrossMediaManager.Current;

        private IAudioPlayer _audioPlayer;
        protected IAudioPlayer AudioPlayer
        {
            get
            {
                if (_audioPlayer == null)
                    _audioPlayer = mediaManager.AudioPlayer;

                return _audioPlayer;
            }
            set
            {
                mediaManager.AudioPlayer = value;
                _audioPlayer = value;
            }
        }

        protected AudioPlayer NativePlayer => AudioPlayer as AudioPlayer;

        protected MediaSessionCompat MediaSession { get; set; }
        protected MediaDescriptionAdapter MediaDescriptionAdapter { get; set; }
        protected PlayerNotificationManager PlayerNotificationManager { get; set; }

        public readonly string ChannelId = "audio_channel";
        public readonly int ForegroundNotificationId = 1;

        public MediaBrowserService()
        {
        }

        public MediaBrowserService(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();

            // Build a PendingIntent that can be used to launch the UI.
            var sessionIntent = PackageManager.GetLaunchIntentForPackage(PackageName);
            var sessionActivityPendingIntent = PendingIntent.GetActivity(this, 0, sessionIntent, 0);

            MediaSession = new MediaSessionCompat(this, nameof(MediaBrowserService));
            MediaSession.SetSessionActivity(sessionActivityPendingIntent);
            MediaSession.Active = true;

            SessionToken = MediaSession.SessionToken;

            MediaSession.SetFlags(MediaSessionCompat.FlagHandlesMediaButtons |
                                   MediaSessionCompat.FlagHandlesTransportControls);

            NativePlayer.Initialize(MediaSession);

            MediaDescriptionAdapter = new MediaDescriptionAdapter(sessionActivityPendingIntent);
            PlayerNotificationManager = Com.Google.Android.Exoplayer2.UI.PlayerNotificationManager.CreateWithNotificationChannel(
                this,
                ChannelId,
                Resource.String.exo_download_notification_channel_name,
                ForegroundNotificationId,
                MediaDescriptionAdapter);
            PlayerNotificationManager.SetPlayer(NativePlayer.Player);
            PlayerNotificationManager.SetMediaSessionToken(SessionToken);
        }

        public override StartCommandResult OnStartCommand(Intent startIntent, StartCommandFlags flags, int startId)
        {
            if (startIntent != null)
            {
                MediaButtonReceiver.HandleIntent(MediaSession, startIntent);
            }
            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            // Service is being killed, so make sure we release our resources

            //unregisterCarConnectionReceiver();
            /*
            if (mCastSessionManager != null)
            {
                mCastSessionManager.removeSessionManagerListener(mCastSessionManagerListener,
                        CastSession.class);
            }
            */

            PlayerNotificationManager.SetPlayer(null);
            NativePlayer.Player.Release();
            NativePlayer.Player = null;
            MediaSession.Release();
        }

        public override BrowserRoot OnGetRoot(string clientPackageName, int clientUid, Bundle rootHints)
        {
            return new BrowserRoot(nameof(ApplicationContext.ApplicationInfo.Name), null);
        }

        public override void OnLoadChildren(string parentId, Result result)
        {
            var mediaItems = new JavaList<MediaBrowserCompat.MediaItem>();

            foreach (var item in mediaManager.MediaQueue)
                mediaItems.Add(item.ToMediaBrowserMediaItem());

            result.SendResult(mediaItems);

            //result.SendResult(null);
        }

        public void OnPlaybackStart()
        {
            // The service needs to continue running even after the bound client (usually a
            // MediaController) disconnects, otherwise the music playback will stop.
            // Calling startService(Intent) will keep the service running until it is explicitly killed.
            StartService(new Intent(ApplicationContext, typeof(MediaBrowserService)));
        }

        public void OnPlaybackStop()
        {
            // Reset the delayed stop handler, so after STOP_DELAY it will be executed again,
            // potentially stopping the service.
            StopForeground(true);
        }

        /**
        * A simple handler that stops the service if playback is not active (playing)
        */
        public class DelayedStopHandler : Handler
        {
            private WeakReference<MediaBrowserService> _weakReference;

            public DelayedStopHandler(MediaBrowserService service)
            {
                _weakReference = new WeakReference<MediaBrowserService>(service);
            }

            public override void HandleMessage(Message msg)
            {
                MediaBrowserService service;
                _weakReference.TryGetTarget(out service);
                if (service != null && service.AudioPlayer != null)
                {
                    if (service.AudioPlayer.State == Media.MediaPlayerState.Playing)
                    {
                        return;
                    }
                    service.StopSelf();
                    //service.serviceStarted = false;
                }
            }
        }
    }
}

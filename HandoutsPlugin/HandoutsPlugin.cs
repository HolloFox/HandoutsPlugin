using System;
using BepInEx;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Linq;
using UnityEngine.UI;
using BepInEx.Configuration;
using PhotonUtil;

namespace HandoutsPlugin
{
    [BepInPlugin(Guid, "Handouts Plug-In", Version)]
    [BepInDependency(PhotonUtilPlugin.Guid)]
    public class HandoutsPlugin: BaseUnityPlugin
    {
        private const string Guid = "org.hollofox.plugins.handouts";
        private const string Version = "3.0.0.0";

        // Configuration
        private ConfigEntry<KeyboardShortcut> ShowHandout { get; set; }
        
        // Awake is called once when both the game and the plug-in are loaded
        void Awake()
        {
            ShowHandout = Config.Bind("Hotkeys", "Handout Shortcut", new KeyboardShortcut(KeyCode.P, KeyCode.LeftControl));
            
            instance = this;
            Logger.LogInfo("In Awake for Handouts Plug-in");

            UnityEngine.Debug.Log("Handouts Plug-in loaded");
            ModdingTales.ModdingUtils.Initialize(this, Logger);
            PhotonUtilPlugin.AddMod(Guid);
        }
        
        public static HandoutsPlugin instance;
        private GameObject handout;
        private DateTime lastHandout;


        //public Sprite sprite;

        IEnumerator DownloadImage(string MediaUrl)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(MediaUrl))
            {
                yield return request.SendWebRequest();
                if (request.isNetworkError || request.isHttpError)
                    Debug.Log(request.error);
                else
                {
                    Debug.Log("Downloaded!");
                    Texture2D texture = ((DownloadHandlerTexture) request.downloadHandler).texture;
                    float aspectRatio = ((float) texture.width / (float) texture.height);

                    Sprite sprite = Sprite.Create(texture,
                        new Rect(0, 0, texture.width, texture.height),
                        Vector2.one / 2);

                    if (handout)
                    {
                        Destroy(handout);
                    }

                    handout = new GameObject("Handout");
                    Image image = instance.handout.AddComponent<Image>();
                    image.sprite = sprite;

                    lastHandout = DateTime.Now;
                    instance.handout.SetActive(true);

                    Canvas canvas = GUIManager.GetCanvas();
                    instance.handout.transform.SetParent(canvas.transform, false);

                    float worldScreenHeight = (float) (Camera.main.orthographicSize * 2.0f);
                    float worldScreenWidth = worldScreenHeight / Screen.height * Screen.width;

                    float Scale = (float) texture.width / worldScreenWidth * 0.15f;

                    instance.handout.transform.localScale = new Vector3(Scale * aspectRatio, Scale, 1);
                    lastHandout = DateTime.Now;
                }
            }
        }

        private DateTime _lastFetched = DateTime.Now;
        private readonly TimeSpan _breakTimeSpan = TimeSpan.FromSeconds(10);
        private readonly TimeSpan _fetchTimeSpan = TimeSpan.FromSeconds(1);

        private bool IsBoardLoaded()
        {
            return (BoardSessionManager.HasInstance &&
                    BoardSessionManager.HasBoardAndIsInNominalState &&
                    !BoardSessionManager.IsLoading);
        }

        void Update()
        {
            if (IsBoardLoaded())
            {
                if (ShowHandout.Value.IsUp())
                {
                    SystemMessage.AskForTextInput("Handout URL", "Enter the Handout URL (PNG or JPG Image Only)", "OK", delegate (string mediaUrl)
                    {
                        if (mediaUrl.Length > 256)
                        {
                        }
                        else
                        {
                            var message = new PhotonMessage
                            {
                                PackageId = Guid,
                                SerializedMessage = mediaUrl,
                                Version = Version,
                                
                            };
                            PhotonUtilPlugin.AddMessage(message);
                        }
                    }, delegate
                    {
                    }, "Cancel", delegate
                    {
                    });
                }
                else if (DateTime.Now - _lastFetched > _fetchTimeSpan)
                {
                    var messages = PhotonUtilPlugin.GetNewMessages(Guid);

                    foreach (var message in from m in messages.Values from message in m where message != null && !message.Viewed select message)
                    {
                        StartCoroutine(DownloadImage(message.SerializedMessage));
                    }
                    _lastFetched = DateTime.Now;
                }
                if (handout != null &&  DateTime.Now - lastHandout > _breakTimeSpan.Add(_breakTimeSpan))
                {
                    Destroy(handout);
                    handout = null;
                    lastHandout = DateTime.Now;
                }
            }
        }

    }
}

﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTK;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Threading;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Chat;
using osu.Game.Online.Chat.Drawables;
using osu.Game.Graphics.UserInterface;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.UserInterface;
using OpenTK.Graphics;
using osu.Framework.Input;

namespace osu.Game.Overlays
{
    public class ChatOverlay : FocusedOverlayContainer, IOnlineComponent
    {
        private const float textbox_height = 40;

        private ScheduledDelegate messageRequest;

        private readonly Container currentChannelContainer;

        private readonly FocusedTextBox inputTextBox;

        private APIAccess api;

        private const int transition_length = 500;

        private GetMessagesRequest fetchReq;

        private readonly OsuTabControl<Channel> channelTabs;

        public ChatOverlay()
        {
            RelativeSizeAxes = Axes.X;
            Size = new Vector2(1, 300);
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            Children = new Drawable[]
            {
                channelTabs = new OsuTabControl<Channel>()
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 20,
                },
                new Box
                {
                    Depth = float.MaxValue,
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.9f,
                },
                currentChannelContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 25, Bottom = textbox_height + 5 },
                },
                new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = textbox_height,
                    Padding = new MarginPadding(5),
                    Children = new Drawable[]
                    {
                        inputTextBox = new FocusedTextBox
                        {
                            RelativeSizeAxes = Axes.Both,
                            Height = 1,
                            PlaceholderText = "type your message",
                            Exit = () => State = Visibility.Hidden,
                            OnCommit = postMessage,
                            HoldFocus = true,
                        }
                    }
                }
            };

            channelTabs.Current.ValueChanged += newChannel => CurrentChannel = newChannel;
        }

        public void APIStateChanged(APIAccess api, APIState state)
        {
            switch (state)
            {
                case APIState.Online:
                    initializeChannels();
                    break;
                default:
                    messageRequest?.Cancel();
                    break;
            }
        }

        protected override bool OnFocus(InputState state)
        {
            //this is necessary as inputTextBox is masked away and therefore can't get focus :(
            inputTextBox.TriggerFocus();
            return false;
        }

        protected override void PopIn()
        {
            MoveToY(0, transition_length, EasingTypes.OutQuint);
            FadeIn(transition_length, EasingTypes.OutQuint);

            inputTextBox.HoldFocus = true;
            base.PopIn();
        }

        protected override void PopOut()
        {
            MoveToY(DrawSize.Y, transition_length, EasingTypes.InSine);
            FadeOut(transition_length, EasingTypes.InSine);

            inputTextBox.HoldFocus = false;
            base.PopOut();
        }

        [BackgroundDependencyLoader]
        private void load(APIAccess api)
        {
            this.api = api;
            api.Register(this);
        }

        private long? lastMessageId;

        private List<Channel> careChannels;

        private void initializeChannels()
        {
            currentChannelContainer.Clear();

            careChannels = new List<Channel>();

            SpriteText loading;
            Add(loading = new OsuSpriteText
            {
                Text = @"initialising chat...",
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                TextSize = 40,
            });

            messageRequest?.Cancel();

            ListChannelsRequest req = new ListChannelsRequest();
            req.Success += delegate (List<Channel> channels)
            {
                Debug.Assert(careChannels.Count == 0);

                Scheduler.Add(delegate
                {
                    loading.FadeOut(100);
                    addChannel(channels.Find(c => c.Name == @"#lazer"));
                    addChannel(channels.Find(c => c.Name == @"#osu"));
                    addChannel(channels.Find(c => c.Name == @"#lobby"));
                });

                messageRequest = Scheduler.AddDelayed(() => fetchNewMessages(), 1000, true);
            };

            api.Queue(req);
        }

        private Channel currentChannel;

        protected Channel CurrentChannel
        {
            get
            {
                return currentChannel;
            }

            set
            {
                if (currentChannel == value) return;

                if (currentChannel != null)
                    currentChannelContainer.Clear();

                currentChannel = value;
                currentChannelContainer.Add(new DrawableChannel(currentChannel));

                channelTabs.Current.Value = value;
            }
        }

        private void addChannel(Channel channel)
        {
            careChannels.Add(channel);
            channelTabs.AddItem(channel);

            // we need to get a good number of messages initially for each channel we care about.
            fetchNewMessages(channel);

            if (CurrentChannel == null)
                CurrentChannel = channel;
        }

        private void fetchNewMessages(Channel specificChannel = null)
        {
            if (fetchReq != null) return;

            fetchReq = new GetMessagesRequest(specificChannel != null ? new List<Channel> { specificChannel } : careChannels, lastMessageId);
            fetchReq.Success += delegate (List<Message> messages)
            {
                var ids = messages.Where(m => m.TargetType == TargetType.Channel).Select(m => m.TargetId).Distinct();

                //batch messages per channel.
                foreach (var id in ids)
                    careChannels.Find(c => c.Id == id)?.AddNewMessages(messages.Where(m => m.TargetId == id));

                lastMessageId = messages.LastOrDefault()?.Id ?? lastMessageId;

                Debug.Write("success!");
                fetchReq = null;
            };
            fetchReq.Failure += delegate
            {
                Debug.Write("failure!");
                fetchReq = null;
            };

            api.Queue(fetchReq);
        }

        private void postMessage(TextBox textbox, bool newText)
        {
            var postText = textbox.Text;

            if (!string.IsNullOrEmpty(postText) && api.LocalUser.Value != null)
            {
                var currentChannel = careChannels.FirstOrDefault();

                if (currentChannel == null) return;

                var message = new Message
                {
                    Sender = api.LocalUser.Value,
                    Timestamp = DateTimeOffset.Now,
                    TargetType = TargetType.Channel, //TODO: read this from currentChannel
                    TargetId = currentChannel.Id,
                    Content = postText
                };

                textbox.ReadOnly = true;
                var req = new PostMessageRequest(message);

                req.Failure += e =>
                {
                    textbox.FlashColour(Color4.Red, 1000);
                    textbox.ReadOnly = false;
                };

                req.Success += m =>
                {
                    currentChannel.AddNewMessages(new[] { m });

                    textbox.ReadOnly = false;
                    textbox.Text = string.Empty;
                };

                api.Queue(req);
            }
        }
    }
}

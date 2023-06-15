// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mods
{
    public class ModRewind : ModBlockFail, IUpdatableByPlayfield, IApplicableToPlayer, IApplicableToScoreProcessor
    {
        public override string Name => "Rewind";

        public override string Acronym => "RE";

        public override IconUsage? Icon => FontAwesome.Solid.Clock; // temp

        public override ModType Type => ModType.Fun;

        public override LocalisableString Description => "It's rewind time.";

        public override double ScoreMultiplier => 1;

        public override Type[] IncompatibleMods => new[] { typeof(ModNoFail), typeof(ModSuddenDeath), typeof(ModPerfect), typeof(ModAutoplay) };

        public override bool ValidForMultiplayer => false;

        public override bool ValidForMultiplayerAsFreeMod => false;

        [SettingSource("Rewind Time", "The amount of time (in seconds) to rewind when a miss occurs")]
        public BindableNumber<double> RewindTime { get; } = new BindableDouble
        {
            MinValue = 1,
            MaxValue = 10,
            Default = 3,
            Value = 3,
            Precision = 0.1
        };

        [SettingSource("Grace Period", "The amount of time (in seconds) to prevent rewinding after a miss occurs")]
        public BindableNumber<double> GracePeriod { get; } = new BindableDouble
        {
            MinValue = 1,
            MaxValue = 10,
            Default = 1.5,
            Value = 1.5,
            Precision = 0.1
        };

        public BindableBool Missed = new BindableBool();

        private double invulnerableTime;

        protected double CurrentTime;

        private BindableNumber<double> accuracy = new BindableDouble();

        private bool running;

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;

        public void ApplyToPlayer(Player player)
        {
            Missed.ValueChanged += missed =>
            {
                // if (player.Time.Current - RewindTime.Value >= 0) does not work
                // if (player.GameplayClockContainer.CurrentTime - RewindTime.Value >= 0) works if GameplayClockContainer.CurrentTime is exposed
                if (CurrentTime >= invulnerableTime)
                {
                    if (CurrentTime - (RewindTime.Value * 1000) >= 0)
                    {
                        // player.Seek(player.GameplayClockContainer.CurrentTime - RewindTime.Value);
                        invulnerableTime = (CurrentTime - RewindTime.Value * 1000) + (GracePeriod.Value * 1000);
                        player.Seek(CurrentTime - (RewindTime.Value * 1000));
                    }
                    else
                    {
                        invulnerableTime = GracePeriod.Value * 1000;
                        player.Seek(0);
                    }
                }

                // This seems like the only way to prevent multiple subsequent rewinds without causing any
                // real problems... Since there's no way to tell when Seek() ends and Seek() isn't blocking,
                // and since (i think) accuracy and the last HitEvent are changing as Seek() runs, afaik
                // there will always be a race condition for whether CurrentTime < invulnerableTime.
                // I believe one way this could be fixed is if there is another way to detect combo breaks that
                // isn't affected by Seek(), but I can't figure out any.
                // This fix breaks if CurrentTime >= invulnerableTime after approximately 500ms, which should
                // only occur if Seek() takes a really long time to complete.
                Task.Run(async () =>
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    running = false;
                });
            };
        }

        public virtual void Update(Playfield playfield)
        {
            // jank...
            CurrentTime = playfield.Clock.CurrentTime;
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            accuracy = scoreProcessor.Accuracy.GetBoundCopy(); // making local copy per https://github.com/ppy/osu-framework/wiki/Bindable-Flow#binding-bindablets-together
            accuracy.BindValueChanged(acc =>
            {
                if (!running)
                {
                    if (scoreProcessor.HitEvents.LastOrDefault().Result.BreaksCombo())
                    {
                        running = true;
                        Missed.TriggerChange();
                    }
                }
            });
        }
    }
}

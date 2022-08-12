// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
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

        public override IconUsage? Icon => FontAwesome.Solid.ArrowLeft; // temp

        public override ModType Type => ModType.Fun;

        public override string Description => "It's rewind time.";

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

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;

        public void ApplyToPlayer(Player player)
        {
            Missed.BindValueChanged(missed =>
            {
                // if (player.Time.Current - RewindTime.Value >= 0) does not work
                // if (player.GameplayClockContainer.CurrentTime - RewindTime.Value >= 0) works if GameplayClockContainer.CurrentTime is exposed
                if (missed.NewValue && CurrentTime >= invulnerableTime)
                {
                    Missed.Value = false;

                    if (CurrentTime - (RewindTime.Value * 1000) >= 0)
                    {
                        // player.Seek(player.GameplayClockContainer.CurrentTime - RewindTime.Value);
                        invulnerableTime = (CurrentTime - RewindTime.Value * 1000) + (GracePeriod.Value * 1000);
                        player.Seek(CurrentTime - (RewindTime.Value * 1000));
                    }
                    else
                    {
                        player.Seek(0);
                        invulnerableTime = GracePeriod.Value * 1000;
                    }
                }
            });
        }

        public virtual void Update(Playfield playfield)
        {
            // jank...
            CurrentTime = playfield.Clock.CurrentTime;
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            // possible race condition if two combo breaks occur in quick succession (since Missed.Value may not have been updated yet)
            scoreProcessor.Accuracy.BindValueChanged(acc => Missed.Value = scoreProcessor.HitEvents.LastOrDefault().Result.BreaksCombo());
        }
    }
}

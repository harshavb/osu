// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
    public class ModRewind : ModNoFail, IUpdatableByPlayfield, IApplicableToPlayer, IApplicableToScoreProcessor
    {
        public override string Name => "Practice";

        public override string Acronym => "PR";

        public override IconUsage? Icon => FontAwesome.Solid.ArrowAltCircleRight; // temp

        public override ModType Type => ModType.Automation;

        public override string Description => "You'll get that FC.";

        public override double ScoreMultiplier => 1;

        public override bool ValidForMultiplayer => false;

        public override bool ValidForMultiplayerAsFreeMod => false;

        private ScoreProcessor? scoreProcessor;

        [SettingSource("Rewind Time", "The amount of time (in seconds) to rewind when a miss occurs")]
        public BindableNumber<double> RewindTime { get; } = new BindableDouble
        {
            MinValue = 1,
            MaxValue = 10,
            Default = 3,
            Value = 3,
            Precision = 0.1
        };

        [SettingSource("Invulnerable Time", "The amount of time (in seconds) to prevent rewinding when a miss occurs")]
        public BindableNumber<double> GracePeriod { get; } = new BindableDouble
        {
            MinValue = 1,
            MaxValue = 10,
            Default = 1.5,
            Value = 1.5,
            Precision = 0.1
        };

        public BindableBool Missed = new BindableBool(false);

        private double invulnerableTime = 0;

        protected double CurrentTime = 0;

        public BindableNumber<double> SpeedChange { get; } = new BindableNumber<double>(1);

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;

        public void ApplyToPlayer(Player player)
        {
            Missed.BindValueChanged( missed =>
            {
                // if (player.Time.Current - RewindTime.Value >= 0) does not work
                // if (player.GameplayClockContainer.CurrentTime - RewindTime.Value >= 0) works if GameplayClockContainer.CurrentTime is exposed
                if (CurrentTime >= invulnerableTime)
                {
                    if (CurrentTime - (RewindTime.Value * 1000) >= 0)
                    {
                        // player.Seek(player.GameplayClockContainer.CurrentTime - RewindTime.Value);
                        player.Seek(CurrentTime - (RewindTime.Value * 1000));
                        invulnerableTime = (CurrentTime - RewindTime.Value * 1000) + (GracePeriod.Value * 1000);
                    }
                    else
                    {
                        player.Seek(0);
                        invulnerableTime = GracePeriod.Value * 1000;
                    }
                }

                Missed.Value = false;
            });
        }

        public virtual void Update(Playfield playfield)
        {
            // jank...
            CurrentTime = playfield.Clock.CurrentTime;

            if (!Missed.Value && scoreProcessor?.HitEvents.Count > 0 && (scoreProcessor?.HitEvents.Last().Result.BreaksCombo() ?? false))
            {
                Missed.Value = true;
            }
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            this.scoreProcessor = scoreProcessor;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModPractice : ModBlockFail, IApplicableToPlayer, IApplicableToScoreProcessor
    {
        public override string Name => "Practice";

        public override string Acronym => "PR";

        public override IconUsage? Icon => FontAwesome.Solid.ArrowAltCircleRight; // temp

        public override ModType Type => ModType.Automation;

        public override string Description => "You'll get that FC.";

        public override double ScoreMultiplier => 1;

        public override bool ValidForMultiplayer => false;

        public override bool ValidForMultiplayerAsFreeMod => false;

        protected readonly BindableNumber<int> CurrentCombo = new BindableInt();

        protected readonly BindableNumber<double> RewindTime = new BindableDouble(10000);

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;

        public void ApplyToPlayer(Player player)
        {
            CurrentCombo.BindValueChanged(combo =>
            {
                if (combo.NewValue == 0)
                {
                    // if (player.Time.Current - RewindTime.Value >= 0) does not work
                    if (player.GameplayClockContainer.CurrentTime - RewindTime.Value >= 0)
                    {
                        player.Seek(player.GameplayClockContainer.CurrentTime - RewindTime.Value);
                    }
                    else
                    {
                        player.Seek(0);
                    }
                }
            });
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            CurrentCombo.BindTo(scoreProcessor.Combo);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModPractice : Mod
    {
        public override string Name => "Practice";

        public override string Acronym => "PR";

        public override IconUsage? Icon => FontAwesome.Solid.History; // temp

        public override ModType Type => ModType.Fun;

        public override string Description => "You'll get that FC.";

        public override double ScoreMultiplier => 1;
    }
}
